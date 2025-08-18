using FluentAssertions;
using Xunit;

namespace VDG.Core.Tests
{
    public class BuildGate
    {
        [Fact]
        public void CoreAssembly_Loads_WithExpectedName()
        {
            var asm = typeof(VDG.Core.AssemblyMarker).Assembly;
            asm.GetName().Name.Should().Be("VDG.Core");
        }
    }
}