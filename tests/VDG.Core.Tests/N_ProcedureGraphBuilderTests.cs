using System.Collections.Generic;
using VDG.Core.Analysis;
using VDG.Core.Vba;
using Xunit;

namespace VDG.Core.Tests
{
    public class N_ProcedureGraphBuilderTests
    {
        private sealed class StubGateway : IVbeGateway
        {
            public IEnumerable<VbaModule> ExportModules(string projectFilePath)
            {
                // Simple module with two procedures and one call
                yield return new VbaModule("Module1", "Sub A()\n    Call B\nEnd Sub\nSub B()\nEnd Sub\n");
            }
        }

        [Fact]
        public void GenerateProcedureGraph_ReturnsModelWithNodesAndEdges()
        {
            var gateway = new StubGateway();
            var model = ProcedureGraphBuilder.GenerateProcedureGraph(gateway, "dummy.xlsm");
            Assert.NotNull(model);
            Assert.Equal(2, model.Nodes.Count);
            Assert.Single(model.Edges);
        }
    }
}