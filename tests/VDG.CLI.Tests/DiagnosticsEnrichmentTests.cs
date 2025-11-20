using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VDG.CLI;
using VDG.Core.Models;
using VisioDiagramGenerator.Algorithms;
using Xunit;

namespace VDG.CLI.Tests
{
    public sealed class DiagnosticsEnrichmentTests
    {
        [Fact]
        public void Diagnostics_report_connector_overlimit_and_truncations()
        {
            var model = new DiagramModel();
            model.Metadata["layout.outputMode"] = "view";
            model.Metadata["layout.page.heightIn"] = "5.0";
            model.Metadata["layout.page.marginIn"] = "0.5";
            model.Metadata["layout.page.paginate"] = "true";

            var layouts = new List<NodeLayout>();
            for (int i = 0; i < 3; i++)
            {
                var node = new Node($"n{i}", $"Node {i}");
                node.Metadata["moduleId"] = "ModuleA";
                model.Nodes.Add(node);

                layouts.Add(new NodeLayout
                {
                    Id = node.Id,
                    Position = new PointF { X = 0f, Y = i * 0.5f },
                    Size = new Nullable<Size>(new Size(1.5f, 4.5f))
                });
            }

            // Create multiple connectors so planner metrics have a non-trivial count.
            model.Edges.Add(new Edge("e0", "n0", "n1"));
            model.Edges.Add(new Edge("e1", "n1", "n2"));
            model.Edges.Add(new Edge("e2", "n0", "n2"));
            model.Edges.Add(new Edge("e3", "n2", "n0"));

            var layout = new LayoutResult
            {
                Nodes = layouts.ToArray(),
                Edges = Array.Empty<EdgeRoute>()
            };

            var plan = new LayoutPlan
            {
                OutputMode = "view",
                CanvasWidth = 6f,
                CanvasHeight = 4f,
                Nodes = layout.Nodes,
                Edges = layout.Edges,
                Containers = Array.Empty<ContainerLayout>(),
                PageContexts = Array.Empty<PageContextPlan>(),
                Pages = Array.Empty<PagePlan>(),
                Stats = new LayoutStats
                {
                    NodeCount = layout.Nodes.Length,
                    ConnectorCount = model.Edges.Count,
                    ModuleCount = 1,
                    ContainerCount = 0,
                    ModuleIds = new[] { "ModuleA" },
                    Overflows = Array.Empty<LayoutOverflow>()
                }
            };

            var (dataset, _, metrics) = Program.BuildPagingDatasetForTests(model, layout);
            var primaryModule = Assert.Single(dataset.Modules);

            var plannedConnectors = primaryModule.ConnectorCount > 0 ? primaryModule.ConnectorCount : 3;
            plannedConnectors += 1; // force an over-limit condition
            var connectorLimit = Math.Max(1, plannedConnectors - 2);
            var pagePlans = new[]
            {
                new PagePlan(
                    0,
                    new[] { primaryModule.ModuleId },
                    plannedConnectors,
                    primaryModule.NodeCount,
                    120.0)
            };

            var options = new PageSplitOptions
            {
                MaxConnectors = connectorLimit,
                MaxOccupancyPercent = 150.0,
                LaneSplitAllowed = false,
                MaxPageHeightIn = 5.0,
                MaxModulesPerPage = 10,
                HeightSlackPercent = 0.0
            };

            var (_, diagnostics) = Program.RunDiagnosticsForTests(model, layout, pagePlans, options, metrics, dataset, layoutPlan: plan);

            Assert.Equal(1, diagnostics.ConnectorOverLimitPageCount);
            Assert.True(diagnostics.PartialRender);
            Assert.Equal(diagnostics.TruncatedNodeCount, diagnostics.Pages.Sum(p => p.TruncatedNodeCount));
            Assert.True(diagnostics.TruncatedNodeCount > 0);

            var pageDetail = Assert.Single(diagnostics.Pages);
            Assert.Equal(1, pageDetail.PageNumber);
            Assert.Equal(plannedConnectors, pageDetail.PlannedConnectors);
            Assert.Equal(connectorLimit, pageDetail.ConnectorLimit);
            Assert.Equal(plannedConnectors - connectorLimit, pageDetail.ConnectorOverLimit);
            Assert.True(pageDetail.HasPartialRender);
            Assert.True(pageDetail.TruncatedNodeCount > 0);

            var summaryText = Program.InvokePlannerSummaryForTests(
                pagePlans,
                metrics.OriginalModuleCount,
                metrics.SegmentCount,
                metrics.SplitModuleCount,
                metrics.AverageSegmentsPerModule,
                diagnostics);

            Assert.Contains("connectorOverLimitPages=1", summaryText);
            Assert.Contains("page 1", summaryText);
            Assert.Contains("truncated=", summaryText);
            Assert.Contains("outputMode=view", summaryText);
            Assert.Equal((float?)plan.CanvasWidth, diagnostics.LayoutCanvasWidth);
            Assert.Equal((float?)plan.CanvasHeight, diagnostics.LayoutCanvasHeight);
            Assert.Equal(plan.Stats.NodeCount, diagnostics.LayoutNodeCount);
            Assert.Equal(plan.Stats.ModuleCount, diagnostics.LayoutModuleCount);
            Assert.Equal(plan.Stats.ContainerCount, diagnostics.LayoutContainerCount);

            var tempJson = Path.Combine(Path.GetTempPath(), $"vdg_diag_{Guid.NewGuid():N}.json");
            model.Metadata["layout.diagnostics.emitJson"] = "true";
            model.Metadata["layout.diagnostics.jsonPath"] = tempJson;

            try
            {
                Program.RunDiagnosticsForTests(model, layout, pagePlans, options, metrics, dataset, layoutPlan: plan);
                Assert.True(File.Exists(tempJson), "Diagnostics JSON should be emitted when requested.");

                using var json = JsonDocument.Parse(File.ReadAllText(tempJson));
                var metricsElement = json.RootElement.GetProperty("Metrics");
                Assert.Equal("view", metricsElement.GetProperty("LayoutOutputMode").GetString());
                Assert.Equal((double)plan.CanvasWidth, metricsElement.GetProperty("LayoutCanvasWidth").GetDouble(), 3);
                Assert.Equal((double)plan.CanvasHeight, metricsElement.GetProperty("LayoutCanvasHeight").GetDouble(), 3);
                Assert.Equal(plan.Stats.NodeCount, metricsElement.GetProperty("LayoutNodeCount").GetInt32());
                Assert.Equal(plan.Stats.ModuleCount, metricsElement.GetProperty("LayoutModuleCount").GetInt32());
                Assert.Equal(plan.Stats.ContainerCount, metricsElement.GetProperty("LayoutContainerCount").GetInt32());
                Assert.Equal(1, metricsElement.GetProperty("ConnectorOverLimitPageCount").GetInt32());
                Assert.True(metricsElement.GetProperty("TruncatedNodeCount").GetInt32() > 0);

                var pageElement = metricsElement.GetProperty("Pages")[0];
                Assert.Equal(connectorLimit, pageElement.GetProperty("ConnectorLimit").GetInt32());
                Assert.Equal(plannedConnectors - connectorLimit, pageElement.GetProperty("ConnectorOverLimit").GetInt32());
                Assert.True(pageElement.GetProperty("TruncatedNodes").GetInt32() > 0);
            }
            finally
            {
                if (File.Exists(tempJson))
                {
                    File.Delete(tempJson);
                }
            }
        }
    }
}
