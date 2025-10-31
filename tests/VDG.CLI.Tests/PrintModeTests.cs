using System.Linq;
using VDG.Core.Models;
using VisioDiagramGenerator.Algorithms;
using Xunit;

namespace VDG.CLI.Tests
{
    public sealed class PrintModeTests
    {
        [Fact]
        public void Print_mode_layout_plan_emits_multiple_pages()
        {
            var model = new DiagramModel();
            model.Metadata["layout.page.heightIn"] = "1.0";
            model.Metadata["layout.page.marginIn"] = "0.1";

            for (int i = 0; i < 6; i++)
            {
                var node = new Node($"n{i}", $"Node {i}")
                {
                    Tier = "Modules"
                };
                node.Metadata["moduleId"] = $"Module{i}";
                model.Nodes.Add(node);
            }

            var layout = LayoutEngine.compute(model);
            var plan = PrintPlanner.ComputeLayoutPlan(model, layout);

            Assert.NotNull(plan);
            Assert.Equal("print", plan.OutputMode);
            Assert.NotNull(plan.Pages);
            Assert.True(plan.Pages.Length > 1);
            Assert.True(plan.Pages.SelectMany(p => p.Modules ?? new string[0]).Distinct().Count() >= 2);
        }
    }
}
