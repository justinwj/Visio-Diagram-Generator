using System;
using System.Collections.Generic;
using System.Linq;
using VDG.CLI;
using VDG.Core.Models;
using VisioDiagramGenerator.Algorithms;
using Xunit;

namespace VDG.CLI.Tests
{
    public sealed class MultiPageTraversalTests
    {
        [Fact]
        public void Planner_creates_multiple_pages_when_layout_span_exceeds_page_height()
        {
            var model = new DiagramModel();
            model.Metadata["layout.page.heightIn"] = "5.0";
            model.Metadata["layout.page.marginIn"] = "0.5";
            model.Metadata["layout.spacing.vertical"] = "0.5";
            model.Metadata["layout.page.paginate"] = "true";

            var nodes = new List<NodeLayout>();
            const int nodeCount = 8;
            for (int i = 0; i < nodeCount; i++)
            {
                var node = new Node($"node{i}", $"Node {i}");
                node.Metadata["moduleId"] = $"Module{i}";
                model.Nodes.Add(node);

                nodes.Add(new NodeLayout
                {
                    Id = node.Id,
                    Position = new PointF { X = 0f, Y = i * 3.0f },
                    Size = new Nullable<Size>(new Size(1.8f, 2.8f))
                });
            }

            model.Edges.Add(new Edge("edge0", "node0", "node1"));
            model.Edges.Add(new Edge("edge1", "node4", "node5"));

            var layout = new LayoutResult
            {
                Nodes = nodes.ToArray(),
                Edges = Array.Empty<EdgeRoute>()
            };

            var (dataset, overrides, metrics) = Program.BuildPagingDatasetForTests(model, layout);

            var options = new PageSplitOptions
            {
                MaxConnectors = 400,
                MaxOccupancyPercent = 110.0,
                LaneSplitAllowed = false,
                MaxPageHeightIn = 5.0,
                MaxModulesPerPage = 3,
                HeightSlackPercent = 25.0
            };

            var pagePlans = PagingPlanner.computePages(options, dataset);
            Assert.NotNull(pagePlans);
            var summary = Program.InvokePlannerSummaryForTests(pagePlans, metrics.OriginalModuleCount, metrics.SegmentCount, metrics.SplitModuleCount, metrics.AverageSegmentsPerModule);
            Assert.True(pagePlans!.Length >= 2, $"Planner should emit multiple planned pages for tall layout span. Summary: {summary}");
            Assert.All(pagePlans, plan => Assert.True(plan.Modules != null && plan.Modules.Length > 0, "Each plan should contain at least one module segment."));
            Assert.Contains("pages=", summary);
            Assert.Contains("segments=", summary);
        }

        [Fact]
        public void Diagnostics_surface_overflow_when_occupancy_exceeds_threshold()
        {
            var model = new DiagramModel();
            model.Metadata["layout.page.heightIn"] = "5.0";
            model.Metadata["layout.page.marginIn"] = "0.5";
            model.Metadata["layout.spacing.vertical"] = "0.5";
            model.Metadata["layout.page.paginate"] = "true";

            var nodes = new List<NodeLayout>();
            for (int i = 0; i < 6; i++)
            {
                var node = new Node($"overflow{i}", $"Overflow {i}");
                node.Metadata["moduleId"] = "OverflowGroup";
                model.Nodes.Add(node);

                nodes.Add(new NodeLayout
                {
                    Id = node.Id,
                    Position = new PointF { X = 0f, Y = i * 2.5f },
                    Size = new Nullable<Size>(new Size(2.0f, 3.0f))
                });
            }

            var layout = new LayoutResult
            {
                Nodes = nodes.ToArray(),
                Edges = Array.Empty<EdgeRoute>()
            };

            var (dataset, overrides, metrics) = Program.BuildPagingDatasetForTests(model, layout);

            var options = new PageSplitOptions
            {
                MaxConnectors = 400,
                MaxOccupancyPercent = 90.0,
                LaneSplitAllowed = false,
                MaxPageHeightIn = 5.0,
                MaxModulesPerPage = 8,
                HeightSlackPercent = 0.0
            };

            var pagePlans = PagingPlanner.computePages(options, dataset);
            Assert.NotNull(pagePlans);

            var (stats, diagnostics) = Program.RunDiagnosticsForTests(model, layout, pagePlans, options, metrics);
            Assert.True(diagnostics.PageOverflowCount > 0, "Expected at least one page overflow to be flagged.");
            Assert.True(diagnostics.PartialRender, "Overflow diagnostics should mark partial render.");

            var summary = Program.InvokePlannerSummaryForTests(pagePlans, metrics.OriginalModuleCount, metrics.SegmentCount, metrics.SplitModuleCount, metrics.AverageSegmentsPerModule, diagnostics);
            Assert.Contains("overflowPages=", summary);
        }
    }
}



