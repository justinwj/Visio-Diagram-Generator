using System.Collections.Generic;
using NSubstitute;
using VDG.Core;
using VDG.Core.Providers;
using VDG.Core.Vba;
using Xunit;

namespace VDG.Tests.P9_11
{
    public class VBAMapProviderTests
    {
        [Fact]
        public void Falls_Back_When_Not_Trusted()
        {
            var vbe = Substitute.For<IVbeGateway>();
            vbe.IsTrusted().Returns(false);

            var fb = Substitute.For<IMapProvider>();
            fb.GetItems().Returns(new List<DiagramItem> { new DiagramItem("1","Module","FromFallback",0,0) });
            fb.GetConnections().Returns(new List<DiagramConnection>());

            var provider = new VBAMapProvider(vbe, fb);
            var items = provider.GetItems();
            Assert.Collection(items, i => Assert.Equal("FromFallback", i.Label));
        }

        [Fact]
        public void Uses_VBIDE_When_Trusted()
        {
            var vbe = Substitute.For<IVbeGateway>();
            vbe.IsTrusted().Returns(true);
            vbe.EnumerateModules().Returns(new[]
            {
                new VbaModule("ModA", null),
                new VbaModule("ModB", null)
            });

            var fb = Substitute.For<IMapProvider>();
            var provider = new VBAMapProvider(vbe, fb);
            var items = provider.GetItems();

            Assert.Equal(2, items.Count);
            Assert.Contains(items, i => i.Label == "ModA");
            Assert.Contains(items, i => i.Label == "ModB");
        }
    }
}
