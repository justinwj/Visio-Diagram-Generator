using System;
using System.Collections.Generic;
using System.Linq;
using VDG.CLI;
using VDG.Core.Models;
using VisioDiagramGenerator.Algorithms;
using Xunit;

namespace VDG.CLI.Tests
{
    public sealed class SegmentationTests
    {
        [Fact]
        public void Oversized_module_is_split_into_sequential_parts()
        {
            var (dataset, overrides, metrics, totalConnectors, totalNodes) = BuildMegaModuleDataset(nodeCount: 60, connectorFanOut: true);

            var parts = dataset.Modules
                .Where(m => m.ModuleId.StartsWith("MegaProc#part", StringComparison.Ordinal))
                .OrderBy(m => m.ModuleId, StringComparer.Ordinal)
                .ToArray();

            Assert.True(parts.Length > 1);
            Assert.InRange(parts.Length, 2, 16); // capped by ModuleSplitMaxSegments and node-based limit

            for (int i = 0; i < parts.Length; i++)
            {
                Assert.Equal($"MegaProc#part{i + 1}", parts[i].ModuleId);
            }

            Assert.Equal(totalNodes, parts.Sum(p => p.NodeCount));
            Assert.Equal(totalConnectors, parts.Sum(p => p.ConnectorCount));

            Assert.Equal(totalNodes, overrides.Count);
            foreach (var kvp in overrides)
            {
                Assert.StartsWith("MegaProc#part", kvp.Value, StringComparison.Ordinal);
            }

            Assert.Equal("MegaProc", dataset.Modules.First(m => m.ModuleId.StartsWith("MegaProc#part", StringComparison.Ordinal)).ModuleId.Split('#')[0]);
            Assert.Equal(1, metrics.SplitModuleCount);
            Assert.True(metrics.SegmentCount > metrics.OriginalModuleCount);
        }

        [Fact]
        public void Planner_distributes_module_parts_across_pages()
        {
            var (dataset, _, _, totalConnectors, totalNodes) = BuildMegaModuleDataset(nodeCount: 75, connectorFanOut: false);

            var options = new PageSplitOptions
            {
                MaxConnectors = 400,
                MaxOccupancyPercent = 110.0,
                LaneSplitAllowed = false,
                MaxPageHeightIn = 9.0,
                MaxModulesPerPage = 10,
                HeightSlackPercent = 25.0
            };

            var plans = PagingPlanner.computePages(options, dataset);

            Assert.True(plans.Length > 1);

            var flattenedModules = plans.SelectMany(p => p.Modules ?? Array.Empty<string>()).ToArray();
            Assert.Equal(dataset.Modules.Length, flattenedModules.Length);
            Assert.Equal(dataset.Modules.Select(m => m.ModuleId).OrderBy(id => id, StringComparer.Ordinal),
                         flattenedModules.OrderBy(id => id, StringComparer.Ordinal));
            Assert.Equal(totalConnectors, plans.Sum(p => p.Connectors));
        }

        [Fact]
        public void Planner_summary_reports_segmentation_and_pages()
        {
            var plans = new[]
            {
                new PagePlan(0, new[] { "MegaProc#part1", "Other" }, 10, 12, 180.0),
                new PagePlan(1, new[] { "MegaProc#part2" }, 5, 6, 250.0)
            };

            var summary = Program.InvokePlannerSummaryForTests(plans, originalModules: 2, segmentCount: 3, splitModuleCount: 1);

            Assert.Contains("modules=2", summary);
            Assert.Contains("segments=3", summary);
            Assert.Contains("delta=+1", summary);
            Assert.Contains("splitModules=1", summary);
            Assert.Contains("avgSegments/module=1.50", summary);
            Assert.Contains("pages=2", summary);
            Assert.Contains("avgModules/page=1.5", summary);
            Assert.Contains("avgConnectors/page=7.5", summary);
            Assert.Contains("maxOccupancy=250.0%", summary);
            Assert.Contains("maxConnectors=10", summary);
        }

        private static (DiagramDataset Dataset, Dictionary<string, string> Overrides, Program.PlannerMetrics Metrics, int TotalConnectors, int TotalNodes) BuildMegaModuleDataset(int nodeCount, bool connectorFanOut)
        {
            var nodes = new List<Node>(capacity: nodeCount);
            var layouts = new List<NodeLayout>(capacity: nodeCount);
            var edges = new List<Edge>();

            for (int i = 0; i < nodeCount; i++)
            {
                var node = new Node($"n{i}", $"Node {i}");
                node.Metadata["moduleId"] = "MegaProc";
                nodes.Add(node);

                layouts.Add(new NodeLayout
                {
                    Id = node.Id,
                    Position = new PointF { X = 0f, Y = i * 0.8f },
                    Size = new Nullable<Size>(new Size(1.0f, 0.6f))
                });
            }

            for (int i = 0; i < nodeCount - 1; i++)
            {
                var edge = connectorFanOut
                    ? new Edge($"e{i}", nodes[i].Id, nodes[i + 1].Id)
                    : new Edge($"e{i}", nodes[0].Id, nodes[i + 1].Id);
                edges.Add(edge);
            }

            var model = new DiagramModel(nodes, edges);
            model.Metadata["layout.page.heightIn"] = "11";
            model.Metadata["layout.page.marginIn"] = "1";

            var layout = new LayoutResult
            {
                Nodes = layouts.ToArray(),
                Edges = Array.Empty<EdgeRoute>()
            };

            var (dataset, overrides, metrics) = Program.BuildPagingDatasetForTests(model, layout);

            return (dataset, overrides, metrics, edges.Count, nodeCount);
        }
    }
}
