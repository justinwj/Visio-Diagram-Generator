using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using Microsoft.CSharp.RuntimeBinder;
using VDG.Core.Models;
using VDG.Core.Safety;
using VisioDiagramGenerator.Algorithms;

namespace VDG.CLI
{
    internal static class ExitCodes
    {
        public const int Ok = 0;
        public const int Usage = 64;
        public const int InvalidInput = 65;
        public const int VisioUnavailable = 69;
        public const int InternalError = 70;
    }

    internal sealed class UsageException : Exception
    {
        public UsageException(string message) : base(message) { }
    }

    internal static partial class Program
    {
        private const string CurrentSchemaVersion = "1.2";
        private static readonly string[] SupportedSchemaVersions = { "1.0", "1.1", CurrentSchemaVersion };
        private const double DefaultNodeWidth = 1.8;
        private const double DefaultNodeHeight = 1.0;
        private const double Margin = 1.0;
        private const double ModuleSplitThresholdMultiplier = 2.5;
        private const int ModuleSplitMaxSegments = 24;
        private static readonly object ConsoleRedirectLock = new object();

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private sealed class PerPageTierComparer : IEqualityComparer<(int page, string tier)>
        {
            public bool Equals((int page, string tier) a, (int page, string tier) b)
            {
                return a.page == b.page && string.Equals(a.tier, b.tier, StringComparison.OrdinalIgnoreCase);
            }
            public int GetHashCode((int page, string tier) x)
            {
                unchecked { return (x.page * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(x.tier ?? string.Empty); }
            }
        }

        // M5 structured diagnostics payloads
        private sealed class DiagIssue
        {
            public string Code { get; set; } = string.Empty;
            public string Level { get; set; } = "info";
            public string Message { get; set; } = string.Empty;
            public string? Lane { get; set; }
            public int? Page { get; set; }
        }

        private sealed class LanePageMetric
        {
            public string Tier { get; set; } = string.Empty;
            public int Page { get; set; }
            public double OccupancyRatio { get; set; }
            public int Nodes { get; set; }
        }

        private sealed class LayerMetric
        {
            public int Layer { get; set; }
            public int ShapeCount { get; set; }
            public int ConnectorCount { get; set; }
            public int ModuleCount { get; set; }
            public int BridgesOut { get; set; }
            public int BridgesIn { get; set; }
            public bool SoftShapeLimitExceeded { get; set; }
            public bool SoftConnectorLimitExceeded { get; set; }
            public bool HardShapeLimitExceeded { get; set; }
            public bool HardConnectorLimitExceeded { get; set; }
            public List<string> Modules { get; set; } = new List<string>();
            public List<string> OverflowModules { get; set; } = new List<string>();
        }

        internal sealed class LaneAlert
        {
            public string Tier { get; set; } = string.Empty;
            public double OccupancyRatio { get; set; }
            public int Nodes { get; set; }
            public string Severity { get; set; } = "warning";
        }

        internal sealed class LayerDiagnosticsDetail
        {
            public int LayerNumber { get; set; }
            public int ShapeCount { get; set; }
            public int ConnectorCount { get; set; }
            public int ModuleCount { get; set; }
            public int BridgesOut { get; set; }
            public int BridgesIn { get; set; }
            public bool SoftShapeLimitExceeded { get; set; }
            public bool SoftConnectorLimitExceeded { get; set; }
            public bool HardShapeLimitExceeded { get; set; }
            public bool HardConnectorLimitExceeded { get; set; }
            public List<string> Modules { get; } = new List<string>();
            public List<string> OverflowModules { get; } = new List<string>();
        }

        private sealed class LayerRenderContext
        {
            private LayerRenderContext(
                bool enabled,
                Dictionary<string, int> moduleToLayer,
                Dictionary<int, string> layerNames,
                HashSet<int>? visibleLayers)
            {
                Enabled = enabled;
                ModuleToLayer = moduleToLayer;
                LayerNames = layerNames;
                VisibleLayers = visibleLayers;
            }

            public bool Enabled { get; }
            public Dictionary<string, int> ModuleToLayer { get; }
            public Dictionary<int, string> LayerNames { get; }
            public HashSet<int>? VisibleLayers { get; }
            public bool HasLayerFilter => Enabled && VisibleLayers != null && VisibleLayers.Count > 0;

            public bool IsLayerVisible(int layerIndex)
            {
                if (!Enabled) return true;
                if (!HasLayerFilter) return true;
                return VisibleLayers!.Contains(layerIndex);
            }

            public bool ShouldRenderModule(string? moduleId)
            {
                if (!Enabled || string.IsNullOrWhiteSpace(moduleId)) return true;
                if (!ModuleToLayer.TryGetValue(moduleId.Trim(), out var assignedLayer)) return true;
                return IsLayerVisible(assignedLayer);
            }

            public static LayerRenderContext Disabled { get; } =
                new LayerRenderContext(false, new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase), new Dictionary<int, string>(), null);

            public static LayerRenderContext Create(
                Dictionary<string, int> moduleToLayer,
                Dictionary<int, string> layerNames,
                HashSet<int>? visibleLayers) =>
                new LayerRenderContext(true, moduleToLayer, layerNames, visibleLayers);
        }

        internal sealed class PageDiagnosticsDetail
        {
            public int PageNumber { get; set; }
            public int? ConnectorLimit { get; set; }
            public int ConnectorOverLimit { get; set; }
            public int? PlannedConnectors { get; set; }
            public int? PlannedNodes { get; set; }
            public int? ModuleCount { get; set; }
            public int LaneCrowdingWarnings { get; set; }
            public int LaneCrowdingErrors { get; set; }
            public bool HasPageOverflow { get; set; }
            public bool HasPageCrowding { get; set; }
            public bool HasPartialRender { get; set; }
            public int TruncatedNodeCount { get; set; }
            public List<string> TruncatedNodes { get; } = new List<string>();
            public List<LaneAlert> LaneAlerts { get; } = new List<LaneAlert>();
        }

        private sealed class DiagnosticsJson
        {
            public string Version { get; set; } = "1.0";
            public Metrics Metrics { get; set; } = new Metrics();
            public List<DiagIssue> Issues { get; set; } = new List<DiagIssue>();
        }

        private sealed class Metrics
        {
            public string? LayoutOutputMode { get; set; }
            public float? LayoutCanvasWidth { get; set; }
            public float? LayoutCanvasHeight { get; set; }
            public int LayoutNodeCount { get; set; }
            public int LayoutModuleCount { get; set; }
            public int LayoutContainerCount { get; set; }
            public int LayerCount { get; set; }
            public int LayerCrowdingCount { get; set; }
            public int LayerOverflowCount { get; set; }
            public int BridgeCount { get; set; }
            public int ConnectorCount { get; set; }
            public int StraightLineCrossings { get; set; }
            public double? PageHeight { get; set; }
            public double? UsableHeight { get; set; }
            public int ModuleCount { get; set; }
            public int SegmentCount { get; set; }
            public int SegmentDelta { get; set; }
            public int SplitModuleCount { get; set; }
            public double AverageSegmentsPerModule { get; set; }
            public int PlannerPageCount { get; set; }
            public double PlannerAverageModulesPerPage { get; set; }
            public double PlannerAverageConnectorsPerPage { get; set; }
            public double PlannerMaxOccupancyPercent { get; set; }
            public int PlannerMaxConnectorsPerPage { get; set; }
            public int PageOverflowCount { get; set; }
            public int PageCrowdingCount { get; set; }
            public int LaneOverflowCount { get; set; }
            public int LaneCrowdingCount { get; set; }
            public int ContainerOverflowCount { get; set; }
            public bool PartialRender { get; set; }
            public int ConnectorOverLimitPageCount { get; set; }
            public int PagesWithPartialRender { get; set; }
            public int TruncatedNodeCount { get; set; }
            public List<string> SkippedModules { get; set; } = new List<string>();
            public List<PageMetric> Pages { get; set; } = new List<PageMetric>();
            public List<LayerMetric> Layers { get; set; } = new List<LayerMetric>();
            public List<LanePageMetric> LanePages { get; set; } = new List<LanePageMetric>();
            public List<ContainerPageMetric> Containers { get; set; } = new List<ContainerPageMetric>();
        }

        private sealed class PageMetric
        {
            public int Index { get; set; }
            public string? Name { get; set; }
            public int Nodes { get; set; }
            public int Connectors { get; set; }
            public int SkippedConnectors { get; set; }
            public bool Overflow { get; set; }
            public int? ConnectorLimit { get; set; }
            public int ConnectorOverLimit { get; set; }
            public int ModuleCount { get; set; }
            public bool Partial { get; set; }
            public int LaneCrowdingWarnings { get; set; }
            public int LaneCrowdingErrors { get; set; }
            public bool PageCrowding { get; set; }
            public bool PageOverflow { get; set; }
            public int TruncatedNodes { get; set; }
        }

        private sealed class ContainerPageMetric
        {
            public string Id { get; set; } = string.Empty;
            public string Tier { get; set; } = string.Empty;
            public int Page { get; set; }
            public double OccupancyRatio { get; set; }
            public int Nodes { get; set; }
        }

