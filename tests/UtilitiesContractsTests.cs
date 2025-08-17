using FluentAssertions;
using Xunit;
using VDG.Core.Core.Utilities; // adjust if Logging/ComSafety live elsewhere

namespace VDG.Core.Tests
{
    public class UtilitiesContractsTests
    {
        [Fact]
        public void Logging_module_exists()
        {
            // Contract-presence test: type should be resolvable.
            typeof(LoggingExtensions).Should().NotBeNull();
        }

        [Fact]
        public void ComSafety_extensions_exist()
        {
            typeof(ComObjectExtensions).Should().NotBeNull();
        }
    }
}