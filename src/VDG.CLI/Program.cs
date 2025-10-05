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

    internal static class Program
    {
        private const string CurrentSchemaVersion = "1.2";
        private static readonly string[] SupportedSchemaVersions = { "1.0", "1.1", CurrentSchemaVersion };
        private const double DefaultNodeWidth = 1.8;
        private const double DefaultNodeHeight = 1.0;
        private const double Margin = 1.0;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

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

                int index = 0;
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
                if (!string.IsNullOrWhiteSpace(routeModeOverride)) model.Metadata["layout.routing.mode"] = routeModeOverride!;
                if (!string.IsNullOrWhiteSpace(bundleByOverride)) model.Metadata["layout.routing.bundleBy"] = bundleByOverride!;
                if (bundleSepOverride.HasValue) model.Metadata["layout.routing.bundleSeparationIn"] = bundleSepOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (channelGapOverride.HasValue) model.Metadata["layout.routing.channels.gapIn"] = channelGapOverride.Value.ToString(CultureInfo.InvariantCulture);
                if (routeAroundOverride.HasValue) model.Metadata["layout.routing.routeAroundContainers"] = routeAroundOverride.Value.ToString();
                var layout = LayoutEngine.compute(model);
                EmitDiagnostics(model, layout, diagHeightOverride, diagLaneMaxOverride);

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
                    RunVisio(model, layout, outputPath);
                    DeleteErrorLog(outputPath);

                    Console.WriteLine($"Saved diagram: {outputPath}");
                    return ExitCodes.Ok;
                }
            }
            catch (UsageException uex)
            {
                Console.Error.WriteLine($"usage: {uex.Message}");
                PrintUsage();
                return ExitCodes.Usage;
            }
            catch (FileNotFoundException fnf)
            {
                WriteErrorLog(outputPath, fnf);
                Console.Error.WriteLine($"input file not found: {fnf.FileName}");
                return ExitCodes.InvalidInput;
            }
            catch (JsonException jex)
            {
                WriteErrorLog(outputPath, jex);
                Console.Error.WriteLine($"invalid diagram JSON: {jex.Message}");
                return ExitCodes.InvalidInput;
            }
            catch (InvalidDataException idex)
            {
                WriteErrorLog(outputPath, idex);
                Console.Error.WriteLine($"invalid diagram: {idex.Message}");
                return ExitCodes.InvalidInput;
            }
            catch (COMException comEx)
            {
                WriteErrorLog(outputPath, comEx);
                Console.Error.WriteLine($"Visio automation error: {comEx.Message}");
                return ExitCodes.VisioUnavailable;
            }
            catch (Exception ex)
            {
                WriteErrorLog(outputPath, ex);
                Console.Error.WriteLine($"fatal: {ex.Message}");
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
            Console.Error.WriteLine("  --page-width <in>       Page width (inches)");
            Console.Error.WriteLine("  --page-height <in>      Page height (inches)");
            Console.Error.WriteLine("  --page-margin <in>      Page margin (inches)");
            Console.Error.WriteLine("  --paginate <bool>       Enable pagination (future use)");
            // M3 routing options
            Console.Error.WriteLine("  --route-mode <orthogonal|straight>  Preferred connector style (M3)");
            Console.Error.WriteLine("  --bundle-by <lane|group|nodepair|none> Group edges for bundling (M3)");
            Console.Error.WriteLine("  --bundle-sep <in>       Gap between bundled edges (M3)");
            Console.Error.WriteLine("  --channel-gap <in>      Reserved corridor gap between lanes (M3)");
            Console.Error.WriteLine("  --route-around <true|false> Route around lane containers (M3)");
            Console.Error.WriteLine("  -h, --help              Show this help");
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

        private static void EmitDiagnostics(DiagramModel model, LayoutResult layout, double? pageHeightOverride = null, int? laneMaxOverride = null)
        {
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
                Console.WriteLine($"warning: {missingTier} node(s) have no 'tier'; placed into '{tiers[0]}' lane.");
            }

            if (unknown.Count > 0)
            {
                Console.WriteLine($"warning: {unknown.Count} unknown tier name(s) encountered: {string.Join(", ", unknown)}. Nodes were placed into '{tiers[0]}' lane.");
            }

            // Read diagnostics settings
            bool enabled = true;
            if (model.Metadata.TryGetValue("layout.diagnostics.enabled", out var enabledRaw))
            {
                if (bool.TryParse(enabledRaw, out var b)) enabled = b;
            }
            if (!enabled) return;

            // M3: minimal routing diagnostics
            var routeMode = (model.Metadata.TryGetValue("layout.routing.mode", out var rm) && !string.IsNullOrWhiteSpace(rm)) ? rm.Trim() : "orthogonal";
            Console.WriteLine($"info: routing mode: {routeMode}");
            Console.WriteLine($"info: connector count: {model.Edges.Count}");

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
                Console.WriteLine($"hint: layout height {height:F1}in exceeds threshold {pageThreshold:F1}in; consider pagination, reducing spacing, or vertical orientation.");
            }

            // Lane crowding hint
            foreach (var kv in laneCounts)
            {
                if (kv.Value > laneMax)
                {
                    Console.WriteLine($"hint: lane '{kv.Key}' contains {kv.Value} nodes (max {laneMax}); consider splitting the lane or paginating.");
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
                    Console.WriteLine($"info: pagination analysis — usable height {usable:F2}in, predicted pages {pageCount}.");

                    foreach (var kv in tierPageCounts)
                    {
                        if (kv.Value.Count == 0) continue;
                        var parts = kv.Value.OrderBy(p => p.Key).Select(p => $"{p.Key + 1}:{p.Value}");
                        Console.WriteLine($"info: lane '{kv.Key}' nodes per page => {string.Join(", ", parts)}");
                    }

                    int cross = 0;
                    foreach (var e in model.Edges)
                    {
                        if (!nodePage.TryGetValue(e.SourceId, out var sp) || !nodePage.TryGetValue(e.TargetId, out var tp)) continue;
                        if (sp != tp) cross++;
                    }
                    if (cross > 0)
                    {
                        Console.WriteLine($"info: cross-page edges: {cross}");
                    }

                    // Single node too tall to ever fit
                    foreach (var nl in layout.Nodes)
                    {
                        var hNode = (nl.Size.HasValue && nl.Size.Value.Height > 0) ? nl.Size.Value.Height : (float)DefaultNodeHeight;
                        if (hNode > usable)
                        {
                            Console.WriteLine($"warning: node '{nl.Id}' height {hNode:F2}in exceeds usable page height {usable:F2}in. Increase page height/margin or resize node.");
                        }
                    }
                }
            }

            // M3 diagnostics: bundle planning and straight-line crossing estimate
            try
            {
                var bundleBy = model.Metadata.TryGetValue("layout.routing.bundleBy", out var bb) ? (bb ?? "none").Trim() : "none";
                var nodeById = model.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);

                // Build centers from layout (node center points)
                var layoutMap = layout.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);
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
                    Console.WriteLine($"info: bundles planned: {groups.Count} groups; max bundle size: {maxBundle}");
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
                        Console.WriteLine($"info: channels gapIn={gapIn:F2}in; vertical corridors at X≈ {midsStr}");
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
                Console.WriteLine($"info: estimated straight-line crossings: {crossings}");

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
                        Console.WriteLine($"info: corridor staggering applied where available (lane bundles with >1 edges: {multi}).");

                    // After-routing plan estimate (layout-based)
                    int afterCross = 0; double totalLen = 0; int util = 0; int crossLane = 0; int missingCorridor = 0;
                    var tiers2 = GetOrderedTiers(model);
                    var nlMap = layout.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);
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
                    Console.WriteLine($"info: planned route crossings: {afterCross}; avg path length: {(model.Edges.Count > 0 ? totalLen / model.Edges.Count : 0):F2}in");
                    if (crossLane > 0)
                        Console.WriteLine($"info: channel utilization: {util}/{crossLane} cross-lane edges ({(100.0 * util / crossLane):F1}%)");
                    if (missingCorridor > 0)
                        Console.WriteLine($"warning: {missingCorridor} cross-lane edge(s) had no corridor available; check lane spans or channel gap settings.");
                }
                // Waypoints count
                var wpCount = model.Edges.Count(e => e.Metadata != null && e.Metadata.ContainsKey("edge.waypoints"));
                if (wpCount > 0) Console.WriteLine($"info: edges with explicit waypoints: {wpCount}");
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
                        Console.WriteLine($"warning: bundle separation {sepIn:F2}in may be ineffective for {impacted} end(s) due to small node height; consider reducing layout.routing.bundleSeparationIn or increasing node heights.");
                    }
                }
            }
            catch { }
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
            var nodeMap = model.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);

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
            var nodeMap = model.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);

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
            var nodeMap = model.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);
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
            var nodeMap = model.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);
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

        private static void RunVisio(DiagramModel model, LayoutResult layout, string outputPath)
        {
            dynamic? app = null;
            dynamic? documents = null;
            dynamic? document = null;
            dynamic? pages = null;
            dynamic? page = null;

            var placements = new Dictionary<string, NodePlacement>(StringComparer.OrdinalIgnoreCase);

            try
            {
                app = CreateVisioApplication();
                app.Visible = false;

                app.AlertResponse = 7;

                documents = app.Documents;
                document = documents.Add("");
                pages = document.Pages;
                page = pages[1];

                // Stamp document metadata (schema/generator/version/title)
                StampDocumentMetadata(model, document);

                if (layout.Nodes.Length == 0)
                {
                    throw new InvalidDataException("Layout produced zero nodes.");
                }

                if (ShouldPaginate(model, layout))
                {
                    DrawMultiPage(model, layout, document, pages);
                }
                else
                {
                    ComputePlacements(model, layout, placements, page);
                    DrawLaneContainers(model, layout, page);
                    DrawConnectors(model, placements, page);
                }

                document.SaveAs(outputPath);
            }
            finally
            {
                var pageCom = (object?)page;
                var pagesCom = (object?)pages;
                var documentCom = (object?)document;
                var documentsCom = (object?)documents;
                var appCom = (object?)app;

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

        private static void ComputePlacements(DiagramModel model, LayoutResult layout, IDictionary<string, NodePlacement> placements, dynamic page)
        {
            dynamic visioPage = page ?? throw new COMException("Visio page was not created.");
            var nodeMap = model.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);

            var (minLeft, minBottom, maxRight, maxTop) = ComputeLayoutBounds(layout);
            var margin = GetPageMargin(model) ?? Margin;
            var titleHeight = GetTitleHeight(model);
            var offsetX = margin - minLeft;
            var offsetY = margin + titleHeight - minBottom;
            var pageWidth = Math.Max(1.0, (maxRight - minLeft) + (margin * 2));
            var pageHeight = Math.Max(1.0, (maxTop - minBottom) + (margin * 2) + titleHeight);

            var pageWidthOverride = GetPageWidth(model);
            var pageHeightOverride = GetPageHeight(model);
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

                dynamic shape = visioPage.DrawRectangle(left, bottom, right, top);
                shape.Text = node.Label;
                ApplyNodeStyle(node, shape);

                placements[nodeLayout.Id] = new NodePlacement(shape, left, bottom, width, height);
            }
        }

        private static bool IsFinite(double v) => !(double.IsNaN(v) || double.IsInfinity(v));

        private static void DrawLaneContainers(DiagramModel model, LayoutResult layout, dynamic page)
        {
            dynamic visioPage = page ?? throw new COMException("Visio page was not created.");
            var nodeMap = model.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);
            var tiers = GetOrderedTiers(model);

            var (minLeft, minBottom, maxRight, maxTop) = ComputeLayoutBounds(layout);
            var margin = GetPageMargin(model) ?? Margin;
            var titleHeight = GetTitleHeight(model);
            var offsetX = margin - minLeft;
            var offsetY = margin + titleHeight - minBottom;

            var padding = 0.3; // inches around lane

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
                    TrySetFormula(lane, "FillForegnd", "RGB(245,248,250)");
                    TrySetFormula(lane, "LinePattern", "2");
                    TrySetFormula(lane, "LineColor", "RGB(200,200,200)");
                    try { lane.SendToBack(); } catch { }
                    ReleaseCom(lane);
                }
                catch { }
            }
        }

        private static void DrawConnectors(DiagramModel model, IDictionary<string, NodePlacement> placements, dynamic page)
        {
            dynamic visioPage = page ?? throw new COMException("Visio page was not created.");
            var routeMode = (model.Metadata.TryGetValue("layout.routing.mode", out var rm) && !string.IsNullOrWhiteSpace(rm)) ? rm.Trim() : "orthogonal";
            var useOrthogonal = !string.Equals(routeMode, "straight", StringComparison.OrdinalIgnoreCase);
            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);
            var nodeMap = model.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);
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
            foreach (var edge in model.Edges)
            {
                if (!placements.TryGetValue(edge.SourceId, out var source) ||
                    !placements.TryGetValue(edge.TargetId, out var target))
                {
                    continue;
                }

                if (useOrthogonal)
                {
                    dynamic connector = null;
                    try
                    {
                        var app = visioPage.Application;
                        connector = visioPage.Drop(app.ConnectorToolDataObject, source.CenterX, source.CenterY);
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
                                if (source.CenterX <= target.CenterX) { srcSide = "right"; dstSide = "left"; }
                                else { srcSide = "left"; dstSide = "right"; }
                            }
                            else if (sTier < tTier) { srcSide = "right"; dstSide = "left"; }
                            else { srcSide = "left"; dstSide = "right"; }
                        }

                        var (sxr, syr) = SideToRel(srcSide);
                        var (dxr, dyr) = SideToRel(dstSide);
                        if (bundles.TryGetValue(edge.Id, out var binfo))
                        {
                            (sxr, syr) = ApplyBundleOffsetRel(srcSide, (sxr, syr), source, binfo, bundleSepIn);
                            (dxr, dyr) = ApplyBundleOffsetRel(dstSide, (dxr, dyr), target, binfo, bundleSepIn);
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
                            var (sx, sy) = ToAbsAttach(source, srcSide, sxr, syr);
                            var (tx, ty) = ToAbsAttach(target, dstSide, dxr, dyr);
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
                                ApplyEdgeStyle(edge, pl);
                                try { pl.SendToBack(); } catch { }
                                ReleaseCom(pl);
                                usedPolyline = true;
                            }
                            catch { usedPolyline = false; }
                        }
                        if (!usedPolyline && cx.HasValue)
                        {
                            var (sx, sy) = ToAbsAttach(source, srcSide, sxr, syr);
                            var (tx, ty) = ToAbsAttach(target, dstSide, dxr, dyr);
                            // Stagger vertical corridor by lane bundle index to reduce overlaps
                            var corridorX = cx.Value;
                            var laneBundles = BuildBundleIndex(model, "lane");
                            if (laneBundles.TryGetValue(edge.Id, out var cinfo) && channelsGapValue > 0)
                            {
                                var center = (cinfo.size - 1) / 2.0;
                                var delta = (cinfo.index - center) * Math.Min(channelsGapValue * 0.4, 0.4);
                                corridorX += delta;
                            }
                            try
                            {
                                var arr = new double[] { sx, sy, corridorX, sy, corridorX, ty, tx, ty };
                                dynamic pl = visioPage.DrawPolyline(arr, 0);
                                if (!string.IsNullOrWhiteSpace(edge.Label))
                                {
                                    // Place detached label near middle segment
                                    var mx = (arr[2] + arr[4]) / 2.0; var my = (arr[3] + arr[5]) / 2.0;
                                    var off = 0.15; if (edge.Metadata != null && edge.Metadata.TryGetValue("edge.label.offsetIn", out var offRaw)) { double.TryParse(offRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out off); }
                                    DrawLabelBox(visioPage, edge.Label!, mx, my, 0.0, off);
                                }
                                if (edge.Directed) TrySetFormula(pl, "EndArrow", "5"); else TrySetFormula(pl, "EndArrow", "0");
                                ApplyEdgeStyle(edge, pl);
                                try { pl.SendToBack(); } catch { }
                                ReleaseCom(pl);
                                usedPolyline = true;
                            }
                            catch { usedPolyline = false; }
                        }
                        if (!usedPolyline)
                        {
                            connector = visioPage.Drop(app.ConnectorToolDataObject, source.CenterX, source.CenterY);
                            // Prefer GlueToPos if available; fall back to PinX
                            try { connector.CellsU["BeginX"].GlueToPos(((dynamic)source.Shape), sxr, syr); }
                            catch { connector.CellsU["BeginX"].GlueTo(((dynamic)source.Shape).CellsU["PinX"]); }
                            try { connector.CellsU["EndX"].GlueToPos(((dynamic)target.Shape), dxr, dyr); }
                            catch { connector.CellsU["EndX"].GlueTo(((dynamic)target.Shape).CellsU["PinX"]); }

                            if (!string.IsNullOrWhiteSpace(edge.Label))
                            {
                                connector.Text = edge.Label;
                            }

                            if (edge.Directed) TrySetFormula(connector, "EndArrow", "5"); else TrySetFormula(connector, "EndArrow", "0");
                            // Prefer right-angle routing
                            TrySetFormula(connector, "Routestyle", "16");
                            try { TrySetFormula(connector, "LineRouteExt", "2"); } catch { }
                            ApplyEdgeStyle(edge, connector);
                            try { connector.SendToBack(); } catch { }
                        }
                    }
                    finally
                    {
                        if (connector != null) ReleaseCom(connector);
                    }
                }
                else
                {
                    dynamic line = visioPage.DrawLine(source.CenterX, source.CenterY, target.CenterX, target.CenterY);
                    if (!string.IsNullOrWhiteSpace(edge.Label)) line.Text = edge.Label;
                    if (edge.Directed) TrySetFormula(line, "EndArrow", "5"); else TrySetFormula(line, "EndArrow", "0");
                    ApplyEdgeStyle(edge, line);
                    ReleaseCom(line);
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
            public NodePlacement(object shape, double left, double bottom, double width, double height)
            {
                Shape = shape;
                Left = left;
                Bottom = bottom;
                Width = width;
                Height = height;
            }

            public object Shape { get; }
            public double Left { get; }
            public double Bottom { get; }
            public double Width { get; }
            public double Height { get; }
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

        private sealed class LayoutDto
        {
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
        }

        private sealed class DiagnosticsDto
        {
            [JsonPropertyName("enabled")]
            public bool? Enabled { get; set; }

            [JsonPropertyName("pageHeightThresholdIn")]
            public double? PageHeightThresholdIn { get; set; }

            [JsonPropertyName("laneMaxNodes")]
            public int? LaneMaxNodes { get; set; }
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

        private static void DrawMultiPage(DiagramModel model, LayoutResult layout, dynamic document, dynamic pages)
        {
            var pageWidth = GetPageWidth(model) ?? 11.0;
            var pageHeight = GetPageHeight(model) ?? 8.5;
            var margin = GetPageMargin(model) ?? Margin;
            var title = GetTitleHeight(model);
            var usable = pageHeight - (2 * margin) - title;
            if (usable <= 0) usable = pageHeight; // fallback

            var (minLeft, minBottom, maxRight, maxTop) = ComputeLayoutBounds(layout);

            // Compute page index for each node
            var nodePage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int maxPage = 0;
            foreach (var nl in layout.Nodes)
            {
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
                dynamic page = (pi == 0) ? pages[1] : pages.Add();
                try { page.NameU = $"Layered {pi + 1}"; } catch { }

                TrySetResult(page.PageSheet.CellsU["PageWidth"], pageWidth);
                TrySetResult(page.PageSheet.CellsU["PageHeight"], pageHeight);

                var placements = new Dictionary<string, NodePlacement>(StringComparer.OrdinalIgnoreCase);
                ComputePlacementsForPage(model, layout, placements, page, pi, minLeft, minBottom, usable, margin, title);
                DrawLaneContainersForPage(model, layout, page, pi, pageCount, minLeft, minBottom, usable, margin, title);
                placementsPerPage[pi] = placements;
            }

            DrawConnectorsPaged(model, layout, placementsPerPage, pages);
        }

        private static void ComputePlacementsForPage(DiagramModel model, LayoutResult layout, IDictionary<string, NodePlacement> placements, dynamic page, int pageIndex, double minLeft, double minBottom, double usableHeight, double margin, double title)
        {
            dynamic visioPage = page ?? throw new COMException("Visio page was not created.");
            var nodeMap = model.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);

            foreach (var nl in layout.Nodes)
            {
                var yNorm = nl.Position.Y - (float)minBottom;
                var idx = (int)Math.Floor(yNorm / (float)usableHeight);
                if (idx != pageIndex) continue;

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

                dynamic shape = visioPage.DrawRectangle(left, bottom, right, top);
                shape.Text = node.Label;
                ApplyNodeStyle(node, shape);
                placements[nl.Id] = new NodePlacement(shape, left, bottom, width, height);
            }
        }

        private static void DrawLaneContainersForPage(DiagramModel model, LayoutResult layout, dynamic page, int pageIndex, int pageCount, double minLeft, double minBottom, double usableHeight, double margin, double title)
        {
            dynamic visioPage = page ?? throw new COMException("Visio page was not created.");
            var nodeMap = model.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);
            var tiers = GetOrderedTiers(model);

            var padding = 0.3; // inches
            foreach (var tier in tiers)
            {
                double tMinL = double.MaxValue, tMinB = double.MaxValue, tMaxR = double.MinValue, tMaxT = double.MinValue;
                foreach (var nl in layout.Nodes)
                {
                    var yNorm = nl.Position.Y - (float)minBottom;
                    var idx = (int)Math.Floor(yNorm / (float)usableHeight);
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
                    TrySetFormula(lane, "FillForegnd", "RGB(245,248,250)");
                    TrySetFormula(lane, "LinePattern", "2");
                    TrySetFormula(lane, "LineColor", "RGB(200,200,200)");
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
        }

        private static void DrawConnectorsPaged(DiagramModel model, LayoutResult layout, Dictionary<int, Dictionary<string, NodePlacement>> perPage, dynamic pages)
        {
            var pageCount = perPage.Keys.Count == 0 ? 1 : perPage.Keys.Max() + 1;
            var bounds = ComputeLayoutBounds(layout);
            var margin = GetPageMargin(model) ?? Margin;
            var title = GetTitleHeight(model);
            var pageHeight = GetPageHeight(model) ?? 0.0;
            var usable = pageHeight > 0 ? pageHeight - (2 * margin) - title : double.PositiveInfinity;
            var routeMode = (model.Metadata.TryGetValue("layout.routing.mode", out var rm) && !string.IsNullOrWhiteSpace(rm)) ? rm.Trim() : "orthogonal";
            var useOrthogonal = !string.Equals(routeMode, "straight", StringComparison.OrdinalIgnoreCase);
            var tiers = GetOrderedTiers(model);
            var tiersSet = new HashSet<string>(tiers, StringComparer.OrdinalIgnoreCase);
            var nodeMap = model.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);
            var bundleByRaw = model.Metadata.TryGetValue("layout.routing.bundleBy", out var bb) ? (bb ?? "none").Trim() : "none";
            double bundleSepIn = 0.12;
            if (model.Metadata.TryGetValue("layout.routing.bundleSeparationIn", out var bsep) && double.TryParse(bsep, NumberStyles.Float, CultureInfo.InvariantCulture, out var bsv) && bsv >= 0) bundleSepIn = bsv;
            var channelGapIn = (model.Metadata.TryGetValue("layout.routing.channels.gapIn", out var cgap) && double.TryParse(cgap, NumberStyles.Float, CultureInfo.InvariantCulture, out var cg)) ? cg : 0.0;
            var effectiveBundleBy = (string.Equals(bundleByRaw, "none", StringComparison.OrdinalIgnoreCase) && channelGapIn > 0.0) ? "lane" : bundleByRaw;
            var bundles = BuildBundleIndex(model, effectiveBundleBy);
            for (int pi = 0; pi < pageCount; pi++)
            {
                foreach (var edge in model.Edges)
                {
                    dynamic page = pages[pi + 1];
                    if (!perPage.TryGetValue(pi, out var placementsOnPage)) placementsOnPage = new Dictionary<string, NodePlacement>();

                    var hasSrc = placementsOnPage.TryGetValue(edge.SourceId, out var src);
                    var hasDst = placementsOnPage.TryGetValue(edge.TargetId, out var dst);

                    int sp = 0, tp = 0;
                    if (usable > 0 && IsFinite(usable))
                    {
                        var sNode = layout.Nodes.FirstOrDefault(n => string.Equals(n.Id, edge.SourceId, StringComparison.OrdinalIgnoreCase));
                        var tNode = layout.Nodes.FirstOrDefault(n => string.Equals(n.Id, edge.TargetId, StringComparison.OrdinalIgnoreCase));
                        if (sNode.Id != null) { var y = sNode.Position.Y - (float)bounds.minBottom; sp = (int)Math.Floor(y / (float)usable); }
                        if (tNode.Id != null) { var y2 = tNode.Position.Y - (float)bounds.minBottom; tp = (int)Math.Floor(y2 / (float)usable); }
                    }

                    if (hasSrc && hasDst)
                    {
                        if (useOrthogonal)
                        {
                            dynamic connector = null;
                            try
                            {
                                var app = page.Application;
                                connector = page.Drop(app.ConnectorToolDataObject, src.CenterX, src.CenterY);
                                // Corridor-aware: choose side based on relative tier positions (same logic as single-page)
                                string GetTier2(VDG.Core.Models.Node n)
                                {
                                    var t = !string.IsNullOrWhiteSpace(n.Tier) ? n.Tier! : (n.Metadata.TryGetValue("tier", out var tv) ? tv : tiers.First());
                                    return tiersSet.Contains(t) ? t : tiers.First();
                                }
                                string srcSide = "right";
                                string dstSide = "left";
                                if (nodeMap.TryGetValue(edge.SourceId, out var srcNode) && nodeMap.TryGetValue(edge.TargetId, out var dstNode))
                                {
                                    var sTier = Array.IndexOf(tiers, GetTier2(srcNode));
                                    var tTier = Array.IndexOf(tiers, GetTier2(dstNode));
                                    if (sTier == tTier)
                                    {
                                        if (src.CenterX <= dst.CenterX) { srcSide = "right"; dstSide = "left"; }
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
                                if (bundles.TryGetValue(edge.Id, out var binfo))
                                {
                                    (sxr, syr) = ApplyBundleOffsetRel(srcSide, (sxr, syr), src, binfo, bundleSepIn);
                                    (dxr, dyr) = ApplyBundleOffsetRel(dstSide, (dxr, dyr), dst, binfo, bundleSepIn);
                                }
                                bool usedPolyline = false;
                                double channelsGapValue = 0.0; string? channelsGapStr;
                                var hasKey = model.Metadata.TryGetValue("layout.routing.channels.gapIn", out channelsGapStr);
                                if (hasKey && !string.IsNullOrWhiteSpace(channelsGapStr)) double.TryParse(channelsGapStr, NumberStyles.Float, CultureInfo.InvariantCulture, out channelsGapValue);
                                var hasChannels = channelsGapValue > 0.0;
                                var cx = hasChannels ? CorridorXForEdge(model, placementsOnPage, edge.SourceId, edge.TargetId) : null;
                                // Waypoints override first
                                if (!usedPolyline && TryGetWaypointsFromMetadata(edge, out var wps))
                                {
                                    var (sx, sy) = ToAbsAttach(src, srcSide, sxr, syr);
                                    var (tx, ty) = ToAbsAttach(dst, dstSide, dxr, dyr);
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
                                        ReleaseCom(pl); usedPolyline = true;
                                    }
                                    catch { usedPolyline = false; }
                                }
                                if (!usedPolyline && cx.HasValue)
                                {
                                    var (sx, sy) = ToAbsAttach(src, srcSide, sxr, syr);
                                    var (tx, ty) = ToAbsAttach(dst, dstSide, dxr, dyr);
                                    // Stagger vertical corridor by lane bundle index to reduce overlaps (paged)
                                    var corridorX = cx.Value;
                                    var laneBundles = BuildBundleIndex(model, "lane");
                                    if (laneBundles.TryGetValue(edge.Id, out var cinfo) && channelsGapValue > 0)
                                    {
                                        var center = (cinfo.size - 1) / 2.0;
                                        var delta = (cinfo.index - center) * Math.Min(channelsGapValue * 0.4, 0.4);
                                        corridorX += delta;
                                    }
                                    try
                                    {
                                        var arr = new double[] { sx, sy, corridorX, sy, corridorX, ty, tx, ty };
                                        dynamic pl = page.DrawPolyline(arr, 0);
                                        if (!string.IsNullOrWhiteSpace(edge.Label))
                                        {
                                            var mx = (arr[2] + arr[4]) / 2.0; var my = (arr[3] + arr[5]) / 2.0;
                                            var off = 0.15; if (edge.Metadata != null && edge.Metadata.TryGetValue("edge.label.offsetIn", out var offRaw)) { double.TryParse(offRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out off); } DrawLabelBox(page, edge.Label!, mx, my, 0.0, off);
                                        }
                                        if (edge.Directed) TrySetFormula(pl, "EndArrow", "5"); else TrySetFormula(pl, "EndArrow", "0");
                                        ApplyEdgeStyle(edge, pl); try { pl.SendToBack(); } catch { }
                                        ReleaseCom(pl); usedPolyline = true;
                                    }
                                    catch { usedPolyline = false; }
                                }
                                if (!usedPolyline)
                                {
                                    try { connector.CellsU["BeginX"].GlueToPos(((dynamic)src.Shape), sxr, syr); }
                                    catch { connector.CellsU["BeginX"].GlueTo(((dynamic)src.Shape).CellsU["PinX"]); }
                                    try { connector.CellsU["EndX"].GlueToPos(((dynamic)dst.Shape), dxr, dyr); }
                                    catch { connector.CellsU["EndX"].GlueTo(((dynamic)dst.Shape).CellsU["PinX"]); }
                                    if (!string.IsNullOrWhiteSpace(edge.Label)) connector.Text = edge.Label;
                                    if (edge.Directed) TrySetFormula(connector, "EndArrow", "5"); else TrySetFormula(connector, "EndArrow", "0");
                                    TrySetFormula(connector, "Routestyle", "16");
                                    try { TrySetFormula(connector, "LineRouteExt", "2"); } catch { }
                                    ApplyEdgeStyle(edge, connector);
                                    try { connector.SendToBack(); } catch { }
                                }
                            }
                            finally { if (connector != null) ReleaseCom(connector); }
                        }
                        else
                        {
                            dynamic line = page.DrawLine(src.CenterX, src.CenterY, dst.CenterX, dst.CenterY);
                            if (!string.IsNullOrWhiteSpace(edge.Label)) line.Text = edge.Label;
                            if (edge.Directed) TrySetFormula(line, "EndArrow", "5"); else TrySetFormula(line, "EndArrow", "0");
                            ApplyEdgeStyle(edge, line); ReleaseCom(line);
                        }
                    }
                    else if (hasSrc && !hasDst)
                    {
                        var markerLeft = src.Left + src.Width + 0.1; var markerBottom = src.CenterY - 0.1;
                        dynamic m = page.DrawRectangle(markerLeft, markerBottom, markerLeft + 1.4, markerBottom + 0.35);
                        m.Text = (usable > 0 && IsFinite(usable)) ? $"to {edge.TargetId} (p{tp + 1})" : $"to {edge.TargetId}";
                        TrySetFormula(m, "LinePattern", "2"); ReleaseCom(m);
                    }
                    else if (!hasSrc && hasDst)
                    {
                        var markerRight = dst.Left - 0.1; var markerLeft2 = markerRight - 0.8; var markerBottom2 = dst.CenterY - 0.1;
                        dynamic m = page.DrawRectangle(markerLeft2 - 0.6, markerBottom2, markerRight, markerBottom2 + 0.35);
                        m.Text = (usable > 0 && IsFinite(usable)) ? $"from {edge.SourceId} (p{sp + 1})" : $"from {edge.SourceId}";
                        TrySetFormula(m, "LinePattern", "2"); ReleaseCom(m);
                    }
                }
            }
        }

    }
}

