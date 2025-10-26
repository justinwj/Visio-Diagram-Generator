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
    public sealed class ViewModeTests
    {
        [Fact]
        public void LoadDiagramModel_captures_layout_output_mode()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"vdg_viewmode_{Guid.NewGuid():N}.json");
            var json = """
{
  "schemaVersion": "1.2",
  "layout": {
    "outputMode": "view",
    "tiers": ["Modules"],
    "spacing": { "horizontal": 1.2, "vertical": 0.6 },
    "page": { "marginIn": 0.5 }
  },
  "nodes": [
    { "id": "n1", "label": "Node 1", "tier": "Modules" }
  ],
  "edges": []
}
""";
            File.WriteAllText(tempPath, json);
            try
            {
                var model = Program.LoadDiagramModelForTests(tempPath);
                Assert.True(model.Metadata.TryGetValue("layout.outputMode", out var mode));
                Assert.Equal("view", mode);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Fact]
        public void View_mode_can_paginate_when_page_height_constrained()
        {
            var model = new DiagramModel();
            model.Metadata["layout.outputMode"] = "view";
            model.Metadata["layout.page.heightIn"] = "1.0";
            model.Metadata["layout.page.marginIn"] = "0.1";
            model.Metadata["layout.page.paginate"] = "true";
            model.Metadata["layout.tiers"] = "Modules";

            var node = new Node("node-1", "Node 1")
            {
                Tier = "Modules",
                GroupId = "Modules"
            };
            model.Nodes.Add(node);

            var layout = LayoutEngine.compute(model);
            Assert.True(Program.ShouldPaginateForTests(model, layout));
        }

        [Fact]
        public void AnalyzeViewModeContent_flags_missing_content()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"vdg_viewmode_{Guid.NewGuid():N}.json");
            var json = """
{
  "schemaVersion": "1.2",
  "layout": {
    "outputMode": "view",
    "tiers": ["Forms", "Modules"],
    "spacing": { "horizontal": 1.2, "vertical": 0.6 },
    "page": { "heightIn": 8.5, "marginIn": 0.5, "paginate": true }
  },
  "nodes": [
    { "id": "modFilled.proc1", "label": "proc1", "tier": "Modules", "containerId": "modFilled" }
  ],
  "edges": [],
  "containers": [
    { "id": "frmEmpty", "label": "frmEmpty", "tier": "Forms" },
    { "id": "modFilled", "label": "modFilled", "tier": "Modules" },
    { "id": "modEmpty", "label": "modEmpty", "tier": "Modules" }
  ]
}
""";
            File.WriteAllText(tempPath, json);
            try
            {
                var model = Program.LoadDiagramModelForTests(tempPath);
                var analysis = Program.AnalyzeViewModeContentForTests(model);
                Assert.True(analysis.Enabled);
                Assert.True(analysis.NodeCountTooLow);
                Assert.True(analysis.MissingConnectors);
                Assert.Contains("frmEmpty", analysis.EmptyForms);
                Assert.Contains("modEmpty", analysis.EmptyContainers);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Fact]
        public void View_mode_layout_clusters_nodes_by_container()
        {
            var model = new DiagramModel();
            model.Metadata["layout.outputMode"] = "view";
            model.Metadata["layout.tiers"] = "Forms";

            static Node CreateNode(string id, string label, string container)
            {
                var node = new Node(id, label) { Tier = "Forms" };
                node.Metadata["node.containerId"] = container;
                return node;
            }

            var f1a = CreateNode("frmItemSearch.Init", "Init", "frmItemSearch");
            var f1b = CreateNode("frmItemSearch.Render", "Render", "frmItemSearch");
            var f2a = CreateNode("MouseOverControl.Run", "Run", "MouseOverControl");

            model.Nodes.Add(f1a);
            model.Nodes.Add(f1b);
            model.Nodes.Add(f2a);

            model.Edges.Add(new Edge("edge1", f1a.Id, f2a.Id, "call"));

            var layout = Program.ComputeViewModeLayoutForTests(model);
            Assert.NotNull(layout);
            Assert.Equal(3, layout.Nodes.Length);
            Assert.Single(layout.Edges);

            var positions = layout.Nodes.ToDictionary(n => n.Id, n => n.Position);
            Assert.True(Math.Abs(positions[f1a.Id].X - positions[f1b.Id].X) < 0.001f);
            Assert.True(Math.Abs(positions[f1a.Id].X - positions[f2a.Id].X) > 0.2f);
        }

        [Fact]
        public void View_mode_layout_records_truncation_metadata()
        {
            var model = new DiagramModel();
            model.Metadata["layout.outputMode"] = "view";
            model.Metadata["layout.tiers"] = "Modules";

            for (int i = 0; i < 30; i++)
            {
                var node = new Node($"n{i}", $"Node {i}")
                {
                    Tier = "Modules"
                };
                node.Metadata["node.containerId"] = "modA";
                model.Nodes.Add(node);
            }

            var layout = Program.ComputeViewModeLayoutForTests(model);
            Assert.NotNull(layout);
            Assert.True(model.Metadata.TryGetValue("layout.view.truncatedModules", out var raw));
            var parsed = JsonSerializer.Deserialize<Dictionary<string, int>>(raw);
            Assert.NotNull(parsed);
            Assert.True(parsed!.TryGetValue("modA", out var overflow));
            Assert.Equal(18, overflow);
        }

        [Fact]
        public void View_mode_inter_module_edges_use_stub_routes()
        {
            var model = new DiagramModel();
            model.Metadata["layout.outputMode"] = "view";
            model.Metadata["layout.tiers"] = "Forms";

            Node MakeNode(string id, string module, float y)
            {
                var node = new Node(id, id) { Tier = "Forms" };
                node.Metadata["node.containerId"] = module;
                model.Nodes.Add(node);
                return node;
            }

            var a1 = MakeNode("a1", "modA", 0);
            var b1 = MakeNode("b1", "modB", 0);

            model.Edges.Add(new Edge("edgeAB", a1.Id, b1.Id, "call"));

            var layout = Program.ComputeViewModeLayoutForTests(model);
            var route = layout.Edges.Single(e => e.Id == "edgeAB");
            Assert.True(route.Points.Length >= 4);
        }
    }
}
