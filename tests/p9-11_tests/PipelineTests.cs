using System.Collections.Generic;
using NSubstitute;
using VDG.Core;
using Xunit;

namespace VDG.Tests.P9_11
{
    public class PipelineTests
    {
        [Fact]
        public void Aggregates_Items_And_Connections_Into_Commands()
        {
            var p1 = Substitute.For<IMapProvider>();
            p1.GetItems().Returns(new List<DiagramItem> { new DiagramItem("1","T","One",0,0) });
            p1.GetConnections().Returns(new List<DiagramConnection>());

            var p2 = Substitute.For<IMapProvider>();
            p2.GetItems().Returns(new List<DiagramItem> { new DiagramItem("2","T","Two",0,0) });
            p2.GetConnections().Returns(new List<DiagramConnection> { new DiagramConnection("1","2","line") });

            var pipe = new Pipeline(new[] { p1, p2 }, new GridLayoutAlgorithm());
            var cmds = pipe.BuildCommands();

            Assert.Equal(3, cmds.Count); // 2 shapes + 1 connector
        }
    }
}