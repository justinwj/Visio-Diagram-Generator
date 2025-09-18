using System.Collections.Generic;
using VDG.Core;
using Xunit;

namespace VDG.Tests.P9_11
{
    public class GridLayoutAlgorithmTests
    {
        [Fact]
        public void Computes_Simple_Grid_Positions()
        {
            var items = new List<DiagramItem>
            {
                new DiagramItem("1","T","1",0,0),
                new DiagramItem("2","T","2",0,0),
                new DiagramItem("3","T","3",0,0),
                new DiagramItem("4","T","4",0,0)
            };
            var grid = new GridLayoutAlgorithm();
            var positioned = grid.Compute(items, new List<DiagramConnection>());

            Assert.Equal(0, positioned[0].X);
            Assert.Equal(0, positioned[0].Y);
            Assert.Equal(2, positioned[1].X);
            Assert.Equal(0, positioned[1].Y);
            Assert.Equal(0, positioned[2].X);
            Assert.Equal(2, positioned[2].Y);
        }
    }
}