using System;
using System.Collections.Generic;
using System.Linq;
using VDG.CLI;
using VDG.Core.Models;
using VisioDiagramGenerator.Algorithms;
using Xunit;

namespace VDG.CLI.Tests
{
    public sealed class FilteringTests
    {
        private static LayoutResult BuildLayoutForNodes(IEnumerable<Node> nodes)
        {
            var layouts = new List<NodeLayout>();
            int index = 0;
            foreach (var node in nodes)
            {
                layouts.Add(new NodeLayout
                {
                    Id = node.Id,
                    Position = new PointF { X = 0f, Y = index * 1.5f },
                    Size = new Nullable<Size>(new Size(1.2f, 1.0f))
                });
                index++;
            }

            return new LayoutResult
            {
                Nodes = layouts.ToArray(),
                Edges = Array.Empty<EdgeRoute>()
            };
        }

        [Fact]
        public void Module_filter_removes_unlisted_modules_and_tracks_exclusions()
        {
            var model = new DiagramModel();
            model.Metadata["layout.page.heightIn"] = "6.0";
            model.Metadata["layout.page.marginIn"] = "0.5";
            model.Metadata["layout.spacing.vertical"] = "0.5";

            var alphaNodes = new[] { new Node("a1", "Alpha 1"), new Node("a2", "Alpha 2") };
            foreach (var node in alphaNodes)
            {
                node.Metadata["moduleId"] = "Alpha";
                model.Nodes.Add(node);
            }

            var betaNodes = new[] { new Node("b1", "Beta 1"), new Node("b2", "Beta 2") };
            foreach (var node in betaNodes)
            {
                node.Metadata["moduleId"] = "Beta";
                model.Nodes.Add(node);
            }

            model.Edges.Add(new Edge("ea", "a1", "a2"));
            model.Edges.Add(new Edge("eb", "b1", "b2"));

            var originalModules = Program.CollectModuleIds(model);
            Assert.Contains("Alpha", originalModules, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Beta", originalModules, StringComparer.OrdinalIgnoreCase);

            var filteredModel = Program.FilterModelByModules(model, new[] { "Alpha" });
            var filteredModules = Program.CollectModuleIds(filteredModel);
            Assert.Contains("Alpha", filteredModules, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("Beta", filteredModules, StringComparer.OrdinalIgnoreCase);
            Assert.All(filteredModel.Nodes, n => Assert.Equal("Alpha", n.Metadata["moduleId"]));
            Assert.Single(filteredModel.Edges);
            Assert.Equal("ea", filteredModel.Edges[0].Id);

            var layout = BuildLayoutForNodes(filteredModel.Nodes);
            var (dataset, _, metrics) = Program.BuildPagingDatasetForTests(filteredModel, layout);
            var options = new PageSplitOptions
            {
                MaxConnectors = 10,
                MaxOccupancyPercent = 150,
                LaneSplitAllowed = false,
                MaxPageHeightIn = 6.0,
                MaxModulesPerPage = 10,
                HeightSlackPercent = 0.0
            };
            var pagePlans = PagingPlanner.computePages(options, dataset) ?? Array.Empty<PagePlan>();
            var removedModules = originalModules.Where(m => !filteredModules.Contains(m, StringComparer.OrdinalIgnoreCase));
            var (_, diagnostics) = Program.RunDiagnosticsForTests(filteredModel, layout, pagePlans, options, metrics, dataset, removedModules);

            Assert.Equal(new[] { "Beta" }, diagnostics.SkippedModules);
            Assert.True(diagnostics.PartialRender);
        }

        [Fact]
        public void Module_exclude_trims_selected_modules_and_reports_them()
        {
            var model = new DiagramModel();
            model.Metadata["layout.page.heightIn"] = "6.0";
            model.Metadata["layout.page.marginIn"] = "0.5";
            model.Metadata["layout.spacing.vertical"] = "0.5";

            void Add(string prefix, params string[] ids)
            {
                foreach (var id in ids)
                {
                    var node = new Node(id, $"{prefix} {id}");
                    node.Metadata["moduleId"] = prefix;
                    model.Nodes.Add(node);
                }
            }

            Add("Alpha", "a1", "a2");
            Add("Beta", "b1", "b2");
            Add("Gamma", "g1");

            var allModules = Program.CollectModuleIds(model);
            Assert.Contains("Beta", allModules, StringComparer.OrdinalIgnoreCase);

            var keep = allModules.Where(m => !string.Equals(m, "Beta", StringComparison.OrdinalIgnoreCase)).ToArray();
            var filteredModel = Program.FilterModelByModules(model, keep);
            var filteredModules = Program.CollectModuleIds(filteredModel);
            Assert.DoesNotContain("Beta", filteredModules, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Alpha", filteredModules, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Gamma", filteredModules, StringComparer.OrdinalIgnoreCase);

            var layout = BuildLayoutForNodes(filteredModel.Nodes);
            var (dataset, _, metrics) = Program.BuildPagingDatasetForTests(filteredModel, layout);
            var options = new PageSplitOptions
            {
                MaxConnectors = 10,
                MaxOccupancyPercent = 150,
                LaneSplitAllowed = false,
                MaxPageHeightIn = 6.0,
                MaxModulesPerPage = 10,
                HeightSlackPercent = 0.0
            };
            var pagePlans = PagingPlanner.computePages(options, dataset) ?? Array.Empty<PagePlan>();
            var excluded = allModules.Where(m => !keep.Contains(m, StringComparer.OrdinalIgnoreCase));
            var (_, diagnostics) = Program.RunDiagnosticsForTests(filteredModel, layout, pagePlans, options, metrics, dataset, excluded);

            Assert.Equal(new[] { "Beta" }, diagnostics.SkippedModules);
            Assert.True(diagnostics.PartialRender);
        }

        [Fact]
        public void Max_pages_limit_trims_modules_beyond_page_cap()
        {
            var model = new DiagramModel();
            model.Metadata["layout.page.heightIn"] = "5.0";
            model.Metadata["layout.page.marginIn"] = "0.5";
            model.Metadata["layout.spacing.vertical"] = "0.4";

            void AddModule(string prefix)
            {
                for (int i = 0; i < 2; i++)
                {
                    var node = new Node($"{prefix}{i}", $"{prefix} {i}");
                    node.Metadata["moduleId"] = prefix;
                    model.Nodes.Add(node);
                }
            }

            AddModule("Mod1");
            AddModule("Mod2");
            AddModule("Mod3");

            model.Edges.Add(new Edge("e1", "Mod10", "Mod11"));
            model.Edges.Add(new Edge("e2", "Mod20", "Mod21"));
            model.Edges.Add(new Edge("e3", "Mod30", "Mod31"));

            var layout = BuildLayoutForNodes(model.Nodes);
            var (dataset, _, metrics) = Program.BuildPagingDatasetForTests(model, layout);
            var options = new PageSplitOptions
            {
                MaxConnectors = 5,
                MaxOccupancyPercent = 120,
                LaneSplitAllowed = false,
                MaxPageHeightIn = 5.0,
                MaxModulesPerPage = 1,
                HeightSlackPercent = 0.0
            };
            var pagePlans = PagingPlanner.computePages(options, dataset) ?? Array.Empty<PagePlan>();
            Assert.True(pagePlans.Length >= 3, "Precondition: planner should emit multiple pages.");

            var pageLimit = 2;
            var modulesToKeep = new HashSet<string>(
                pagePlans.OrderBy(p => p.PageIndex)
                         .Take(pageLimit)
                         .SelectMany(p => p.Modules ?? Array.Empty<string>()),
                StringComparer.OrdinalIgnoreCase);

            var filteredModel = Program.FilterModelByModules(model, modulesToKeep);
            Assert.Equal(pageLimit * 2, filteredModel.Nodes.Count);

            var droppedModules = Program.CollectModuleIds(model)
                .Where(m => !modulesToKeep.Contains(m))
                .ToArray();
            Assert.NotEmpty(droppedModules);

            var filteredLayout = BuildLayoutForNodes(filteredModel.Nodes);
            var (filteredDataset, _, filteredMetrics) = Program.BuildPagingDatasetForTests(filteredModel, filteredLayout);
            var filteredPlans = PagingPlanner.computePages(options, filteredDataset) ?? Array.Empty<PagePlan>();
            Assert.True(filteredPlans.Length <= pageLimit);

            var (_, diagnostics) = Program.RunDiagnosticsForTests(filteredModel, filteredLayout, filteredPlans, options, filteredMetrics, filteredDataset, droppedModules);
            Assert.True(diagnostics.SkippedModules.Intersect(droppedModules, StringComparer.OrdinalIgnoreCase).Any());
            Assert.True(diagnostics.SkippedModules.SequenceEqual(diagnostics.SkippedModules.OrderBy(m => m, StringComparer.OrdinalIgnoreCase)));
            Assert.True(diagnostics.PartialRender);
        }
    }
}
