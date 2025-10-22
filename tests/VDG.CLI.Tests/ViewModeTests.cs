using System;
using System.Collections.Generic;
using System.IO;
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
        public void View_mode_disables_pagination_even_with_page_height()
        {
            var model = new DiagramModel();
            model.Metadata["layout.outputMode"] = "view";
            model.Metadata["layout.page.heightIn"] = "8.5";
            model.Metadata["layout.page.marginIn"] = "0.5";
            model.Metadata["layout.tiers"] = "Modules";

            var node = new Node("node-1", "Node 1")
            {
                Tier = "Modules",
                GroupId = "Modules"
            };
            model.Nodes.Add(node);

            var layout = LayoutEngine.compute(model);
            Assert.False(Program.ShouldPaginateForTests(model, layout));
        }
    }
}