        [STAThread]
        private static int Main(string[] args)
        {
            string? outputPath = null;

            try
            {
                double? diagHeightOverride = null;
                int? diagLaneMaxOverride = null;
                double? spacingHOverride = null;
                double? spacingVOverride = null;
                double? pageWidthOverride = null;
                double? pageHeightOverride = null;
                double? pageMarginOverride = null;
                bool? paginateOverride = null;

                // M3 routing CLI overrides
                string? routeModeOverride = null;             // orthogonal|straight
                string? bundleByOverride = null;              // lane|group|nodepair|none
                double? bundleSepOverride = null;             // inches
                double? channelGapOverride = null;            // inches
                bool? routeAroundOverride = null;             // true|false
                // M4 container CLI overrides
                double? containerPaddingOverride = null;      // inches
                double? containerCornerOverride = null;       // inches
                // M5 diagnostics overrides
                double? diagLaneWarnOverride = null;          // ratio 0..1
                double? diagLaneErrorOverride = null;         // ratio 0..1
                double? diagPageWarnOverride = null;          // ratio 0..1
                int? diagCrossWarnOverride = null;            // integer
                int? diagCrossErrorOverride = null;           // integer
                double? diagUtilWarnMinOverride = null;       // percent 0..100

                var moduleIncludeFilters = new List<string>();
                var moduleExcludeFilters = new List<string>();
                var modulesFilteredOut = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int? maxPagesFilter = null;
                int? layerMaxShapesOverride = null;
                int? layerMaxConnectorsOverride = null;
                var layerIncludeFilters = new List<int>();
                var layerExcludeFilters = new List<int>();
                bool layerFilterSpecified = false;

                int index = 0;
                bool diagJsonEnable = false; string? diagJsonPath = null; string? diagLevelOverride = null;
                while (index < args.Length && args[index].StartsWith("-"))
                {
                    var flag = args[index];
                    if (string.Equals(flag, "--help", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(flag, "-h", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new UsageException("Expected arguments: [options] <input.diagram.json> <output.vsdx>");
                    }
                    else if (string.Equals(flag, "--diag-height", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length)
                        {
                            throw new UsageException("--diag-height requires a numeric inches value.");
                        }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
                        {
                            throw new UsageException("--diag-height must be a number (inches).");
                        }
                        diagHeightOverride = h;
                        index += 2;
                        continue;
                    }
                    else if (string.Equals(flag, "--diag-lane-max", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length)
                        {
                            throw new UsageException("--diag-lane-max requires an integer value.");
                        }
                        if (!int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                        {
                            throw new UsageException("--diag-lane-max must be an integer.");
                        }
                        diagLaneMaxOverride = n;
                        index += 2;
                        continue;
                    }
                    else if (string.Equals(flag, "--spacing-h", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--spacing-h requires inches value."); }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        { throw new UsageException("--spacing-h must be a number (inches)."); }
                        spacingHOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--spacing-v", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--spacing-v requires inches value."); }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        { throw new UsageException("--spacing-v must be a number (inches)."); }
                        spacingVOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--modules", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--modules requires at least one module id."); }
                        var collected = new List<string>();
                        var cursor = index + 1;
                        while (cursor < args.Length && !args[cursor].StartsWith("-", StringComparison.Ordinal))
                        {
                            var token = args[cursor];
                            if (!string.IsNullOrWhiteSpace(token))
                            {
                                var parts = token.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var part in parts)
                                {
                                    var trimmed = part.Trim();
                                    if (!string.IsNullOrWhiteSpace(trimmed))
                                    {
                                        collected.Add(trimmed);
                                    }
                                }
                            }
                            cursor++;
                        }
                        if (collected.Count == 0)
                        {
                            throw new UsageException("--modules requires at least one module id.");
                        }
                        var mode = "include";
                        if (collected.Count > 0)
                        {
                            var first = collected[0];
                            if (string.Equals(first, "include", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(first, "exclude", StringComparison.OrdinalIgnoreCase))
                            {
                                mode = first.ToLowerInvariant();
                                collected.RemoveAt(0);
                                if (collected.Count == 0)
                                {
                                    throw new UsageException($"--modules {mode} requires at least one module id.");
                                }
                            }
                        }

                        if (string.Equals(mode, "exclude", StringComparison.OrdinalIgnoreCase))
                        {
                            moduleExcludeFilters.AddRange(collected);
                        }
                        else
                        {
                            moduleIncludeFilters.AddRange(collected);
                        }
                        index = cursor;
                        continue;
                    }
                    else if (string.Equals(flag, "--max-pages", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--max-pages requires integer value."); }
                        if (!int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPages) || parsedPages <= 0)
                        {
                            throw new UsageException("--max-pages must be a positive integer.");
                        }
                        maxPagesFilter = parsedPages;
                        index += 2;
                        continue;
                    }
                    else if (string.Equals(flag, "--page-width", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--page-width requires inches value."); }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        { throw new UsageException("--page-width must be a number (inches)."); }
                        pageWidthOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--page-height", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--page-height requires inches value."); }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        { throw new UsageException("--page-height must be a number (inches)."); }
                        pageHeightOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--page-margin", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--page-margin requires inches value."); }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        { throw new UsageException("--page-margin must be a number (inches)."); }
                        pageMarginOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--paginate", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--paginate requires true|false."); }
                        if (!bool.TryParse(args[index + 1], out var b)) { throw new UsageException("--paginate must be true or false."); }
                        paginateOverride = b; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--layer-max-shapes", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--layer-max-shapes requires integer 1-1000."); }
                        if (!int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 1 || parsed > 1000)
                        {
                            throw new UsageException("--layer-max-shapes must be an integer between 1 and 1000.");
                        }
                        layerMaxShapesOverride = parsed;
                        index += 2;
                        continue;
                    }
                    else if (string.Equals(flag, "--layer-max-connectors", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--layer-max-connectors requires integer 1-1000."); }
                        if (!int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 1 || parsed > 1000)
                        {
                            throw new UsageException("--layer-max-connectors must be an integer between 1 and 1000.");
                        }
                        layerMaxConnectorsOverride = parsed;
                        index += 2;
                        continue;
                    }
                    else if (string.Equals(flag, "--layers", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--layers requires at least one layer index."); }
                        var collected = new List<string>();
                        var cursor = index + 1;
                        while (cursor < args.Length && !args[cursor].StartsWith("-", StringComparison.Ordinal))
                        {
                            var token = args[cursor];
                            if (!string.IsNullOrWhiteSpace(token))
                            {
                                var parts = token.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var part in parts)
                                {
                                    var trimmed = part.Trim();
                                    if (!string.IsNullOrWhiteSpace(trimmed))
                                    {
                                        collected.Add(trimmed);
                                    }
                                }
                            }
                            cursor++;
                        }
                        if (collected.Count == 0)
                        {
                            throw new UsageException("--layers requires at least one layer index.");
                        }

                        var mode = "include";
                        var firstToken = collected[0];
                        if (string.Equals(firstToken, "include", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(firstToken, "exclude", StringComparison.OrdinalIgnoreCase))
                        {
                            mode = firstToken.ToLowerInvariant();
                            collected.RemoveAt(0);
                            if (collected.Count == 0)
                            {
                                throw new UsageException($"--layers {mode} requires at least one layer index.");
                            }
                        }

                        if (collected.Count == 1 && string.Equals(collected[0], "all", StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.Equals(mode, "exclude", StringComparison.OrdinalIgnoreCase))
                            {
                                throw new UsageException("--layers exclude all would hide every layer.");
                            }
                            layerIncludeFilters.Clear();
                            layerFilterSpecified = true;
                            index = cursor;
                            continue;
                        }

                        var parsedLayers = ParseLayerFilterTokens(collected);
                        if (parsedLayers.Count == 0)
                        {
                            throw new UsageException("--layers requires at least one valid layer index.");
                        }

                        layerFilterSpecified = true;
                        if (string.Equals(mode, "exclude", StringComparison.OrdinalIgnoreCase))
                        {
                            layerExcludeFilters.AddRange(parsedLayers);
                        }
                        else
                        {
                            layerIncludeFilters.AddRange(parsedLayers);
                        }
                        index = cursor;
                        continue;
                    }
                    else if (string.Equals(flag, "--route-mode", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--route-mode requires orthogonal|straight."); }
                        var v = args[index + 1];
                        if (!string.Equals(v, "orthogonal", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(v, "straight", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new UsageException("--route-mode must be orthogonal or straight.");
                        }
                        routeModeOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--bundle-by", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--bundle-by requires lane|group|nodepair|none."); }
                        var v = args[index + 1];
                        if (!(new[] { "lane", "group", "nodepair", "none" }).Any(x => string.Equals(x, v, StringComparison.OrdinalIgnoreCase)))
                        { throw new UsageException("--bundle-by must be lane|group|nodepair|none."); }
                        bundleByOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--bundle-sep", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--bundle-sep requires inches value."); }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        { throw new UsageException("--bundle-sep must be a number (inches)."); }
                        bundleSepOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--channel-gap", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--channel-gap requires inches value."); }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        { throw new UsageException("--channel-gap must be a number (inches)."); }
                        channelGapOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--route-around", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--route-around requires true|false."); }
                        if (!bool.TryParse(args[index + 1], out var b)) { throw new UsageException("--route-around must be true or false."); }
                        routeAroundOverride = b; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--container-padding", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--container-padding requires inches value."); }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        { throw new UsageException("--container-padding must be a number (inches)."); }
                        containerPaddingOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--container-corner", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--container-corner requires inches value."); }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        { throw new UsageException("--container-corner must be a number (inches)."); }
                        containerCornerOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--diag-lane-warn", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--diag-lane-warn requires ratio (0..1)."); }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v < 0 || v > 1)
                        { throw new UsageException("--diag-lane-warn must be a number between 0 and 1."); }
                        diagLaneWarnOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--diag-lane-error", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--diag-lane-error requires ratio (0..1)."); }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v < 0 || v > 1)
                        { throw new UsageException("--diag-lane-error must be a number between 0 and 1."); }
                        diagLaneErrorOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--diag-page-warn", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--diag-page-warn requires ratio (0..1)."); }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || v < 0 || v > 1)
                        { throw new UsageException("--diag-page-warn must be a number between 0 and 1."); }
                        diagPageWarnOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--diag-cross-warn", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--diag-cross-warn requires integer value."); }
                        if (!int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv) || iv < 0)
                        { throw new UsageException("--diag-cross-warn must be a non-negative integer."); }
                        diagCrossWarnOverride = iv; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--diag-cross-err", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--diag-cross-err requires integer value."); }
                        if (!int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv) || iv < 0)
                        { throw new UsageException("--diag-cross-err must be a non-negative integer."); }
                        diagCrossErrorOverride = iv; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--diag-util-warn", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--diag-util-warn requires percent (0..100)."); }
                        if (!double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var dv) || dv < 0 || dv > 100)
                        { throw new UsageException("--diag-util-warn must be a number between 0 and 100."); }
                        diagUtilWarnMinOverride = dv; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--diag-level", StringComparison.OrdinalIgnoreCase))
                    {
                        if (index + 1 >= args.Length) { throw new UsageException("--diag-level requires info|warning|error."); }
                        var v = args[index + 1];
                        if (!(new[] { "info", "warning", "error" }).Any(x => string.Equals(x, v, StringComparison.OrdinalIgnoreCase)))
                        { throw new UsageException("--diag-level must be info|warning|error."); }
                        diagLevelOverride = v; index += 2; continue;
                    }
                    else if (string.Equals(flag, "--diag-json", StringComparison.OrdinalIgnoreCase))
                    {
                        diagJsonEnable = true;
                        // Only consume the next token as a path if it looks like a .json file
                        // and there will still remain two arguments for input and output.
                        if (index + 1 < args.Length)
                        {
                            var cand = args[index + 1];
                            bool looksJson = !cand.StartsWith("-") && cand.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                            bool leavesIo = (args.Length - (index + 2)) >= 2;
                            if (looksJson && leavesIo)
                            {
                                diagJsonPath = cand; index += 2; continue;
                            }
                        }
                        index += 1; continue;
                    }
                    else
                    {
                        break;
                    }
                }

                if (args.Length - index < 2)
                {
                    throw new UsageException("Expected arguments: [options] <input.diagram.json> <output.vsdx>");
                }

                var inputPath = Path.GetFullPath(args[index]);
                outputPath = Path.GetFullPath(args[index + 1]);

                var model = LoadDiagramModel(inputPath);
                // Apply CLI overrides into metadata for downstream components
                if (spacingHOverride.HasValue) model.Metadata["layout.spacing.horizontal"] = spacingHOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (spacingVOverride.HasValue) model.Metadata["layout.spacing.vertical"] = spacingVOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (pageWidthOverride.HasValue) model.Metadata["layout.page.widthIn"] = pageWidthOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (pageHeightOverride.HasValue) model.Metadata["layout.page.heightIn"] = pageHeightOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (pageMarginOverride.HasValue) model.Metadata["layout.page.marginIn"] = pageMarginOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (paginateOverride.HasValue) model.Metadata["layout.page.paginate"] = paginateOverride.Value.ToString();
                if (layerMaxShapesOverride.HasValue) model.Metadata["layout.layers.maxShapes"] = layerMaxShapesOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (layerMaxConnectorsOverride.HasValue) model.Metadata["layout.layers.maxConnectors"] = layerMaxConnectorsOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(routeModeOverride)) model.Metadata["layout.routing.mode"] = routeModeOverride!;
                if (!string.IsNullOrWhiteSpace(bundleByOverride)) model.Metadata["layout.routing.bundleBy"] = bundleByOverride!;
                if (bundleSepOverride.HasValue) model.Metadata["layout.routing.bundleSeparationIn"] = bundleSepOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (channelGapOverride.HasValue) model.Metadata["layout.routing.channels.gapIn"] = channelGapOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (routeAroundOverride.HasValue) model.Metadata["layout.routing.routeAroundContainers"] = routeAroundOverride.Value.ToString();
                if (containerPaddingOverride.HasValue) model.Metadata["layout.containers.paddingIn"] = containerPaddingOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (containerCornerOverride.HasValue) model.Metadata["layout.containers.cornerIn"] = containerCornerOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (diagLaneWarnOverride.HasValue) model.Metadata["layout.diagnostics.laneCrowdWarnRatio"] = diagLaneWarnOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (diagLaneErrorOverride.HasValue) model.Metadata["layout.diagnostics.laneCrowdErrorRatio"] = diagLaneErrorOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (diagPageWarnOverride.HasValue) model.Metadata["layout.diagnostics.pageCrowdWarnRatio"] = diagPageWarnOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (diagCrossWarnOverride.HasValue) model.Metadata["layout.diagnostics.crossingsWarn"] = diagCrossWarnOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (diagCrossErrorOverride.HasValue) model.Metadata["layout.diagnostics.crossingsError"] = diagCrossErrorOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (diagUtilWarnMinOverride.HasValue) model.Metadata["layout.diagnostics.utilizationWarnMin"] = diagUtilWarnMinOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(diagLevelOverride)) model.Metadata["layout.diagnostics.level"] = diagLevelOverride!;
                if (diagJsonEnable) model.Metadata["layout.diagnostics.emitJson"] = "true";
                if (!string.IsNullOrWhiteSpace(diagJsonPath))
                {
                    model.Metadata["layout.diagnostics.jsonPath"] = diagJsonPath!;
                }
                else if (diagJsonEnable)
                {
                    // Default JSON path alongside the output VSDX
                    var defaultJson = !string.IsNullOrWhiteSpace(outputPath)
                        ? (outputPath + ".diagnostics.json")
                        : Path.Combine("out", "diagnostics.json");
                    model.Metadata["layout.diagnostics.jsonPath"] = defaultJson;
                }
                if (moduleIncludeFilters.Count > 0)
                {
                    var beforeModules = CollectModuleIds(model);
                    model = FilterModelByModules(model, moduleIncludeFilters);
                    var afterModules = CollectModuleIds(model);
                    beforeModules.ExceptWith(afterModules);
                    foreach (var dropped in beforeModules)
                    {
                        modulesFilteredOut.Add(dropped);
                    }
                    if (model.Nodes.Count == 0)
                    {
                        Console.Error.WriteLine("warning: no modules matched --modules filter.");
                        return ExitCodes.InvalidInput;
                    }
                }

                if (moduleExcludeFilters.Count > 0)
                {
                    var allModules = CollectModuleIds(model);
                    var excludeSet = new HashSet<string>(moduleExcludeFilters, StringComparer.OrdinalIgnoreCase);
                    var keepSet = allModules.Where(m => !excludeSet.Contains(m)).ToList();
                    if (keepSet.Count == 0)
                    {
                        Console.Error.WriteLine("warning: --modules exclude removed all modules.");
                        return ExitCodes.InvalidInput;
                    }

                    foreach (var excluded in allModules.Where(m => excludeSet.Contains(m)))
                    {
                        modulesFilteredOut.Add(excluded);
                    }

                    model = FilterModelByModules(model, keepSet);
                }

                var layoutComputation = ComputeLayout(model);
                var layout = layoutComputation.Layout;
                var layoutPlan = layoutComputation.Plan;
                var pagingOptions = BuildPageSplitOptions(model);

                PagePlan[] pagePlans;
                if (layoutPlan?.Pages != null && layoutPlan.Pages.Length > 0)
                {
                    pagePlans = layoutPlan.Pages;
                }
                else
                {
                    pagePlans = Array.Empty<PagePlan>();
                }

                if (maxPagesFilter.HasValue && maxPagesFilter.Value > 0 && pagePlans.Length > maxPagesFilter.Value)
                {
                    var pageLimit = maxPagesFilter.Value;
                    var modulesToKeep = new HashSet<string>(
                        pagePlans
                            .OrderBy(p => p.PageIndex)
                            .Take(pageLimit)
                            .SelectMany(p => p.Modules ?? Array.Empty<string>())
                            .Where(m => !string.IsNullOrWhiteSpace(m))
                            .Select(m => m.Trim()),
                        StringComparer.OrdinalIgnoreCase);

                    if (modulesToKeep.Count > 0)
                    {
                        var allModules = CollectModuleIds(model);
                        foreach (var removed in allModules.Where(m => !modulesToKeep.Contains(m)))
                        {
                            modulesFilteredOut.Add(removed);
                        }

                        model = FilterModelByModules(model, modulesToKeep);
                        if (model.Nodes.Count == 0)
                        {
                            Console.Error.WriteLine($"warning: --max-pages {pageLimit} removed all modules.");
                            return ExitCodes.InvalidInput;
                        }

                        layoutComputation = ComputeLayout(model);
                        layout = layoutComputation.Layout;
                        layoutPlan = layoutComputation.Plan;
                        pagingOptions = BuildPageSplitOptions(model);
                        pagePlans = layoutPlan?.Pages ?? Array.Empty<PagePlan>();
                        if (pagePlans.Length > pageLimit)
                        {
                            pagePlans = pagePlans.OrderBy(p => p.PageIndex).Take(pageLimit).ToArray();
                        }
                }
                else
                {
                    pagePlans = pagePlans.OrderBy(p => p.PageIndex).Take(pageLimit).ToArray();
                }
            }

            var layerVisibility = BuildLayerVisibility(layoutPlan, layerIncludeFilters, layerExcludeFilters, layerFilterSpecified);
            if (layerFilterSpecified && layerVisibility != null && layerVisibility.Count == 0)
            {
                Console.Error.WriteLine("warning: --layers filters removed all layers; no output generated.");
                return ExitCodes.InvalidInput;
            }

            ValidateViewModeContent(model);

            var nodePageAssignments = BuildNodePageAssignments(model, pagePlans, null);
            var plannerStats = BuildPlannerSummaryStats(pagePlans);
                var diagnosticsSummary = EmitDiagnostics(model, layout, diagHeightOverride, diagLaneMaxOverride, pagePlans, pagingOptions, null, plannerStats, null, modulesFilteredOut, layoutPlan);
                try
                {
                    PrintPlannerSummary(plannerStats, null, diagnosticsSummary);
                }
                catch (ObjectDisposedException)
                {
                    // Tests may dispose redirected writers; ignore logging failures.
                }
                var diagnosticsRank = diagnosticsSummary.Rank;

                var failLevelEnv = Environment.GetEnvironmentVariable("VDG_DIAG_FAIL_LEVEL");
                if (string.IsNullOrWhiteSpace(failLevelEnv))
                {
                    failLevelEnv = Environment.GetEnvironmentVariable("VDG_DIAG_FAIL_ON");
                }
                if (!string.IsNullOrWhiteSpace(failLevelEnv))
                {
                    var normalizedFail = failLevelEnv.Trim();
                    var failRank = DiagRank(normalizedFail);
                    if (failRank > 0 && diagnosticsRank >= failRank)
                    {
                        Console.Error.WriteLine($"diagnostics reached '{normalizedFail}' severity; failing render (set by environment).");
                        return ExitCodes.InvalidInput;
                    }
                }

                // Allow tests/CI to skip Visio automation and still succeed
                var skipRunnerEnv = Environment.GetEnvironmentVariable("VDG_SKIP_RUNNER");
                var skipRunner = !string.IsNullOrEmpty(skipRunnerEnv) &&
                                 (skipRunnerEnv.Equals("1", StringComparison.OrdinalIgnoreCase) || skipRunnerEnv.Equals("true", StringComparison.OrdinalIgnoreCase));

                if (skipRunner)
                {
                    EnsureDirectory(outputPath);
                    File.WriteAllText(outputPath, "Runner skipped (VDG_SKIP_RUNNER)");
                    Console.WriteLine("info: skipping Visio runner due to VDG_SKIP_RUNNER");
                    return ExitCodes.Ok;
                }
                else
                {
                    EnsureDirectory(outputPath);
                    RunVisio(model, layout, layoutPlan, outputPath, pagePlans, nodePageAssignments, layerVisibility);
                    DeleteErrorLog(outputPath);

                    Console.WriteLine($"Saved diagram: {outputPath}");
                    return ExitCodes.Ok;
                }
            }
            catch (UsageException uex)
            {
                TryConsoleError($"usage: {uex.Message}");
                PrintUsage();
                return ExitCodes.Usage;
            }
            catch (FileNotFoundException fnf)
            {
                WriteErrorLog(outputPath, fnf);
                TryConsoleError($"input file not found: {fnf.FileName}");
                return ExitCodes.InvalidInput;
            }
            catch (JsonException jex)
            {
                WriteErrorLog(outputPath, jex);
                TryConsoleError($"invalid diagram JSON: {jex.Message}");
                return ExitCodes.InvalidInput;
            }
            catch (InvalidDataException idex)
            {
                WriteErrorLog(outputPath, idex);
                TryConsoleError($"invalid diagram: {idex.Message}");
                return ExitCodes.InvalidInput;
            }
            catch (COMException comEx)
            {
                WriteErrorLog(outputPath, comEx);
                TryConsoleError($"Visio automation error: {comEx.Message}");
                return ExitCodes.VisioUnavailable;
            }
            catch (Exception ex)
            {
                WriteErrorLog(outputPath, ex);
                TryConsoleError($"fatal: {ex.Message}");
                return ExitCodes.InternalError;
            }
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("VDG.CLI runner");
            Console.Error.WriteLine("Usage: VDG.CLI.exe [options] <input.diagram.json> <output.vsdx>");
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --diag-height <in>      Page height threshold for overflow hints (inches). Default 11.0");
            Console.Error.WriteLine("  --diag-lane-max <n>     Max nodes per lane before crowding hint. Default 12");
            Console.Error.WriteLine("  --spacing-h <in>        Horizontal spacing (inches) override");
            Console.Error.WriteLine("  --spacing-v <in>        Vertical spacing (inches) override");
            Console.Error.WriteLine("  --modules <ids...>      Include only specified module ids (space or comma separated)");
            Console.Error.WriteLine("  --max-pages <n>         Limit render to the first N planned pages");
            Console.Error.WriteLine("  --page-width <in>       Page width (inches)");
            Console.Error.WriteLine("  --page-height <in>      Page height (inches)");
            Console.Error.WriteLine("  --page-margin <in>      Page margin (inches)");
            Console.Error.WriteLine("  --paginate <bool>       Enable pagination (future use)");
            Console.Error.WriteLine("  --layer-max-shapes <1-1000>     Override layer shape soft cap (default 900)");
            Console.Error.WriteLine("  --layer-max-connectors <1-1000> Override layer connector soft cap (default 900)");
            Console.Error.WriteLine("  --layers [include|exclude] <list> Render specific layers (e.g., --layers include 1,3)");
            // M3 routing options
            Console.Error.WriteLine("  --route-mode <orthogonal|straight>  Preferred connector style (M3)");
            Console.Error.WriteLine("  --bundle-by <lane|group|nodepair|none> Group edges for bundling (M3)");
            Console.Error.WriteLine("  --bundle-sep <in>       Gap between bundled edges (M3)");
            Console.Error.WriteLine("  --channel-gap <in>      Reserved corridor gap between lanes (M3)");
            Console.Error.WriteLine("  --route-around <true|false> Route around lane containers (M3)");
            Console.Error.WriteLine("  --container-padding <in>   Container padding (M4)");
            Console.Error.WriteLine("  --container-corner <in>    Container corner radius (M4)");
            // M5 diagnostics
            Console.Error.WriteLine("  --diag-level <info|warning|error>   Minimum diagnostics level");
            Console.Error.WriteLine("  --diag-lane-warn <0..1>    Lane occupancy warn ratio");
            Console.Error.WriteLine("  --diag-lane-error <0..1>   Lane occupancy error ratio");
            Console.Error.WriteLine("  --diag-page-warn <0..1>    Page occupancy warn ratio");
            Console.Error.WriteLine("  --diag-cross-warn <n>      Planned crossings warn threshold");
            Console.Error.WriteLine("  --diag-cross-err <n>       Planned crossings error threshold");
            Console.Error.WriteLine("  --diag-util-warn <0..100>  Corridor utilization warn minimum (%)");
            Console.Error.WriteLine("  --diag-json [path]         Emit structured diagnostics JSON (optional path)");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Environment overrides:");
            Console.Error.WriteLine("  VDG_DIAG_LANE_WARN / VDG_DIAG_LANE_ERR / VDG_DIAG_PAGE_WARN  Override crowding ratios (0..1).");
            Console.Error.WriteLine("  VDG_DIAG_FAIL_LEVEL=<warning|error>   Fail when diagnostics reach level (default warn-only).");
            Console.Error.WriteLine("  -h, --help              Show this help");
        }

        private static void TryConsoleError(string message)
        {
            try { Console.Error.WriteLine(message); }
            catch (ObjectDisposedException) { }
        }

        private static DiagramModel LoadDiagramModel(string inputPath)
        {
            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException("Diagram model file not found.", inputPath);
            }

            var json = File.ReadAllText(inputPath);
            var dto = JsonSerializer.Deserialize<DiagramEnvelope>(json, JsonOptions)
                      ?? throw new InvalidDataException("Diagram JSON deserialized to null.");

            var schemaVersion = dto.SchemaVersion;
            if (!string.IsNullOrEmpty(schemaVersion) &&
                !SupportedSchemaVersions.Any(v => string.Equals(v, schemaVersion, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(string.Format(
                    "Unsupported schemaVersion '{0}'. Supported versions: {1}.",
                    schemaVersion,
                    string.Join(", ", SupportedSchemaVersions)));
            }

            if (dto.Nodes is null || dto.Nodes.Count == 0)
            {
                throw new InvalidDataException("Diagram contains no nodes.");
            }

            var nodes = new List<Node>(dto.Nodes.Count);
            foreach (var nodeDto in dto.Nodes)
            {
                if (string.IsNullOrWhiteSpace(nodeDto.Id))
                {
                    throw new InvalidDataException("Node is missing 'id'.");
                }

                if (string.IsNullOrWhiteSpace(nodeDto.Label))
                {
                    throw new InvalidDataException($"Node '{nodeDto.Id}' is missing 'label'.");
                }

                var nodeId = nodeDto.Id!;
                var nodeLabel = nodeDto.Label!;
                var node = new Node(nodeId, nodeLabel);

                if (!string.IsNullOrWhiteSpace(nodeDto.Type))
                {
                    node.Type = nodeDto.Type;
                }

                if (!string.IsNullOrWhiteSpace(nodeDto.Tier))
                {
                    node.Tier = nodeDto.Tier;
                }

                if (!string.IsNullOrWhiteSpace(nodeDto.GroupId))
                {
                    node.GroupId = nodeDto.GroupId;
                }

                if (nodeDto.Size != null &&
                    nodeDto.Size.Width.HasValue && nodeDto.Size.Width.Value > 0 &&
                    nodeDto.Size.Height.HasValue && nodeDto.Size.Height.Value > 0)
                {
                    node.Size = new Size(nodeDto.Size.Width.Value, nodeDto.Size.Height.Value);
                }

                ApplyStyle(node.Style, nodeDto.Style);
                ApplyMetadata(node.Metadata, nodeDto.Metadata);

                // M4: explicit container membership
                if (!string.IsNullOrWhiteSpace(nodeDto.ContainerId))
                {
                    node.Metadata["node.containerId"] = nodeDto.ContainerId!;
                }

                // M3: map ports hints into node metadata for downstream algorithms
                if (nodeDto.Ports != null)
                {
                    if (!string.IsNullOrWhiteSpace(nodeDto.Ports.InSide))
                    {
                        node.Metadata["node.ports.inSide"] = nodeDto.Ports.InSide!;
                    }
                    if (!string.IsNullOrWhiteSpace(nodeDto.Ports.OutSide))
                    {
                        node.Metadata["node.ports.outSide"] = nodeDto.Ports.OutSide!;
                    }
                }

                nodes.Add(node);
            }

            var edges = new List<Edge>();
            if (dto.Edges != null)
            {
                foreach (var edgeDto in dto.Edges)
                {
                    if (string.IsNullOrWhiteSpace(edgeDto.SourceId) || string.IsNullOrWhiteSpace(edgeDto.TargetId))
                    {
                        throw new InvalidDataException($"Edge '{edgeDto.Id}' is missing source or target id.");
                    }

                    var sourceId = edgeDto.SourceId!;
                    var targetId = edgeDto.TargetId!;
                    var edgeId = string.IsNullOrWhiteSpace(edgeDto.Id)
                        ? string.Format("{0}->{1}", sourceId, targetId)
                        : edgeDto.Id!;

                    var edge = new Edge(edgeId, sourceId, targetId, edgeDto.Label)
                    {
                        Directed = edgeDto.Directed ?? true
                    };

                    ApplyStyle(edge.Style, edgeDto.Style);
                    ApplyMetadata(edge.Metadata, edgeDto.Metadata);

                    // M3: map waypoints/priority into metadata for routing layer
                    if (edgeDto.Priority.HasValue)
                    {
                        edge.Metadata["edge.priority"] = edgeDto.Priority.Value.ToString(CultureInfo.InvariantCulture);
                    }
                    if (edgeDto.Waypoints != null && edgeDto.Waypoints.Count > 0)
                    {
                        try
                        {
                            var wps = edgeDto.Waypoints
                                .Where(p => p != null && p.X.HasValue && p.Y.HasValue)
                                .Select(p => new { x = p.X!.Value, y = p.Y!.Value })
                                .ToList();
                            if (wps.Count > 0)
                            {
                                var jsonWps = JsonSerializer.Serialize(wps, JsonOptions);
                                edge.Metadata["edge.waypoints"] = jsonWps;
                            }
                        }
                        catch { /* ignore bad waypoint data */ }
                    }

                    edges.Add(edge);
                }
            }

            var model = new DiagramModel(nodes, edges);
            if (dto.Metadata != null)
            {
                ApplyMetadata(model.Metadata, dto.Metadata.Properties);

                if (!string.IsNullOrWhiteSpace(dto.Metadata.Title))
                {
                    model.Metadata["title"] = dto.Metadata.Title!;
                }

                if (!string.IsNullOrWhiteSpace(dto.Metadata.Description))
                {
                    model.Metadata["description"] = dto.Metadata.Description!;
                }

                if (!string.IsNullOrWhiteSpace(dto.Metadata.Version))
                {
                    model.Metadata["version"] = dto.Metadata.Version!;
                }

                if (!string.IsNullOrWhiteSpace(dto.Metadata.Author))
                {
                    model.Metadata["author"] = dto.Metadata.Author!;
                }

                if (!string.IsNullOrWhiteSpace(dto.Metadata.CreatedUtc))
                {
                    model.Metadata["createdUtc"] = dto.Metadata.CreatedUtc!;
                }

                if (dto.Metadata.Tags != null && dto.Metadata.Tags.Count > 0)
                {
                    var cleaned = dto.Metadata.Tags
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .Select(tag => tag!.Trim())
                        .Where(tag => tag.Length > 0)
                        .ToArray();

                    if (cleaned.Length > 0)
                    {
                        model.Metadata["tags"] = string.Join(", ", cleaned);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.DiagramType))
            {
                model.Metadata["diagramType"] = dto.DiagramType!;
            }

            if (dto.Layout != null)
            {
                if (!string.IsNullOrWhiteSpace(dto.Layout.OutputMode))
                {
                    model.Metadata["layout.outputMode"] = dto.Layout.OutputMode!.Trim();
                }

                if (!string.IsNullOrWhiteSpace(dto.Layout.Orientation))
                {
                    model.Metadata["layout.orientation"] = dto.Layout.Orientation!.Trim();
                }

                if (dto.Layout.Tiers != null && dto.Layout.Tiers.Count > 0)
                {
                    var tiers = dto.Layout.Tiers
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Select(t => t!.Trim())
                        .Where(t => t.Length > 0)
                        .ToArray();

                    if (tiers.Length > 0)
                    {
                        model.Metadata["layout.tiers"] = string.Join(",", tiers);
                    }
                }

                if (dto.Layout.Diagnostics != null)
                {
                    if (dto.Layout.Diagnostics.Enabled.HasValue)
                    {
                        model.Metadata["layout.diagnostics.enabled"] = dto.Layout.Diagnostics.Enabled.Value.ToString();
                    }
                    if (dto.Layout.Diagnostics.PageHeightThresholdIn.HasValue)
                    {
                        model.Metadata["layout.diagnostics.pageHeightThresholdIn"] = dto.Layout.Diagnostics.PageHeightThresholdIn.Value.ToString(CultureInfo.InvariantCulture);
                    }
                    if (dto.Layout.Diagnostics.LaneMaxNodes.HasValue)
                    {
                        model.Metadata["layout.diagnostics.laneMaxNodes"] = dto.Layout.Diagnostics.LaneMaxNodes.Value.ToString(CultureInfo.InvariantCulture);
                    }
                    if (!string.IsNullOrWhiteSpace(dto.Layout.Diagnostics.Level))
                        model.Metadata["layout.diagnostics.level"] = dto.Layout.Diagnostics.Level!.Trim();
                    if (dto.Layout.Diagnostics.LaneCrowdWarnRatio.HasValue)
                        model.Metadata["layout.diagnostics.laneCrowdWarnRatio"] = dto.Layout.Diagnostics.LaneCrowdWarnRatio.Value.ToString(CultureInfo.InvariantCulture);
                    if (dto.Layout.Diagnostics.LaneCrowdErrorRatio.HasValue)
                        model.Metadata["layout.diagnostics.laneCrowdErrorRatio"] = dto.Layout.Diagnostics.LaneCrowdErrorRatio.Value.ToString(CultureInfo.InvariantCulture);
                    if (dto.Layout.Diagnostics.PageCrowdWarnRatio.HasValue)
                        model.Metadata["layout.diagnostics.pageCrowdWarnRatio"] = dto.Layout.Diagnostics.PageCrowdWarnRatio.Value.ToString(CultureInfo.InvariantCulture);
                }

                if (dto.Layout.Spacing != null)
                {
                    if (dto.Layout.Spacing.Horizontal.HasValue)
                    {
                        model.Metadata["layout.spacing.horizontal"] = dto.Layout.Spacing.Horizontal.Value.ToString(CultureInfo.InvariantCulture);
                    }
                    if (dto.Layout.Spacing.Vertical.HasValue)
                    {
                        model.Metadata["layout.spacing.vertical"] = dto.Layout.Spacing.Vertical.Value.ToString(CultureInfo.InvariantCulture);
                    }
                }

                if (dto.Layout.Page != null)
                {
                    if (dto.Layout.Page.WidthIn.HasValue)
                        model.Metadata["layout.page.widthIn"] = dto.Layout.Page.WidthIn.Value.ToString(CultureInfo.InvariantCulture);
                    if (dto.Layout.Page.HeightIn.HasValue)
                        model.Metadata["layout.page.heightIn"] = dto.Layout.Page.HeightIn.Value.ToString(CultureInfo.InvariantCulture);
                    if (dto.Layout.Page.MarginIn.HasValue)
                        model.Metadata["layout.page.marginIn"] = dto.Layout.Page.MarginIn.Value.ToString(CultureInfo.InvariantCulture);
                    if (dto.Layout.Page.Paginate.HasValue)
                        model.Metadata["layout.page.paginate"] = dto.Layout.Page.Paginate.Value.ToString();
                    if (dto.Layout.Page.Plan != null && dto.Layout.Page.Plan.LaneSplitAllowed.HasValue)
                        model.Metadata["layout.page.plan.laneSplitAllowed"] = dto.Layout.Page.Plan.LaneSplitAllowed.Value.ToString();
                }

                // M3: layout.routing mapping to metadata for downstream routing engine
                if (dto.Layout.Routing != null)
                {
                    if (!string.IsNullOrWhiteSpace(dto.Layout.Routing.Mode))
                        model.Metadata["layout.routing.mode"] = dto.Layout.Routing.Mode!.Trim();
                    if (!string.IsNullOrWhiteSpace(dto.Layout.Routing.BundleBy))
                        model.Metadata["layout.routing.bundleBy"] = dto.Layout.Routing.BundleBy!.Trim();
                    if (dto.Layout.Routing.BundleSeparationIn.HasValue)
                        model.Metadata["layout.routing.bundleSeparationIn"] = dto.Layout.Routing.BundleSeparationIn.Value.ToString(CultureInfo.InvariantCulture);
                    if (dto.Layout.Routing.Channels != null && dto.Layout.Routing.Channels.GapIn.HasValue)
                        model.Metadata["layout.routing.channels.gapIn"] = dto.Layout.Routing.Channels.GapIn.Value.ToString(CultureInfo.InvariantCulture);
                    if (dto.Layout.Routing.RouteAroundContainers.HasValue)
                        model.Metadata["layout.routing.routeAroundContainers"] = dto.Layout.Routing.RouteAroundContainers.Value.ToString();
                }

                // M4: layout.containers mapping to metadata
                if (dto.Layout.Containers != null)
                {
                    if (dto.Layout.Containers.PaddingIn.HasValue)
                        model.Metadata["layout.containers.paddingIn"] = dto.Layout.Containers.PaddingIn.Value.ToString(CultureInfo.InvariantCulture);
                    if (dto.Layout.Containers.CornerIn.HasValue)
                        model.Metadata["layout.containers.cornerIn"] = dto.Layout.Containers.CornerIn.Value.ToString(CultureInfo.InvariantCulture);
                    if (dto.Layout.Containers.Style != null)
                    {
                        if (!string.IsNullOrWhiteSpace(dto.Layout.Containers.Style.Fill))
                            model.Metadata["layout.containers.style.fill"] = dto.Layout.Containers.Style.Fill!;
                        if (!string.IsNullOrWhiteSpace(dto.Layout.Containers.Style.Stroke))
                            model.Metadata["layout.containers.style.stroke"] = dto.Layout.Containers.Style.Stroke!;
                        if (!string.IsNullOrWhiteSpace(dto.Layout.Containers.Style.LinePattern))
                            model.Metadata["layout.containers.style.linePattern"] = dto.Layout.Containers.Style.LinePattern!;
                    }
                }
            }

            // M4: persist explicit containers list
            if (dto.Containers != null && dto.Containers.Count > 0)
            {
                try
                {
                    var jsonContainers = JsonSerializer.Serialize(dto.Containers, JsonOptions);
                    model.Metadata["layout.containers.json"] = jsonContainers;
                    model.Metadata["layout.containers.count"] = dto.Containers.Count.ToString(CultureInfo.InvariantCulture);
                }
                catch { }
            }

            return model;
        }
        private static void ApplyStyle(ShapeStyle style, StyleDto? dto)
        {
            if (style == null || dto == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(dto.Fill))
            {
                style.FillColor = dto.Fill;
            }

            if (!string.IsNullOrWhiteSpace(dto.Stroke))
            {
                style.StrokeColor = dto.Stroke;
            }

            if (!string.IsNullOrWhiteSpace(dto.LinePattern))
            {
                style.LinePattern = dto.LinePattern;
            }
        }

        private static void ApplyMetadata(IDictionary<string, string> target, IDictionary<string, string>? source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var kvp in source)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Key))
                {
                    target[kvp.Key] = kvp.Value ?? string.Empty;
                }
            }
        }

        private static void EnsureDirectory(string outputPath)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        // Global diagnostics gating helpers (used outside EmitDiagnostics)
        private static int DiagRank(string level)
        {
            if (string.Equals(level, "error", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(level, "warning", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(level, "warn", StringComparison.OrdinalIgnoreCase)) return 1;
            return 0; // info and unknown
        }

        private static bool ShouldEmit(DiagramModel model, string level)
        {
            var min = "info";
            if (model.Metadata.TryGetValue("layout.diagnostics.level", out var lvl) && !string.IsNullOrWhiteSpace(lvl))
            {
                min = lvl.Trim();
            }
            return DiagRank(level) >= DiagRank(min);
        }

        private static DiagnosticsSummary EmitDiagnostics(
            DiagramModel model,
            LayoutResult layout,
            double? pageHeightOverride = null,
            int? laneMaxOverride = null,
            PagePlan[]? pagePlans = null,
            PageSplitOptions? pageOptions = null,
            PlannerMetrics? plannerMetrics = null,
            PlannerSummaryStats? plannerSummary = null,
            DiagramDataset? plannerDataset = null,
            IEnumerable<string>? filteredModules = null,
            LayoutPlan? layoutPlan = null)
        {
            var summary = new DiagnosticsSummary();
            int highestRank = 0;
            var pageDetails = new Dictionary<int, PageDiagnosticsDetail>();
            var outputMode = GetOutputMode(model);
            summary.OutputMode = outputMode;

            if (layoutPlan != null)
            {
                summary.LayoutCanvasWidth = layoutPlan.CanvasWidth;
                summary.LayoutCanvasHeight = layoutPlan.CanvasHeight;
                if (layoutPlan.Stats != null)
                {
                    summary.LayoutNodeCount = layoutPlan.Stats.NodeCount;
                    summary.LayoutModuleCount = layoutPlan.Stats.ModuleCount;
                    summary.LayoutContainerCount = layoutPlan.Stats.ContainerCount;
                }
            }

            PageDiagnosticsDetail GetPageDetail(int pageIndex)
            {
                if (pageIndex < 0)
                {
                    pageIndex = 0;
                }

                if (!pageDetails.TryGetValue(pageIndex, out var detail))
                {
                    detail = new PageDiagnosticsDetail { PageNumber = pageIndex + 1 };
                    pageDetails[pageIndex] = detail;
                }

                return detail;
            }

            // Diagnostics level gating (info|warning|error); default info
            string minLevel = "info";
            if (model.Metadata.TryGetValue("layout.diagnostics.level", out var lvl) && !string.IsNullOrWhiteSpace(lvl))
            {
                minLevel = lvl.Trim().ToLowerInvariant();
            }
            static int LevelRank(string level)
            {
                if (string.Equals(level, "error", StringComparison.OrdinalIgnoreCase)) return 2;
                if (string.Equals(level, "warning", StringComparison.OrdinalIgnoreCase)) return 1;
                // treat unknown/hint/info as info
                return 0;
            }
            int minRank = LevelRank(minLevel);
            void Emit(string level, string message)
            {
                int r = LevelRank(level);
                if (r >= minRank)
                {
                    if (r > highestRank) highestRank = r;
                    try
                    {
                        Console.WriteLine($"{level}: {message}");
                    }
                    catch (ObjectDisposedException)
                    {
                        // ignore logging failures when console is redirected and disposed by tests
                    }
                }
            }

            // Collect issues for JSON (apply same gating)
            var gatedIssues = new List<DiagIssue>();
            void AddIssue(string code, string level, string message, string? lane = null, int? page = null)
            {
                var rank = LevelRank(level);
                if (rank >= minRank)
                {
                    if (rank > highestRank) highestRank = rank;
                    gatedIssues.Add(new DiagIssue { Code = code, Level = level, Message = message, Lane = lane, Page = page });
            }
        }

        static int ClampLayerBudget(string? raw, int defaultValue)
        {
            if (!string.IsNullOrWhiteSpace(raw) &&
                int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                if (parsed < 1) return 1;
                if (parsed > 1000) return 1000;
                return parsed;
            }
            return defaultValue;
        }

            if (pagePlans != null && pagePlans.Length > 0)
            {
                var connectorLimitValue = (pageOptions != null && pageOptions.MaxConnectors > 0)
                    ? pageOptions.MaxConnectors
                    : int.MaxValue;
                var connectorsLimit = connectorLimitValue < int.MaxValue
                    ? connectorLimitValue.ToString(CultureInfo.InvariantCulture)
                    : "unbounded";
                Emit("info", $"paging planner planned {pagePlans.Length} page(s) (connector limit {connectorsLimit})");

                if (pageOptions != null)
                {
                    var occupancyLimit = (!pageOptions.LaneSplitAllowed && pageOptions.MaxOccupancyPercent > 0)
                        ? pageOptions.MaxOccupancyPercent
                        : double.PositiveInfinity;

                    foreach (var plan in pagePlans)
                    {
                        var pageNumber = plan.PageIndex + 1;
                        var pageDetail = GetPageDetail(plan.PageIndex);
                        pageDetail.PlannedConnectors = plan.Connectors;
                        pageDetail.PlannedNodes = plan.Nodes;
                        pageDetail.ModuleCount = plan.Modules?.Length ?? 0;
                        if (connectorLimitValue < int.MaxValue)
                        {
                            pageDetail.ConnectorLimit = connectorLimitValue;
                            if (plan.Connectors > connectorLimitValue)
                            {
                                pageDetail.ConnectorOverLimit = plan.Connectors - connectorLimitValue;
                                var msg = $"paging planner connectors exceed limit on page {pageNumber}: connectors={plan.Connectors} limit={connectorLimitValue}";
                                Emit("info", msg);
                                AddIssue("PagePlanConnectors", "info", msg, page: pageNumber);
                                summary.ConnectorOverLimitPageCount++;
                            }
                        }

                        if (!double.IsInfinity(occupancyLimit) && plan.Occupancy > occupancyLimit)
                        {
                            pageDetail.HasPageCrowding = true;
                            var msg = $"paging planner occupancy exceeds limit on page {pageNumber}: occupancy={plan.Occupancy:F1}% limit={occupancyLimit:F1}%";
                            Emit("info", msg);
                            AddIssue("PagePlanOccupancy", "info", msg, page: pageNumber);
                        }
                    }
                }
            }

            if (layoutPlan?.Layers != null && layoutPlan.Layers.Length > 0)
            {
                var layers = layoutPlan.Layers;
                var bridges = layoutPlan.Bridges ?? Array.Empty<LayerBridge>();
                var moduleLookup = new Dictionary<string, ModuleStats>(StringComparer.OrdinalIgnoreCase);
                if (plannerDataset?.Modules != null)
                {
                    foreach (var module in plannerDataset.Modules)
                    {
                        if (module == null) continue;
                        var moduleId = module.ModuleId;
                        if (!string.IsNullOrWhiteSpace(moduleId))
                        {
                            moduleLookup[moduleId!] = module;
                        }
                    }
                }

                var softShapeLimit = ClampLayerBudget(
                    model.Metadata.TryGetValue("layout.layers.maxShapes", out var lms) ? lms : null,
                    900);
                var softConnectorLimit = ClampLayerBudget(
                    model.Metadata.TryGetValue("layout.layers.maxConnectors", out var lmc) ? lmc : null,
                    900);
                const int hardShapeLimit = 1000;
                const int hardConnectorLimit = 1000;

                summary.LayerCount = layers.Length;
                summary.BridgeCount = bridges.Length;

                var orderedLayers = layers.OrderBy(lp => lp.LayerIndex).ToArray();
                foreach (var plan in orderedLayers)
                {
                    var detail = new LayerDiagnosticsDetail
                    {
                        LayerNumber = plan.LayerIndex + 1,
                        ShapeCount = plan.ShapeCount,
                        ConnectorCount = plan.ConnectorCount,
                        ModuleCount = plan.Modules?.Length ?? 0
                    };

                    if (plan.Modules != null)
                    {
                        foreach (var moduleId in plan.Modules.Where(m => !string.IsNullOrWhiteSpace(m)))
                        {
                            detail.Modules.Add(moduleId);
                            if (moduleLookup.TryGetValue(moduleId, out var stats))
                            {
                                var moduleShapeShare = Math.Max(1, stats.NodeCount + 1);
                                var moduleConnectorShare = Math.Max(1, stats.ConnectorCount / 2);
                                if (moduleShapeShare > hardShapeLimit || moduleConnectorShare > hardConnectorLimit)
                                {
                                    if (!detail.OverflowModules.Contains(moduleId))
                                    {
                                        detail.OverflowModules.Add(moduleId);
                                    }
                                }
                                else
                                {
                                    if (moduleShapeShare > softShapeLimit) detail.SoftShapeLimitExceeded = true;
                                    if (moduleConnectorShare > softConnectorLimit) detail.SoftConnectorLimitExceeded = true;
                                }
                            }
                        }
                    }

                    if (detail.ShapeCount > softShapeLimit) detail.SoftShapeLimitExceeded = true;
                    if (detail.ConnectorCount > softConnectorLimit) detail.SoftConnectorLimitExceeded = true;
                    if (detail.ShapeCount > hardShapeLimit) detail.HardShapeLimitExceeded = true;
                    if (detail.ConnectorCount > hardConnectorLimit) detail.HardConnectorLimitExceeded = true;

                    var overflowModulesText = detail.OverflowModules.Count > 0
                        ? $" ({string.Join(", ", detail.OverflowModules)})"
                        : string.Empty;

                    if (detail.HardShapeLimitExceeded || detail.HardConnectorLimitExceeded || detail.OverflowModules.Count > 0)
                    {
                        summary.LayerOverflowCount++;
                        var msg = detail.OverflowModules.Count > 0
                            ? $"layer {detail.LayerNumber} contains module(s) exceeding hard layer cap{overflowModulesText}; shapes={detail.ShapeCount}, connectors={detail.ConnectorCount}."
                            : $"layer {detail.LayerNumber} exceeds hard layer cap; shapes={detail.ShapeCount}, connectors={detail.ConnectorCount}.";
                        Emit("warning", msg);
                        AddIssue("LayerOverflow", "warning", msg);
                    }
                    else if (detail.SoftShapeLimitExceeded || detail.SoftConnectorLimitExceeded)
                    {
                        summary.LayerCrowdingCount++;
                        var msg = $"layer {detail.LayerNumber} exceeds soft layer budget; shapes={detail.ShapeCount}/{softShapeLimit}, connectors={detail.ConnectorCount}/{softConnectorLimit}.";
                        Emit("info", msg);
                        AddIssue("LayerBudget", "info", msg);
                    }

                    summary.Layers.Add(detail);
                }

                if (bridges.Length > 0)
                {
                    foreach (var bridge in bridges)
                    {
                        if (bridge.SourceLayer >= 0 && bridge.SourceLayer < summary.Layers.Count)
                        {
                            summary.Layers[bridge.SourceLayer].BridgesOut++;
                        }
                        if (bridge.TargetLayer >= 0 && bridge.TargetLayer < summary.Layers.Count)
                        {
                            summary.Layers[bridge.TargetLayer].BridgesIn++;
                        }
                    }

                    var msg = $"layer planner created {summary.LayerCount} layer(s) with {summary.BridgeCount} bridge(s).";
                    Emit("info", msg);
                    AddIssue("LayerBridge", "info", msg);
                }
                else
                {
                    Emit("info", $"layer planner created {summary.LayerCount} layer(s) (no cross-layer bridges).");
                }
            }

            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);

            int missingTier = 0;
            var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var laneCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in tiers) laneCounts[t] = 0;

            foreach (var node in model.Nodes)
            {
                var tier = node.Tier;
                if (string.IsNullOrWhiteSpace(tier) && node.Metadata.TryGetValue("tier", out var m)) tier = m;

                if (string.IsNullOrWhiteSpace(tier))
                {
                    missingTier++;
                    laneCounts[tiers[0]] = laneCounts[tiers[0]] + 1;
                    continue;
                }

                if (!tiersSet.Contains(tier!))
                {
                    unknown.Add(tier!);
                    laneCounts[tiers[0]] = laneCounts[tiers[0]] + 1;
                }
                else
                {
                    laneCounts[tier!] = laneCounts.TryGetValue(tier!, out var c) ? c + 1 : 1;
                }
            }

            if (missingTier > 0)
            {
                var msg = $"{missingTier} node(s) have no 'tier'; placed into '{tiers[0]}' lane.";
                Emit("warning", msg);
                AddIssue("MissingTier", "warning", msg);
            }

            if (unknown.Count > 0)
            {
                var msg = $"{unknown.Count} unknown tier name(s) encountered: {string.Join(", ", unknown)}. Nodes were placed into '{tiers[0]}' lane.";
                Emit("warning", msg);
                AddIssue("UnknownTier", "warning", msg);
            }

            // Read diagnostics settings
            bool enabled = true;
            if (model.Metadata.TryGetValue("layout.diagnostics.enabled", out var enabledRaw))
            {
                if (bool.TryParse(enabledRaw, out var b)) enabled = b;
            }
            if (!enabled)
            {
                summary.Rank = highestRank;
                return summary;
            }

            // M3: minimal routing diagnostics
            var routeMode = (model.Metadata.TryGetValue("layout.routing.mode", out var rm) && !string.IsNullOrWhiteSpace(rm)) ? rm.Trim() : "orthogonal";
            Emit("info", $"routing mode: {routeMode}");
            Emit("info", $"connector count: {model.Edges.Count}");

            double pageThreshold = 11.0;
            if (pageHeightOverride.HasValue) pageThreshold = pageHeightOverride.Value;
            else if (model.Metadata.TryGetValue("layout.diagnostics.pageHeightThresholdIn", out var ph) &&
                     double.TryParse(ph, NumberStyles.Float, CultureInfo.InvariantCulture, out var phv))
            {
                pageThreshold = phv;
            }

            int laneMax = 12;
            if (laneMaxOverride.HasValue) laneMax = laneMaxOverride.Value;
            else if (model.Metadata.TryGetValue("layout.diagnostics.laneMaxNodes", out var lm) &&
                     int.TryParse(lm, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lmv))
            {
                laneMax = lmv;
            }

            // Page overflow hint based on layout bounds vs. threshold
            var bounds = ComputeLayoutBounds(layout);
            var width = (bounds.maxRight - bounds.minLeft) + (Margin * 2);
            var height = (bounds.maxTop - bounds.minBottom) + (Margin * 2);

            if (height > pageThreshold)
            {
                var msg = $"layout height {height:F1}in exceeds threshold {pageThreshold:F1}in; consider pagination, reducing spacing, or vertical orientation.";
                // treat hint as info for gating
                Emit("info", msg);
                AddIssue("LayoutHeightThreshold", "warning", msg);
            }

            // Lane crowding hint
            foreach (var kv in laneCounts)
            {
                if (kv.Value > laneMax)
                {
                    var msg = $"lane '{kv.Key}' contains {kv.Value} nodes (max {laneMax}); consider splitting the lane or paginating.";
                    Emit("info", msg);
                    AddIssue("LaneNodeMax", "warning", msg, lane: kv.Key);
                }
            }

            // Pagination diagnostics (M2): estimate pages, bands, cross‑page edges
            var margin = GetPageMargin(model) ?? Margin;
            var title = GetTitleHeight(model);
            var configuredPageHeight = GetPageHeight(model);
            if (configuredPageHeight.HasValue)
            {
                var usable = configuredPageHeight.Value - (2 * margin) - title;
                if (usable > 0)
                {
                    var nodePage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var tierPageCounts = new Dictionary<string, Dictionary<int, int>>(StringComparer.OrdinalIgnoreCase);

                    foreach (var t in tiers) tierPageCounts[t] = new Dictionary<int, int>();

                    foreach (var nl in layout.Nodes)
                    {
                        var normY = nl.Position.Y - (float)bounds.minBottom;
                        var pi = (int)Math.Floor(normY / (float)usable);
                        if (pi < 0) pi = 0;
                        nodePage[nl.Id] = pi;

                        // lookup node tier
                        var nodeTier = string.Empty;
                        var foundNode = model.Nodes.FirstOrDefault(n => string.Equals(n.Id, nl.Id, StringComparison.OrdinalIgnoreCase));
                        if (foundNode != null)
                        {
                            nodeTier = !string.IsNullOrWhiteSpace(foundNode.Tier) ? foundNode.Tier! : (foundNode.Metadata.TryGetValue("tier", out var t) ? t : tiers.First());
                        }
                        else nodeTier = tiers.First();

                        if (!tierPageCounts.TryGetValue(nodeTier, out var map)) { map = new Dictionary<int, int>(); tierPageCounts[nodeTier] = map; }
                        map[pi] = (map.TryGetValue(pi, out var c) ? c : 0) + 1;
                    }

                    var pageCount = nodePage.Values.Count == 0 ? 1 : nodePage.Values.Max() + 1;
                    Emit("info", $"pagination analysis - usable height {usable:F2}in, predicted pages {pageCount}.");

                    foreach (var kv in tierPageCounts)
                    {
                        if (kv.Value.Count == 0) continue;
                        var parts = kv.Value.OrderBy(p => p.Key).Select(p => $"{p.Key + 1}:{p.Value}");
                        Emit("info", $"lane '{kv.Key}' nodes per page => {string.Join(", ", parts)}");
                    }

                    int cross = 0;
                    foreach (var e in model.Edges)
                    {
                        if (!nodePage.TryGetValue(e.SourceId, out var sp) || !nodePage.TryGetValue(e.TargetId, out var tp)) continue;
                        if (sp != tp) cross++;
                    }
                    if (cross > 0)
                    {
                        Emit("info", $"cross-page edges: {cross}");
                    }

                    // Single node too tall to ever fit
                    foreach (var nl in layout.Nodes)
                    {
                        var hNode = (nl.Size.HasValue && nl.Size.Value.Height > 0) ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                        if (hNode > usable)
                        {
                            var msg = $"node '{nl.Id}' height {hNode:F2}in exceeds usable page height {usable:F2}in. Increase page height/margin or resize node.";
                            Emit("warning", msg);
                            AddIssue("NodeTooTall", "warning", msg);
                            var pageIndex = nodePage.TryGetValue(nl.Id, out var mapped) ? mapped : 0;
                            var pageDetail = GetPageDetail(pageIndex);
                            if (!pageDetail.TruncatedNodes.Any(id => string.Equals(id, nl.Id, StringComparison.OrdinalIgnoreCase)))
                            {
                                pageDetail.TruncatedNodes.Add(nl.Id);
                                pageDetail.TruncatedNodeCount++;
                            }
                            pageDetail.HasPartialRender = true;
                            summary.TruncatedNodeCount++;
                            summary.PartialRender = true;
                        }
                    }
                }
            }

            var layoutOutputModeValue = model.Metadata.TryGetValue("layout.outputMode", out var layoutOutputModeRaw)
                ? (layoutOutputModeRaw ?? string.Empty).Trim()
                : string.Empty;
            var isViewModeLayout = string.Equals(layoutOutputModeValue, "view", StringComparison.OrdinalIgnoreCase);

            // M5: Lane overcrowding diagnostics (per page)
            try
            {
                var pageHeight = GetPageHeight(model) ?? pageHeightOverride ?? 0.0;
                if (pageHeight > 0)
                {
                      var margin2 = GetPageMargin(model) ?? Margin;
                      var title2 = GetTitleHeight(model);
                      var usable = pageHeight - (2 * margin2) - title2;
                      if (usable > 0 && IsFinite(usable))
                      {
                          static double? ReadRatioEnv(string key)
                          {
                              var raw = Environment.GetEnvironmentVariable(key);
                              if (string.IsNullOrWhiteSpace(raw)) return null;
                              if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0 && value <= 1)
                              {
                                  return value;
                              }
                              return null;
                          }

                        double laneWarn = 0.85, laneErr = 0.95, pageWarn = 0.90;
                        if (isViewModeLayout)
                        {
                            laneWarn = 0.92;
                            laneErr = 1.02;
                            pageWarn = 0.98;
                        }
                        var envLaneWarn = ReadRatioEnv("VDG_DIAG_LANE_WARN");
                        if (envLaneWarn.HasValue) laneWarn = envLaneWarn.Value;
                        var envLaneErr = ReadRatioEnv("VDG_DIAG_LANE_ERR");
                        if (envLaneErr.HasValue) laneErr = envLaneErr.Value;
                        var envPageWarn = ReadRatioEnv("VDG_DIAG_PAGE_WARN");
                          if (envPageWarn.HasValue) pageWarn = envPageWarn.Value;
                          if (model.Metadata.TryGetValue("layout.diagnostics.laneCrowdWarnRatio", out var lw) && double.TryParse(lw, NumberStyles.Float, CultureInfo.InvariantCulture, out var lwv)) laneWarn = lwv;
                          if (model.Metadata.TryGetValue("layout.diagnostics.laneCrowdErrorRatio", out var le) && double.TryParse(le, NumberStyles.Float, CultureInfo.InvariantCulture, out var lev)) laneErr = lev;
                          if (model.Metadata.TryGetValue("layout.diagnostics.pageCrowdWarnRatio", out var pw) && double.TryParse(pw, NumberStyles.Float, CultureInfo.InvariantCulture, out var pwv)) pageWarn = pwv;

                          var vs = 0.6; if (model.Metadata.TryGetValue("layout.spacing.vertical", out var vsv) && double.TryParse(vsv, NumberStyles.Float, CultureInfo.InvariantCulture, out var vparsed)) vs = vparsed;

                        var (minLeft2, minBottom2, _, _) = ComputeLayoutBounds(layout);
                        var perPageTier = new Dictionary<(int page, string tier), (double sumH, int count)>(new PerPageTierComparer());
                        var nodeMap2 = BuildNodeMap(model);
                        foreach (var nl in layout.Nodes)
                        {
                            var yNorm = nl.Position.Y - (float)minBottom2;
                            var pi = (int)Math.Floor(yNorm / (float)usable); if (pi < 0) pi = 0;
                            if (!nodeMap2.TryGetValue(nl.Id, out var node)) continue;
                            var t = !string.IsNullOrWhiteSpace(node.Tier) ? node.Tier! : (node.Metadata.TryGetValue("tier", out var tv) ? tv : tiers.First());
                            if (!tiersSet.Contains(t)) t = tiers.First();
                            var h = (nl.Size.HasValue && nl.Size.Value.Height > 0) ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                            var key = (pi, t);
                            var cur = perPageTier.TryGetValue(key, out var v) ? v : (0.0, 0);
                            var sumCur = cur.Item1; var countCur = cur.Item2;
                            perPageTier[key] = (sumCur + h, countCur + 1);
                        }

                        // Aggregate per-page maxima for band crowding
                        var perPageMax = new Dictionary<int, double>();
                        var perPageTopHeights = new Dictionary<int, List<(string id, double h)>>();

                        foreach (var kv in perPageTier)
                        {
                            var page = kv.Key.page;
                            var tierName = kv.Key.tier;
                            var (sumH, count) = kv.Value;
                            var occupied = sumH + (count > 0 ? (count - 1) * vs : 0);
                            var ratio = occupied / usable;
                            var pageDetail = GetPageDetail(page);
                            if (ratio >= laneErr)
                            {
                                var msg = $"lane overcrowded: lane='{tierName}' page={page + 1} occupancy={(ratio * 100):F0}% nodes={count} usable={usable:F2}in";
                                var degradeToWarning =
                                    (pagePlans != null && pagePlans.Length > 1) ||
                                    isViewModeLayout ||
                                    (pageOptions != null && pageOptions.LaneSplitAllowed);
                                var severity = degradeToWarning ? "warning" : "error";
                                Emit(severity, msg);
                                AddIssue("LaneCrowding", severity, msg, lane: tierName, page: page + 1);
                                pageDetail.LaneAlerts.Add(new LaneAlert
                                {
                                    Tier = tierName,
                                    OccupancyRatio = ratio,
                                    Nodes = count,
                                    Severity = severity
                                });
                                if (degradeToWarning)
                                {
                                    summary.LaneCrowdingCount++;
                                    summary.PartialRender = true;
                                    pageDetail.HasPartialRender = true;
                                    pageDetail.LaneCrowdingWarnings++;
                                }
                                else
                                {
                                    summary.LaneOverflowCount++;
                                    pageDetail.HasPartialRender = true;
                                    pageDetail.LaneCrowdingErrors++;
                                }
                            }
                            else if (ratio >= laneWarn)
                            {
                                var msg = $"lane crowded: lane='{tierName}' page={page + 1} occupancy={(ratio * 100):F0}% nodes={count} usable={usable:F2}in";
                                Emit("warning", msg);
                                AddIssue("LaneCrowding", "warning", msg, lane: tierName, page: page + 1);
                                summary.LaneCrowdingCount++;
                                pageDetail.LaneCrowdingWarnings++;
                                pageDetail.LaneAlerts.Add(new LaneAlert
                                {
                                    Tier = tierName,
                                    OccupancyRatio = ratio,
                                    Nodes = count,
                                    Severity = "warning"
                                });
                            }

                            // update per-page maximum occupancy ratio across lanes
                            if (!perPageMax.TryGetValue(page, out var cur) || ratio > cur) perPageMax[page] = ratio;
                        }

                        // Build per-page top offenders by height
                        var nodeMapForTops = BuildNodeMap(model);
                        foreach (var nl in layout.Nodes)
                        {
                            var yNorm = nl.Position.Y - (float)minBottom2;
                            var pi = (int)Math.Floor(yNorm / (float)usable); if (pi < 0) pi = 0;
                            var h = (nl.Size.HasValue && nl.Size.Value.Height > 0) ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                            if (!perPageTopHeights.TryGetValue(pi, out var list)) { list = new List<(string, double)>(); perPageTopHeights[pi] = list; }
                            list.Add((nl.Id, h));
                        }
                        foreach (var kvp in perPageTopHeights) kvp.Value.Sort((a, b) => b.h.CompareTo(a.h));

                        // Page/band crowding based on max lane occupancy on the page
                        foreach (var kvp in perPageMax)
                        {
                            var page = kvp.Key; var r = kvp.Value; var pct = r * 100.0;
                            var tops = perPageTopHeights.TryGetValue(page, out var list) ? string.Join(", ", list.Take(3).Select(x => $"{x.id}({x.h:F2}in)")) : string.Empty;
                            var detail = GetPageDetail(page);
                            if (r > 1.0)
                            {
                                var msg = $"page overflow: page={page + 1} occupancy={pct:F0}% (usable {usable:F2}in); top: [{tops}]";
                                Emit("error", msg);
                                AddIssue("PageOverflow", "error", msg, page: page + 1);
                                summary.PageOverflowCount++;
                                summary.PartialRender = true;
                                detail.HasPageOverflow = true;
                                detail.HasPartialRender = true;
                            }
                            else if (r >= pageWarn)
                            {
                                var msg = $"page crowded: page={page + 1} occupancy={pct:F0}% (usable {usable:F2}in); top: [{tops}]";
                                Emit("warning", msg);
                                AddIssue("PageCrowding", "warning", msg, page: page + 1);
                                summary.PageCrowdingCount++;
                                detail.HasPageCrowding = true;
                            }
                        }
                    }
                }
            }
            catch { }

            // M3 diagnostics: bundle planning and straight-line crossing estimate
            try
            {
                var bundleBy = model.Metadata.TryGetValue("layout.routing.bundleBy", out var bb) ? (bb ?? "none").Trim() : "none";
                var nodeById = BuildNodeMap(model);

                // Build centers from layout (node center points)
                var layoutMap = BuildLayoutMap(layout);
                (double x, double y) GetCenter(string id)
                {
                    if (!layoutMap.TryGetValue(id, out var nl)) return (0, 0);
                    var w = (nl.Size.HasValue && nl.Size.Value.Width > 0) ? nl.Size.Value.Width : (float)DefaultNodeWidth;
                    var h = (nl.Size.HasValue && nl.Size.Value.Height > 0) ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                    return (nl.Position.X + (w / 2.0), nl.Position.Y + (h / 2.0));
                }

                // Bundling groups
                var groups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                string GetTierForNode(VDG.Core.Models.Node n)
                {
                    var t = !string.IsNullOrWhiteSpace(n.Tier) ? n.Tier! : (n.Metadata.TryGetValue("tier", out var tv) ? tv : tiers.First());
                    if (!tiersSet.Contains(t)) t = tiers.First();
                    return t;
                }

                foreach (var e in model.Edges)
                {
                    string key = "none";
                    if (string.Equals(bundleBy, "lane", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!nodeById.TryGetValue(e.SourceId, out var sn) || !nodeById.TryGetValue(e.TargetId, out var tn)) continue;
                        key = $"{GetTierForNode(sn)}->{GetTierForNode(tn)}";
                    }
                    else if (string.Equals(bundleBy, "group", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!nodeById.TryGetValue(e.SourceId, out var sn) || !nodeById.TryGetValue(e.TargetId, out var tn)) continue;
                        var sg = string.IsNullOrWhiteSpace(sn.GroupId) ? "~" : sn.GroupId!;
                        var tg = string.IsNullOrWhiteSpace(tn.GroupId) ? "~" : tn.GroupId!;
                        key = $"{sg}->{tg}";
                    }
                    else if (string.Equals(bundleBy, "nodepair", StringComparison.OrdinalIgnoreCase) || string.Equals(bundleBy, "nodePair", StringComparison.OrdinalIgnoreCase))
                    {
                        var a = e.SourceId; var b = e.TargetId;
                        var k1 = string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? a : b;
                        var k2 = string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? b : a;
                        key = $"{k1}<->{k2}";
                    }
                    else
                    {
                        continue;
                    }

                    groups[key] = groups.TryGetValue(key, out var c) ? c + 1 : 1;
                }

                if (groups.Count > 0)
                {
                    var maxBundle = groups.Values.Count == 0 ? 0 : groups.Values.Max();
                    Emit("info", $"bundles planned: {groups.Count} groups; max bundle size: {maxBundle}");
                }

                // Channels (vertical corridors) positions between tiers
                if (model.Metadata.TryGetValue("layout.routing.channels.gapIn", out var gapRaw) && double.TryParse(gapRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var gapIn))
                {
                    var tierRanges = new List<(string tier, double left, double right)>();
                    foreach (var t in tiers)
                    {
                        var xs = new List<double>();
                        var xe = new List<double>();
                        foreach (var nl in layout.Nodes)
                        {
                            if (!nodeById.TryGetValue(nl.Id, out var node)) continue;
                            var tn = GetTierForNode(node);
                            if (!string.Equals(tn, t, StringComparison.OrdinalIgnoreCase)) continue;
                            var w = (nl.Size.HasValue && nl.Size.Value.Width > 0) ? nl.Size.Value.Width : (float)DefaultNodeWidth;
                            xs.Add(nl.Position.X);
                            xe.Add(nl.Position.X + w);
                        }
                        if (xs.Count > 0)
                        {
                            tierRanges.Add((t, xs.Min(), xe.Max()));
                        }
                    }
                    var mids = new List<double>();
                    for (int i = 0; i + 1 < tierRanges.Count; i++)
                    {
                        mids.Add((tierRanges[i].right + tierRanges[i + 1].left) / 2.0);
                    }
                    if (mids.Count > 0)
                    {
                        var midsStr = string.Join(", ", mids.Select(v => v.ToString("F2", CultureInfo.InvariantCulture)));
                        // Use plain ASCII separator instead of approximate symbol
                        Emit("info", $"channels gapIn={gapIn:F2}in; vertical corridors at X~ {midsStr}");
                    }
                }

                // Straight-line crossing estimate (baseline)
                int crossings = 0;
                for (int i = 0; i < model.Edges.Count; i++)
                {
                    var e1 = model.Edges[i];
                    var a1 = GetCenter(e1.SourceId); var b1 = GetCenter(e1.TargetId);
                    for (int j = i + 1; j < model.Edges.Count; j++)
                    {
                        var e2 = model.Edges[j];
                        if (e1.SourceId.Equals(e2.SourceId, StringComparison.OrdinalIgnoreCase) ||
                            e1.SourceId.Equals(e2.TargetId, StringComparison.OrdinalIgnoreCase) ||
                            e1.TargetId.Equals(e2.SourceId, StringComparison.OrdinalIgnoreCase) ||
                            e1.TargetId.Equals(e2.TargetId, StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // share endpoint
                        }
                        var a2 = GetCenter(e2.SourceId); var b2 = GetCenter(e2.TargetId);
                        if (SegmentsIntersect(a1.x, a1.y, b1.x, b1.y, a2.x, a2.y, b2.x, b2.y)) crossings++;
                    }
                }
                Emit("info", $"estimated straight-line crossings: {crossings}");

                // Corridor staggering potential
                if (model.Metadata.TryGetValue("layout.routing.channels.gapIn", out var cgap) && double.TryParse(cgap, NumberStyles.Float, CultureInfo.InvariantCulture, out var cgv) && cgv > 0)
                {
                    // Count lane-pair groups with more than one edge
                    var laneBundles = BuildBundleIndex(model, "lane");
                    var multi = laneBundles.GroupBy(k => k.Value)
                                           .Select(g => new { size = g.Key.size, count = g.Count() })
                                           .Where(x => x.size > 1)
                                           .Sum(x => x.count);
                    if (multi > 0)
                        Emit("info", $"corridor staggering applied where available (lane bundles with >1 edges: {multi}).");

                    // After-routing plan estimate (layout-based)
                    int afterCross = 0; double totalLen = 0; int util = 0; int crossLane = 0; int missingCorridor = 0;
                    var tiers2 = GetOrderedTiers(model);
                    var nlMap = BuildLayoutMap(layout);
                    (double x, double y) AttachFromLayout(NodeLayout nl, string side)
                    {
                        var w = nl.Size.HasValue && nl.Size.Value.Width > 0 ? nl.Size.Value.Width : (float)DefaultNodeWidth;
                        var h = nl.Size.HasValue && nl.Size.Value.Height > 0 ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                        if (side.Equals("left", StringComparison.OrdinalIgnoreCase)) return (nl.Position.X, nl.Position.Y + h / 2.0);
                        if (side.Equals("right", StringComparison.OrdinalIgnoreCase)) return (nl.Position.X + w, nl.Position.Y + h / 2.0);
                        if (side.Equals("top", StringComparison.OrdinalIgnoreCase)) return (nl.Position.X + w / 2.0, nl.Position.Y + h);
                        if (side.Equals("bottom", StringComparison.OrdinalIgnoreCase)) return (nl.Position.X + w / 2.0, nl.Position.Y);
                        return (nl.Position.X + w / 2.0, nl.Position.Y + h / 2.0);
                    }
                    List<(double x, double y)> PathFor(Edge e)
                    {
                        if (!nlMap.TryGetValue(e.SourceId, out var snl) || !nlMap.TryGetValue(e.TargetId, out var tnl)) return new List<(double, double)>();
                        // waypoints precedence (layout coords assumed)
                        if (TryGetWaypointsFromMetadata(e, out var wps))
                        {
                            var a = AttachFromLayout(snl, "right"); var b = AttachFromLayout(tnl, "left");
                            var pts = new List<(double, double)> { a };
                            pts.AddRange(wps);
                            pts.Add(b);
                            return pts;
                        }
                        string GetTier(Node n)
                        {
                            var t = !string.IsNullOrWhiteSpace(n.Tier) ? n.Tier! : (n.Metadata.TryGetValue("tier", out var tv) ? tv : tiers2.First());
                            return t;
                        }
                        var srcN = nodeById[e.SourceId]; var dstN = nodeById[e.TargetId];
                        var si = Array.IndexOf(tiers2, GetTier(srcN));
                        var ti = Array.IndexOf(tiers2, GetTier(dstN));
                        var sourceSide = "right"; var targetSide = "left";
                        if (si == ti)
                        {
                            var scx = snl.Position.X + ((snl.Size.HasValue ? snl.Size.Value.Width : (float)DefaultNodeWidth) / 2.0);
                            var tcx = tnl.Position.X + ((tnl.Size.HasValue ? tnl.Size.Value.Width : (float)DefaultNodeWidth) / 2.0);
                            if (scx > tcx) { sourceSide = "left"; targetSide = "right"; }
                        }
                        var a2 = AttachFromLayout(snl, sourceSide);
                        var b2 = AttachFromLayout(tnl, targetSide);
                        var pts2 = new List<(double, double)> { a2 };
                        if (si != ti)
                        {
                            crossLane++;
                            var cx2 = CorridorXForEdgeFromLayout(model, layout, e.SourceId, e.TargetId);
                            if (cx2.HasValue)
                            {
                                util++;
                                pts2.Add((cx2.Value, a2.y));
                                pts2.Add((cx2.Value, b2.y));
                            }
                            else { missingCorridor++; }
                        }
                        pts2.Add(b2);
                        return pts2;
                    }

                    var planned = model.Edges.Select(PathFor).ToArray();
                    for (int i = 0; i < planned.Length; i++)
                    {
                        var pi = planned[i]; if (pi.Count < 2) continue;
                        for (int k = 0; k + 1 < pi.Count; k++)
                        {
                            var dxi = pi[k + 1].x - pi[k].x; var dyi = pi[k + 1].y - pi[k].y; totalLen += Math.Sqrt(dxi * dxi + dyi * dyi);
                        }
                        for (int j = i + 1; j < planned.Length; j++)
                        {
                            var pj = planned[j]; if (pj.Count < 2) continue;
                            for (int k = 0; k + 1 < pi.Count; k++)
                                for (int m = 0; m + 1 < pj.Count; m++)
                                    if (SegmentsIntersect(pi[k].x, pi[k].y, pi[k + 1].x, pi[k + 1].y, pj[m].x, pj[m].y, pj[m + 1].x, pj[m + 1].y)) { afterCross++; goto nextPair2; }
                        nextPair2: ;
                        }
                    }
                    Emit("info", $"planned route crossings: {afterCross}; avg path length: {(model.Edges.Count > 0 ? totalLen / model.Edges.Count : 0):F2}in");
                    double utilPct = (crossLane > 0) ? (100.0 * util / crossLane) : 0.0;
                    if (crossLane > 0)
                        Emit("info", $"channel utilization: {util}/{crossLane} cross-lane edges ({utilPct:F1}%)");
                    if (missingCorridor > 0)
                        Emit("warning", $"{missingCorridor} cross-lane edge(s) had no corridor available; check lane spans or channel gap settings.");

                    // Thresholds for crossings/utilization
                    int crossWarn = 200, crossErr = 400; double utilWarnMin = 40.0;
                    if (model.Metadata.TryGetValue("layout.diagnostics.crossingsWarn", out var cw) && int.TryParse(cw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cwv)) crossWarn = cwv;
                    if (model.Metadata.TryGetValue("layout.diagnostics.crossingsError", out var ce) && int.TryParse(ce, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cev)) crossErr = cev;
                    if (model.Metadata.TryGetValue("layout.diagnostics.utilizationWarnMin", out var uw) && double.TryParse(uw, NumberStyles.Float, CultureInfo.InvariantCulture, out var uwv)) utilWarnMin = uwv;

                    if (afterCross >= crossErr)
                    {
                        var msg = $"crossing density high: planned crossings={afterCross} (>= {crossErr})";
                        Emit("error", msg);
                        AddIssue("CrossingDensity", "error", msg);
                    }
                    else if (afterCross >= crossWarn)
                    {
                        var msg = $"crossing density elevated: planned crossings={afterCross} (>= {crossWarn})";
                        Emit("warning", msg);
                        AddIssue("CrossingDensity", "warning", msg);
                    }

                    if (crossLane > 0 && utilPct < utilWarnMin)
                    {
                        var msg = $"corridor utilization low: {utilPct:F1}% (< {utilWarnMin:F0}%)";
                        Emit("warning", msg);
                        AddIssue("LowUtilization", "warning", msg);
                    }
                }
                // Waypoints count
                var wpCount = model.Edges.Count(e => e.Metadata != null && e.Metadata.ContainsKey("edge.waypoints"));
                if (wpCount > 0) Emit("info", $"edges with explicit waypoints: {wpCount}");

                // M4: containers diagnostics (best-effort)
                if (model.Metadata.TryGetValue("layout.containers.count", out var ccountRaw) && int.TryParse(ccountRaw, out var ccount) && ccount > 0)
                {
                    Emit("info", $"containers: {ccount}");
                }
                else if (model.Metadata.TryGetValue("layout.containers.json", out var cj) && !string.IsNullOrWhiteSpace(cj))
                {
                    try { using var doc = JsonDocument.Parse(cj); if (doc.RootElement.ValueKind == JsonValueKind.Array) Emit("info", $"containers: {doc.RootElement.GetArrayLength()}"); } catch { }
                }
                if (model.Metadata.TryGetValue("layout.containers.paddingIn", out var pin) && double.TryParse(pin, NumberStyles.Float, CultureInfo.InvariantCulture, out var pval))
                {
                    var corner = 0.12; if (model.Metadata.TryGetValue("layout.containers.cornerIn", out var cin) && double.TryParse(cin, NumberStyles.Float, CultureInfo.InvariantCulture, out var cval)) corner = cval;
                    Emit("info", $"containers paddingIn={pval:F2}in; cornerIn={corner:F2}in");
                }
                if (TryGetExplicitContainers(model, out var contList))
                {
                    var valid = new HashSet<string>(contList.Where(c => !string.IsNullOrWhiteSpace(c.Id)).Select(c => c.Id!), StringComparer.OrdinalIgnoreCase);
                    int unknownAssigned = 0;
                    foreach (var n in model.Nodes)
                    {
                        if (n.Metadata != null && n.Metadata.TryGetValue("node.containerId", out var cid) && !string.IsNullOrWhiteSpace(cid))
                        {
                            if (!valid.Contains(cid)) unknownAssigned++;
                        }
                    }
                    if (unknownAssigned > 0) Emit("warning", $"{unknownAssigned} node(s) assigned to unknown container id(s).");

                    // Best-effort overflow detection without invoking Visio runner (page 1 only)
                    try
                    {
                        var tiers2 = GetOrderedTiers(model);
                        var tiersSet2 = new HashSet<string>(tiers2, StringComparer.OrdinalIgnoreCase);
                        var (minLeft, minBottom, _, _) = ComputeLayoutBounds(layout);
                        var margin2 = GetPageMargin(model) ?? Margin;
                        var title2 = GetTitleHeight(model);
                        var pageHeight2 = GetPageHeight(model) ?? 0.0;
                        var usable2 = pageHeight2 > 0 ? pageHeight2 - (2 * margin2) - title2 : double.PositiveInfinity;
                        int pageIndex = 0;
                        if (double.IsInfinity(usable2) || usable2 <= 0) { usable2 = double.PositiveInfinity; pageIndex = 0; }

                        var nodeMap = BuildNodeMap(model);
                        var tierBounds = new Dictionary<string, (double l, double b, double r, double t)>(StringComparer.OrdinalIgnoreCase);
                        foreach (var t in tiers2)
                        {
                            double tMinL = double.MaxValue, tMinB = double.MaxValue, tMaxR = double.MinValue, tMaxT = double.MinValue;
                            foreach (var nl in layout.Nodes)
                            {
                                var yNorm = nl.Position.Y - (float)minBottom;
                                var idx = (IsFinite(usable2) && usable2 > 0) ? (int)Math.Floor(yNorm / (float)usable2) : 0;
                                if (idx != pageIndex) continue;
                                if (!nodeMap.TryGetValue(nl.Id, out var node)) continue;
                                var nt = !string.IsNullOrWhiteSpace(node.Tier) ? node.Tier! : (node.Metadata.TryGetValue("tier", out var tv) ? tv : tiers2.First());
                                if (!string.Equals(nt, t, StringComparison.OrdinalIgnoreCase)) continue;
                                var w = nl.Size.HasValue && nl.Size.Value.Width > 0 ? nl.Size.Value.Width : (float)DefaultNodeWidth;
                                var h = nl.Size.HasValue && nl.Size.Value.Height > 0 ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                                var offsetX = margin2 - minLeft;
                                var bandOffset = (pageIndex * usable2);
                                var offsetY = margin2 + title2 - minBottom - (IsFinite(usable2) ? bandOffset : 0);
                                var l = nl.Position.X + offsetX;
                                var b = nl.Position.Y + offsetY;
                                tMinL = Math.Min(tMinL, l); tMinB = Math.Min(tMinB, b);
                                tMaxR = Math.Max(tMaxR, l + w); tMaxT = Math.Max(tMaxT, b + h);
                            }
                            if (!(double.IsInfinity(tMinL) || tMinL == double.MaxValue))
                            {
                                tierBounds[t] = (tMinL, tMinB, tMaxR, tMaxT);
                            }
                        }

                        foreach (var c in contList)
                        {
                            if (c == null || string.IsNullOrWhiteSpace(c.Id)) continue;
                            var ctier = !string.IsNullOrWhiteSpace(c.Tier) ? c.Tier! : tiers2.First();
                            if (!tiersSet2.Contains(ctier)) ctier = tiers2.First();
                            if (!tierBounds.TryGetValue(ctier, out var tb)) continue;
                            var offsetX = margin2 - minLeft;
                            var bandOffset = (pageIndex * usable2);
                            var offsetY = margin2 + title2 - minBottom - (IsFinite(usable2) ? bandOffset : 0);

                            double l, b, r, t;
                            if (c.Bounds != null && c.Bounds.Width.HasValue && c.Bounds.Height.HasValue)
                            {
                                l = (c.Bounds.X ?? 0.0) + offsetX;
                                b = (c.Bounds.Y ?? 0.0) + offsetY;
                                r = l + c.Bounds.Width!.Value;
                                t = b + c.Bounds.Height!.Value;
                            }
                            else
                            {
                                // Infer bounds from member nodes
                                double cMinL = double.MaxValue, cMinB = double.MaxValue, cMaxR = double.MinValue, cMaxT = double.MinValue;
                                foreach (var nl in layout.Nodes)
                                {
                                    var yNorm = nl.Position.Y - (float)minBottom;
                                    var idx = (IsFinite(usable2) && usable2 > 0) ? (int)Math.Floor(yNorm / (float)usable2) : 0;
                                    if (idx != pageIndex) continue;
                                    if (!nodeMap.TryGetValue(nl.Id, out var node)) continue;
                                    var nt = !string.IsNullOrWhiteSpace(node.Tier) ? node.Tier! : (node.Metadata.TryGetValue("tier", out var tv) ? tv : tiers2.First());
                                    if (!string.Equals(nt, ctier, StringComparison.OrdinalIgnoreCase)) continue;
                                    if (node.Metadata == null || !node.Metadata.TryGetValue("node.containerId", out var cid) || !string.Equals(cid, c.Id, StringComparison.OrdinalIgnoreCase)) continue;
                                    var w = nl.Size.HasValue && nl.Size.Value.Width > 0 ? nl.Size.Value.Width : (float)DefaultNodeWidth;
                                    var h = nl.Size.HasValue && nl.Size.Value.Height > 0 ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                                    var lx = nl.Position.X + offsetX; var by = nl.Position.Y + offsetY;
                                    cMinL = Math.Min(cMinL, lx); cMinB = Math.Min(cMinB, by);
                                    cMaxR = Math.Max(cMaxR, lx + w); cMaxT = Math.Max(cMaxT, by + h);
                                }
                                if (double.IsInfinity(cMinL) || cMinL == double.MaxValue)
                                {
                                    // no members on this page; skip
                                    continue;
                                }
                                var pad = GetContainerPadding(model);
                                l = cMinL - pad; b = cMinB - pad; r = cMaxR + pad; t = cMaxT + pad;
                            }

                            bool overflow = (l < tb.l) || (r > tb.r) || (b < tb.b) || (t > tb.t);
                            if (overflow)
                            {
                                var msg = $"sub-container '{c.Id}' overflows lane '{ctier}'.";
                                Emit("warning", msg);
                                AddIssue("ContainerOverflow", "warning", msg, lane: ctier, page: 1);
                                summary.ContainerOverflowCount++;
                            }

                            // Container occupancy ratio vs usable page height (crowding)
                            try
                            {
                                double vs = 0.6; if (model.Metadata.TryGetValue("layout.spacing.vertical", out var vsv) && double.TryParse(vsv, NumberStyles.Float, CultureInfo.InvariantCulture, out var vparsed)) vs = vparsed;
                                int count = 0; double sumH = 0.0;
                                foreach (var nl in layout.Nodes)
                                {
                                    var yNorm = nl.Position.Y - (float)minBottom;
                                    var idx = (IsFinite(usable2) && usable2 > 0) ? (int)Math.Floor(yNorm / (float)usable2) : 0;
                                    if (idx != pageIndex) continue;
                                    if (!nodeMap.TryGetValue(nl.Id, out var node)) continue;
                                    var nt = !string.IsNullOrWhiteSpace(node.Tier) ? node.Tier! : (node.Metadata.TryGetValue("tier", out var tv) ? tv : tiers2.First());
                                    if (!string.Equals(nt, ctier, StringComparison.OrdinalIgnoreCase)) continue;
                                    if (node.Metadata == null || !node.Metadata.TryGetValue("node.containerId", out var cid) || !string.Equals(cid, c.Id, StringComparison.OrdinalIgnoreCase)) continue;
                                    var h = (nl.Size.HasValue && nl.Size.Value.Height > 0) ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                                    sumH += h; count += 1;
                                }
                                if (count > 0 && IsFinite(usable2) && usable2 > 0)
                                {
                                    var occupied = sumH + ((count - 1) * vs);
                                    var ratio = occupied / usable2;
                                    double laneWarn = 0.85, laneErr = 0.95;
                                    if (model.Metadata.TryGetValue("layout.diagnostics.laneCrowdWarnRatio", out var lw) && double.TryParse(lw, NumberStyles.Float, CultureInfo.InvariantCulture, out var lwv)) laneWarn = lwv;
                                    if (model.Metadata.TryGetValue("layout.diagnostics.laneCrowdErrorRatio", out var le) && double.TryParse(le, NumberStyles.Float, CultureInfo.InvariantCulture, out var lev)) laneErr = lev;
                                    if (ratio >= laneErr)
                                    {
                                        var msg = $"container crowded: id='{c.Id}' lane='{ctier}' occupancy={(ratio * 100):F0}% nodes={count}";
                                        var severity = isViewModeLayout || (pagePlans != null && pagePlans.Length > 1) ? "warning" : "error";
                                        Emit(severity, msg);
                                        AddIssue("ContainerCrowding", severity, msg, lane: ctier, page: 1);
                                    }
                                    else if (ratio >= laneWarn)
                                    {
                                        var msg = $"container crowded: id='{c.Id}' lane='{ctier}' occupancy={(ratio * 100):F0}% nodes={count}";
                                        Emit("warning", msg);
                                        AddIssue("ContainerCrowding", "warning", msg, lane: ctier, page: 1);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { /* diagnostics are best-effort */ }

            // Bundle separation effectiveness warning (best-effort)
            try
            {
                if (model.Metadata.TryGetValue("layout.routing.bundleSeparationIn", out var sepRaw) &&
                    double.TryParse(sepRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var sepIn) && sepIn > 0)
                {
                    var nodeHeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    foreach (var nl in layout.Nodes)
                    {
                        var h = (nl.Size.HasValue && nl.Size.Value.Height > 0) ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                        nodeHeights[nl.Id] = h;
                    }
                    int impacted = 0;
                    foreach (var e in model.Edges)
                    {
                        if (nodeHeights.TryGetValue(e.SourceId, out var hs) && hs < (sepIn * 2.0)) impacted++;
                        if (nodeHeights.TryGetValue(e.TargetId, out var ht) && ht < (sepIn * 2.0)) impacted++;
                    }
                    if (impacted > 0)
                    {
                        Emit("warning", $"bundle separation {sepIn:F2}in may be ineffective for {impacted} end(s) due to small node height; consider reducing layout.routing.bundleSeparationIn or increasing node heights.");
                    }
                }
            }
            catch { }

            var layoutPlanModules = layoutPlan?.Stats?.ModuleIds;
            if (layoutPlanModules != null && layoutPlanModules.Length > 0 && pagePlans != null)
            {
                var datasetModules = new HashSet<string>(
                    layoutPlanModules
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                        .Select(m => m.Trim()),
                    StringComparer.OrdinalIgnoreCase);

                var plannedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var plan in pagePlans)
                {
                    if (plan?.Modules == null) continue;
                    foreach (var module in plan.Modules)
                    {
                        if (string.IsNullOrWhiteSpace(module)) continue;
                        plannedModules.Add(module.Trim());
                    }
                }

                datasetModules.ExceptWith(plannedModules);
                if (datasetModules.Count > 0)
                {
                    foreach (var module in datasetModules)
                    {
                        if (string.IsNullOrWhiteSpace(module)) continue;
                        if (!summary.SkippedModules.Any(m => string.Equals(m, module, StringComparison.OrdinalIgnoreCase)))
                        {
                            summary.SkippedModules.Add(module);
                        }
                    }
                    if (summary.SkippedModules.Count > 0)
                    {
                        summary.PartialRender = true;
                    }
                }
            }
            else if (plannerDataset != null && plannerDataset.Modules != null && plannerDataset.Modules.Length > 0 && pagePlans != null)
            {
                var datasetModules = new HashSet<string>(
                    plannerDataset.Modules
                        .Where(m => m != null && !string.IsNullOrWhiteSpace(m.ModuleId))
                        .Select(m => m.ModuleId.Trim()),
                    StringComparer.OrdinalIgnoreCase);
                var plannedModules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var plan in pagePlans)
                {
                    if (plan?.Modules == null) continue;
                    foreach (var module in plan.Modules)
                    {
                        if (string.IsNullOrWhiteSpace(module)) continue;
                        plannedModules.Add(module.Trim());
                    }
                }

                datasetModules.ExceptWith(plannedModules);
                if (datasetModules.Count > 0)
                {
                    foreach (var module in datasetModules)
                    {
                        if (string.IsNullOrWhiteSpace(module)) continue;
                        if (!summary.SkippedModules.Any(m => string.Equals(m, module, StringComparison.OrdinalIgnoreCase)))
                        {
                            summary.SkippedModules.Add(module);
                        }
                    }
                    if (summary.SkippedModules.Count > 0)
                    {
                        summary.PartialRender = true;
                    }
                }
            }

            if (pageDetails.Count > 0)
            {
                summary.Pages.AddRange(pageDetails.Values.OrderBy(p => p.PageNumber));
                summary.ConnectorOverLimitPageCount = summary.Pages.Count(p => p.ConnectorOverLimit > 0);
                summary.PagesWithPartialRender = summary.Pages.Count(p => p.HasPartialRender);
                summary.TruncatedNodeCount = summary.Pages.Sum(p => p.TruncatedNodeCount);
                foreach (var page in summary.Pages)
                {
                    if (page.TruncatedNodes.Count > 1)
                    {
                        page.TruncatedNodes.Sort(StringComparer.OrdinalIgnoreCase);
                    }

                    if (page.LaneAlerts.Count > 1)
                    {
                        static int SeverityRank(string? severity)
                        {
                            if (string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase)) return 0;
                            if (string.Equals(severity, "warning", StringComparison.OrdinalIgnoreCase)) return 1;
                            return 2;
                        }

                        page.LaneAlerts.Sort((a, b) =>
                        {
                            var rank = SeverityRank(a.Severity).CompareTo(SeverityRank(b.Severity));
                            if (rank != 0) return rank;
                            var ratioCompare = b.OccupancyRatio.CompareTo(a.OccupancyRatio);
                            if (ratioCompare != 0) return ratioCompare;
                            var nodeCompare = b.Nodes.CompareTo(a.Nodes);
                            if (nodeCompare != 0) return nodeCompare;
                            return StringComparer.OrdinalIgnoreCase.Compare(a.Tier, b.Tier);
                        });
                    }
                }
                if (summary.PagesWithPartialRender > 0)
                {
                    summary.PartialRender = true;
                }
            }

            if (filteredModules != null)
            {
                foreach (var module in filteredModules)
                {
                    if (string.IsNullOrWhiteSpace(module)) continue;
                    if (!summary.SkippedModules.Any(m => string.Equals(m, module, StringComparison.OrdinalIgnoreCase)))
                    {
                        summary.SkippedModules.Add(module);
                    }
                }
                if (summary.SkippedModules.Count > 0)
                {
                    summary.PartialRender = true;
                }
            }
            if (summary.SkippedModules.Count > 1)
            {
                summary.SkippedModules.Sort(StringComparer.OrdinalIgnoreCase);
            }

            // M5: Emit structured diagnostics JSON (best-effort)
            try
            {
                bool emitJson = false; string? outPath = null;
                if (model.Metadata.TryGetValue("layout.diagnostics.emitJson", out var ej) && bool.TryParse(ej, out var ejv) && ejv) emitJson = true;
                if (model.Metadata.TryGetValue("layout.diagnostics.jsonPath", out var jp) && !string.IsNullOrWhiteSpace(jp)) { emitJson = true; outPath = jp; }
                if (emitJson)
                {
                    var payload = new DiagnosticsJson();
                    payload.Metrics.LayoutOutputMode = summary.OutputMode;
                    if (layoutPlan != null)
                    {
                        payload.Metrics.LayoutCanvasWidth = layoutPlan.CanvasWidth;
                        payload.Metrics.LayoutCanvasHeight = layoutPlan.CanvasHeight;
                        if (layoutPlan.Stats != null)
                        {
                            payload.Metrics.LayoutNodeCount = layoutPlan.Stats.NodeCount;
                            payload.Metrics.LayoutModuleCount = layoutPlan.Stats.ModuleCount;
                            payload.Metrics.LayoutContainerCount = layoutPlan.Stats.ContainerCount;
                        }
                    }
                    payload.Metrics.LayerCount = summary.LayerCount;
                    payload.Metrics.LayerCrowdingCount = summary.LayerCrowdingCount;
                    payload.Metrics.LayerOverflowCount = summary.LayerOverflowCount;
                    payload.Metrics.BridgeCount = summary.BridgeCount;
                    if (summary.Layers.Count > 0)
                    {
                        foreach (var layer in summary.Layers.OrderBy(l => l.LayerNumber))
                        {
                            payload.Metrics.Layers.Add(
                                new LayerMetric
                                {
                                    Layer = layer.LayerNumber,
                                    ShapeCount = layer.ShapeCount,
                                    ConnectorCount = layer.ConnectorCount,
                                    ModuleCount = layer.ModuleCount,
                                    BridgesOut = layer.BridgesOut,
                                    BridgesIn = layer.BridgesIn,
                                    SoftShapeLimitExceeded = layer.SoftShapeLimitExceeded,
                                    SoftConnectorLimitExceeded = layer.SoftConnectorLimitExceeded,
                                    HardShapeLimitExceeded = layer.HardShapeLimitExceeded,
                                    HardConnectorLimitExceeded = layer.HardConnectorLimitExceeded,
                                    Modules = new List<string>(layer.Modules),
                                    OverflowModules = new List<string>(layer.OverflowModules)
                                });
                        }
                    }
                    payload.Metrics.ConnectorCount = model.Edges.Count;
                    if (plannerMetrics != null)
                    {
                        payload.Metrics.ModuleCount = plannerMetrics.OriginalModuleCount;
                        payload.Metrics.SegmentCount = plannerMetrics.SegmentCount;
                        payload.Metrics.SegmentDelta = plannerMetrics.SegmentDelta;
                        payload.Metrics.SplitModuleCount = plannerMetrics.SplitModuleCount;
                        payload.Metrics.AverageSegmentsPerModule = plannerMetrics.AverageSegmentsPerModule;
                    }
                    if (plannerSummary != null)
                    {
                        payload.Metrics.PlannerPageCount = plannerSummary.PageCount;
                        payload.Metrics.PlannerAverageModulesPerPage = plannerSummary.AverageModulesPerPage;
                        payload.Metrics.PlannerAverageConnectorsPerPage = plannerSummary.AverageConnectorsPerPage;
                        payload.Metrics.PlannerMaxOccupancyPercent = plannerSummary.MaxOccupancy;
                        payload.Metrics.PlannerMaxConnectorsPerPage = plannerSummary.MaxConnectors;
                    }
                    payload.Metrics.PageOverflowCount = summary.PageOverflowCount;
                    payload.Metrics.PageCrowdingCount = summary.PageCrowdingCount;
                    payload.Metrics.LaneOverflowCount = summary.LaneOverflowCount;
                    payload.Metrics.LaneCrowdingCount = summary.LaneCrowdingCount;
                    payload.Metrics.ContainerOverflowCount = summary.ContainerOverflowCount;
                    payload.Metrics.PartialRender = summary.PartialRender;
                    payload.Metrics.ConnectorOverLimitPageCount = summary.ConnectorOverLimitPageCount;
                    payload.Metrics.PagesWithPartialRender = summary.PagesWithPartialRender;
                    payload.Metrics.TruncatedNodeCount = summary.TruncatedNodeCount;
                    if (summary.SkippedModules.Count > 0)
                    {
                        payload.Metrics.SkippedModules.AddRange(summary.SkippedModules);
                    }

                    // Straight-line crossings
                    try
                    {
                        var layoutMap = BuildLayoutMap(layout);
                        (double x, double y) Center(string id)
                        {
                            if (!layoutMap.TryGetValue(id, out var nl)) return (0, 0);
                            var w = (nl.Size.HasValue && nl.Size.Value.Width > 0) ? nl.Size.Value.Width : (float)DefaultNodeWidth;
                            var h = (nl.Size.HasValue && nl.Size.Value.Height > 0) ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                            return (nl.Position.X + (w / 2.0), nl.Position.Y + (h / 2.0));
                        }
                        int crossings = 0;
                        for (int i = 0; i < model.Edges.Count; i++)
                        {
                            var e1 = model.Edges[i];
                            var a1 = Center(e1.SourceId); var b1 = Center(e1.TargetId);
                            for (int j = i + 1; j < model.Edges.Count; j++)
                            {
                                var e2 = model.Edges[j];
                                if (e1.SourceId.Equals(e2.SourceId, StringComparison.OrdinalIgnoreCase) ||
                                    e1.SourceId.Equals(e2.TargetId, StringComparison.OrdinalIgnoreCase) ||
                                    e1.TargetId.Equals(e2.SourceId, StringComparison.OrdinalIgnoreCase) ||
                                    e1.TargetId.Equals(e2.TargetId, StringComparison.OrdinalIgnoreCase)) continue;
                                var a2 = Center(e2.SourceId); var b2 = Center(e2.TargetId);
                                if (SegmentsIntersect(a1.x, a1.y, b1.x, b1.y, a2.x, a2.y, b2.x, b2.y)) crossings++;
                            }
                        }
                        payload.Metrics.StraightLineCrossings = crossings;
                    }
                    catch { }

                    // Lane page occupancy
                    try
                    {
                        var pageHeight = GetPageHeight(model) ?? 0.0;
                        payload.Metrics.PageHeight = pageHeight;
                        if (pageHeight > 0)
                        {
                            var margin2 = GetPageMargin(model) ?? Margin;
                            var title2 = GetTitleHeight(model);
                            var usable = pageHeight - (2 * margin2) - title2;
                            payload.Metrics.UsableHeight = usable;
                            if (usable > 0 && IsFinite(usable))
                            {
                                var (minLeft, minBottom, _, _) = ComputeLayoutBounds(layout);
                                var vs = 0.6; if (model.Metadata.TryGetValue("layout.spacing.vertical", out var vsv) && double.TryParse(vsv, NumberStyles.Float, CultureInfo.InvariantCulture, out var vparsed)) vs = vparsed;
                                var nodeMap = BuildNodeMap(model);
                                var tiers2 = GetOrderedTiers(model); var tiersSet2 = new HashSet<string>(tiers2, StringComparer.OrdinalIgnoreCase);
                                var perPageTier = new Dictionary<(int page, string tier), (double sumH, int count)>(new PerPageTierComparer());
                                foreach (var nl in layout.Nodes)
                                {
                                    var yNorm = nl.Position.Y - (float)minBottom;
                                    var pi = (int)Math.Floor(yNorm / (float)usable); if (pi < 0) pi = 0;
                                    if (!nodeMap.TryGetValue(nl.Id, out var node)) continue;
                                    var t = !string.IsNullOrWhiteSpace(node.Tier) ? node.Tier! : (node.Metadata.TryGetValue("tier", out var tv) ? tv : tiers2.First());
                                    if (!tiersSet2.Contains(t)) t = tiers2.First();
                                    var h = (nl.Size.HasValue && nl.Size.Value.Height > 0) ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                                    var key = (pi, t);
                                    var cur = perPageTier.TryGetValue(key, out var v) ? v : (0.0, 0);
                                    perPageTier[key] = (cur.Item1 + h, cur.Item2 + 1);
                                }
                                foreach (var kv in perPageTier)
                                {
                                    var page = kv.Key.page; var tierName = kv.Key.tier; var (sumH, count) = kv.Value;
                                    var occ = sumH + (count > 0 ? (count - 1) * vs : 0);
                                    var ratio = occ / usable;
                                    payload.Metrics.LanePages.Add(new LanePageMetric { Tier = tierName, Page = page + 1, OccupancyRatio = ratio, Nodes = count });
                                }

                                // Container page occupancy metrics (explicit containers only)
                                try
                                {
                                    if (TryGetExplicitContainers(model, out var contList) && contList.Count > 0)
                                    {
                                        var contById = contList.Where(c => !string.IsNullOrWhiteSpace(c.Id))
                                                                .ToDictionary(c => c.Id!, c => c, StringComparer.OrdinalIgnoreCase);
                                        var perPageContainer = new Dictionary<(int page, string id, string tier), (double sumH, int count)>();
                                        foreach (var nl in layout.Nodes)
                                        {
                                            var yNorm = nl.Position.Y - (float)minBottom;
                                            var pi = (int)Math.Floor(yNorm / (float)usable); if (pi < 0) pi = 0;
                                            if (!nodeMap.TryGetValue(nl.Id, out var node)) continue;
                                            if (node.Metadata == null || !node.Metadata.TryGetValue("node.containerId", out var cid) || string.IsNullOrWhiteSpace(cid)) continue;
                                            if (!contById.TryGetValue(cid!, out var c)) continue;
                                            var t = !string.IsNullOrWhiteSpace(node.Tier) ? node.Tier! : (node.Metadata.TryGetValue("tier", out var tv) ? tv : tiers2.First());
                                            if (!tiersSet2.Contains(t)) t = tiers2.First();
                                            var h = (nl.Size.HasValue && nl.Size.Value.Height > 0) ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                                            var key = (pi, cid!, c.Tier ?? t);
                                            var cur = perPageContainer.TryGetValue(key, out var v) ? v : (0.0, 0);
                                            perPageContainer[key] = (cur.Item1 + h, cur.Item2 + 1);
                                        }
                                        foreach (var kv2 in perPageContainer)
                                        {
                                            var page = kv2.Key.page; var id = kv2.Key.id; var tierName = kv2.Key.tier; var (sumH, count) = kv2.Value;
                                            var occ = sumH + (count > 0 ? (count - 1) * vs : 0);
                                            var ratio = occ / usable;
                                            payload.Metrics.Containers.Add(new ContainerPageMetric { Id = id, Tier = tierName, Page = page + 1, OccupancyRatio = ratio, Nodes = count });
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }

                    if (pagePlans != null && pagePlans.Length > 0)
                    {
                        var occupancyLimit = double.PositiveInfinity;
                        if (pageOptions != null && !pageOptions.LaneSplitAllowed && pageOptions.MaxOccupancyPercent > 0)
                        {
                            occupancyLimit = pageOptions.MaxOccupancyPercent;
                        }

                        var connectorLimit = (pageOptions != null && pageOptions.MaxConnectors > 0)
                            ? pageOptions.MaxConnectors
                            : int.MaxValue;

                        foreach (var plan in pagePlans.OrderBy(p => p.PageIndex))
                        {
                            var metric = new PageMetric
                            {
                                Index = plan.PageIndex + 1,
                                Name = (plan.Modules != null && plan.Modules.Length > 0) ? string.Join(", ", plan.Modules) : null,
                                Nodes = plan.Nodes,
                                Connectors = plan.Connectors,
                                SkippedConnectors = 0,
                                Overflow = (plan.Connectors > connectorLimit && connectorLimit < int.MaxValue) ||
                                           (!double.IsInfinity(occupancyLimit) && plan.Occupancy > occupancyLimit)
                            };
                            if (pageDetails.TryGetValue(plan.PageIndex, out var detail))
                            {
                                metric.ConnectorLimit = detail.ConnectorLimit;
                                metric.ConnectorOverLimit = detail.ConnectorOverLimit;
                                metric.ModuleCount = detail.ModuleCount ?? (plan.Modules?.Length ?? 0);
                                metric.Partial = detail.HasPartialRender;
                                metric.LaneCrowdingWarnings = detail.LaneCrowdingWarnings;
                                metric.LaneCrowdingErrors = detail.LaneCrowdingErrors;
                                metric.PageCrowding = detail.HasPageCrowding;
                                metric.PageOverflow = detail.HasPageOverflow;
                                metric.TruncatedNodes = detail.TruncatedNodeCount;
                            }
                            else
                            {
                                metric.ConnectorLimit = connectorLimit < int.MaxValue ? (int?)connectorLimit : null;
                                metric.ConnectorOverLimit = 0;
                                metric.ModuleCount = plan.Modules?.Length ?? 0;
                                metric.Partial = false;
                                metric.LaneCrowdingWarnings = 0;
                                metric.LaneCrowdingErrors = 0;
                                metric.PageCrowding = false;
                                metric.PageOverflow = false;
                                metric.TruncatedNodes = 0;
                            }
                            payload.Metrics.Pages.Add(metric);
                        }
                    }

                    // Attach gated issues collected during diagnostics
                    if (gatedIssues.Count > 0)
                    {
                        payload.Issues.AddRange(gatedIssues);
                    }

                    var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    var target = string.IsNullOrWhiteSpace(outPath) ? Path.Combine("out", "diagnostics.json") : Path.GetFullPath(outPath!);
                    var dir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(target, json);
                    Emit("info", $"diagnostics JSON written: {target}");
                  }
              }
              catch { }

            summary.Rank = highestRank;
            return summary;
        }

        private static double Orientation(double ax, double ay, double bx, double by, double cx, double cy)
        {
            return (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
        }

        private static bool OnSegment(double ax, double ay, double bx, double by, double px, double py)
        {
            return Math.Min(ax, bx) <= px && px <= Math.Max(ax, bx) && Math.Min(ay, by) <= py && py <= Math.Max(ay, by);
        }

        private static bool SegmentsIntersect(double a1x, double a1y, double a2x, double a2y, double b1x, double b1y, double b2x, double b2y)
        {
            var o1 = Orientation(a1x, a1y, a2x, a2y, b1x, b1y);
            var o2 = Orientation(a1x, a1y, a2x, a2y, b2x, b2y);
            var o3 = Orientation(b1x, b1y, b2x, b2y, a1x, a1y);
            var o4 = Orientation(b1x, b1y, b2x, b2y, a2x, a2y);

            if ((o1 > 0 && o2 < 0 || o1 < 0 && o2 > 0) && (o3 > 0 && o4 < 0 || o3 < 0 && o4 > 0)) return true;

            // Colinear special cases
            if (Math.Abs(o1) < 1e-6 && OnSegment(a1x, a1y, a2x, a2y, b1x, b1y)) return true;
            if (Math.Abs(o2) < 1e-6 && OnSegment(a1x, a1y, a2x, a2y, b2x, b2y)) return true;
            if (Math.Abs(o3) < 1e-6 && OnSegment(b1x, b1y, b2x, b2y, a1x, a1y)) return true;
            if (Math.Abs(o4) < 1e-6 && OnSegment(b1x, b1y, b2x, b2y, a2x, a2y)) return true;
            return false;
        }

        private static string[] GetOrderedTiers(DiagramModel model)
        {
            if (model.Metadata.TryGetValue("layout.tiers", out var csv) && !string.IsNullOrWhiteSpace(csv))
            {
                return csv.Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => s.Trim())
                          .Where(s => s.Length > 0)
                          .ToArray();
            }

            return new[] { "External", "Edge", "Services", "Data", "Observability" };
        }

        private static Dictionary<string, (int index, int size)> BuildBundleIndex(DiagramModel model, string bundleBy)
        {
            var result = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(bundleBy) || string.Equals(bundleBy, "none", StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            var groups = new Dictionary<string, List<(string edgeId, string src, string dst)>>(StringComparer.OrdinalIgnoreCase);
            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);
            var nodeMap = BuildNodeMap(model);
            var outputMode = GetOutputMode(model);
            var isViewMode = string.Equals(outputMode, "view", StringComparison.OrdinalIgnoreCase);

            string GetTier(VDG.Core.Models.Node n)
            {
                var t = !string.IsNullOrWhiteSpace(n.Tier) ? n.Tier! : (n.Metadata.TryGetValue("tier", out var tv) ? tv : tiers.First());
                return tiersSet.Contains(t) ? t : tiers.First();
            }

            foreach (var e in model.Edges)
            {
                string? key = null;
                if (string.Equals(bundleBy, "lane", StringComparison.OrdinalIgnoreCase))
                {
                    if (!nodeMap.TryGetValue(e.SourceId, out var sn) || !nodeMap.TryGetValue(e.TargetId, out var tn)) continue;
                    key = $"{GetTier(sn)}->{GetTier(tn)}";
                }
                else if (string.Equals(bundleBy, "group", StringComparison.OrdinalIgnoreCase))
                {
                    if (!nodeMap.TryGetValue(e.SourceId, out var sn) || !nodeMap.TryGetValue(e.TargetId, out var tn)) continue;
                    var sg = string.IsNullOrWhiteSpace(sn.GroupId) ? "~" : sn.GroupId!;
                    var tg = string.IsNullOrWhiteSpace(tn.GroupId) ? "~" : tn.GroupId!;
                    key = $"{sg}->{tg}";
                }
                else if (string.Equals(bundleBy, "nodepair", StringComparison.OrdinalIgnoreCase) || string.Equals(bundleBy, "nodePair", StringComparison.OrdinalIgnoreCase))
                {
                    var a = e.SourceId; var b = e.TargetId;
                    var k1 = string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? a : b;
                    var k2 = string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? b : a;
                    key = $"{k1}<->{k2}";
                }

                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!groups.TryGetValue(key!, out var list)) { list = new List<(string, string, string)>(); groups[key!] = list; }
                list.Add((e.Id, e.SourceId, e.TargetId));
            }

            foreach (var kv in groups)
            {
                var list = kv.Value;
                list.Sort((x, y) =>
                {
                    var c1 = string.Compare(x.src, y.src, StringComparison.OrdinalIgnoreCase);
                    if (c1 != 0) return c1;
                    var c2 = string.Compare(x.dst, y.dst, StringComparison.OrdinalIgnoreCase);
                    if (c2 != 0) return c2;
                    return string.Compare(x.edgeId, y.edgeId, StringComparison.OrdinalIgnoreCase);
                });
                for (int i = 0; i < list.Count; i++)
                {
                    result[list[i].edgeId] = (i, list.Count);
                }
            }

            return result;
        }

        private static Dictionary<string, Node> BuildNodeMap(DiagramModel model)
        {
            var map = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
            if (model?.Nodes == null) return map;

            foreach (var node in model.Nodes)
            {
                if (node?.Id == null) continue;
                if (!map.ContainsKey(node.Id))
                {
                    map[node.Id] = node;
                }
            }

            return map;
        }

        private static Dictionary<string, NodeLayout> BuildLayoutMap(LayoutResult layout)
        {
            var map = new Dictionary<string, NodeLayout>(StringComparer.OrdinalIgnoreCase);
            if (layout.Nodes == null) return map;

            foreach (var nl in layout.Nodes)
            {
                if (nl.Id == null) continue;
                if (!map.ContainsKey(nl.Id))
                {
                    map[nl.Id] = nl;
                }
            }

            return map;
        }

        private sealed class ModuleAccumulator
        {
            public ModuleAccumulator()
            {
                Nodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Edges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CrossEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            public HashSet<string> Nodes { get; }
            public HashSet<string> Edges { get; }
            public HashSet<string> CrossEdges { get; }
            public int FirstIndex { get; set; }
            public bool HasPlacement { get; set; }
            public double MinY { get; set; } = double.PositiveInfinity;
            public double MaxY { get; set; } = double.NegativeInfinity;
        }

        private sealed class ModuleProjection
        {
            public ModuleStats Stats { get; set; } = null!;
            public int FirstIndex { get; set; }
            public bool HasPlacement { get; set; }
            public double MinY { get; set; }
        }

        private sealed class NodePlacementInfo
        {
            public NodePlacementInfo(string id, double bottom, double top, double height)
            {
                Id = id;
                Bottom = bottom;
                Top = top;
                Height = height;
                Center = bottom + (height / 2.0);
            }

            public string Id { get; }
            public double Bottom { get; }
            public double Top { get; }
            public double Height { get; }
            public double Center { get; }
        }

        private sealed class SegmentInfo
        {
            public SegmentInfo(int index)
            {
                Index = index;
            }

            public int Index { get; }
            public List<string> Nodes { get; } = new List<string>();
            public double MinY { get; set; } = double.PositiveInfinity;
            public double MaxY { get; set; } = double.NegativeInfinity;
            public string ModuleId { get; set; } = string.Empty;
        }

        internal sealed class PlannerMetrics
        {
            public int OriginalModuleCount { get; set; }
            public int SegmentCount { get; set; }
            public int SplitModuleCount { get; set; }
            public double AverageSegmentsPerModule { get; set; }
            public int SegmentDelta => SegmentCount - OriginalModuleCount;
        }

        internal sealed class PlannerSummaryStats
        {
            public int PageCount { get; set; }
            public double AverageModulesPerPage { get; set; }
            public double AverageConnectorsPerPage { get; set; }
            public double MaxOccupancy { get; set; }
            public int MaxConnectors { get; set; }
        }

        internal sealed class DiagnosticsSummary
        {
            public string OutputMode { get; set; } = "print";
            public int Rank { get; set; }
            public int PageOverflowCount { get; set; }
            public int PageCrowdingCount { get; set; }
            public int LaneOverflowCount { get; set; }
            public int LaneCrowdingCount { get; set; }
            public int ContainerOverflowCount { get; set; }
            public bool PartialRender { get; set; }
            public int ConnectorOverLimitPageCount { get; set; }
            public int PagesWithPartialRender { get; set; }
            public int TruncatedNodeCount { get; set; }
            public float? LayoutCanvasWidth { get; set; }
            public float? LayoutCanvasHeight { get; set; }
            public int? LayoutNodeCount { get; set; }
            public int? LayoutModuleCount { get; set; }
            public int? LayoutContainerCount { get; set; }
            public int LayerCount { get; set; }
            public int LayerCrowdingCount { get; set; }
            public int LayerOverflowCount { get; set; }
            public int BridgeCount { get; set; }
            public List<PageDiagnosticsDetail> Pages { get; } = new List<PageDiagnosticsDetail>();
            public List<LayerDiagnosticsDetail> Layers { get; } = new List<LayerDiagnosticsDetail>();
            public List<string> SkippedModules { get; } = new List<string>();
            public bool HasOverflow =>
                PageOverflowCount > 0 ||
                LaneOverflowCount > 0 ||
                ContainerOverflowCount > 0 ||
                LayerOverflowCount > 0 ||
                ConnectorOverLimitPageCount > 0;
        }

        private sealed class PlannerBuildResult
        {
            public PlannerBuildResult(DiagramDataset dataset, Dictionary<string, string> nodeOverrides, PlannerMetrics metrics)
            {
                Dataset = dataset;
                NodeSegmentOverrides = nodeOverrides;
                Metrics = metrics;
            }

            public DiagramDataset Dataset { get; }
            public Dictionary<string, string> NodeSegmentOverrides { get; }
            public PlannerMetrics Metrics { get; }
        }

        internal sealed class ViewModeValidationResult
        {
            public bool Enabled { get; set; }
            public int NodeCount { get; set; }
            public int ModuleCount { get; set; }
            public int ConnectorCount { get; set; }
            public bool NodeCountTooLow { get; set; }
            public bool MissingConnectors { get; set; }
            public List<string> EmptyForms { get; } = new List<string>();
            public List<string> EmptyContainers { get; } = new List<string>();
            public Dictionary<string, int> TruncatedModules { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class LayoutComputation
        {
            public LayoutComputation(LayoutResult layout, LayoutPlan? plan = null)
            {
                Layout = layout ?? throw new ArgumentNullException(nameof(layout));
                Plan = plan;
            }

            public LayoutResult Layout { get; }
            public LayoutPlan? Plan { get; }
        }

        private static string ResolveModuleIdForNode(VDG.Core.Models.Node node, string[] tiers, HashSet<string> tiersSet)
        {
            if (!string.IsNullOrWhiteSpace(node.GroupId)) return node.GroupId!.Trim();

            if (node.Metadata.TryGetValue("moduleId", out var moduleId) && !string.IsNullOrWhiteSpace(moduleId))
            {
                return moduleId.Trim();
            }

            if (node.Metadata.TryGetValue("node.containerId", out var container) && !string.IsNullOrWhiteSpace(container))
            {
                return container.Trim();
            }

            if (!string.IsNullOrWhiteSpace(node.Tier) && tiersSet.Contains(node.Tier!))
            {
                return node.Tier!.Trim();
            }

            if (node.Metadata.TryGetValue("tier", out var tierValue) && !string.IsNullOrWhiteSpace(tierValue))
            {
                var tier = tierValue.Trim();
                if (tiersSet.Contains(tier)) return tier;
            }

            return tiers.Length > 0 ? tiers[0] : "Default";
        }

        private static LayoutComputation ComputeLayout(DiagramModel model)
        {
            if (string.Equals(GetOutputMode(model), "view", StringComparison.OrdinalIgnoreCase))
            {
                return ComputeViewModeLayout(model);
            }

            var layout = LayoutEngine.compute(model);
            var plan = PrintPlanner.ComputeLayoutPlan(model, layout);
            return new LayoutComputation(layout, plan);
        }

        private static string ResolveTierForNode(VDG.Core.Models.Node node, string[] tiers, HashSet<string> tiersSet)
        {
            if (!string.IsNullOrWhiteSpace(node.Tier) && tiersSet.Contains(node.Tier!)) return node.Tier!.Trim();
            if (node.Metadata.TryGetValue("tier", out var tierMeta) && !string.IsNullOrWhiteSpace(tierMeta))
            {
                var trimmed = tierMeta.Trim();
                if (tiersSet.Contains(trimmed)) return trimmed;
            }
            return tiers.Length > 0 ? tiers[0] : "Modules";
        }

        private static LayoutComputation ComputeViewModeLayout(DiagramModel model)
        {
            var plan = ViewModePlanner.ComputeViewLayout(model);
            if (plan == null || plan.Nodes == null || plan.Nodes.Length == 0)
            {
                model.Metadata.Remove("layout.containers.json");
                model.Metadata.Remove("layout.containers.count");
                model.Metadata.Remove("layout.view.truncatedModules");
                var fallback = LayoutEngine.compute(model);
                return new LayoutComputation(fallback);
            }

            ApplyViewModeMetadata(model, plan);

            var result = new LayoutResult
            {
                Nodes = plan.Nodes,
                Edges = plan.Edges
            };

            return new LayoutComputation(result, plan);
        }

        private static void ApplyViewModeMetadata(DiagramModel model, LayoutPlan plan)
        {
            if (plan?.Containers != null && plan.Containers.Length > 0)
            {
                var containers = new List<ContainerDto>(plan.Containers.Length);
                foreach (var container in plan.Containers)
                {
                    if (container == null || string.IsNullOrWhiteSpace(container.Id)) continue;
                    containers.Add(new ContainerDto
                    {
                        Id = container.Id,
                        Label = container.Label,
                        Tier = container.Tier,
                        Bounds = new BoundsDto
                        {
                            X = container.Bounds.Left,
                            Y = container.Bounds.Bottom,
                            Width = container.Bounds.Width,
                            Height = container.Bounds.Height
                        }
                    });
                }

                if (containers.Count > 0)
                {
                    model.Metadata["layout.containers.json"] = JsonSerializer.Serialize(containers, JsonOptions);
                    model.Metadata["layout.containers.count"] = containers.Count.ToString(CultureInfo.InvariantCulture);
                    model.Metadata["layout.containers.paddingIn"] = "0.2";
                    model.Metadata["layout.containers.cornerIn"] = "0.12";
                }
                else
                {
                    model.Metadata.Remove("layout.containers.json");
                    model.Metadata.Remove("layout.containers.count");
                }
            }
            else
            {
                model.Metadata.Remove("layout.containers.json");
                model.Metadata.Remove("layout.containers.count");
            }

            if (plan?.Stats?.Overflows != null && plan.Stats.Overflows.Length > 0)
            {
                var truncated = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var overflow in plan.Stats.Overflows)
                {
                    if (overflow == null || string.IsNullOrWhiteSpace(overflow.ContainerId)) continue;
                    if (overflow.HiddenNodeCount <= 0) continue;
                    truncated[overflow.ContainerId] = overflow.HiddenNodeCount;
                }

                if (truncated.Count > 0)
                {
                    model.Metadata["layout.view.truncatedModules"] = JsonSerializer.Serialize(truncated, JsonOptions);
                }
                else
                {
                    model.Metadata.Remove("layout.view.truncatedModules");
                }
            }
            else
            {
                model.Metadata.Remove("layout.view.truncatedModules");
            }
        }

        private static ViewModeValidationResult AnalyzeViewModeContent(DiagramModel model)
        {
            var result = new ViewModeValidationResult();
            if (model == null) return result;
            if (!string.Equals(GetOutputMode(model), "view", StringComparison.OrdinalIgnoreCase)) return result;

            result.Enabled = true;
            result.NodeCount = model.Nodes?.Count ?? 0;
            var modules = CollectModuleIds(model);
            result.ModuleCount = modules.Count;
            result.ConnectorCount = model.Edges?.Count ?? 0;

            if (result.ModuleCount > 0 && result.NodeCount <= result.ModuleCount)
            {
                result.NodeCountTooLow = true;
            }

            if (result.ConnectorCount <= 0)
            {
                result.MissingConnectors = true;
            }

            if (model.Metadata.TryGetValue("layout.view.truncatedModules", out var truncatedRaw) &&
                !string.IsNullOrWhiteSpace(truncatedRaw))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, int>>(truncatedRaw, JsonOptions);
                    if (parsed != null)
                    {
                        foreach (var kv in parsed)
                        {
                            if (kv.Value > 0)
                            {
                                result.TruncatedModules[kv.Key] = kv.Value;
                            }
                        }
                    }
                }
                catch
                {
                    // ignore malformed metadata
                }
            }

            if (TryGetExplicitContainers(model, out var containers) && containers.Count > 0)
            {
                var membership = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var node in model.Nodes ?? Array.Empty<VDG.Core.Models.Node>())
                {
                    if (node?.Metadata == null) continue;
                    if (!node.Metadata.TryGetValue("node.containerId", out var cid) || string.IsNullOrWhiteSpace(cid)) continue;
                    var key = cid.Trim();
                    membership[key] = membership.TryGetValue(key, out var count) ? count + 1 : 1;
                }

                foreach (var container in containers)
                {
                    if (container == null || string.IsNullOrWhiteSpace(container.Id)) continue;
                    var id = container.Id!.Trim();
                    if (membership.TryGetValue(id, out var count) && count > 0) continue;
                    var tier = container.Tier ?? string.Empty;
                    if (string.Equals(tier, "Forms", StringComparison.OrdinalIgnoreCase))
                    {
                        result.EmptyForms.Add(id);
                    }
                    else
                    {
                        result.EmptyContainers.Add(id);
                    }
                }
            }

            return result;
        }

        private static void ValidateViewModeContent(DiagramModel model)
        {
            var analysis = AnalyzeViewModeContent(model);
            if (!analysis.Enabled) return;

            if (analysis.NodeCountTooLow)
            {
                Console.Error.WriteLine($"warning: view-mode diagram only has {analysis.NodeCount} node(s) for {analysis.ModuleCount} module container(s); per-procedure nodes may be missing.");
            }

            if (analysis.MissingConnectors)
            {
                Console.Error.WriteLine("warning: view-mode diagram contains zero connectors; call edges may be missing.");
            }

            void EmitEmpty(string label, List<string> list)
            {
                if (list.Count == 0) return;
                var preview = string.Join(", ", list.Take(5));
                var suffix = list.Count > 5 ? $" (+{list.Count - 5} more)" : string.Empty;
                Console.Error.WriteLine($"warning: view-mode {label} lacking procedures: {preview}{suffix}");
            }

            EmitEmpty("forms", analysis.EmptyForms);
            EmitEmpty("modules", analysis.EmptyContainers);

            if (analysis.TruncatedModules.Count > 0)
            {
                var preview = string.Join(", ",
                    analysis.TruncatedModules
                        .OrderByDescending(kv => kv.Value)
                        .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                        .Take(5)
                        .Select(kv => $"{kv.Key}(+{kv.Value})"));
                var remainder = analysis.TruncatedModules.Count > 5
                    ? $" (+{analysis.TruncatedModules.Count - 5} more)"
                    : string.Empty;
                Console.Error.WriteLine($"warning: view-mode truncated modules => {preview}{remainder}");
            }
        }

        internal static HashSet<string> CollectModuleIds(DiagramModel model)
        {
            var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);
            foreach (var node in model.Nodes)
            {
                if (node == null) continue;
                var moduleId = ResolveModuleIdForNode(node, tiers, tiersSet);
                if (!string.IsNullOrWhiteSpace(moduleId))
                {
                    modules.Add(moduleId);
                }
            }
            return modules;
        }

        internal static DiagramModel FilterModelByModules(DiagramModel model, IEnumerable<string> includeModules)
        {
            var includeSet = new HashSet<string>(
                includeModules
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m.Trim()),
                StringComparer.OrdinalIgnoreCase);

            if (includeSet.Count == 0)
            {
                var empty = new DiagramModel();
                foreach (var kvp in model.Metadata)
                {
                    empty.Metadata[kvp.Key] = kvp.Value;
                }
                return empty;
            }

            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);

            var keptNodes = new List<Node>();
            foreach (var node in model.Nodes)
            {
                if (node == null) continue;
                var moduleId = ResolveModuleIdForNode(node, tiers, tiersSet);
                if (includeSet.Contains(moduleId))
                {
                    keptNodes.Add(node);
                }
            }

            var keepNodeIds = new HashSet<string>(keptNodes.Select(n => n.Id), StringComparer.OrdinalIgnoreCase);
            var keptEdges = model.Edges
                .Where(e =>
                    e != null &&
                    !string.IsNullOrWhiteSpace(e.SourceId) &&
                    !string.IsNullOrWhiteSpace(e.TargetId) &&
                    keepNodeIds.Contains(e.SourceId!) &&
                    keepNodeIds.Contains(e.TargetId!))
                .ToList();

            var filtered = new DiagramModel(keptNodes, keptEdges);
            foreach (var kvp in model.Metadata)
            {
                filtered.Metadata[kvp.Key] = kvp.Value;
            }

            return filtered;
        }

        private static PlannerBuildResult BuildPagingDataset(DiagramModel model, LayoutResult layout)
        {
            _ = layout; // layout reserved for future heuristics (crowding metrics, etc.)
            var nodeSegmentOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);

            var moduleOrder = new List<string>();
            var moduleAccumulators = new Dictionary<string, ModuleAccumulator>(StringComparer.OrdinalIgnoreCase);
            var nodeToModule = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            ModuleAccumulator GetOrCreate(string moduleId)
            {
                if (!moduleAccumulators.TryGetValue(moduleId, out var acc))
                {
                    acc = new ModuleAccumulator { FirstIndex = moduleOrder.Count };
                    moduleAccumulators[moduleId] = acc;
                    moduleOrder.Add(moduleId);
                }
                return acc;
            }

            foreach (var node in model.Nodes)
            {
                var moduleId = ResolveModuleIdForNode(node, tiers, tiersSet);
                var acc = GetOrCreate(moduleId);
                acc.Nodes.Add(node.Id);
                nodeToModule[node.Id] = moduleId;
            }

            var nodeMap = BuildNodeMap(model);
            var edgeLookup = new Dictionary<string, Edge>(StringComparer.OrdinalIgnoreCase);
            foreach (var edge in model.Edges)
            {
                if (edge == null || string.IsNullOrWhiteSpace(edge.Id)) continue;
                if (!edgeLookup.ContainsKey(edge.Id))
                {
                    edgeLookup[edge.Id] = edge;
                }
            }

            string? GetModuleIdForNode(string? nodeId)
            {
                if (string.IsNullOrWhiteSpace(nodeId)) return null;
                if (nodeToModule.TryGetValue(nodeId!, out var cached)) return cached;
                if (nodeMap.TryGetValue(nodeId!, out var node))
                {
                    var moduleId = ResolveModuleIdForNode(node, tiers, tiersSet);
                    nodeToModule[node.Id] = moduleId;
                    return moduleId;
                }
                return null;
            }

            foreach (var edge in model.Edges)
            {
                var srcModule = GetModuleIdForNode(edge.SourceId);
                if (!string.IsNullOrWhiteSpace(srcModule))
                {
                    GetOrCreate(srcModule!).Edges.Add(edge.Id);
                }

                var dstModule = GetModuleIdForNode(edge.TargetId);
                if (!string.IsNullOrWhiteSpace(dstModule))
                {
                    GetOrCreate(dstModule!).Edges.Add(edge.Id);
                }

                if (!string.IsNullOrWhiteSpace(srcModule) &&
                    !string.IsNullOrWhiteSpace(dstModule) &&
                    !string.Equals(srcModule, dstModule, StringComparison.OrdinalIgnoreCase))
                {
                    GetOrCreate(srcModule!).CrossEdges.Add(edge.Id);
                    GetOrCreate(dstModule!).CrossEdges.Add(edge.Id);
                }
            }

            var totalNodes = model.Nodes.Count;
            double? usableHeight = null;
            var pageHeight = GetPageHeight(model);
            if (pageHeight.HasValue)
            {
                var margin = GetPageMargin(model) ?? Margin;
                var title = GetTitleHeight(model);
                var usable = pageHeight.Value - (2 * margin) - title;
                if (usable > 0.01) usableHeight = usable;
            }

            var verticalSpacing = 0.6;
            if (model.Metadata.TryGetValue("layout.spacing.vertical", out var verticalRaw) &&
                double.TryParse(verticalRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var vsParsed) &&
                vsParsed >= 0)
            {
                verticalSpacing = vsParsed;
            }

            var placementMap = new Dictionary<string, NodeLayout>(StringComparer.OrdinalIgnoreCase);
            if (layout?.Nodes != null)
            {
                foreach (var nl in layout.Nodes)
                {
                    if (nl == null || string.IsNullOrWhiteSpace(nl.Id)) continue;
                    placementMap[nl.Id] = nl;
                }
            }

            foreach (var moduleId in moduleOrder)
            {
                var acc = moduleAccumulators[moduleId];
                foreach (var nodeId in acc.Nodes)
                {
                    if (!placementMap.TryGetValue(nodeId, out var placement)) continue;
                    var height = placement.Size.HasValue && placement.Size.Value.Height > 0
                        ? placement.Size.Value.Height
                        : (float)DefaultNodeHeight;
                    var bottom = placement.Position.Y;
                    var top = bottom + height;
                    if (!acc.HasPlacement)
                    {
                        acc.HasPlacement = true;
                        acc.MinY = bottom;
                        acc.MaxY = top;
                    }
                    else
                    {
                        if (bottom < acc.MinY) acc.MinY = bottom;
                        if (top > acc.MaxY) acc.MaxY = top;
                    }
                }
            }

            var projections = new List<ModuleProjection>();
            int splitModuleCount = 0;
            foreach (var moduleId in moduleOrder)
            {
                var acc = moduleAccumulators[moduleId];
                var moduleSegments = ExpandModuleStatistics(
                        moduleId,
                        acc,
                        usableHeight,
                        verticalSpacing,
                        totalNodes,
                        placementMap,
                        nodeMap,
                        edgeLookup,
                        nodeSegmentOverrides)
                    .ToList();
                if (moduleSegments.Count > 1) splitModuleCount++;
                projections.AddRange(moduleSegments);
            }

            var metrics = new PlannerMetrics
            {
                OriginalModuleCount = moduleOrder.Count,
                SegmentCount = projections.Count,
                SplitModuleCount = splitModuleCount,
                AverageSegmentsPerModule = moduleOrder.Count > 0 ? projections.Count / (double)moduleOrder.Count : 0.0
            };

            var modules = projections
                .OrderBy(m => m.HasPlacement ? m.MinY : double.PositiveInfinity)
                .ThenBy(m => m.FirstIndex)
                .Select(m => m.Stats)
                .ToArray();

            var debugPaging = Environment.GetEnvironmentVariable("VDG_DEBUG_PAGING");
            if (!string.IsNullOrWhiteSpace(debugPaging) &&
                (string.Equals(debugPaging, "1", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(debugPaging, "true", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var mod in modules)
                {
                    Console.WriteLine($"info: debug paging module={mod.ModuleId} nodes={mod.NodeCount} connectors={mod.ConnectorCount} occupancy≈{mod.OccupancyPercent:F2}%");
                }
            }

            var dataset = new DiagramDataset { Modules = modules };
            return new PlannerBuildResult(dataset, nodeSegmentOverrides, metrics);
        }

        private static IEnumerable<ModuleProjection> ExpandModuleStatistics(
            string moduleId,
            ModuleAccumulator accumulator,
            double? usableHeight,
            double verticalSpacing,
            int totalNodes,
            IDictionary<string, NodeLayout> placementMap,
            IDictionary<string, VDG.Core.Models.Node> nodeMap,
            IDictionary<string, Edge> edgeLookup,
            IDictionary<string, string> nodeSegmentOverrides)
        {
            double ComputeEstimatedHeight(int nodeCount, double span)
            {
                if (span > 0.0)
                {
                    return Math.Max(DefaultNodeHeight, span + verticalSpacing);
                }

                var perNode = DefaultNodeHeight + Math.Max(0.0, verticalSpacing);
                return Math.Max(DefaultNodeHeight, nodeCount * perNode);
            }

            double ComputeOccupancy(double estimatedHeight, int nodeCount)
            {
                if (usableHeight.HasValue && usableHeight.Value > 0.01)
                {
                    return Math.Min(100.0, Math.Max(0.0, (estimatedHeight / usableHeight.Value) * 100.0));
                }

                return totalNodes > 0 ? Math.Min(100.0, (nodeCount * 100.0) / totalNodes) : 0.0;
            }

            var projections = new List<ModuleProjection>();
            var hasSpan = accumulator.HasPlacement && !double.IsInfinity(accumulator.MinY) && !double.IsInfinity(accumulator.MaxY);
            var span = hasSpan ? accumulator.MaxY - accumulator.MinY : 0.0;

            var placementInfos = new List<NodePlacementInfo>();
            foreach (var nodeId in accumulator.Nodes)
            {
                if (placementMap.TryGetValue(nodeId, out var placement))
                {
                    var height = placement.Size.HasValue && placement.Size.Value.Height > 0
                        ? placement.Size.Value.Height
                        : (float)DefaultNodeHeight;
                    var bottom = placement.Position.Y;
                    var top = bottom + height;
                    placementInfos.Add(new NodePlacementInfo(nodeId, bottom, top, height));
                }
            }

            bool ShouldSplit()
            {
                if (accumulator.Nodes.Count <= 6) return false;

                if (!usableHeight.HasValue || usableHeight.Value <= 0.01)
                {
                    return accumulator.Nodes.Count > 100;
                }

                if (placementInfos.Count == 0)
                {
                    return accumulator.Nodes.Count > 50;
                }

                if (!hasSpan) return false;
                if (span <= usableHeight.Value * ModuleSplitThresholdMultiplier) return false;
                return true;
            }

            double ClampSpan(double rawSpan)
            {
                if (!usableHeight.HasValue || usableHeight.Value <= 0.01) return rawSpan;
                var threshold = usableHeight.Value * ModuleSplitThresholdMultiplier;
                return Math.Min(rawSpan, threshold);
            }

            if (!ShouldSplit())
            {
                var clampedSpan = ClampSpan(span);
                var estimatedHeight = ComputeEstimatedHeight(accumulator.Nodes.Count, clampedSpan);
                var occupancy = ComputeOccupancy(estimatedHeight, accumulator.Nodes.Count);
                var normalizedSpan = Math.Max(DefaultNodeHeight, clampedSpan);
                projections.Add(new ModuleProjection
                {
                    Stats = new ModuleStats
                    {
                        ModuleId = moduleId,
                        ConnectorCount = accumulator.Edges.Count,
                        NodeCount = accumulator.Nodes.Count,
                        OccupancyPercent = occupancy,
                        HeightEstimate = (float)estimatedHeight,
                        SpanMin = 0f,
                        SpanMax = (float)normalizedSpan,
                        HasSpan = hasSpan && normalizedSpan > 0.0,
                        CrossModuleConnectors = accumulator.CrossEdges.Count
                    },
                    FirstIndex = accumulator.FirstIndex,
                    HasPlacement = hasSpan,
                    MinY = hasSpan ? accumulator.MinY : double.PositiveInfinity
                });

                return projections;
            }

            var targetHeight = usableHeight!.Value;
            var effectiveSpan = span > 0.0 ? span : accumulator.Nodes.Count * (DefaultNodeHeight + verticalSpacing);
            var segmentCount = Math.Min(ModuleSplitMaxSegments, Math.Max(1, (int)Math.Ceiling(effectiveSpan / targetHeight)));
            segmentCount = Math.Min(segmentCount, Math.Max(1, placementInfos.Count));
            var nodeBasedLimit = Math.Max(1, (int)Math.Ceiling(accumulator.Nodes.Count / 20.0));
            segmentCount = Math.Min(segmentCount, nodeBasedLimit);
            if (segmentCount <= 1)
            {
                var clampedSpan = ClampSpan(span);
                var estimatedHeight = ComputeEstimatedHeight(accumulator.Nodes.Count, clampedSpan);
                var occupancy = ComputeOccupancy(estimatedHeight, accumulator.Nodes.Count);
                var normalizedSpan = Math.Max(DefaultNodeHeight, clampedSpan);
                projections.Add(new ModuleProjection
                {
                    Stats = new ModuleStats
                    {
                        ModuleId = moduleId,
                        ConnectorCount = accumulator.Edges.Count,
                        NodeCount = accumulator.Nodes.Count,
                        OccupancyPercent = occupancy,
                        HeightEstimate = (float)estimatedHeight,
                        SpanMin = 0f,
                        SpanMax = (float)normalizedSpan,
                        HasSpan = hasSpan && normalizedSpan > 0.0,
                        CrossModuleConnectors = accumulator.CrossEdges.Count
                    },
                    FirstIndex = accumulator.FirstIndex,
                    HasPlacement = hasSpan,
                    MinY = hasSpan ? accumulator.MinY : double.PositiveInfinity
                });

                return projections;
            }

            var rawSegments = new SegmentInfo[segmentCount];
            for (int i = 0; i < segmentCount; i++)
            {
                rawSegments[i] = new SegmentInfo(i);
            }

            if (placementInfos.Count > 0)
            {
                foreach (var info in placementInfos.OrderBy(p => p.Center))
                {
                    var relative = info.Center - accumulator.MinY;
                    var idx = (int)Math.Floor(relative / targetHeight);
                    if (idx < 0) idx = 0;
                    if (idx >= segmentCount) idx = segmentCount - 1;
                    var seg = rawSegments[idx];
                    seg.Nodes.Add(info.Id);
                    if (info.Bottom < seg.MinY) seg.MinY = info.Bottom;
                    if (info.Top > seg.MaxY) seg.MaxY = info.Top;
                }
            }
            else
            {
                var orderedNodes = accumulator.Nodes
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var perSegment = Math.Max(1, (int)Math.Ceiling(orderedNodes.Count / (double)segmentCount));
                for (int i = 0; i < orderedNodes.Count; i++)
                {
                    var idx = i / perSegment;
                    if (idx >= segmentCount) idx = segmentCount - 1;
                    var seg = rawSegments[idx];
                    seg.Nodes.Add(orderedNodes[i]);
                    if (double.IsPositiveInfinity(seg.MinY)) seg.MinY = accumulator.MinY;
                    if (double.IsNegativeInfinity(seg.MaxY)) seg.MaxY = accumulator.MaxY;
                }
            }

            var nodesWithoutPlacement = accumulator.Nodes
                .Where(id => !placementMap.ContainsKey(id))
                .ToList();

            var nonEmptySegments = rawSegments.Where(s => s.Nodes.Count > 0).ToList();
            if (nonEmptySegments.Count == 0)
            {
                var estimatedHeight = ComputeEstimatedHeight(accumulator.Nodes.Count, span);
                var occupancy = ComputeOccupancy(estimatedHeight, accumulator.Nodes.Count);
                projections.Add(new ModuleProjection
                {
                    Stats = new ModuleStats
                    {
                        ModuleId = moduleId,
                        ConnectorCount = accumulator.Edges.Count,
                        NodeCount = accumulator.Nodes.Count,
                        OccupancyPercent = occupancy,
                        HeightEstimate = (float)estimatedHeight,
                        SpanMin = hasSpan ? (float)accumulator.MinY : 0f,
                        SpanMax = hasSpan ? (float)accumulator.MaxY : 0f,
                        HasSpan = hasSpan,
                        CrossModuleConnectors = accumulator.CrossEdges.Count
                    },
                    FirstIndex = accumulator.FirstIndex,
                    HasPlacement = hasSpan,
                    MinY = hasSpan ? accumulator.MinY : double.PositiveInfinity
                });

                return projections;
            }

            if (nodesWithoutPlacement.Count > 0)
            {
                foreach (var nodeId in nodesWithoutPlacement)
                {
                    var targetSegment = nonEmptySegments.OrderBy(s => s.Nodes.Count).First();
                    targetSegment.Nodes.Add(nodeId);
                }
            }

            var fallbackMin = double.IsPositiveInfinity(accumulator.MinY) ? 0.0 : accumulator.MinY;
            var fallbackMax = double.IsNegativeInfinity(accumulator.MaxY) ? fallbackMin : accumulator.MaxY;

            foreach (var seg in nonEmptySegments)
            {
                if (double.IsPositiveInfinity(seg.MinY)) seg.MinY = fallbackMin;
                if (double.IsNegativeInfinity(seg.MaxY)) seg.MaxY = fallbackMax;
            }

            var nodeToSegment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < nonEmptySegments.Count; i++)
            {
                var seg = nonEmptySegments[i];
                seg.ModuleId = $"{moduleId}#part{i + 1}";
                foreach (var nodeId in seg.Nodes)
                {
                    nodeToSegment[nodeId] = seg.ModuleId;
                    nodeSegmentOverrides[nodeId] = seg.ModuleId;
                }
            }

            var connectorCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var crossCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var seg in nonEmptySegments)
            {
                connectorCounts[seg.ModuleId] = 0;
                crossCounts[seg.ModuleId] = 0;
            }

            foreach (var edgeId in accumulator.Edges)
            {
                if (string.IsNullOrWhiteSpace(edgeId)) continue;
                if (!edgeLookup.TryGetValue(edgeId, out var edge)) continue;

                string? segId = null;
                if (!string.IsNullOrWhiteSpace(edge.SourceId) && nodeToSegment.TryGetValue(edge.SourceId, out var sourceSeg))
                {
                    segId = sourceSeg;
                }
                else if (!string.IsNullOrWhiteSpace(edge.TargetId) && nodeToSegment.TryGetValue(edge.TargetId, out var targetSegFallback))
                {
                    segId = targetSegFallback;
                }

                if (segId == null) continue;
                connectorCounts[segId] += 1;

                if (!string.IsNullOrWhiteSpace(edge.TargetId) &&
                    nodeToSegment.TryGetValue(edge.TargetId, out var targetSeg) &&
                    !string.Equals(targetSeg, segId, StringComparison.OrdinalIgnoreCase))
                {
                    crossCounts[segId] += 1;
                }
            }

            double runningOffset = 0.0;
            foreach (var seg in nonEmptySegments)
            {
                var nodeCount = seg.Nodes.Count;
                var spanAbsMin = double.IsPositiveInfinity(seg.MinY) ? accumulator.MinY : seg.MinY;
                var spanAbsMax = double.IsNegativeInfinity(seg.MaxY) ? accumulator.MaxY : seg.MaxY;
                if (spanAbsMax < spanAbsMin) spanAbsMax = spanAbsMin;
                var segSpan = spanAbsMax - spanAbsMin;
                var clampedSpan = ClampSpan(segSpan);
                var normalizedSpan = Math.Max(DefaultNodeHeight, clampedSpan);

                var spanMin = runningOffset;
                var spanMax = spanMin + normalizedSpan;
                runningOffset = spanMax + verticalSpacing;

                var estimatedHeight = ComputeEstimatedHeight(nodeCount, clampedSpan);
                var occupancy = ComputeOccupancy(estimatedHeight, nodeCount);

                projections.Add(new ModuleProjection
                {
                    Stats = new ModuleStats
                    {
                        ModuleId = seg.ModuleId,
                        ConnectorCount = connectorCounts.TryGetValue(seg.ModuleId, out var c) ? c : 0,
                        NodeCount = nodeCount,
                        OccupancyPercent = occupancy,
                        HeightEstimate = (float)estimatedHeight,
                        SpanMin = (float)spanMin,
                        SpanMax = (float)spanMax,
                        HasSpan = true,
                        CrossModuleConnectors = crossCounts.TryGetValue(seg.ModuleId, out var cross) ? cross : 0
                    },
                    FirstIndex = accumulator.FirstIndex,
                    HasPlacement = true,
                    MinY = spanAbsMin
                });
            }

            return projections;
        }

        private static IReadOnlyDictionary<string, int> BuildNodePageAssignments(DiagramModel model, PagePlan[] pagePlans, IDictionary<string, string>? nodeModuleOverrides)
        {
            var assignments = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (pagePlans == null || pagePlans.Length == 0) return assignments;

            var moduleToPage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var plan in pagePlans)
            {
                if (plan == null || plan.Modules == null) continue;
                foreach (var module in plan.Modules)
                {
                    if (string.IsNullOrWhiteSpace(module)) continue;
                    var key = module.Trim();
                    if (!moduleToPage.ContainsKey(key))
                    {
                        var index = plan.PageIndex;
                        if (index < 0) index = 0;
                        moduleToPage[key] = index;
                    }
                }
            }

            if (moduleToPage.Count == 0) return assignments;

            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);
            foreach (var node in model.Nodes)
            {
                string moduleId;
                if (nodeModuleOverrides != null && nodeModuleOverrides.TryGetValue(node.Id, out var overrideModule) && !string.IsNullOrWhiteSpace(overrideModule))
                {
                    moduleId = overrideModule;
                }
                else
                {
                    moduleId = ResolveModuleIdForNode(node, tiers, tiersSet);
                }

                if (moduleToPage.TryGetValue(moduleId, out var pageIndex))
                {
                    if (pageIndex < 0) pageIndex = 0;
                    assignments[node.Id] = pageIndex;
                }
            }

            return assignments;
        }

        private static PagePlan[] BuildLayoutPagePlans(DiagramModel model, LayoutResult layout, PageSplitOptions options)
        {
            if (layout == null || layout.Nodes == null || layout.Nodes.Length == 0) return Array.Empty<PagePlan>();

            var pageHeight = GetPageHeight(model);
            if (!pageHeight.HasValue) return Array.Empty<PagePlan>();

            var margin = GetPageMargin(model) ?? Margin;
            var title = GetTitleHeight(model);
            var usable = pageHeight.Value - (2 * margin) - title;
            if (usable <= 0) return Array.Empty<PagePlan>();

            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);
            var nodeMap = BuildNodeMap(model);
            var (minLeft, minBottom, _, _) = ComputeLayoutBounds(layout);

            var nodePage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var pageData = new Dictionary<int, PageAccumulator>();

            foreach (var nl in layout.Nodes)
            {
                if (nl == null || string.IsNullOrWhiteSpace(nl.Id)) continue;
                var yNorm = nl.Position.Y - (float)minBottom;
                var pageIndex = (int)Math.Floor(yNorm / (float)usable);
                if (pageIndex < 0) pageIndex = 0;
                nodePage[nl.Id] = pageIndex;

                if (!pageData.TryGetValue(pageIndex, out var acc))
                {
                    acc = new PageAccumulator();
                    pageData[pageIndex] = acc;
                }

                acc.NodeCount += 1;

                if (nodeMap.TryGetValue(nl.Id, out var node))
                {
                    var moduleId = ResolveModuleIdForNode(node, tiers, tiersSet);
                    if (!string.IsNullOrWhiteSpace(moduleId)) acc.Modules.Add(moduleId);
                }

                var height = (nl.Size.HasValue && nl.Size.Value.Height > 0)
                    ? nl.Size.Value.Height
                    : (float)DefaultNodeHeight;
                acc.HeightSum += height;
            }

            foreach (var edge in model.Edges)
            {
                if (edge == null) continue;
                if (string.IsNullOrWhiteSpace(edge.SourceId)) continue;
                if (!nodePage.TryGetValue(edge.SourceId, out var srcPage)) continue;
                if (!pageData.TryGetValue(srcPage, out var acc)) continue;

                var destPage = 0;
                if (!string.IsNullOrWhiteSpace(edge.TargetId) && nodePage.TryGetValue(edge.TargetId, out var dst))
                {
                    destPage = dst;
                }

                if (options != null && !options.LaneSplitAllowed && srcPage != destPage)
                {
                    continue;
                }

                acc.ConnectorCount += 1;
            }

            var plans = pageData
                .OrderBy(kvp => kvp.Key)
                .Select(kvp =>
                {
                    var pageIndex = kvp.Key;
                    var acc = kvp.Value;
                    var occupancy = usable > 0 ? Math.Min(100.0, Math.Max(0.0, (acc.HeightSum / usable) * 100.0)) : 0.0;
                    return new PagePlan
                    {
                        PageIndex = pageIndex,
                        Modules = acc.Modules.OrderBy(m => m, StringComparer.OrdinalIgnoreCase).ToArray(),
                        Connectors = acc.ConnectorCount,
                        Nodes = acc.NodeCount,
                        Occupancy = occupancy
                    };
                })
                .ToArray();

            return plans;
        }

        private sealed class PageAccumulator
        {
            public PageAccumulator()
            {
                Modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            public HashSet<string> Modules { get; }
            public int NodeCount { get; set; }
            public int ConnectorCount { get; set; }
            public double HeightSum { get; set; }
        }

        private static PageSplitOptions BuildPageSplitOptions(DiagramModel model)
        {
            var options = new PageSplitOptions
            {
                MaxConnectors = 400,
                MaxOccupancyPercent = 110.0,
                LaneSplitAllowed = false,
                MaxPageHeightIn = 0.0,
                MaxModulesPerPage = 10,
                HeightSlackPercent = 25.0
            };

            if (model.Metadata.TryGetValue("layout.page.plan.maxConnectors", out var connectorsRaw) &&
                int.TryParse(connectorsRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var connectors) &&
                connectors > 0)
            {
                options.MaxConnectors = connectors;
            }

            if (model.Metadata.TryGetValue("layout.page.plan.maxOccupancyPercent", out var occupancyRaw) &&
                double.TryParse(occupancyRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var occupancy) &&
                occupancy > 0)
            {
                options.MaxOccupancyPercent = occupancy;
            }

            if (model.Metadata.TryGetValue("layout.page.plan.laneSplitAllowed", out var laneRaw) &&
                bool.TryParse(laneRaw, out var laneSplit))
            {
                options.LaneSplitAllowed = laneSplit;
            }

            var pageHeight = GetPageHeight(model);
            if (pageHeight.HasValue)
            {
                var margin = GetPageMargin(model) ?? Margin;
                var title = GetTitleHeight(model);
                var usable = pageHeight.Value - (2 * margin) - title;
                if (usable > 0.01) options.MaxPageHeightIn = usable;
            }

            if (model.Metadata.TryGetValue("layout.page.plan.maxHeightIn", out var heightRaw) &&
                double.TryParse(heightRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var maxHeight) &&
                maxHeight > 0.0)
            {
                options.MaxPageHeightIn = maxHeight;
            }

            if (model.Metadata.TryGetValue("layout.page.plan.maxModulesPerPage", out var modulesRaw) &&
                int.TryParse(modulesRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxModules) &&
                maxModules > 0)
            {
                options.MaxModulesPerPage = maxModules;
            }

            if (model.Metadata.TryGetValue("layout.page.plan.heightSlackPercent", out var slackRaw) &&
                double.TryParse(slackRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var slack) &&
                slack >= 0.0)
            {
                options.HeightSlackPercent = slack;
            }

            return options;
        }

        private static double Clamp01(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static (double xr, double yr) ApplyBundleOffsetRel(string side, (double xr, double yr) rel, NodePlacement shape, (int index, int size) info, double sepIn)
        {
            if (info.size <= 1 || sepIn <= 0) return rel;
            var centerIndex = (info.size - 1) / 2.0;
            var delta = (info.index - centerIndex) * sepIn;
            if (side.Equals("left", StringComparison.OrdinalIgnoreCase) || side.Equals("right", StringComparison.OrdinalIgnoreCase))
            {
                var frac = shape.Height > 0 ? delta / shape.Height : 0.0;
                var y = Clamp01(rel.yr + frac, 0.1, 0.9);
                return (rel.xr, y);
            }
            else if (side.Equals("top", StringComparison.OrdinalIgnoreCase) || side.Equals("bottom", StringComparison.OrdinalIgnoreCase))
            {
                var frac = shape.Width > 0 ? delta / shape.Width : 0.0;
                var x = Clamp01(rel.xr + frac, 0.1, 0.9);
                return (x, rel.yr);
            }
            return rel;
        }

        private static List<double> ComputeCorridorMidXs(DiagramModel model, IDictionary<string, NodePlacement> placements)
        {
            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);
            var nodeMap = BuildNodeMap(model);

            var spans = new List<(bool has, double left, double right)>(tiers.Length);
            foreach (var t in tiers) spans.Add((false, double.MaxValue, double.MinValue));

            foreach (var kv in placements)
            {
                if (!nodeMap.TryGetValue(kv.Key, out var node)) continue;
                var tier = !string.IsNullOrWhiteSpace(node.Tier) ? node.Tier! : (node.Metadata.TryGetValue("tier", out var tv) ? tv : tiers.First());
                if (!tiersSet.Contains(tier)) tier = tiers.First();
                var idx = Array.IndexOf(tiers, tier);
                var np = kv.Value;
                var left = np.Left;
                var right = np.Left + np.Width;
                var s = spans[idx];
                s.has = true; s.left = Math.Min(s.left, left); s.right = Math.Max(s.right, right);
                spans[idx] = s;
            }

            var mids = new List<double>();
            for (int i = 0; i + 1 < spans.Count; i++)
            {
                var a = spans[i]; var b = spans[i + 1];
                if (a.has && b.has)
                {
                    mids.Add((a.right + b.left) / 2.0);
                }
            }
            return mids;
        }

        private static List<double> ComputeCorridorMidXsFromLayout(DiagramModel model, LayoutResult layout)
        {
            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);
            var spans = new List<(bool has, double left, double right)>(tiers.Length);
            for (int i = 0; i < tiers.Length; i++) spans.Add((false, double.MaxValue, double.MinValue));

            foreach (var nl in layout.Nodes)
            {
                var node = model.Nodes.FirstOrDefault(n => string.Equals(n.Id, nl.Id, StringComparison.OrdinalIgnoreCase));
                if (node == null) continue;
                var t = !string.IsNullOrWhiteSpace(node.Tier) ? node.Tier! : (node.Metadata.TryGetValue("tier", out var tv) ? tv : tiers.First());
                if (!tiersSet.Contains(t)) t = tiers.First();
                var idx = Array.IndexOf(tiers, t);
                var left = nl.Position.X;
                var right = nl.Position.X + (nl.Size.HasValue && nl.Size.Value.Width > 0 ? nl.Size.Value.Width : (float)DefaultNodeWidth);
                var s = spans[idx];
                s.has = true; s.left = Math.Min(s.left, left); s.right = Math.Max(s.right, right);
                spans[idx] = s;
            }

            var mids = new List<double>();
            for (int i = 0; i + 1 < spans.Count; i++)
            {
                var a = spans[i]; var b = spans[i + 1];
                if (a.has && b.has) mids.Add((a.right + b.left) / 2.0);
            }
            return mids;
        }

        private static (double x, double y) ToAbsAttach(NodePlacement shape, string side, double xr, double yr)
        {
            if (side.Equals("left", StringComparison.OrdinalIgnoreCase))
                return (shape.Left, shape.Bottom + yr * shape.Height);
            if (side.Equals("right", StringComparison.OrdinalIgnoreCase))
                return (shape.Left + shape.Width, shape.Bottom + yr * shape.Height);
            if (side.Equals("top", StringComparison.OrdinalIgnoreCase))
                return (shape.Left + xr * shape.Width, shape.Bottom + shape.Height);
            if (side.Equals("bottom", StringComparison.OrdinalIgnoreCase))
                return (shape.Left + xr * shape.Width, shape.Bottom);
            return (shape.Left + xr * shape.Width, shape.Bottom + yr * shape.Height);
        }

        private static double? CorridorXForEdge(DiagramModel model, IDictionary<string, NodePlacement> placements, string srcId, string dstId)
        {
            var tiers = GetOrderedTiers(model);
            var nodeMap = BuildNodeMap(model);
            if (!nodeMap.TryGetValue(srcId, out var s) || !nodeMap.TryGetValue(dstId, out var t)) return null;
            string GetTier(VDG.Core.Models.Node n)
            {
                var tr = !string.IsNullOrWhiteSpace(n.Tier) ? n.Tier! : (n.Metadata.TryGetValue("tier", out var tv) ? tv : tiers.First());
                return tiers.Contains(tr) ? tr : tiers.First();
            }
            var si = Array.IndexOf(tiers, GetTier(s));
            var ti = Array.IndexOf(tiers, GetTier(t));
            if (si == ti) return null;
            var mids = ComputeCorridorMidXs(model, placements);
            if (mids.Count == 0) return null;
            var a = Math.Min(si, ti);
            var b = Math.Max(si, ti);
            // Average of mids between lanes [a..b)
            double sum = 0; int c = 0;
            for (int i = a; i < b && i < mids.Count; i++) { sum += mids[i]; c++; }
            return c > 0 ? sum / c : (double?)null;
        }

        private static double? CorridorXForEdgeFromLayout(DiagramModel model, LayoutResult layout, string srcId, string dstId)
        {
            var tiers = GetOrderedTiers(model);
            var nodeMap = BuildNodeMap(model);
            if (!nodeMap.TryGetValue(srcId, out var s) || !nodeMap.TryGetValue(dstId, out var t)) return null;
            string GetTier(VDG.Core.Models.Node n)
            {
                var tr = !string.IsNullOrWhiteSpace(n.Tier) ? n.Tier! : (n.Metadata.TryGetValue("tier", out var tv) ? tv : tiers.First());
                return tiers.Contains(tr) ? tr : tiers.First();
            }
            var si = Array.IndexOf(tiers, GetTier(s));
            var ti = Array.IndexOf(tiers, GetTier(t));
            if (si == ti) return null;
            var mids = ComputeCorridorMidXsFromLayout(model, layout);
            if (mids.Count == 0) return null;
            var a = Math.Min(si, ti);
            var b = Math.Max(si, ti);
            double sum = 0; int c = 0;
            for (int i = a; i < b && i < mids.Count; i++) { sum += mids[i]; c++; }
            return c > 0 ? sum / c : (double?)null;
        }

        private static bool TryGetWaypointsFromMetadata(Edge edge, out List<(double x, double y)> points)
        {
            points = new List<(double x, double y)>();
            if (edge.Metadata == null) return false;
            if (!edge.Metadata.TryGetValue("edge.waypoints", out var json) || string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        if (el.TryGetProperty("x", out var xEl) && el.TryGetProperty("y", out var yEl))
                        {
                            var x = xEl.GetDouble(); var y = yEl.GetDouble();
                            points.Add((x, y));
                        }
                    }
                    return points.Count > 0;
                }
            }
            catch { }
            return false;
        }

        private static void DrawLabelBox(dynamic page, string text, double midX, double midY, double offX, double offY)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var width = Math.Max(0.6, Math.Min(3.0, text.Length * 0.09));
            var height = 0.25;
            var left = midX + offX - (width / 2.0);
            var bottom = midY + offY - (height / 2.0);
            try
            {
                dynamic box = page.DrawRectangle(left, bottom, left + width, bottom + height);
                box.Text = text;
                TrySetFormula(box, "LinePattern", "1");
                TrySetFormula(box, "LineColor", "RGB(200,200,200)");
                TrySetFormula(box, "FillForegnd", "RGB(255,255,255)");
                try { box.BringToFront(); } catch { }
                ReleaseCom(box);
            }
            catch { }
        }

        private static (double minLeft, double minBottom, double maxRight, double maxTop) ComputeLayoutBounds(LayoutResult layout)
        {
            double minLeft = double.MaxValue, minBottom = double.MaxValue, maxRight = double.MinValue, maxTop = double.MinValue;

            foreach (var nl in layout.Nodes)
            {
                var w = nl.Size.HasValue && nl.Size.Value.Width > 0 ? nl.Size.Value.Width : (float)DefaultNodeWidth;
                var h = nl.Size.HasValue && nl.Size.Value.Height > 0 ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                var l = nl.Position.X;
                var b = nl.Position.Y;
                minLeft = Math.Min(minLeft, l);
                minBottom = Math.Min(minBottom, b);
                maxRight = Math.Max(maxRight, l + w);
                maxTop = Math.Max(maxTop, b + h);
            }

            if (double.IsInfinity(minLeft)) { minLeft = 0; minBottom = 0; maxRight = 0; maxTop = 0; }
            return (minLeft, minBottom, maxRight, maxTop);
        }

        private static LayerRenderContext BuildLayerRenderContext(DiagramModel model, LayoutPlan? layoutPlan, HashSet<int>? visibleLayers)
        {
            if (layoutPlan?.Layers == null || layoutPlan.Layers.Length == 0)
            {
                return LayerRenderContext.Disabled;
            }

            var moduleToLayer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var layerNames = new Dictionary<int, string>();

            string[]? parsedNames = null;
            if (model.Metadata != null &&
                model.Metadata.TryGetValue("layout.layers.names", out var namesRaw) &&
                !string.IsNullOrWhiteSpace(namesRaw))
            {
                parsedNames = namesRaw
                    .Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => n.Trim())
                    .Where(n => n.Length > 0)
                    .ToArray();
            }

            foreach (var plan in layoutPlan.Layers)
            {
                if (plan == null) continue;
                var layerIndex = plan.LayerIndex;
                if (!layerNames.ContainsKey(layerIndex))
                {
                    string defaultName = $"Layer {layerIndex + 1}";
                    if (parsedNames != null && layerIndex >= 0 && layerIndex < parsedNames.Length)
                    {
                        layerNames[layerIndex] = parsedNames[layerIndex];
                    }
                    else
                    {
                        layerNames[layerIndex] = defaultName;
                    }
                }

                if (plan.Modules == null) continue;
                foreach (var moduleId in plan.Modules)
                {
                    if (string.IsNullOrWhiteSpace(moduleId)) continue;
                    moduleToLayer[moduleId.Trim()] = layerIndex;
                }
            }

            if (moduleToLayer.Count == 0)
            {
                return LayerRenderContext.Disabled;
            }

            return LayerRenderContext.Create(moduleToLayer, layerNames, visibleLayers);
        }

        private static void RunVisio(
            DiagramModel model,
            LayoutResult layout,
            LayoutPlan? layoutPlan,
            string outputPath,
            PagePlan[] pagePlans,
            IReadOnlyDictionary<string, int> nodePageAssignments,
            HashSet<int>? visibleLayers)
        {
            dynamic? app = null;
            dynamic? documents = null;
            dynamic? document = null;
            dynamic? pages = null;
            dynamic? page = null;
            VisioPageManager? pageManager = null;

            var placements = new Dictionary<string, NodePlacement>(StringComparer.OrdinalIgnoreCase);

            try
            {
                app = CreateVisioApplication();
                app.Visible = false;

                app.AlertResponse = 7;

                documents = app.Documents;
                document = documents.Add("");
                pages = document.Pages;
                pageManager = new VisioPageManager(pages);
                pageManager.SetPagePlans(pagePlans);
                var firstPageInfo = pageManager.EnsurePage(0, "Layered 1");
                page = firstPageInfo.Page;

                // Stamp document metadata (schema/generator/version/title)
                StampDocumentMetadata(model, document);

                if (layout.Nodes.Length == 0)
                {
                    throw new InvalidDataException("Layout produced zero nodes.");
                }

                var layerContext = BuildLayerRenderContext(model, layoutPlan, visibleLayers);

                if (ShouldPaginate(model, layout))
                {
                    DrawMultiPage(model, layout, layoutPlan, pageManager, nodePageAssignments, layerContext);
                }
                else
                {
                    ComputePlacements(model, layout, layoutPlan, placements, firstPageInfo, layerContext);
                    DrawLaneContainers(model, layout, firstPageInfo.Page);
                    DrawConnectors(model, placements, firstPageInfo, pageManager, layout, layerContext);
                }

                EmitPageSummary(pageManager);

                document.SaveAs(outputPath);
            }
            finally
            {
                var pageCom = (object?)page;
                var pagesCom = (object?)pages;
                var documentCom = (object?)document;
                var documentsCom = (object?)documents;
                var appCom = (object?)app;
                var managerPages = pageManager?.Pages;

                if (managerPages != null)
                {
                    foreach (var info in managerPages)
                    {
                        var pageObj = (object?)info.Page;
                        if (pageObj == null) continue;
                        if (ReferenceEquals(pageObj, pageCom)) continue;
                        ReleaseCom(pageObj);
                    }
                }

                if (document != null)
                {
                    try { document.Close(); } catch { /* ignore */ }
                }

                if (app != null)
                {
                    try { app.Quit(); } catch { /* ignore */ }
                }

                foreach (var placement in placements.Values)
                {
                    ReleaseCom(placement.Shape);
                }

                ReleaseCom(pageCom);
                ReleaseCom(pagesCom);
                ReleaseCom(documentCom);
                ReleaseCom(documentsCom);
                ReleaseCom(appCom);
            }
        }

        private static void ApplyNodeStyle(Node node, dynamic shape)
        {
            if (node.Style == null || node.Style.IsDefault())
            {
                return;
            }

            if (TryParseColor(node.Style.FillColor, out var fill))
            {
                TrySetFormula(shape, "FillForegnd", $"RGB({fill.R},{fill.G},{fill.B})");
            }

            if (TryParseColor(node.Style.StrokeColor, out var stroke))
            {
                TrySetFormula(shape, "LineColor", $"RGB({stroke.R},{stroke.G},{stroke.B})");
            }

            ApplyLinePattern(shape, node.Style.LinePattern);
        }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
        private static dynamic? EnsureVisioLayer(VisioPageManager.PageInfo pageInfo, LayerRenderContext context, int layerIndex)
        {
            if (!context.Enabled || layerIndex < 0)
            {
                return null;
            }

            if (pageInfo.LayerObjects.TryGetValue(layerIndex, out var cachedLayer) && cachedLayer != null)
            {
                return cachedLayer;
            }

            var layerName = context.LayerNames.TryGetValue(layerIndex, out var name)
                ? name
                : $"Layer {layerIndex + 1}";

            dynamic? page = pageInfo.Page ?? throw new COMException("Visio page was not created.");
            dynamic? layers = null;
            try { layers = page.Layers; }
            catch { layers = null; }
            if (layers == null) return null;

            dynamic? layer = TryGetLayerByName(layers, layerName);
            if (layer == null)
            {
                layer = CreateLayer(layers, layerName);
            }

            if (layer != null)
            {
                pageInfo.LayerObjects[layerIndex] = layer;
            }

            return layer;
        }

        private static dynamic? TryGetLayerByName(dynamic layers, string layerName)
        {
            if (layers == null || string.IsNullOrWhiteSpace(layerName))
            {
                return null;
            }

            try { return layers.ItemU[layerName]; }
            catch { }
            try { return layers.Item[layerName]; }
            catch { }
            try { return layers[layerName]; }
            catch { }

            int count = 0;
            try { count = Convert.ToInt32(layers.Count); }
            catch { count = 0; }

            for (int index = 1; index <= count; index++)
            {
                dynamic? candidate = null;
                try { candidate = layers.Item(index); }
                catch
                {
                    try { candidate = layers[index]; }
                    catch
                    {
                        try { candidate = layers.get_Item(index); }
                        catch { candidate = null; }
                    }
                }

                if (candidate == null) continue;

                string? candidateName = null;
                try { candidateName = candidate.NameU; }
                catch { candidateName = null; }
                if (string.IsNullOrWhiteSpace(candidateName))
                {
                    try { candidateName = candidate.Name; }
                    catch { candidateName = null; }
                }

                if (!string.IsNullOrWhiteSpace(candidateName) &&
                    string.Equals(candidateName, layerName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static dynamic? CreateLayer(dynamic layers, string layerName)
        {
            if (layers == null) return null;

            dynamic? layer = null;
            try
            {
                layer = layers.Add(layerName);
            }
            catch
            {
                try
                {
                    layer = layers.Add();
                    if (layer != null)
                    {
                        try { layer.NameU = layerName; } catch { }
                        try { layer.Name = layerName; } catch { }
                    }
                }
                catch
                {
                    layer = null;
                }
            }

            if (layer != null)
            {
                try { layer.NameU = layerName; } catch { }
                try { layer.Name = layerName; } catch { }
            }

            return layer;
        }

        private static void AssignShapeToLayer(LayerRenderContext context, VisioPageManager.PageInfo pageInfo, dynamic shape, int layerIndex)
        {
            if (!context.Enabled || shape == null || layerIndex < 0)
            {
                return;
            }

            var layer = EnsureVisioLayer(pageInfo, context, layerIndex);
            if (layer == null) return;
            AddShapeToLayer(layer, shape);
        }

        private static void AssignConnectorToLayers(LayerRenderContext context, VisioPageManager.PageInfo pageInfo, dynamic connector, NodePlacement? sourcePlacement, NodePlacement? targetPlacement)
        {
            if (!context.Enabled || connector == null)
            {
                return;
            }

            var layerIndices = new HashSet<int>();
            if (sourcePlacement?.LayerIndex is int src && src >= 0) layerIndices.Add(src);
            if (targetPlacement?.LayerIndex is int dst && dst >= 0) layerIndices.Add(dst);

            foreach (var layerIndex in layerIndices)
            {
                var layer = EnsureVisioLayer(pageInfo, context, layerIndex);
                if (layer != null)
                {
                    AddShapeToLayer(layer, connector);
                }
            }
        }

        private static void AddShapeToLayer(dynamic layer, dynamic shape)
        {
            if (layer == null || shape == null)
            {
                return;
            }

            try
            {
                layer.Add(shape, (short)0);
                return;
            }
            catch { }

            try
            {
                layer.Add(shape, 0);
                return;
            }
            catch { }

            try
            {
                shape.AddToLayer(layer);
                return;
            }
            catch { }

            try
            {
                var membership = shape.CellsU["LayerMembership"];
                membership.Add(layer, (short)0);
            }
            catch { }
        }

#pragma warning restore CS8602 // Dereference of a possibly null reference.
        private static void ApplyEdgeStyle(Edge edge, dynamic line)
        {
            if (edge.Style == null || edge.Style.IsDefault())
            {
                return;
            }

            var strokeSource = string.IsNullOrWhiteSpace(edge.Style.StrokeColor)
                ? edge.Style.FillColor
                : edge.Style.StrokeColor;

            if (TryParseColor(strokeSource, out var stroke))
            {
                TrySetFormula(line, "LineColor", $"RGB({stroke.R},{stroke.G},{stroke.B})");
            }

            ApplyLinePattern(line, edge.Style.LinePattern);
        }

        private static void ApplyLinePattern(dynamic shape, string? pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return;
            }

            if (double.TryParse(pattern, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
            {
                TrySetResult(shape.CellsU["LinePattern"], numeric);
            }
            else
            {
                TrySetFormula(shape, "LinePattern", pattern);
            }
        }

        private static bool TryParseColor(string? value, out (int R, int G, int B) color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var span = value!.Trim();

            if (span.StartsWith("#", StringComparison.Ordinal))
            {
                span = span.Substring(1);
            }

            if (span.Length != 6 && span.Length != 8)
            {
                return false;
            }

            var offset = span.Length - 6;

            if (!int.TryParse(span.Substring(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
                !int.TryParse(span.Substring(offset + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
                !int.TryParse(span.Substring(offset + 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                return false;
            }

            color = (r, g, b);
            return true;
        }

        private static dynamic CreateVisioApplication()
        {
            var progType = Type.GetTypeFromProgID("Visio.Application");
            if (progType == null)
            {
                throw new COMException("Visio is not installed or registered on this machine.");
            }

            var instance = Activator.CreateInstance(progType);
            if (instance == null)
            {
                throw new COMException("Failed to create Visio.Application instance.");
            }

            return instance;
        }

        private static void ComputePlacements(DiagramModel model, LayoutResult layout, LayoutPlan? layoutPlan, IDictionary<string, NodePlacement> placements, VisioPageManager.PageInfo pageInfo, LayerRenderContext layerContext)
        {
            if (pageInfo == null) throw new COMException("Visio page metadata was not initialised.");
            dynamic visioPage = pageInfo.Page ?? throw new COMException("Visio page was not created.");
            var nodeMap = BuildNodeMap(model);
            var outputMode = GetOutputMode(model);
            var isViewMode = string.Equals(outputMode, "view", StringComparison.OrdinalIgnoreCase);
            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);

            var (minLeft, minBottom, maxRight, maxTop) = ComputeLayoutBounds(layout);
            var margin = GetPageMargin(model) ?? Margin;
            var titleHeight = GetTitleHeight(model);
            var offsetX = margin - minLeft;
            var offsetY = margin + titleHeight - minBottom;
            var pageWidth = Math.Max(1.0, (maxRight - minLeft) + (margin * 2));
            var pageHeight = Math.Max(1.0, (maxTop - minBottom) + (margin * 2) + titleHeight);

            var pageWidthOverride = isViewMode ? (double?)null : GetPageWidth(model);
            var pageHeightOverride = isViewMode ? (double?)null : GetPageHeight(model);
            if (pageWidthOverride.HasValue) pageWidth = pageWidthOverride.Value;
            if (pageHeightOverride.HasValue) pageHeight = pageHeightOverride.Value;

            TrySetResult(visioPage.PageSheet.CellsU["PageWidth"], pageWidth);
            TrySetResult(visioPage.PageSheet.CellsU["PageHeight"], pageHeight);

            DrawTitleBanner(model, visioPage, pageWidth, titleHeight, margin, null, null);

            foreach (var nodeLayout in layout.Nodes)
            {
                if (!nodeMap.TryGetValue(nodeLayout.Id, out var node))
                {
                    continue;
                }

                var width = nodeLayout.Size.HasValue && nodeLayout.Size.Value.Width > 0
                    ? nodeLayout.Size.Value.Width
                    : (float)DefaultNodeWidth;

                var height = nodeLayout.Size.HasValue && nodeLayout.Size.Value.Height > 0
                    ? nodeLayout.Size.Value.Height
                    : (float)DefaultNodeHeight;

                var left = nodeLayout.Position.X + offsetX;
                var bottom = nodeLayout.Position.Y + offsetY;
                var right = left + width;
                var top = bottom + height;

                int? layerIndex = null;
                string? moduleId = null;
                if (layerContext.Enabled)
                {
                    moduleId = ResolveModuleIdForNode(node, tiers, tiersSet);
                    if (!layerContext.ShouldRenderModule(moduleId))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(moduleId) && layerContext.ModuleToLayer.TryGetValue(moduleId, out var assignedLayer))
                    {
                        layerIndex = assignedLayer;
                    }
                }

                dynamic shape = visioPage.DrawRectangle(left, bottom, right, top);
                shape.Text = node.Label;
                ApplyNodeStyle(node, shape);

                if (layerIndex.HasValue)
                {
                    AssignShapeToLayer(layerContext, pageInfo, shape, layerIndex.Value);
                }

                placements[nodeLayout.Id] = new NodePlacement(shape, left, bottom, width, height, layerIndex);
                pageInfo.IncrementNodeCount();
            }

            DrawLayerBridgeStubsSinglePage(layoutPlan, pageInfo, visioPage, offsetX, offsetY, layerContext);
        }

        private static bool IsFinite(double v) => !(double.IsNaN(v) || double.IsInfinity(v));

        private static void DrawLayerBridgeStubsSinglePage(
            LayoutPlan? layoutPlan,
            VisioPageManager.PageInfo pageInfo,
            dynamic visioPage,
            double offsetX,
            double offsetY,
            LayerRenderContext layerContext)
        {
            if (!layerContext.Enabled || !layerContext.HasLayerFilter) return;
            if (layoutPlan?.Bridges == null || layoutPlan.Bridges.Length == 0) return;

            var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var bridge in layoutPlan.Bridges)
            {
                if (bridge == null) continue;

                var sourceVisible = layerContext.IsLayerVisible(bridge.SourceLayer);
                var targetVisible = layerContext.IsLayerVisible(bridge.TargetLayer);

                if (!sourceVisible && !targetVisible)
                {
                    continue;
                }

                if (sourceVisible)
                {
                    DrawLayerBridgeStubShape(pageInfo, visioPage, bridge.ExitAnchor, offsetX, offsetY, bridge.SourceLayer, bridge.TargetLayer, true, layerContext, dedupe);
                }

                if (targetVisible)
                {
                    DrawLayerBridgeStubShape(pageInfo, visioPage, bridge.EntryAnchor, offsetX, offsetY, bridge.TargetLayer, bridge.SourceLayer, false, layerContext, dedupe);
                }
            }
        }

        private static void DrawLayerBridgeStubShape(
            VisioPageManager.PageInfo pageInfo,
            dynamic visioPage,
            PointF anchor,
            double offsetX,
            double offsetY,
            int owningLayer,
            int otherLayer,
            bool isSource,
            LayerRenderContext layerContext,
            HashSet<string> dedupe)
        {
            if (visioPage == null) return;

            var x = (double)anchor.X + offsetX;
            var y = (double)anchor.Y + offsetY;

            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
            {
                return;
            }

            var key = $"{owningLayer}:{Math.Round(x, 2):F2}:{Math.Round(y, 2):F2}:{(isSource ? "out" : "in")}";
            if (!dedupe.Add(key))
            {
                return;
            }

            var radius = 0.14;
            dynamic? stub = null;
            try
            {
                stub = visioPage.DrawOval(x - radius, y - radius, x + radius, y + radius);
                var arrow = isSource ? "→" : "←";
                stub.Text = $"{arrow} L{otherLayer + 1}";
                TrySetFormula(stub, "LinePattern", "2");
                TrySetFormula(stub, "LineWeight", "0.013");
                TrySetFormula(stub, "FillForegnd", "RGB(240,240,240)");
                TrySetFormula(stub, "Char.Size", "6 pt");
                try { stub.CellsU["LockTextEdit"].FormulaU = "1"; } catch { }
                AssignShapeToLayer(layerContext, pageInfo, stub, owningLayer);
                try { stub.SendToFront(); } catch { }
            }
            catch
            {
                // Ignore failures creating decorative stubs.
            }
            finally
            {
                if (stub != null)
                {
                    ReleaseCom(stub);
                }
            }
        }

        private static void DrawLayerBridgeStubsForPage(
            LayoutPlan? layoutPlan,
            LayoutResult layout,
            VisioPageManager.PageInfo pageInfo,
            dynamic visioPage,
            int pageIndex,
            double minLeft,
            double minBottom,
            double usableHeight,
            double margin,
            double title,
            IReadOnlyDictionary<string, int> nodePageAssignments,
            LayerRenderContext layerContext)
        {
            if (!layerContext.Enabled || !layerContext.HasLayerFilter) return;
            if (layoutPlan?.Bridges == null || layoutPlan.Bridges.Length == 0) return;
            if (visioPage == null) return;

            var offsetX = margin - minLeft;
            var bandOffset = IsFinite(usableHeight) ? pageIndex * usableHeight : 0.0;
            var offsetY = margin + title - minBottom - bandOffset;

            var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var layoutNodes = layout?.Nodes?.Where(n => n != null && !string.IsNullOrWhiteSpace(n.Id))
                                 ?.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase)
                             ?? new Dictionary<string, NodeLayout>(StringComparer.OrdinalIgnoreCase);

            bool IsBridgeOnPage(LayerBridge bridge, bool source)
            {
                var nodeId = source ? bridge.SourceNodeId : bridge.TargetNodeId;
                if (!string.IsNullOrWhiteSpace(nodeId) && nodePageAssignments != null && nodePageAssignments.TryGetValue(nodeId, out var mappedPage))
                {
                    return mappedPage == pageIndex;
                }

                var anchor = source ? bridge.ExitAnchor : bridge.EntryAnchor;
                if (IsFinite(usableHeight) && usableHeight > 0.0)
                {
                    var normalized = anchor.Y - (float)minBottom;
                    var idx = (int)Math.Floor(normalized / (float)usableHeight);
                    return idx == pageIndex;
                }

                if (!string.IsNullOrWhiteSpace(nodeId) && layoutNodes.TryGetValue(nodeId, out var nodeLayout))
                {
                    var normalized = nodeLayout.Position.Y - (float)minBottom;
                    var idx = usableHeight > 0.0 ? (int)Math.Floor(normalized / (float)usableHeight) : 0;
                    return idx == pageIndex;
                }

                return true;
            }

            foreach (var bridge in layoutPlan.Bridges)
            {
                if (bridge == null) continue;

                var sourceVisible = layerContext.IsLayerVisible(bridge.SourceLayer);
                var targetVisible = layerContext.IsLayerVisible(bridge.TargetLayer);
                if (!sourceVisible && !targetVisible) continue;

                if (sourceVisible && IsBridgeOnPage(bridge, true))
                {
                    DrawLayerBridgeStubShape(pageInfo, visioPage, bridge.ExitAnchor, offsetX, offsetY, bridge.SourceLayer, bridge.TargetLayer, true, layerContext, dedupe);
                }

                if (targetVisible && IsBridgeOnPage(bridge, false))
                {
                    DrawLayerBridgeStubShape(pageInfo, visioPage, bridge.EntryAnchor, offsetX, offsetY, bridge.TargetLayer, bridge.SourceLayer, false, layerContext, dedupe);
                }
            }
        }

        private static void DrawLaneContainers(DiagramModel model, LayoutResult layout, dynamic page)
        {
            dynamic visioPage = page ?? throw new COMException("Visio page was not created.");
            var nodeMap = BuildNodeMap(model);
            var tiers = GetOrderedTiers(model);

            var (minLeft, minBottom, maxRight, maxTop) = ComputeLayoutBounds(layout);
            var margin = GetPageMargin(model) ?? Margin;
            var titleHeight = GetTitleHeight(model);
            var offsetX = margin - minLeft;
            var offsetY = margin + titleHeight - minBottom;

            var padding = GetContainerPadding(model);
            var corner = GetContainerCorner(model);

            foreach (var tier in tiers)
            {
                double tMinL = double.MaxValue, tMinB = double.MaxValue, tMaxR = double.MinValue, tMaxT = double.MinValue;

                foreach (var nl in layout.Nodes)
                {
                    if (!nodeMap.TryGetValue(nl.Id, out var node)) continue;
                    var nodeTier = node.Tier;
                    if (string.IsNullOrWhiteSpace(nodeTier) && node.Metadata.TryGetValue("tier", out var tMeta))
                    {
                        nodeTier = tMeta;
                    }
                    var tierKey = string.IsNullOrWhiteSpace(nodeTier) ? tiers.First() : nodeTier!;
                    if (!tier.Equals(tierKey, StringComparison.OrdinalIgnoreCase)) continue;

                    var w = nl.Size.HasValue && nl.Size.Value.Width > 0 ? nl.Size.Value.Width : (float)DefaultNodeWidth;
                    var h = nl.Size.HasValue && nl.Size.Value.Height > 0 ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                    var l = nl.Position.X + offsetX;
                    var b = nl.Position.Y + offsetY;

                    tMinL = Math.Min(tMinL, l);
                    tMinB = Math.Min(tMinB, b);
                    tMaxR = Math.Max(tMaxR, l + w);
                    tMaxT = Math.Max(tMaxT, b + h);
                }

                if (double.IsInfinity(tMinL) || tMinL == double.MaxValue)
                {
                    continue; // no nodes in this tier
                }

                var left = tMinL - padding;
                var bottom = tMinB - padding;
                var right = tMaxR + padding;
                var top = tMaxT + padding;

                try
                {
                    dynamic lane = visioPage.DrawRectangle(left, bottom, right, top);
                    lane.Text = tier;
                    ApplyContainerStyle(model, lane);
                    try { TrySetResult(lane.CellsU["Rounding"], corner); } catch { }
                    try { lane.SendToBack(); } catch { }
                    ReleaseCom(lane);
                }
                catch { }
            }

            // Draw explicit sub-containers (single-page)
            DrawExplicitContainers(model, layout, visioPage, minLeft, minBottom, double.PositiveInfinity, margin, titleHeight, 0);
        }

        private static void DrawConnectors(DiagramModel model, IDictionary<string, NodePlacement> placements, VisioPageManager.PageInfo pageInfo, VisioPageManager? pageManager, LayoutResult? layout = null, LayerRenderContext? layerContext = null)
        {
            if (pageInfo == null) throw new COMException("Visio page metadata was not initialised.");
            dynamic visioPage = pageInfo.Page ?? throw new COMException("Visio page was not created.");
            var pageIndex = pageInfo.Index;
            var routeMode = (model.Metadata.TryGetValue("layout.routing.mode", out var rm) && !string.IsNullOrWhiteSpace(rm)) ? rm.Trim() : "orthogonal";
            var useOrthogonal = !string.Equals(routeMode, "straight", StringComparison.OrdinalIgnoreCase);
            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);
            var nodeMap = BuildNodeMap(model);
            layerContext ??= LayerRenderContext.Disabled;
            var bundleByRaw = model.Metadata.TryGetValue("layout.routing.bundleBy", out var bb) ? (bb ?? "none").Trim() : "none";
            double bundleSepIn = 0.12;
            if (model.Metadata.TryGetValue("layout.routing.bundleSeparationIn", out var bsep) && double.TryParse(bsep, NumberStyles.Float, CultureInfo.InvariantCulture, out var bsv) && bsv >= 0) bundleSepIn = bsv;
            var channelGapIn = (model.Metadata.TryGetValue("layout.routing.channels.gapIn", out var cgap) && double.TryParse(cgap, NumberStyles.Float, CultureInfo.InvariantCulture, out var cg)) ? cg : 0.0;
            var effectiveBundleBy = (string.Equals(bundleByRaw, "none", StringComparison.OrdinalIgnoreCase) && channelGapIn > 0.0) ? "lane" : bundleByRaw;
            var bundles = BuildBundleIndex(model, effectiveBundleBy);
            string GetTier(VDG.Core.Models.Node n)
            {
                var t = !string.IsNullOrWhiteSpace(n.Tier) ? n.Tier! : (n.Metadata.TryGetValue("tier", out var tv) ? tv : tiers.First());
                return tiersSet.Contains(t) ? t : tiers.First();
            }
            (double xr, double yr) SideToRel(string side) => side.ToLowerInvariant() switch
            {
                "left" => (0.0, 0.5),
                "right" => (1.0, 0.5),
                "top" => (0.5, 1.0),
                "bottom" => (0.5, 0.0),
                _ => (0.5, 0.5)
            };
            var layoutEdgeLookup = new Dictionary<string, EdgeRoute>(StringComparer.OrdinalIgnoreCase);
            if (layout?.Edges != null)
            {
                foreach (var route in layout.Edges)
                {
                    if (route != null && !string.IsNullOrWhiteSpace(route.Id))
                    {
                        layoutEdgeLookup[route.Id] = route;
                    }
                }
            }

            foreach (var edge in model.Edges)
            {
                if (!placements.TryGetValue(edge.SourceId, out var source) || source == null ||
                    !placements.TryGetValue(edge.TargetId, out var target) || target == null)
                {
                    pageManager?.RegisterConnectorSkipped(edge.Id, edge.SourceId, edge.TargetId, pageIndex, "node placement missing on page");
                    continue;
                }

                var connectorDrawn = false;
                var sourcePlacement = source!;
                var targetPlacement = target!;

                if (layoutEdgeLookup.TryGetValue(edge.Id, out var predefinedRoute) &&
                    predefinedRoute.Points != null &&
                    predefinedRoute.Points.Length >= 2)
                {
                    try
                    {
                        var pts = new List<double>();
                        foreach (var pt in predefinedRoute.Points)
                        {
                            pts.Add(pt.X);
                            pts.Add(pt.Y);
                        }
                        dynamic line = visioPage.DrawPolyline(pts.ToArray(), 0);
                        if (!string.IsNullOrWhiteSpace(edge.Label)) line.Text = edge.Label;
                        if (edge.Directed) TrySetFormula(line, "EndArrow", "5"); else TrySetFormula(line, "EndArrow", "0");
                        AssignConnectorToLayers(layerContext, pageInfo, line, sourcePlacement, targetPlacement);
                        ApplyEdgeStyle(edge, line); try { line.SendToBack(); } catch { }
                        ReleaseCom(line);
                        connectorDrawn = true;
                    }
                    catch
                    {
                        connectorDrawn = false;
                    }
                }
                else if (useOrthogonal)
                {
                    dynamic? connector = null;
                    try
                    {
                        var app = visioPage.Application;
                        connector = visioPage.Drop(app.ConnectorToolDataObject, sourcePlacement.CenterX, sourcePlacement.CenterY);
                        // Corridor-aware: choose side based on relative tier positions
                        string srcSide = "right";
                        string dstSide = "left";
                        if (nodeMap.TryGetValue(edge.SourceId, out var srcNode) && nodeMap.TryGetValue(edge.TargetId, out var dstNode))
                        {
                            var sTier = Array.IndexOf(tiers, GetTier(srcNode));
                            var tTier = Array.IndexOf(tiers, GetTier(dstNode));
                            if (sTier == tTier)
                            {
                                // Same lane: choose by X ordering
                                if (sourcePlacement.CenterX <= targetPlacement.CenterX) { srcSide = "right"; dstSide = "left"; }
                                else { srcSide = "left"; dstSide = "right"; }
                            }
                            else if (sTier < tTier) { srcSide = "right"; dstSide = "left"; }
                            else { srcSide = "left"; dstSide = "right"; }
                        }

                        var (sxr, syr) = SideToRel(srcSide);
                        var (dxr, dyr) = SideToRel(dstSide);
                        if (bundles.TryGetValue(edge.Id, out var binfo))
                        {
                            (sxr, syr) = ApplyBundleOffsetRel(srcSide, (sxr, syr), sourcePlacement, binfo, bundleSepIn);
                            (dxr, dyr) = ApplyBundleOffsetRel(dstSide, (dxr, dyr), targetPlacement, binfo, bundleSepIn);
                        }
                        bool usedPolyline = false;
                        // Resolve channels gap once to avoid scope issues and unassigned locals
                        double channelsGapValue = 0.0; string? channelsGapStr;
                        var hasKey = model.Metadata.TryGetValue("layout.routing.channels.gapIn", out channelsGapStr);
                        if (hasKey && !string.IsNullOrWhiteSpace(channelsGapStr)) double.TryParse(channelsGapStr, NumberStyles.Float, CultureInfo.InvariantCulture, out channelsGapValue);
                        var hasChannels = channelsGapValue > 0.0;
                        var cx = hasChannels ? CorridorXForEdge(model, placements, edge.SourceId, edge.TargetId) : null;

                        // Waypoints override: draw polyline honoring explicit waypoints
                        if (!usedPolyline && TryGetWaypointsFromMetadata(edge, out var wps))
                        {
                            var (sx, sy) = ToAbsAttach(sourcePlacement, srcSide, sxr, syr);
                            var (tx, ty) = ToAbsAttach(targetPlacement, dstSide, dxr, dyr);
                            var pts = new List<double>(2 + (wps.Count * 2) + 2) { sx, sy };
                            foreach (var p in wps) { pts.Add(p.x); pts.Add(p.y); }
                            pts.Add(tx); pts.Add(ty);
                            try
                            {
                                dynamic pl = visioPage.DrawPolyline(pts.ToArray(), 0);
                                if (!string.IsNullOrWhiteSpace(edge.Label))
                                {
                                    // label box near midpoint of longest segment
                                    var arr = pts.ToArray();
                                    double maxLen = -1; double mx = sx, my = sy; int segs = (arr.Length / 2) - 1;
                                    for (int i = 0; i < segs; i++)
                                    {
                                        var x1 = arr[i * 2]; var y1 = arr[i * 2 + 1]; var x2 = arr[(i + 1) * 2]; var y2 = arr[(i + 1) * 2 + 1];
                                        var len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                                        if (len > maxLen) { maxLen = len; mx = (x1 + x2) / 2.0; my = (y1 + y2) / 2.0; }
                                    }
                                    var off = 0.15; if (edge.Metadata != null && edge.Metadata.TryGetValue("edge.label.offsetIn", out var offRaw)) { double.TryParse(offRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out off); }
                                    DrawLabelBox(visioPage, edge.Label!, mx, my, 0.0, off);
                                }
                                if (edge.Directed) TrySetFormula(pl, "EndArrow", "5"); else TrySetFormula(pl, "EndArrow", "0");
                                AssignConnectorToLayers(layerContext, pageInfo, pl, sourcePlacement, targetPlacement);
                                ApplyEdgeStyle(edge, pl);
                                try { pl.SendToBack(); } catch { }
                                ReleaseCom(pl);
                                usedPolyline = true;
                                connectorDrawn = true;
                            }
                            catch { usedPolyline = false; }
                        }
                        if (!usedPolyline && cx.HasValue)
                        {
                            var (sx, sy) = ToAbsAttach(sourcePlacement, srcSide, sxr, syr);
                            var (tx, ty) = ToAbsAttach(targetPlacement, dstSide, dxr, dyr);
                            // Stagger vertical corridor by lane bundle index to reduce overlaps
                            var corridorX = cx.Value;
                            var laneBundles = BuildBundleIndex(model, "lane");
                            if (laneBundles.TryGetValue(edge.Id, out var cinfo) && channelsGapValue > 0)
                            {
                                var center = (cinfo.size - 1) / 2.0;
                                var delta = (cinfo.index - center) * Math.Min(channelsGapValue * 0.4, 0.4);
                                corridorX += delta;
                            }
                            // Container-aware routing: add skirt points near lane bounds
                            var routeAround = model.Metadata.TryGetValue("layout.routing.routeAroundContainers", out var rar) && bool.TryParse(rar, out var rarb) && rarb;
                            var skirt = GetContainerPadding(model) * 0.5;
                            var pts = new List<double>(12);
                            pts.Add(sx); pts.Add(sy);
                            // Helper: bounds for a tier computed from current placements (absolute coords)
                            (double left, double right) BoundsForTierFromPlacements(string tier)
                            {
                                double tMinL = double.MaxValue, tMaxR = double.MinValue;
                                foreach (var kv in placements)
                                {
                                    if (!nodeMap.TryGetValue(kv.Key, out var nd)) continue;
                                    var nodeTier = !string.IsNullOrWhiteSpace(nd.Tier) ? nd.Tier! : (nd.Metadata.TryGetValue("tier", out var tMeta) ? tMeta : tiers.First());
                                    var tierKey = tiersSet.Contains(nodeTier) ? nodeTier : tiers.First();
                                    if (!string.Equals(tierKey, tier, StringComparison.OrdinalIgnoreCase)) continue;
                                    var l = kv.Value.Left;
                                    var r = kv.Value.Left + kv.Value.Width;
                                    tMinL = Math.Min(tMinL, l); tMaxR = Math.Max(tMaxR, r);
                                }
                                if (double.IsInfinity(tMinL) || tMinL == double.MaxValue) return (double.NegativeInfinity, double.PositiveInfinity);
                                var pad = GetContainerPadding(model);
                                return (tMinL - pad, tMaxR + pad);
                            }
                            if (routeAround)
                            {
                                var srcTier = nodeMap.TryGetValue(edge.SourceId, out var sn) ? GetTier(sn) : tiers.First();
                                // Skirt points against source lane edge depending on side
                                var (sL, sR) = BoundsForTierFromPlacements(srcTier);
                                if (srcSide.Equals("right", StringComparison.OrdinalIgnoreCase))
                                {
                                    var x = Math.Min(corridorX, sR + skirt);
                                    if (x > sx) { pts.Add(x); pts.Add(sy); }
                                }
                                else if (srcSide.Equals("left", StringComparison.OrdinalIgnoreCase))
                                {
                                    var x = Math.Max(corridorX, sL - skirt);
                                    if (x < sx) { pts.Add(x); pts.Add(sy); }
                                }
                            }
                            // Corridor vertical
                            pts.Add(corridorX); pts.Add(sy);
                            pts.Add(corridorX); pts.Add(ty);
                            if (routeAround)
                            {
                                // Skirt near destination lane before final attach
                                var dstTierLocal = nodeMap.TryGetValue(edge.TargetId, out var tn2) ? GetTier(tn2) : tiers.Last();
                                var (dLx, dRx) = BoundsForTierFromPlacements(dstTierLocal);
                                if (dstSide.Equals("left", StringComparison.OrdinalIgnoreCase))
                                {
                                    var x = Math.Max(corridorX, dLx - skirt);
                                    if (x < tx) { pts.Add(x); pts.Add(ty); }
                                }
                                else if (dstSide.Equals("right", StringComparison.OrdinalIgnoreCase))
                                {
                                    var x = Math.Min(corridorX, dRx + skirt);
                                    if (x > tx) { pts.Add(x); pts.Add(ty); }
                                }
                            }
                            // Final attach
                            pts.Add(tx); pts.Add(ty);
                            try
                            {
                                dynamic pl = visioPage.DrawPolyline(pts.ToArray(), 0);
                                if (!string.IsNullOrWhiteSpace(edge.Label))
                                {
                                    // Place detached label near middle segment
                                    var arr = pts.ToArray();
                                    // Use midpoint of the longest segment
                                    double maxLen = -1; double mx = sx, my = sy; int segs = (arr.Length / 2) - 1;
                                    for (int i = 0; i < segs; i++)
                                    {
                                        var x1 = arr[i * 2]; var y1 = arr[i * 2 + 1]; var x2 = arr[(i + 1) * 2]; var y2 = arr[(i + 1) * 2 + 1];
                                        var len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                                        if (len > maxLen) { maxLen = len; mx = (x1 + x2) / 2.0; my = (y1 + y2) / 2.0; }
                                    }
                                    var off = 0.15; if (edge.Metadata != null && edge.Metadata.TryGetValue("edge.label.offsetIn", out var offRaw)) { double.TryParse(offRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out off); }
                                    DrawLabelBox(visioPage, edge.Label!, mx, my, 0.0, off);
                                }
                                if (edge.Directed) TrySetFormula(pl, "EndArrow", "5"); else TrySetFormula(pl, "EndArrow", "0");
                                ApplyEdgeStyle(edge, pl);
                                try { pl.SendToBack(); } catch { }
                                ReleaseCom(pl);
                                usedPolyline = true;
                                connectorDrawn = true;
                            }
                            catch { usedPolyline = false; }
                        }
                        if (!usedPolyline)
                        {
                            connector = visioPage.Drop(app.ConnectorToolDataObject, sourcePlacement.CenterX, sourcePlacement.CenterY);
                            // Prefer GlueToPos if available; fall back to PinX
                            try { connector.CellsU["BeginX"].GlueToPos(((dynamic)sourcePlacement.Shape), sxr, syr); }
                            catch { connector.CellsU["BeginX"].GlueTo(((dynamic)sourcePlacement.Shape).CellsU["PinX"]); }
                            try { connector.CellsU["EndX"].GlueToPos(((dynamic)targetPlacement.Shape), dxr, dyr); }
                            catch { connector.CellsU["EndX"].GlueTo(((dynamic)targetPlacement.Shape).CellsU["PinX"]); }

                            if (!string.IsNullOrWhiteSpace(edge.Label))
                            {
                                connector.Text = edge.Label;
                            }

                            if (edge.Directed) TrySetFormula(connector, "EndArrow", "5"); else TrySetFormula(connector, "EndArrow", "0");
                            AssignConnectorToLayers(layerContext, pageInfo, connector, sourcePlacement, targetPlacement);
                            // Prefer right-angle routing
                            TrySetFormula(connector, "Routestyle", "16");
                            try { TrySetFormula(connector, "LineRouteExt", "2"); } catch { }
                            ApplyEdgeStyle(edge, connector);
                            try { connector.SendToBack(); } catch { }
                            connectorDrawn = true;
                        }
                    }
                    finally
                    {
                        if (connector != null) ReleaseCom(connector);
                    }
                }
                else
                {
                    dynamic line = visioPage.DrawLine(sourcePlacement.CenterX, sourcePlacement.CenterY, targetPlacement.CenterX, targetPlacement.CenterY);
                    if (!string.IsNullOrWhiteSpace(edge.Label)) line.Text = edge.Label;
                    if (edge.Directed) TrySetFormula(line, "EndArrow", "5"); else TrySetFormula(line, "EndArrow", "0");
                    AssignConnectorToLayers(layerContext, pageInfo, line, sourcePlacement, targetPlacement);
                    ApplyEdgeStyle(edge, line);
                    ReleaseCom(line);
                    connectorDrawn = true;
                }

                if (connectorDrawn)
                {
                    pageInfo.IncrementConnectorCount();
                }
                else
                {
                    pageManager?.RegisterConnectorSkipped(edge.Id, edge.SourceId, edge.TargetId, pageIndex, "connector routing failed");
                }
            }
        }

        private static void DeleteErrorLog(string outputPath)
        {
            var logPath = GetErrorLogPath(outputPath);
            if (File.Exists(logPath))
            {
                try { File.Delete(logPath); } catch { /* ignore */ }
            }
        }

        private static void WriteErrorLog(string? outputPath, Exception ex)
        {
            var logPath = outputPath != null
                ? GetErrorLogPath(outputPath)
                : Path.Combine(Path.GetTempPath(), "vdg.runner.error.log");

            try
            {
                var builder = new StringBuilder();
                builder.AppendLine(DateTimeOffset.UtcNow.ToString("O"));
                builder.AppendLine(ex.GetType().FullName);
                builder.AppendLine(ex.Message);
                builder.AppendLine(ex.StackTrace ?? string.Empty);

                var directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(logPath, builder.ToString());
            }
            catch
            {
                // Swallow logging failures.
            }
        }

        private static string GetErrorLogPath(string outputPath)
        {
            var directory = Path.GetDirectoryName(outputPath);
            var fileName = Path.GetFileNameWithoutExtension(outputPath);
            var logName = string.IsNullOrEmpty(fileName) ? "vdg-runner" : fileName;
            return Path.Combine(directory ?? string.Empty, logName + ".error.log");
        }

        private static void TrySetFormula(dynamic shape, string cellName, string formula)
        {
            try
            {
                shape.CellsU[cellName].FormulaU = formula;
            }
            catch (RuntimeBinderException)
            {
            }
            catch (COMException)
            {
            }
        }

        private static void TrySetResult(dynamic cell, double value)
        {
            try
            {
                cell.ResultIU = value;
            }
            catch (RuntimeBinderException)
            {
            }
            catch (COMException)
            {
            }
        }

        private static void ReleaseCom(object? instance)
        {
            if (instance != null && Marshal.IsComObject(instance))
            {
                try { Marshal.FinalReleaseComObject(instance); } catch { /* ignore */ }
            }
        }

        private sealed class NodePlacement
        {
            public NodePlacement(object shape, double left, double bottom, double width, double height, int? layerIndex = null)
            {
                Shape = shape;
                Left = left;
                Bottom = bottom;
                Width = width;
                Height = height;
                LayerIndex = layerIndex;
            }

            public object Shape { get; }
            public double Left { get; }
            public double Bottom { get; }
            public double Width { get; }
            public double Height { get; }
            public int? LayerIndex { get; }
            public double CenterX => Left + (Width / 2.0);
            public double CenterY => Bottom + (Height / 2.0);
        }

        private sealed class DiagramEnvelope
        {
            [JsonPropertyName("diagramType")]
            public string? DiagramType { get; set; }

            [JsonPropertyName("schemaVersion")]
            public string? SchemaVersion { get; set; }

            [JsonPropertyName("metadata")]
            public DiagramMetadataDto? Metadata { get; set; }

            [JsonPropertyName("layout")]
            public LayoutDto? Layout { get; set; }

            [JsonPropertyName("nodes")]
            public List<NodeDto>? Nodes { get; set; }

            [JsonPropertyName("edges")]
            public List<EdgeDto>? Edges { get; set; }

            // M4: explicit containers (optional)
            [JsonPropertyName("containers")]
            public List<ContainerDto>? Containers { get; set; }
        }

        private sealed class DiagramMetadataDto
        {
            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("version")]
            public string? Version { get; set; }

            [JsonPropertyName("author")]
            public string? Author { get; set; }

            [JsonPropertyName("createdUtc")]
            public string? CreatedUtc { get; set; }

            [JsonPropertyName("tags")]
            public List<string>? Tags { get; set; }

            [JsonPropertyName("properties")]
            public Dictionary<string, string>? Properties { get; set; }
        }

        private static double? GetPageWidth(DiagramModel model)
        {
            if (model.Metadata.TryGetValue("layout.page.widthIn", out var w) &&
                double.TryParse(w, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }
            return null;
        }

        private static string GetOutputMode(DiagramModel model)
        {
            if (model.Metadata.TryGetValue("layout.outputMode", out var mode) && !string.IsNullOrWhiteSpace(mode))
            {
                var normalized = mode.Trim();
                if (string.Equals(normalized, "view", StringComparison.OrdinalIgnoreCase)) return "view";
                if (string.Equals(normalized, "print", StringComparison.OrdinalIgnoreCase)) return "print";
            }
            return "print";
        }

        private static double? GetPageHeight(DiagramModel model)
        {
            if (model.Metadata.TryGetValue("layout.page.heightIn", out var h) &&
                double.TryParse(h, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }
            return null;
        }

        private static double? GetPageMargin(DiagramModel model)
        {
            if (model.Metadata.TryGetValue("layout.page.marginIn", out var m) &&
                double.TryParse(m, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                return v;
            }
            return null;
        }

        private static double GetTitleHeight(DiagramModel model)
        {
            return model.Metadata.TryGetValue("title", out var title) && !string.IsNullOrWhiteSpace(title)
                ? 0.6
                : 0.0;
        }

        private static void StampDocumentMetadata(DiagramModel model, dynamic document)
        {
            try
            {
                if (model.Metadata.TryGetValue("title", out var title) && !string.IsNullOrWhiteSpace(title))
                {
                    try { document.Title = title; } catch { }
                }

                var ver = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
                try
                {
                    dynamic sheet = document.DocumentSheet;
                    TrySetFormula(sheet, "User.SchemaVersion", $"\"{CurrentSchemaVersion}\"");
                    TrySetFormula(sheet, "User.Generator", "\"VDG.CLI\"");
                    TrySetFormula(sheet, "User.GeneratorVersion", $"\"{ver}\"");

                    // M4: export explicit container semantics to document properties (User.* cells)
                    // Persist count and simple CSVs for ids, labels, and tiers to support downstream automation.
                    static string Csv(params string[] values) => string.Join(";", values);
                    static string Q(string s) => "\"" + (s ?? string.Empty).Replace("\"", "\"\"") + "\"";
                    try
                    {
                        if (TryGetExplicitContainers(model, out var list) && list.Count > 0)
                        {
                            var ids = list.Select(c => c?.Id ?? string.Empty).ToArray();
                            var labels = list.Select(c => string.IsNullOrWhiteSpace(c?.Label) ? (c?.Id ?? string.Empty) : c!.Label!).ToArray();
                            var tiers = list.Select(c => c?.Tier ?? string.Empty).ToArray();

                            TrySetFormula(sheet, "User.ContainerCount", list.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                            TrySetFormula(sheet, "User.ContainerIds", Q(Csv(ids)));
                            TrySetFormula(sheet, "User.ContainerLabels", Q(Csv(labels)));
                            TrySetFormula(sheet, "User.ContainerTiers", Q(Csv(tiers)));
                        }
                        else if (model.Metadata.TryGetValue("layout.tiers", out var tiersCsv) && !string.IsNullOrWhiteSpace(tiersCsv))
                        {
                            // Fallback: persist tier names as containers if no explicit containers were provided
                            var tiers = tiersCsv.Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                                .Select(s => s.Trim())
                                                .Where(s => s.Length > 0)
                                                .ToArray();
                            if (tiers.Length > 0)
                            {
                                TrySetFormula(sheet, "User.ContainerCount", tiers.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
                                TrySetFormula(sheet, "User.ContainerLabels", Q(string.Join(";", tiers)));
                            }
                        }
                    }
                    catch { }
                }
                catch { }
            }
            catch { }
        }

        
        private static void DrawTitleBanner(DiagramModel model, dynamic visioPage, double pageWidth, double titleHeight, double margin, int? pageNumber, int? pageCount)
        {
            if (titleHeight <= 0) return;
            try
            {
                // page height in internal units
                double ph;
                try { ph = (double)visioPage.PageSheet.CellsU["PageHeight"].ResultIU; } catch { ph = 0; }
                if (ph <= 0) ph = GetPageHeight(model) ?? 0;

                var left = margin;
                var bottom = ph - margin - titleHeight;
                if (bottom < 0) bottom = 0;
                var right = pageWidth - margin;
                var top = bottom + titleHeight;
                dynamic banner = visioPage.DrawRectangle(left, bottom, right, top);
                var title = model.Metadata.TryGetValue("title", out var t) ? t : string.Empty;
                var suffixParts = new List<string>();
                if (model.Metadata.TryGetValue("version", out var v) && !string.IsNullOrWhiteSpace(v)) suffixParts.Add($"v{v}");
                if (model.Metadata.TryGetValue("author", out var a) && !string.IsNullOrWhiteSpace(a)) suffixParts.Add(a);
                var pagePart = (pageNumber.HasValue && pageCount.HasValue) ? $" — Page {pageNumber} of {pageCount}" : string.Empty;
                banner.Text = title + (suffixParts.Count > 0 ? " (" + string.Join(", ", suffixParts) + ")" : string.Empty) + pagePart;
                TrySetFormula(banner, "FillForegnd", "RGB(240,244,248)");
                TrySetFormula(banner, "LineColor", "RGB(200,200,200)");
                ReleaseCom(banner);
            }
            catch { }
        }

        private static double GetContainerPadding(DiagramModel model)
        {
            if (model.Metadata.TryGetValue("layout.containers.paddingIn", out var p) && double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0)
            {
                return v;
            }
            return 0.3;
        }

        private static double GetContainerCorner(DiagramModel model)
        {
            if (model.Metadata.TryGetValue("layout.containers.cornerIn", out var p) && double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0)
            {
                return v;
            }
            return 0.12;
        }

        private static void ApplyContainerStyle(DiagramModel model, dynamic shape)
        {
            var applied = false;
            if (model.Metadata.TryGetValue("layout.containers.style.fill", out var fillHex) && TryParseColor(fillHex, out var fill))
            {
                TrySetFormula(shape, "FillForegnd", $"RGB({fill.R},{fill.G},{fill.B})"); applied = true;
            }
            if (model.Metadata.TryGetValue("layout.containers.style.stroke", out var strokeHex) && TryParseColor(strokeHex, out var stroke))
            {
                TrySetFormula(shape, "LineColor", $"RGB({stroke.R},{stroke.G},{stroke.B})"); applied = true;
            }
            if (model.Metadata.TryGetValue("layout.containers.style.linePattern", out var pattern) && !string.IsNullOrWhiteSpace(pattern))
            {
                ApplyLinePattern(shape, pattern); applied = true;
            }
            if (!applied)
            {
                // Fallback default style
                TrySetFormula(shape, "FillForegnd", "RGB(245,248,250)");
                TrySetFormula(shape, "LinePattern", "2");
                TrySetFormula(shape, "LineColor", "RGB(200,200,200)");
            }
        }

        private sealed class LayoutDto
        {
            [JsonPropertyName("outputMode")]
            public string? OutputMode { get; set; }

            [JsonPropertyName("orientation")]
            public string? Orientation { get; set; }

            [JsonPropertyName("tiers")]
            public List<string>? Tiers { get; set; }

            [JsonPropertyName("diagnostics")]
            public DiagnosticsDto? Diagnostics { get; set; }

            [JsonPropertyName("spacing")]
            public SpacingDto? Spacing { get; set; }

            [JsonPropertyName("page")]
            public PageDto? Page { get; set; }

            [JsonPropertyName("routing")]
            public RoutingDto? Routing { get; set; }

            [JsonPropertyName("containers")]
            public ContainersLayoutDto? Containers { get; set; }
        }

        private sealed class DiagnosticsDto
        {
            [JsonPropertyName("enabled")]
            public bool? Enabled { get; set; }

            [JsonPropertyName("pageHeightThresholdIn")]
            public double? PageHeightThresholdIn { get; set; }

            [JsonPropertyName("laneMaxNodes")]
            public int? LaneMaxNodes { get; set; }

            // M5 additions
            [JsonPropertyName("level")]
            public string? Level { get; set; }

            [JsonPropertyName("laneCrowdWarnRatio")]
            public double? LaneCrowdWarnRatio { get; set; }

            [JsonPropertyName("laneCrowdErrorRatio")]
            public double? LaneCrowdErrorRatio { get; set; }

            [JsonPropertyName("pageCrowdWarnRatio")]
            public double? PageCrowdWarnRatio { get; set; }
        }

        private sealed class SpacingDto
        {
            [JsonPropertyName("horizontal")]
            public double? Horizontal { get; set; }

            [JsonPropertyName("vertical")]
            public double? Vertical { get; set; }
        }

        private sealed class PageDto
        {
            [JsonPropertyName("widthIn")]
            public double? WidthIn { get; set; }

            [JsonPropertyName("heightIn")]
            public double? HeightIn { get; set; }

            [JsonPropertyName("marginIn")]
            public double? MarginIn { get; set; }

            [JsonPropertyName("paginate")]
            public bool? Paginate { get; set; }

            [JsonPropertyName("plan")]
            public PagePlanDto? Plan { get; set; }
        }

        private sealed class PagePlanDto
        {
            [JsonPropertyName("laneSplitAllowed")]
            public bool? LaneSplitAllowed { get; set; }
        }

        // M3 routing DTOs
        private sealed class RoutingDto
        {
            [JsonPropertyName("mode")]
            public string? Mode { get; set; } // orthogonal|straight

            [JsonPropertyName("bundleBy")]
            public string? BundleBy { get; set; } // lane|group|nodePair|none

            [JsonPropertyName("bundleSeparationIn")]
            public double? BundleSeparationIn { get; set; }

            [JsonPropertyName("channels")]
            public ChannelsDto? Channels { get; set; }

            [JsonPropertyName("routeAroundContainers")]
            public bool? RouteAroundContainers { get; set; }
        }

        private sealed class ChannelsDto
        {
            [JsonPropertyName("gapIn")]
            public double? GapIn { get; set; }
        }

        private static bool TryGetExplicitContainers(DiagramModel model, out List<ContainerDto> containers)
        {
            containers = new List<ContainerDto>();
            if (!model.Metadata.TryGetValue("layout.containers.json", out var json) || string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var list = JsonSerializer.Deserialize<List<ContainerDto>>(json, JsonOptions);
                if (list != null) { containers = list; return containers.Count > 0; }
            }
            catch { }
            return false;
        }

        private static void DrawExplicitContainers(DiagramModel model, LayoutResult layout, dynamic page, double minLeft, double minBottom, double usableHeight, double margin, double title, int pageIndex)
        {
            if (!TryGetExplicitContainers(model, out var containers) || containers.Count == 0) return;
            var tiers = GetOrderedTiers(model);
            var nodeMap = BuildNodeMap(model);
            var padding = GetContainerPadding(model);
            var corner = GetContainerCorner(model);

            foreach (var c in containers)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.Id)) continue;
                var tier = !string.IsNullOrWhiteSpace(c.Tier) ? c.Tier! : tiers.First();
                if (!tiers.Contains(tier)) tier = tiers.First();

                // Compute lane bounds for this page and tier
                double tMinL = double.MaxValue, tMinB = double.MaxValue, tMaxR = double.MinValue, tMaxT = double.MinValue;
                foreach (var nl in layout.Nodes)
                {
                    var yNorm = nl.Position.Y - (float)minBottom;
                    var idx = (usableHeight > 0 && IsFinite(usableHeight)) ? (int)Math.Floor(yNorm / (float)usableHeight) : 0;
                    if (idx != pageIndex) continue;
                    if (!nodeMap.TryGetValue(nl.Id, out var node)) continue;
                    var nodeTier = node.Tier;
                    if (string.IsNullOrWhiteSpace(nodeTier) && node.Metadata.TryGetValue("tier", out var tMeta)) nodeTier = tMeta;
                    var tierKey = string.IsNullOrWhiteSpace(nodeTier) ? tiers.First() : nodeTier!;
                    if (!tier.Equals(tierKey, StringComparison.OrdinalIgnoreCase)) continue;

                    var w = nl.Size.HasValue && nl.Size.Value.Width > 0 ? nl.Size.Value.Width : (float)DefaultNodeWidth;
                    var h = nl.Size.HasValue && nl.Size.Value.Height > 0 ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                    var offsetX = margin - minLeft;
                    var bandOffset = (pageIndex * usableHeight);
                    var offsetY = margin + title - minBottom - (IsFinite(usableHeight) ? bandOffset : 0);
                    var l = nl.Position.X + offsetX;
                    var b = nl.Position.Y + offsetY;
                    tMinL = Math.Min(tMinL, l); tMinB = Math.Min(tMinB, b);
                    tMaxR = Math.Max(tMaxR, l + w); tMaxT = Math.Max(tMaxT, b + h);
                }
                if (double.IsInfinity(tMinL) || tMinL == double.MaxValue) continue; // no nodes in lane on this page

                // Determine container member bounds
                double cMinL = double.MaxValue, cMinB = double.MaxValue, cMaxR = double.MinValue, cMaxT = double.MinValue;
                bool hasExplicit = (c.Bounds != null && c.Bounds.Width.HasValue && c.Bounds.Height.HasValue);
                if (hasExplicit)
                {
                    var offsetX = margin - minLeft;
                    var bandOffset = (pageIndex * usableHeight);
                    var offsetY = margin + title - minBottom - (IsFinite(usableHeight) ? bandOffset : 0);
                    var l = (c.Bounds!.X ?? 0.0) + offsetX;
                    var b = (c.Bounds!.Y ?? 0.0) + offsetY;
                    var w = c.Bounds!.Width!.Value;
                    var h = c.Bounds!.Height!.Value;
                    cMinL = l; cMinB = b; cMaxR = l + w; cMaxT = b + h;
                }
                else
                {
                    foreach (var nl in layout.Nodes)
                    {
                        var yNorm = nl.Position.Y - (float)minBottom;
                        var idx = (usableHeight > 0 && IsFinite(usableHeight)) ? (int)Math.Floor(yNorm / (float)usableHeight) : 0;
                        if (idx != pageIndex) continue;
                        if (!nodeMap.TryGetValue(nl.Id, out var node)) continue;
                        var nodeTier = node.Tier;
                        if (string.IsNullOrWhiteSpace(nodeTier) && node.Metadata.TryGetValue("tier", out var tMeta)) nodeTier = tMeta;
                        var tierKey = string.IsNullOrWhiteSpace(nodeTier) ? tiers.First() : nodeTier!;
                        if (!tier.Equals(tierKey, StringComparison.OrdinalIgnoreCase)) continue;
                        if (node.Metadata == null || !node.Metadata.TryGetValue("node.containerId", out var cid) || !string.Equals(cid, c.Id, StringComparison.OrdinalIgnoreCase)) continue;

                        var w = nl.Size.HasValue && nl.Size.Value.Width > 0 ? nl.Size.Value.Width : (float)DefaultNodeWidth;
                        var h = nl.Size.HasValue && nl.Size.Value.Height > 0 ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                        var offsetX = margin - minLeft;
                        var bandOffset = (pageIndex * usableHeight);
                        var offsetY = margin + title - minBottom - (IsFinite(usableHeight) ? bandOffset : 0);
                        var l = nl.Position.X + offsetX;
                        var b = nl.Position.Y + offsetY;
                        cMinL = Math.Min(cMinL, l); cMinB = Math.Min(cMinB, b);
                        cMaxR = Math.Max(cMaxR, l + w); cMaxT = Math.Max(cMaxT, b + h);
                    }
                }

                if (double.IsInfinity(cMinL) || cMinL == double.MaxValue) continue; // nothing to draw

                var left = cMinL - padding;
                var bottom = cMinB - padding;
                var right = cMaxR + padding;
                var top = cMaxT + padding;

                // Clamp to lane bounds; warn on overflow
                bool overflow = (left < tMinL) || (right > tMaxR) || (bottom < tMinB) || (top > tMaxT);
                if (overflow)
                {
                    if (ShouldEmit(model, "warning"))
                    {
                        Console.WriteLine($"warning: sub-container '{c.Id}' overflows lane '{tier}'.");
                    }
                    left = Math.Max(left, tMinL); right = Math.Min(right, tMaxR); bottom = Math.Max(bottom, tMinB); top = Math.Min(top, tMaxT);
                }

                try
                {
                    dynamic box = page.DrawRectangle(left, bottom, right, top);
                    box.Text = string.IsNullOrWhiteSpace(c.Label) ? c.Id : c.Label;
                    // Per-container style overrides then fallback to layout.containers
                    bool styled = false;
                    if (c.Style != null)
                    {
                        if (!string.IsNullOrWhiteSpace(c.Style.Fill) && TryParseColor(c.Style.Fill, out var fill)) { TrySetFormula(box, "FillForegnd", $"RGB({fill.R},{fill.G},{fill.B})"); styled = true; }
                        if (!string.IsNullOrWhiteSpace(c.Style.Stroke) && TryParseColor(c.Style.Stroke, out var stroke)) { TrySetFormula(box, "LineColor", $"RGB({stroke.R},{stroke.G},{stroke.B})"); styled = true; }
                        if (!string.IsNullOrWhiteSpace(c.Style.LinePattern)) { ApplyLinePattern(box, c.Style.LinePattern); styled = true; }
                    }
                    if (!styled) { ApplyContainerStyle(model, box); }
                    try { TrySetResult(box.CellsU["Rounding"], corner); } catch { }
                    try { box.SendToBack(); } catch { }
                    ReleaseCom(box);
                }
                catch { }
            }
        }

        private sealed class NodeDto
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("label")]
            public string? Label { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("tier")]
            public string? Tier { get; set; }

            [JsonPropertyName("groupId")]
            public string? GroupId { get; set; }

            [JsonPropertyName("containerId")]
            public string? ContainerId { get; set; }

            [JsonPropertyName("size")]
            public SizeDto? Size { get; set; }

            [JsonPropertyName("style")]
            public StyleDto? Style { get; set; }

            [JsonPropertyName("metadata")]
            public Dictionary<string, string>? Metadata { get; set; }

            // M3: optional ports hints
            [JsonPropertyName("ports")]
            public PortsDto? Ports { get; set; }
        }

        private sealed class PortsDto
        {
            [JsonPropertyName("inSide")]
            public string? InSide { get; set; }

            [JsonPropertyName("outSide")]
            public string? OutSide { get; set; }
        }

        private sealed class EdgeDto
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("sourceId")]
            public string? SourceId { get; set; }

            [JsonPropertyName("targetId")]
            public string? TargetId { get; set; }

            [JsonPropertyName("label")]
            public string? Label { get; set; }

            [JsonPropertyName("directed")]
            public bool? Directed { get; set; }

            [JsonPropertyName("style")]
            public StyleDto? Style { get; set; }

            [JsonPropertyName("metadata")]
            public Dictionary<string, string>? Metadata { get; set; }

            // M3: optional routing hints
            [JsonPropertyName("waypoints")]
            public List<PointDto>? Waypoints { get; set; }

            [JsonPropertyName("priority")]
            public double? Priority { get; set; }
        }

        private sealed class PointDto
        {
            [JsonPropertyName("x")]
            public double? X { get; set; }

            [JsonPropertyName("y")]
            public double? Y { get; set; }
        }

        private sealed class SizeDto
        {
            [JsonPropertyName("width")]
            public float? Width { get; set; }

            [JsonPropertyName("height")]
            public float? Height { get; set; }
        }

        private sealed class StyleDto
        {
            [JsonPropertyName("fill")]
            public string? Fill { get; set; }

            [JsonPropertyName("stroke")]
            public string? Stroke { get; set; }

            [JsonPropertyName("linePattern")]
            public string? LinePattern { get; set; }
        }

        private sealed class ContainersLayoutDto
        {
            [JsonPropertyName("paddingIn")]
            public double? PaddingIn { get; set; }

            [JsonPropertyName("cornerIn")]
            public double? CornerIn { get; set; }

            [JsonPropertyName("style")]
            public StyleDto? Style { get; set; }
        }

        private sealed class BoundsDto
        {
            [JsonPropertyName("x")]
            public double? X { get; set; }

            [JsonPropertyName("y")]
            public double? Y { get; set; }

            [JsonPropertyName("width")]
            public double? Width { get; set; }

            [JsonPropertyName("height")]
            public double? Height { get; set; }
        }

        private sealed class ContainerDto
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("label")]
            public string? Label { get; set; }

            [JsonPropertyName("tier")]
            public string? Tier { get; set; }

            [JsonPropertyName("bounds")]
            public BoundsDto? Bounds { get; set; }

            [JsonPropertyName("style")]
            public StyleDto? Style { get; set; }
        }

        private static bool ShouldPaginate(DiagramModel model, LayoutResult layout)
        {
            // default true if height exceeds usable height when page size provided; otherwise false
            var height = (ComputeLayoutBounds(layout).maxTop - ComputeLayoutBounds(layout).minBottom);
            var margin = GetPageMargin(model) ?? Margin;
            var title = GetTitleHeight(model);
            var pageHeight = GetPageHeight(model);
            bool paginate = true;
            if (model.Metadata.TryGetValue("layout.page.paginate", out var p) && bool.TryParse(p, out var pb)) paginate = pb;
            if (!pageHeight.HasValue) return false; // without a fixed page height, keep single page for now
            var usable = pageHeight.Value - (2 * margin) - title;
            // hard error if a single node cannot fit and errorOnOverflow is set
            if (model.Metadata.TryGetValue("layout.page.errorOnOverflow", out var eoo) && bool.TryParse(eoo, out var hard) && hard && usable > 0)
            {
                foreach (var nl in layout.Nodes)
                {
                    var hNode = (nl.Size.HasValue && nl.Size.Value.Height > 0) ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                    if (hNode > usable)
                    {
                        throw new InvalidDataException($"Node '{nl.Id}' height {hNode:F2}in exceeds usable page height {usable:F2}in; increase page height/margins or resize node (layout.page.errorOnOverflow=true).");
                    }
                }
            }

            return paginate && usable > 0 && height > (float)usable;
        }

        private static void DrawMultiPage(DiagramModel model, LayoutResult layout, LayoutPlan? layoutPlan, VisioPageManager pageManager, IReadOnlyDictionary<string, int> plannedPages, LayerRenderContext layerContext)
        {
            _ = layoutPlan;
            var pageWidth = GetPageWidth(model) ?? 11.0;
            var pageHeight = GetPageHeight(model) ?? 8.5;
            var margin = GetPageMargin(model) ?? Margin;
            var title = GetTitleHeight(model);
            var usable = pageHeight - (2 * margin) - title;
            if (usable <= 0) usable = pageHeight; // fallback

            var (minLeft, minBottom, maxRight, maxTop) = ComputeLayoutBounds(layout);

            // Compute page index for each node
            var nodePage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (plannedPages != null)
            {
                foreach (var kv in plannedPages)
                {
                    if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                    var idxPlan = kv.Value;
                    if (idxPlan < 0) idxPlan = 0;
                    nodePage[kv.Key] = idxPlan;
                }
            }
            int maxPage = nodePage.Values.DefaultIfEmpty(0).Max();
            foreach (var nl in layout.Nodes)
            {
                if (nodePage.ContainsKey(nl.Id))
                {
                    var idxPlan = nodePage[nl.Id];
                    if (idxPlan > maxPage) maxPage = idxPlan;
                    continue;
                }

                var y = nl.Position.Y - (float)minBottom;
                var idx = (int)Math.Floor(y / (float)usable);
                if (idx < 0) idx = 0;
                nodePage[nl.Id] = idx;
                if (idx > maxPage) maxPage = idx;
            }

            var placementsPerPage = new Dictionary<int, Dictionary<string, NodePlacement>>();

            var pageCount = maxPage + 1;
            for (int pi = 0; pi <= maxPage; pi++)
            {
                var pageInfo = pageManager.EnsurePage(pi, $"Layered {pi + 1}");
                dynamic page = pageInfo.Page ?? throw new COMException("Visio page was not created.");

                TrySetResult(page.PageSheet.CellsU["PageWidth"], pageWidth);
                TrySetResult(page.PageSheet.CellsU["PageHeight"], pageHeight);

                var placements = new Dictionary<string, NodePlacement>(StringComparer.OrdinalIgnoreCase);
                ComputePlacementsForPage(model, layout, layoutPlan, placements, pageInfo, pi, minLeft, minBottom, usable, margin, title, nodePage, layerContext);
                DrawLaneContainersForPage(model, layout, page, pi, pageCount, minLeft, minBottom, usable, margin, title, nodePage);
                placementsPerPage[pi] = placements;
            }

            DrawConnectorsPaged(model, layout, placementsPerPage, pageManager, nodePage, layerContext);
        }

        private static void ComputePlacementsForPage(DiagramModel model, LayoutResult layout, LayoutPlan? layoutPlan, IDictionary<string, NodePlacement> placements, VisioPageManager.PageInfo pageInfo, int pageIndex, double minLeft, double minBottom, double usableHeight, double margin, double title, IReadOnlyDictionary<string, int> nodePageAssignments, LayerRenderContext layerContext)
        {
            if (pageInfo == null) throw new COMException("Visio page metadata was not initialised.");
            dynamic visioPage = pageInfo.Page ?? throw new COMException("Visio page was not created.");
            var nodeMap = BuildNodeMap(model);
            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);

            foreach (var nl in layout.Nodes)
            {
                var yNorm = nl.Position.Y - (float)minBottom;
                var idx = (int)Math.Floor(yNorm / (float)usableHeight);
                var assignedPage = nodePageAssignments != null && nodePageAssignments.TryGetValue(nl.Id, out var mapped) ? mapped : idx;
                if (assignedPage != pageIndex) continue;

                if (!nodeMap.TryGetValue(nl.Id, out var node)) continue;
                var width = nl.Size.HasValue && nl.Size.Value.Width > 0 ? nl.Size.Value.Width : (float)DefaultNodeWidth;
                var height = nl.Size.HasValue && nl.Size.Value.Height > 0 ? nl.Size.Value.Height : (float)DefaultNodeHeight;

                var offsetX = margin - minLeft;
                var bandOffset = (pageIndex * usableHeight);
                var offsetY = margin + title - minBottom - bandOffset;

                var left = nl.Position.X + offsetX;
                var bottom = nl.Position.Y + offsetY;
                var right = left + width;
                var top = bottom + height;

                int? layerIndex = null;
                string? moduleId = null;
                if (layerContext.Enabled)
                {
                    moduleId = ResolveModuleIdForNode(node, tiers, tiersSet);
                    if (!layerContext.ShouldRenderModule(moduleId))
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(moduleId) && layerContext.ModuleToLayer.TryGetValue(moduleId, out var assignedLayer))
                    {
                        layerIndex = assignedLayer;
                    }
                }

                dynamic shape = visioPage.DrawRectangle(left, bottom, right, top);
                shape.Text = node.Label;
                ApplyNodeStyle(node, shape);

                if (layerIndex.HasValue)
                {
                    AssignShapeToLayer(layerContext, pageInfo, shape, layerIndex.Value);
                }

                placements[nl.Id] = new NodePlacement(shape, left, bottom, width, height, layerIndex);
                pageInfo.IncrementNodeCount();
            }

            DrawLayerBridgeStubsForPage(layoutPlan, layout, pageInfo, visioPage, pageIndex, minLeft, minBottom, usableHeight, margin, title, nodePageAssignments, layerContext);
        }

        private static void DrawLaneContainersForPage(DiagramModel model, LayoutResult layout, dynamic page, int pageIndex, int pageCount, double minLeft, double minBottom, double usableHeight, double margin, double title, IReadOnlyDictionary<string, int> nodePageAssignments)
        {
            dynamic visioPage = page ?? throw new COMException("Visio page was not created.");
            var nodeMap = BuildNodeMap(model);
            var tiers = GetOrderedTiers(model);

            var padding = GetContainerPadding(model);
            var corner = GetContainerCorner(model);
            foreach (var tier in tiers)
            {
                double tMinL = double.MaxValue, tMinB = double.MaxValue, tMaxR = double.MinValue, tMaxT = double.MinValue;
                foreach (var nl in layout.Nodes)
                {
                    var yNorm = nl.Position.Y - (float)minBottom;
                    var idx = (int)Math.Floor(yNorm / (float)usableHeight);
                    var assignedPage = nodePageAssignments != null && nodePageAssignments.TryGetValue(nl.Id, out var mapped) ? mapped : idx;
                    if (assignedPage != pageIndex) continue;
                    if (!nodeMap.TryGetValue(nl.Id, out var node)) continue;
                    var nodeTier = node.Tier;
                    if (string.IsNullOrWhiteSpace(nodeTier) && node.Metadata.TryGetValue("tier", out var tMeta)) nodeTier = tMeta;
                    var tierKey = string.IsNullOrWhiteSpace(nodeTier) ? tiers.First() : nodeTier!;
                    if (!tier.Equals(tierKey, StringComparison.OrdinalIgnoreCase)) continue;

                    var w = nl.Size.HasValue && nl.Size.Value.Width > 0 ? nl.Size.Value.Width : (float)DefaultNodeWidth;
                    var h = nl.Size.HasValue && nl.Size.Value.Height > 0 ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                    var offsetX = margin - minLeft;
                    var bandOffset = (pageIndex * usableHeight);
                    var offsetY = margin + title - minBottom - bandOffset;
                    var l = nl.Position.X + offsetX;
                    var b = nl.Position.Y + offsetY;
                    tMinL = Math.Min(tMinL, l); tMinB = Math.Min(tMinB, b);
                    tMaxR = Math.Max(tMaxR, l + w); tMaxT = Math.Max(tMaxT, b + h);
                }

                if (double.IsInfinity(tMinL) || tMinL == double.MaxValue) continue;

                    var left = tMinL - padding; var bottom = tMinB - padding; var right = tMaxR + padding; var top = tMaxT + padding;
                    // Clamp within page margins if possible (shrink padding first)
                    double ph;
                    try { ph = (double)visioPage.PageSheet.CellsU["PageHeight"].ResultIU; } catch { ph = 0; }
                    // maintain clearance under the title banner on first page
                    var bannerClearance = (pageIndex == 0 ? (title + 0.2) : 0.0);
                    var maxTop = ph > 0 ? ph - margin : top;
                    if (top > maxTop)
                    {
                        var overflow = top - maxTop;
                        var reduce = Math.Min(overflow, padding);
                        top -= reduce; bottom += reduce;
                    }
                    // keep lanes below the banner by clearance amount
                    if (pageIndex == 0)
                    {
                        var limit = ph - margin - title - 0.2;
                        if (top > limit) { var shiftDown = top - limit; top -= shiftDown; bottom -= shiftDown; }
                    }
                    if (bottom < margin)
                    {
                        var shift = margin - bottom;
                        bottom += shift; top += shift;
                    }
                try
                {
                    dynamic lane = visioPage.DrawRectangle(left, bottom, right, top);
                    lane.Text = tier;
                    ApplyContainerStyle(model, lane);
                    try { TrySetResult(lane.CellsU["Rounding"], corner); } catch { }
                    try { lane.SendToBack(); } catch { }
                    ReleaseCom(lane);
                }
                catch { }
            }
            // Draw title banner on every page with page numbers
            double ph2;
            try { ph2 = (double)visioPage.PageSheet.CellsU["PageHeight"].ResultIU; } catch { ph2 = 0; }
            var pw = (double)visioPage.PageSheet.CellsU["PageWidth"].ResultIU;
            DrawTitleBanner(model, visioPage, pw, GetTitleHeight(model), margin, pageIndex + 1, pageCount);

            // Draw explicit sub-containers on this page
            DrawExplicitContainers(model, layout, visioPage, minLeft, minBottom, usableHeight, margin, title, pageIndex);
        }

        private static void DrawConnectorsPaged(DiagramModel model, LayoutResult layout, Dictionary<int, Dictionary<string, NodePlacement>> perPage, VisioPageManager pageManager, IReadOnlyDictionary<string, int> nodePageAssignments, LayerRenderContext layerContext)
        {
            var pageCount = perPage.Keys.Count == 0 ? 1 : perPage.Keys.Max() + 1;
            var (minLeft, minBottom, maxRight, maxTop) = ComputeLayoutBounds(layout);
            var margin = GetPageMargin(model) ?? Margin;
            var title = GetTitleHeight(model);
            var pageHeight = GetPageHeight(model) ?? 8.5;
            if (pageHeight <= 0) pageHeight = 8.5;
            var usable = pageHeight - (2 * margin) - title;
            if (usable <= 0 || double.IsNaN(usable)) usable = pageHeight;
            var offsetXBase = margin - minLeft;
            var offsetYBase = margin + title - minBottom;
            var routeMode = (model.Metadata.TryGetValue("layout.routing.mode", out var rm) && !string.IsNullOrWhiteSpace(rm)) ? rm.Trim() : "orthogonal";
            var useOrthogonal = !string.Equals(routeMode, "straight", StringComparison.OrdinalIgnoreCase);
            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);
            var nodeMap = BuildNodeMap(model);
            var bundleByRaw = model.Metadata.TryGetValue("layout.routing.bundleBy", out var bb) ? (bb ?? "none").Trim() : "none";
            double bundleSepIn = 0.12;
            if (model.Metadata.TryGetValue("layout.routing.bundleSeparationIn", out var bsep) && double.TryParse(bsep, NumberStyles.Float, CultureInfo.InvariantCulture, out var bsv) && bsv >= 0) bundleSepIn = bsv;
            var channelGapIn = (model.Metadata.TryGetValue("layout.routing.channels.gapIn", out var cgap) && double.TryParse(cgap, NumberStyles.Float, CultureInfo.InvariantCulture, out var cg)) ? cg : 0.0;
            var effectiveBundleBy = (string.Equals(bundleByRaw, "none", StringComparison.OrdinalIgnoreCase) && channelGapIn > 0.0) ? "lane" : bundleByRaw;
            var bundles = BuildBundleIndex(model, effectiveBundleBy);
            for (int pi = 0; pi < pageCount; pi++)
            {
                if (!pageManager.TryGetPageInfo(pi, out var pageInfo))
                {
                    pageInfo = pageManager.EnsurePage(pi);
                }
                dynamic page = pageInfo.Page ?? throw new COMException("Visio page was not created.");
                if (!perPage.TryGetValue(pi, out var placementsOnPage)) placementsOnPage = new Dictionary<string, NodePlacement>();

                var layoutEdgeLookup = new Dictionary<string, EdgeRoute>(StringComparer.OrdinalIgnoreCase);
                if (layout?.Edges != null)
                {
                    foreach (var route in layout.Edges)
                    {
                        if (route == null) continue;
                        var routeId = route.Id?.Trim();
                        if (!string.IsNullOrWhiteSpace(routeId))
                        {
                            layoutEdgeLookup[routeId!] = route;
                        }
                    }
                }

                var bandOffset = IsFinite(usable) ? pi * usable : 0.0;
                var offsetX = offsetXBase;
                var offsetY = offsetYBase - bandOffset;

                foreach (var edge in model.Edges)
                {
                    if (edge == null) continue;
                    var sourceId = edge.SourceId ?? string.Empty;
                    var targetId = edge.TargetId ?? string.Empty;
                    var edgeKey = edge.Id ?? string.Empty;

                    var hasSrc = placementsOnPage.TryGetValue(sourceId, out var src) && src != null;
                    var hasDst = placementsOnPage.TryGetValue(targetId, out var dst) && dst != null;

                    int sp = 0, tp = 0;
                    var hasAssignments = nodePageAssignments.Count > 0;
                    if (hasAssignments)
                    {
                        if (!nodePageAssignments.TryGetValue(sourceId, out sp)) sp = 0;
                        if (!nodePageAssignments.TryGetValue(targetId, out tp)) tp = 0;
                    }
                    else if (usable > 0 && IsFinite(usable))
                    {
                        var sNode = layout?.Nodes?.FirstOrDefault(n => string.Equals(n.Id, sourceId, StringComparison.OrdinalIgnoreCase));
                        var tNode = layout?.Nodes?.FirstOrDefault(n => string.Equals(n.Id, targetId, StringComparison.OrdinalIgnoreCase));
                        if (sNode != null && !string.IsNullOrWhiteSpace(sNode.Id))
                        {
                            var y = sNode.Position.Y - (float)minBottom;
                            sp = (int)Math.Floor(y / (float)usable);
                        }
                        if (tNode != null && !string.IsNullOrWhiteSpace(tNode.Id))
                        {
                            var y2 = tNode.Position.Y - (float)minBottom;
                            tp = (int)Math.Floor(y2 / (float)usable);
                        }
                    }

                    var connectorDrawn = false;

                    if (hasSrc && hasDst &&
                        layoutEdgeLookup.TryGetValue(edgeKey, out var predefinedRoute) &&
                        predefinedRoute?.Points != null &&
                        predefinedRoute.Points.Length >= 2)
                    {
                        var pts = new List<double>(predefinedRoute.Points.Length * 2);
                        foreach (var pt in predefinedRoute.Points)
                        {
                            pts.Add(pt.X + offsetX);
                            pts.Add(pt.Y + offsetY);
                        }

                        try
                        {
                            dynamic line = page.DrawPolyline(pts.ToArray(), 0);
                            if (!string.IsNullOrWhiteSpace(edge.Label)) line.Text = edge.Label;
                            if (edge.Directed) TrySetFormula(line, "EndArrow", "5"); else TrySetFormula(line, "EndArrow", "0");
                            ApplyEdgeStyle(edge, line); try { line.SendToBack(); } catch { }
                            ReleaseCom(line);
                            connectorDrawn = true;
                        }
                        catch
                        {
                            connectorDrawn = false;
                        }
                    }
                    else if (hasSrc && hasDst)
                    {
                        var srcPlacement = src!;
                        var dstPlacement = dst!;
                        if (useOrthogonal)
                        {
                            dynamic? connector = null;
                            try
                            {
                                var app = page.Application;
                                connector = page.Drop(app.ConnectorToolDataObject, srcPlacement.CenterX, srcPlacement.CenterY);
                                // Corridor-aware: choose side based on relative tier positions (same logic as single-page)
                                string GetTier2(VDG.Core.Models.Node n)
                                {
                                    var t = !string.IsNullOrWhiteSpace(n.Tier) ? n.Tier! : (n.Metadata.TryGetValue("tier", out var tv) ? tv : tiers.First());
                                    return tiersSet.Contains(t) ? t : tiers.First();
                                }
                                string srcSide = "right";
                                string dstSide = "left";
                                if (nodeMap.TryGetValue(sourceId, out var srcNode) && nodeMap.TryGetValue(targetId, out var dstNode))
                                {
                                    var sTier = Array.IndexOf(tiers, GetTier2(srcNode));
                                    var tTier = Array.IndexOf(tiers, GetTier2(dstNode));
                                    if (sTier == tTier)
                                    {
                                        if (srcPlacement.CenterX <= dstPlacement.CenterX) { srcSide = "right"; dstSide = "left"; }
                                        else { srcSide = "left"; dstSide = "right"; }
                                    }
                                    else if (sTier < tTier) { srcSide = "right"; dstSide = "left"; }
                                    else { srcSide = "left"; dstSide = "right"; }
                                }
                                (double xr, double yr) SideToRel2(string side) => side.ToLowerInvariant() switch
                                {
                                    "left" => (0.0, 0.5),
                                    "right" => (1.0, 0.5),
                                    "top" => (0.5, 1.0),
                                    "bottom" => (0.5, 0.0),
                                    _ => (0.5, 0.5)
                                };
                                var (sxr, syr) = SideToRel2(srcSide);
                                var (dxr, dyr) = SideToRel2(dstSide);
                                if (bundles.TryGetValue(edgeKey, out var binfo))
                                {
                                    (sxr, syr) = ApplyBundleOffsetRel(srcSide, (sxr, syr), srcPlacement, binfo, bundleSepIn);
                                    (dxr, dyr) = ApplyBundleOffsetRel(dstSide, (dxr, dyr), dstPlacement, binfo, bundleSepIn);
                                }
                                bool usedPolyline = false;
                                double channelsGapValue = 0.0; string? channelsGapStr;
                                var hasKey = model.Metadata.TryGetValue("layout.routing.channels.gapIn", out channelsGapStr);
                                if (hasKey && !string.IsNullOrWhiteSpace(channelsGapStr)) double.TryParse(channelsGapStr, NumberStyles.Float, CultureInfo.InvariantCulture, out channelsGapValue);
                                var hasChannels = channelsGapValue > 0.0;
                                var cx = hasChannels ? CorridorXForEdge(model, placementsOnPage, sourceId, targetId) : null;
                                // Waypoints override first
                                if (!usedPolyline && TryGetWaypointsFromMetadata(edge, out var wps))
                                {
                                    var (sx, sy) = ToAbsAttach(srcPlacement, srcSide, sxr, syr);
                                    var (tx, ty) = ToAbsAttach(dstPlacement, dstSide, dxr, dyr);
                                    var pts = new List<double>(2 + (wps.Count * 2) + 2) { sx, sy };
                                    foreach (var p in wps) { pts.Add(p.x); pts.Add(p.y); }
                                    pts.Add(tx); pts.Add(ty);
                                    try
                                    {
                                        dynamic pl = page.DrawPolyline(pts.ToArray(), 0);
                                        if (!string.IsNullOrWhiteSpace(edge.Label))
                                        {
                                            var arr = pts.ToArray();
                                            double maxLen = -1; double mx = sx, my = sy; int segs = (arr.Length / 2) - 1;
                                            for (int i = 0; i < segs; i++)
                                            {
                                                var x1 = arr[i * 2]; var y1 = arr[i * 2 + 1]; var x2 = arr[(i + 1) * 2]; var y2 = arr[(i + 1) * 2 + 1];
                                                var len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                                                if (len > maxLen) { maxLen = len; mx = (x1 + x2) / 2.0; my = (y1 + y2) / 2.0; }
                                            }
                                            var off = 0.15; if (edge.Metadata != null && edge.Metadata.TryGetValue("edge.label.offsetIn", out var offRaw)) { double.TryParse(offRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out off); } DrawLabelBox(page, edge.Label!, mx, my, 0.0, off);
                                        }
                                        if (edge.Directed) TrySetFormula(pl, "EndArrow", "5"); else TrySetFormula(pl, "EndArrow", "0");
                                        ApplyEdgeStyle(edge, pl); try { pl.SendToBack(); } catch { }
                                        ReleaseCom(pl); usedPolyline = true; connectorDrawn = true;
                                    }
                                    catch { usedPolyline = false; }
                                }
                                if (!usedPolyline && cx.HasValue)
                                {
                                    var (sx, sy) = ToAbsAttach(srcPlacement, srcSide, sxr, syr);
                                    var (tx, ty) = ToAbsAttach(dstPlacement, dstSide, dxr, dyr);
                                    // Stagger vertical corridor by lane bundle index to reduce overlaps (paged)
                                    var corridorX = cx.Value;
                                    var laneBundles = BuildBundleIndex(model, "lane");
                                    if (laneBundles.TryGetValue(edgeKey, out var cinfo) && channelsGapValue > 0)
                                    {
                                        var center = (cinfo.size - 1) / 2.0;
                                        var delta = (cinfo.index - center) * Math.Min(channelsGapValue * 0.4, 0.4);
                                        corridorX += delta;
                                    }
                                    try
                                    {
                                        // Container-aware routing (paged)
                                        var routeAround = model.Metadata.TryGetValue("layout.routing.routeAroundContainers", out var rar) && bool.TryParse(rar, out var rarb) && rarb;
                                        var skirt = GetContainerPadding(model) * 0.5;
                                        var pts = new List<double>(12) { sx, sy };
                                        if (routeAround)
                                        {
                                            string GetTier(VDG.Core.Models.Node n)
                                            {
                                                var t = !string.IsNullOrWhiteSpace(n.Tier) ? n.Tier! : (n.Metadata.TryGetValue("tier", out var tv) ? tv : tiers.First());
                                                return t;
                                            }
                                            var srcTier = nodeMap.TryGetValue(sourceId, out var sn) ? GetTier(sn) : tiers.First();
                                            var dstTier = nodeMap.TryGetValue(targetId, out var tn) ? GetTier(tn) : tiers.Last();
                                            // Compute bounds for src/dst tiers using current page placements
                                            double sMinL = double.MaxValue, sMaxR = double.MinValue;
                                            double dMinL = double.MaxValue, dMaxR = double.MinValue;
                                            foreach (var kv in placementsOnPage)
                                            {
                                                if (!nodeMap.TryGetValue(kv.Key, out var node)) continue;
                                                var nodeTier = !string.IsNullOrWhiteSpace(node.Tier) ? node.Tier! : (node.Metadata.TryGetValue("tier", out var tMeta) ? tMeta : tiers.First());
                                                if (string.Equals(nodeTier, srcTier, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    var l = kv.Value.Left; var r = kv.Value.Left + kv.Value.Width;
                                                    sMinL = Math.Min(sMinL, l); sMaxR = Math.Max(sMaxR, r);
                                                }
                                                if (string.Equals(nodeTier, dstTier, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    var l = kv.Value.Left; var r = kv.Value.Left + kv.Value.Width;
                                                    dMinL = Math.Min(dMinL, l); dMaxR = Math.Max(dMaxR, r);
                                                }
                                            }
                                            var pad = GetContainerPadding(model);
                                            var sL = (double.IsInfinity(sMinL) || sMinL == double.MaxValue) ? double.NegativeInfinity : (sMinL - pad);
                                            var sR = (double.IsInfinity(sMaxR) || sMaxR == double.MinValue) ? double.PositiveInfinity : (sMaxR + pad);
                                            var dL = (double.IsInfinity(dMinL) || dMinL == double.MaxValue) ? double.NegativeInfinity : (dMinL - pad);
                                            var dR = (double.IsInfinity(dMaxR) || dMaxR == double.MinValue) ? double.PositiveInfinity : (dMaxR + pad);
                                            if (srcSide.Equals("right", StringComparison.OrdinalIgnoreCase))
                                            {
                                                var x = Math.Min(corridorX, sR + skirt);
                                                if (x > sx) { pts.Add(x); pts.Add(sy); }
                                            }
                                            else if (srcSide.Equals("left", StringComparison.OrdinalIgnoreCase))
                                            {
                                                var x = Math.Max(corridorX, sL - skirt);
                                                if (x < sx) { pts.Add(x); pts.Add(sy); }
                                            }
                                        }
                                        // Corridor vertical
                                        pts.Add(corridorX); pts.Add(sy);
                                        pts.Add(corridorX); pts.Add(ty);
                                        if (routeAround)
                                        {
                                            // Recompute destination lane bounds from placementsOnPage
                                            string GetTier(VDG.Core.Models.Node n)
                                            {
                                                var t = !string.IsNullOrWhiteSpace(n.Tier) ? n.Tier! : (n.Metadata.TryGetValue("tier", out var tv) ? tv : tiers.First());
                                                return t;
                                            }
                                            var dstTierLocal = nodeMap.TryGetValue(targetId, out var tn2) ? GetTier(tn2) : tiers.Last();
                                            double dMinL2 = double.MaxValue, dMaxR2 = double.MinValue;
                                            foreach (var kv in placementsOnPage)
                                            {
                                                if (!nodeMap.TryGetValue(kv.Key, out var node)) continue;
                                                var nodeTier = !string.IsNullOrWhiteSpace(node.Tier) ? node.Tier! : (node.Metadata.TryGetValue("tier", out var tMeta) ? tMeta : tiers.First());
                                                if (!string.Equals(nodeTier, dstTierLocal, StringComparison.OrdinalIgnoreCase)) continue;
                                                var l = kv.Value.Left; var r = kv.Value.Left + kv.Value.Width;
                                                dMinL2 = Math.Min(dMinL2, l); dMaxR2 = Math.Max(dMaxR2, r);
                                            }
                                            var pad2 = GetContainerPadding(model);
                                            var dLcalc = (double.IsInfinity(dMinL2) || dMinL2 == double.MaxValue) ? double.NegativeInfinity : (dMinL2 - pad2);
                                            var dRcalc = (double.IsInfinity(dMaxR2) || dMaxR2 == double.MinValue) ? double.PositiveInfinity : (dMaxR2 + pad2);

                                            if (dstSide.Equals("left", StringComparison.OrdinalIgnoreCase))
                                            {
                                                var x = Math.Max(corridorX, dLcalc - skirt);
                                                if (x < tx) { pts.Add(x); pts.Add(ty); }
                                            }
                                            else if (dstSide.Equals("right", StringComparison.OrdinalIgnoreCase))
                                            {
                                                var x = Math.Min(corridorX, dRcalc + skirt);
                                                if (x > tx) { pts.Add(x); pts.Add(ty); }
                                            }
                                        }
                                        // Final attach
                                        pts.Add(tx); pts.Add(ty);
                                        dynamic pl = page.DrawPolyline(pts.ToArray(), 0);
                                        if (!string.IsNullOrWhiteSpace(edge.Label))
                                        {
                                            var arrPts = pts.ToArray();
                                            double maxLen = -1; double mx = sx, my = sy; int segs = (arrPts.Length / 2) - 1;
                                            for (int i = 0; i < segs; i++)
                                            {
                                                var x1 = arrPts[i * 2]; var y1 = arrPts[i * 2 + 1]; var x2 = arrPts[(i + 1) * 2]; var y2 = arrPts[(i + 1) * 2 + 1];
                                                var len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                                                if (len > maxLen) { maxLen = len; mx = (x1 + x2) / 2.0; my = (y1 + y2) / 2.0; }
                                            }
                                            var off = 0.15; if (edge.Metadata != null && edge.Metadata.TryGetValue("edge.label.offsetIn", out var offRaw)) { double.TryParse(offRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out off); }
                                            DrawLabelBox(page, edge.Label!, mx, my, 0.0, off);
                                        }
                                        if (edge.Directed) TrySetFormula(pl, "EndArrow", "5"); else TrySetFormula(pl, "EndArrow", "0");
                                        ApplyEdgeStyle(edge, pl); try { pl.SendToBack(); } catch { }
                                        ReleaseCom(pl); usedPolyline = true; connectorDrawn = true;
                                    }
                                    catch { usedPolyline = false; }
                                }
                                if (!usedPolyline)
                                {
                                    try { connector.CellsU["BeginX"].GlueToPos(((dynamic)srcPlacement.Shape), sxr, syr); }
                                    catch { connector.CellsU["BeginX"].GlueTo(((dynamic)srcPlacement.Shape).CellsU["PinX"]); }
                                    try { connector.CellsU["EndX"].GlueToPos(((dynamic)dstPlacement.Shape), dxr, dyr); }
                                    catch { connector.CellsU["EndX"].GlueTo(((dynamic)dstPlacement.Shape).CellsU["PinX"]); }
                                    if (!string.IsNullOrWhiteSpace(edge.Label)) connector.Text = edge.Label;
                                    if (edge.Directed) TrySetFormula(connector, "EndArrow", "5"); else TrySetFormula(connector, "EndArrow", "0");
                                    TrySetFormula(connector, "Routestyle", "16");
                                    try { TrySetFormula(connector, "LineRouteExt", "2"); } catch { }
                                    ApplyEdgeStyle(edge, connector);
                                    try { connector.SendToBack(); } catch { }
                                    connectorDrawn = true;
                                }
                            }
                            finally { if (connector != null) ReleaseCom(connector); }
                        }
                        else
                        {
                            dynamic line = page.DrawLine(srcPlacement.CenterX, srcPlacement.CenterY, dstPlacement.CenterX, dstPlacement.CenterY);
                            if (!string.IsNullOrWhiteSpace(edge.Label)) line.Text = edge.Label;
                            if (edge.Directed) TrySetFormula(line, "EndArrow", "5"); else TrySetFormula(line, "EndArrow", "0");
                            ApplyEdgeStyle(edge, line); ReleaseCom(line); connectorDrawn = true;
                        }

                        if (connectorDrawn)
                        {
                            pageInfo.IncrementConnectorCount();
                        }
                        else
                        {
                            pageManager.RegisterConnectorSkipped(edge.Id, sourceId, targetId, pi, "connector routing failed");
                        }
                    }
                    else if (hasSrc && !hasDst)
                    {
                        if (src != null)
                        {
                            var markerLeft = src.Left + src.Width + 0.1; var markerBottom = src.CenterY - 0.1;
                            dynamic m = page.DrawRectangle(markerLeft, markerBottom, markerLeft + 1.4, markerBottom + 0.35);
                            m.Text = (usable > 0 && IsFinite(usable)) ? $"to {edge.TargetId} (p{tp + 1})" : $"to {edge.TargetId}";
                            TrySetFormula(m, "LinePattern", "2"); ReleaseCom(m);
                        }
                        pageManager.RegisterConnectorSkipped(edge.Id, sourceId, targetId, pi, $"target on page {tp + 1}");
                    }
                    else if (!hasSrc && hasDst)
                    {
                        if (dst != null)
                        {
                            var markerRight = dst.Left - 0.1; var markerLeft2 = markerRight - 0.8; var markerBottom2 = dst.CenterY - 0.1;
                            dynamic m = page.DrawRectangle(markerLeft2 - 0.6, markerBottom2, markerRight, markerBottom2 + 0.35);
                            m.Text = (usable > 0 && IsFinite(usable)) ? $"from {edge.SourceId} (p{sp + 1})" : $"from {edge.SourceId}";
                            TrySetFormula(m, "LinePattern", "2"); ReleaseCom(m);
                        }
                        pageManager.RegisterConnectorSkipped(edge.Id, sourceId, targetId, pi, $"source on page {sp + 1}");
                    }
                    else
                    {
                        pageManager.RegisterConnectorSkipped(edge.Id, sourceId, targetId, pi, "connector endpoints missing on page");
                    }
                }
            }
        }

        private static void EmitPageSummary(VisioPageManager? pageManager)
        {
            if (pageManager == null) return;
            var pages = pageManager.Pages;
            if (pages == null || pages.Count == 0) return;

            var totalNodes = pages.Sum(p => p.NodeCount);
            var totalConnectors = pages.Sum(p => p.ConnectorCount);
            var avgConnectors = pages.Count > 0 ? totalConnectors / (double)pages.Count : 0.0;
            Console.WriteLine($"info: created {pages.Count} page(s) ({totalNodes} nodes, {totalConnectors} connectors, avg {avgConnectors:F1} connectors/page)");

            var overflowPages = pages.Where(p => p.HasOverflow).Select(p => p.Index + 1).ToArray();
            if (overflowPages.Length > 0)
            {
                Console.WriteLine($"warning: overflow mitigated on pages {string.Join(", ", overflowPages)}");
            }

            var skipped = pageManager.SkippedConnectors;
            if (skipped != null && skipped.Count > 0)
            {
                Console.WriteLine($"warning: skipped {skipped.Count} connector(s); see diagnostics for pagination details");
            }

            var plans = pageManager.PagePlans;
            if (plans != null && plans.Count > 0)
            {
                Console.WriteLine($"info: planner suggested {plans.Count} page(s); rendered {pages.Count} page(s)");
                foreach (var plan in plans.OrderBy(p => p.PageIndex))
                {
                    var modules = (plan.Modules != null && plan.Modules.Length > 0) ? string.Join(", ", plan.Modules) : "n/a";
                    Console.WriteLine($"info: page plan {plan.PageIndex + 1}: nodes={plan.Nodes}, connectors={plan.Connectors}, occupancy≈{plan.Occupancy:F1}%, modules={modules}");
                }
            }

            foreach (var pageInfo in pages.OrderBy(p => p.Index))
            {
                Console.WriteLine($"info: rendered page {pageInfo.Index + 1}: nodes={pageInfo.NodeCount}, connectors={pageInfo.ConnectorCount}, skippedConnectors={pageInfo.SkippedConnectorCount}");
            }
        }

        private static PlannerSummaryStats BuildPlannerSummaryStats(PagePlan[]? pagePlans)
        {
            int pageCount = 0;
            int totalModules = 0;
            int totalConnectors = 0;
            double maxOccupancy = 0.0;
            int maxConnectors = 0;

            if (pagePlans != null)
            {
                pageCount = pagePlans.Length;
                foreach (var plan in pagePlans)
                {
                    if (plan == null) continue;
                    var modules = plan.Modules?.Length ?? 0;
                    totalModules += modules;
                    totalConnectors += plan.Connectors;
                    if (plan.Occupancy > maxOccupancy) maxOccupancy = plan.Occupancy;
                    if (plan.Connectors > maxConnectors) maxConnectors = plan.Connectors;
                }
            }

            double avgModules = pageCount > 0 ? totalModules / (double)pageCount : 0.0;
            double avgConnectors = pageCount > 0 ? totalConnectors / (double)pageCount : 0.0;

            return new PlannerSummaryStats
            {
                PageCount = pageCount,
                AverageModulesPerPage = avgModules,
                AverageConnectorsPerPage = avgConnectors,
                MaxOccupancy = maxOccupancy,
                MaxConnectors = maxConnectors
            };
        }

        private static string FormatDelta(int delta) => delta >= 0
            ? string.Format(CultureInfo.InvariantCulture, "+{0}", delta)
            : delta.ToString(CultureInfo.InvariantCulture);

        private static List<int> ParseLayerFilterTokens(IEnumerable<string> tokens)
        {
            var result = new List<int>();
            foreach (var token in tokens)
            {
                var normalized = token.Trim();
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                if (normalized.IndexOf("-", StringComparison.Ordinal) >= 0)
                {
                    var parts = normalized.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                    {
                        throw new UsageException($"Invalid layer range '{token}'.");
                    }

                    var start = ParseLayerIndexComponent(parts[0], token);
                    var end = ParseLayerIndexComponent(parts[1], token);
                    if (end < start)
                    {
                        (start, end) = (end, start);
                    }

                    for (var value = start; value <= end; value++)
                    {
                        result.Add(value);
                    }
                }
                else
                {
                    var index = ParseLayerIndexComponent(normalized, token);
                    result.Add(index);
                }
            }

            return result;
        }

        private static int ParseLayerIndexComponent(string component, string originalToken)
        {
            if (string.IsNullOrWhiteSpace(component))
            {
                throw new UsageException($"Invalid layer index '{originalToken}'.");
            }

            var trimmed = component.Trim();
            if (trimmed.StartsWith("L", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(1);
            }

            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new UsageException($"Invalid layer index '{originalToken}'.");
            }

            if (parsed < 1 || parsed > 1000)
            {
                throw new UsageException("Layer index must be between 1 and 1000.");
            }

            return parsed - 1;
        }

        private static HashSet<int>? BuildLayerVisibility(LayoutPlan? layoutPlan, IEnumerable<int> includeFilters, IEnumerable<int> excludeFilters, bool filterSpecified)
        {
            if (layoutPlan?.Layers == null || layoutPlan.Layers.Length == 0)
            {
                return null;
            }

            var available = new HashSet<int>(layoutPlan.Layers.Select(lp => lp.LayerIndex));
            var includeList = includeFilters?.Where(i => i >= 0).Distinct().ToList() ?? new List<int>();
            var excludeList = excludeFilters?.Where(i => i >= 0).Distinct().ToList() ?? new List<int>();

            if (!filterSpecified && includeList.Count == 0 && excludeList.Count == 0)
            {
                return null;
            }

            var missingIncludes = includeList.Where(i => !available.Contains(i)).ToList();
            if (missingIncludes.Count > 0)
            {
                Console.Error.WriteLine($"warning: ignoring unknown layer(s) {FormatLayerIndexList(missingIncludes)} for --layers include.");
                includeList = includeList.Where(available.Contains).ToList();
            }

            var missingExcludes = excludeList.Where(i => !available.Contains(i)).ToList();
            if (missingExcludes.Count > 0)
            {
                Console.Error.WriteLine($"warning: ignoring unknown layer(s) {FormatLayerIndexList(missingExcludes)} for --layers exclude.");
                excludeList = excludeList.Where(available.Contains).ToList();
            }

            var visible = includeList.Count > 0
                ? new HashSet<int>(includeList)
                : new HashSet<int>(available);

            foreach (var ex in excludeList)
            {
                visible.Remove(ex);
            }

            return visible;
        }

        private static string FormatLayerIndexList(IEnumerable<int> zeroBased)
        {
            return string.Join(", ",
                zeroBased
                    .Distinct()
                    .OrderBy(i => i)
                    .Select(i => $"L{i + 1}"));
        }

        private static void PrintPlannerSummary(PlannerSummaryStats stats, PlannerMetrics? segmentationMetrics, DiagnosticsSummary? diagnosticsSummary)
        {
            var modulesValue = segmentationMetrics?.OriginalModuleCount ?? 0;
            var segmentsValue = segmentationMetrics?.SegmentCount ?? modulesValue;
            var splitModulesValue = segmentationMetrics?.SplitModuleCount ?? 0;
            var avgSegmentsPerModule = segmentationMetrics?.AverageSegmentsPerModule ?? (modulesValue > 0 ? segmentsValue / (double)modulesValue : 0.0);
            var deltaText = FormatDelta(segmentsValue - modulesValue);

            var layoutMode = diagnosticsSummary?.OutputMode;
            if (!string.IsNullOrWhiteSpace(layoutMode))
            {
                Console.WriteLine($"info: layout outputMode={layoutMode}");
                if (diagnosticsSummary?.LayoutCanvasWidth is float canvasWidth &&
                    diagnosticsSummary.LayoutCanvasHeight is float canvasHeight &&
                    canvasWidth > 0f && canvasHeight > 0f)
                {
                    Console.WriteLine($"info: layout canvas {canvasWidth:F2}in x {canvasHeight:F2}in");
                }
                if (diagnosticsSummary?.LayoutNodeCount is int layoutNodes && layoutNodes > 0)
                {
                    var moduleCount = diagnosticsSummary.LayoutModuleCount ?? 0;
                    var containerCount = diagnosticsSummary.LayoutContainerCount ?? 0;
                    Console.WriteLine($"info: layout nodes={layoutNodes} modules={moduleCount} containers={containerCount}");
                }
            }

            var summaryLine = string.Format(
                CultureInfo.InvariantCulture,
                "info: planner summary modules={0} segments={1} delta={2} splitModules={3} avgSegments/module={4:F2} pages={5} avgModules/page={6:F1} avgConnectors/page={7:F1} maxOccupancy={8:F1}% maxConnectors={9}",
                modulesValue,
                segmentsValue,
                deltaText,
                splitModulesValue,
                avgSegmentsPerModule,
                stats.PageCount,
                stats.AverageModulesPerPage,
                stats.AverageConnectorsPerPage,
                stats.MaxOccupancy,
                stats.MaxConnectors);

            if (diagnosticsSummary != null)
            {
                var annotations = new List<string>();
                if (diagnosticsSummary.LayerCount > 0)
                {
                    annotations.Add($"layers={diagnosticsSummary.LayerCount}");
                }
                if (diagnosticsSummary.LayerOverflowCount > 0)
                {
                    annotations.Add($"layerOverflow={diagnosticsSummary.LayerOverflowCount}");
                }
                if (diagnosticsSummary.LayerCrowdingCount > 0)
                {
                    annotations.Add($"layerCrowding={diagnosticsSummary.LayerCrowdingCount}");
                }
                if (diagnosticsSummary.BridgeCount > 0)
                {
                    annotations.Add($"bridges={diagnosticsSummary.BridgeCount}");
                }
                if (diagnosticsSummary.PageOverflowCount > 0)
                {
                    annotations.Add($"overflowPages={diagnosticsSummary.PageOverflowCount}");
                }
                if (diagnosticsSummary.LaneOverflowCount > 0)
                {
                    annotations.Add($"overflowLanes={diagnosticsSummary.LaneOverflowCount}");
                }
                if (diagnosticsSummary.ContainerOverflowCount > 0)
                {
                    annotations.Add($"overflowContainers={diagnosticsSummary.ContainerOverflowCount}");
                }
                if (diagnosticsSummary.PageCrowdingCount > 0)
                {
                    annotations.Add($"crowdedPages={diagnosticsSummary.PageCrowdingCount}");
                }
                if (diagnosticsSummary.LaneCrowdingCount > 0)
                {
                    annotations.Add($"crowdedLanes={diagnosticsSummary.LaneCrowdingCount}");
                }
                if (diagnosticsSummary.ConnectorOverLimitPageCount > 0)
                {
                    annotations.Add($"connectorOverLimitPages={diagnosticsSummary.ConnectorOverLimitPageCount}");
                }
                if (diagnosticsSummary.TruncatedNodeCount > 0)
                {
                    annotations.Add($"truncatedNodes={diagnosticsSummary.TruncatedNodeCount}");
                }
                if (diagnosticsSummary.SkippedModules.Count > 0)
                {
                    annotations.Add($"skippedModules={diagnosticsSummary.SkippedModules.Count}");
                }
                if (diagnosticsSummary.PartialRender)
                {
                    annotations.Add("partialRender=yes");
                }

                if (annotations.Count > 0)
                {
                    summaryLine = $"{summaryLine} {string.Join(" ", annotations)}";
                }
            }

            Console.WriteLine(summaryLine);

            if (diagnosticsSummary?.PartialRender == true)
            {
                Console.WriteLine("warning: render completed with mitigations; review docs/ErrorHandling.md for fallback guidance.");
            }

            if (diagnosticsSummary != null)
            {
                foreach (var page in diagnosticsSummary.Pages.OrderBy(p => p.PageNumber))
                {
                    var parts = new List<string>();
                    if (page.PlannedConnectors.HasValue)
                    {
                        parts.Add($"connectors={page.PlannedConnectors.Value}");
                    }
                    if (page.ConnectorLimit.HasValue && page.ConnectorLimit.Value > 0)
                    {
                        parts.Add($"limit={page.ConnectorLimit.Value}");
                        if (page.ConnectorOverLimit > 0)
                        {
                            parts.Add($"over=+{page.ConnectorOverLimit}");
                        }
                    }
                    if (page.ModuleCount.HasValue)
                    {
                        parts.Add($"modules={page.ModuleCount.Value}");
                    }
                    if (page.TruncatedNodeCount > 0)
                    {
                        parts.Add($"truncated={page.TruncatedNodeCount}");
                    }
                    if (page.LaneCrowdingErrors > 0)
                    {
                        parts.Add($"laneErrors={page.LaneCrowdingErrors}");
                    }
                    else if (page.LaneCrowdingWarnings > 0)
                    {
                        parts.Add($"laneWarnings={page.LaneCrowdingWarnings}");
                    }
                    if (page.HasPageOverflow)
                    {
                        parts.Add("pageOverflow=yes");
                    }
                    else if (page.HasPageCrowding)
                    {
                        parts.Add("pageCrowding=yes");
                    }
                    if (page.HasPartialRender)
                    {
                        parts.Add("partial=yes");
                    }

                    if (parts.Count == 0)
                    {
                        continue;
                    }

                    var severity = "info";
                    if (page.HasPageOverflow || page.LaneCrowdingErrors > 0)
                    {
                        severity = "error";
                    }
                    else if (page.ConnectorOverLimit > 0 || page.LaneCrowdingWarnings > 0 || page.HasPageCrowding || page.TruncatedNodeCount > 0 || page.HasPartialRender)
                    {
                        severity = "warning";
                    }

                    Console.WriteLine($"{severity}: page {page.PageNumber} {string.Join(" ", parts)}");
                }

                if (diagnosticsSummary.LayerCount > 0)
                {
                    foreach (var layer in diagnosticsSummary.Layers.OrderBy(l => l.LayerNumber))
                    {
                        var parts = new List<string>
                        {
                            $"modules={layer.ModuleCount}",
                            $"shapes={layer.ShapeCount}",
                            $"connectors={layer.ConnectorCount}"
                        };
                        if (layer.BridgesOut > 0)
                        {
                            parts.Add($"bridgesOut={layer.BridgesOut}");
                        }
                        if (layer.BridgesIn > 0)
                        {
                            parts.Add($"bridgesIn={layer.BridgesIn}");
                        }
                        if (layer.OverflowModules.Count > 0)
                        {
                            parts.Add($"overflowModules={string.Join(",", layer.OverflowModules)}");
                        }
                        var severity = "info";
                        if (layer.HardShapeLimitExceeded || layer.HardConnectorLimitExceeded || layer.OverflowModules.Count > 0)
                        {
                            severity = "error";
                        }
                        else if (layer.SoftShapeLimitExceeded || layer.SoftConnectorLimitExceeded)
                        {
                            severity = "warning";
                        }
                        Console.WriteLine($"{severity}: layer {layer.LayerNumber} {string.Join(" ", parts)}");
                    }
                }

                if (diagnosticsSummary.SkippedModules.Count > 0)
                {
                    var ordered = diagnosticsSummary.SkippedModules
                        .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    var preview = string.Join(", ", ordered.Take(5));
                    var remainder = ordered.Count - 5;
                    var suffix = remainder > 0 ? $" (+{remainder} more)" : string.Empty;
                    Console.WriteLine($"warning: skipped modules {preview}{suffix}");
                }
            }
        }

    }
}








