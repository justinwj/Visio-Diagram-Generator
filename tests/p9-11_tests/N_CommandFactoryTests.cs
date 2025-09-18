using System.Collections.Generic;
using VDG.Core;
using Xunit;

namespace VDG.Tests.P9_11
{
    public class CommandFactoryTests
    {
        [Fact]
        public void Creates_Shape_Then_Connector_Commands()
        {
            var items = new List<DiagramItem>
            {
                new DiagramItem("A","Type","A",0,0),
                new DiagramItem("B","Type","B",0,0)
            };
            var conns = new List<DiagramConnection>
            {
                new DiagramConnection("A","B","line")
            };

            var cmds = CommandFactory.From(items, conns);
            Assert.Equal(3, cmds.Count);
            Assert.Equal("shape", cmds[0].Kind);
            Assert.Equal("shape", cmds[1].Kind);
            Assert.Equal("connector", cmds[2].Kind);
        }
    }
}