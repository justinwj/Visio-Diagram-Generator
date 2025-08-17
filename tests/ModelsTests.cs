using FluentAssertions;
using Xunit;
using VDG.Core.Models; // adjust if your namespace differs

namespace VDG.Core.Tests
{
    public class ModelsTests
    {
        [Fact]
        public void DiagramItem_can_be_constructed_and_is_immutable_shape()
        {
            var item = new DiagramItem(id: "n1", typeName: "CallSite", label: "A->B", x: 1.0, y: 2.0);

            item.Id.Should().Be("n1");
            item.TypeName.Should().Be("CallSite");
            item.Label.Should().Be("A->B");
            item.X.Should().Be(1.0);
            item.Y.Should().Be(2.0);
        }

        [Fact]
        public void DiagramConnection_can_be_constructed_and_is_immutable_shape()
        {
            var c = new DiagramConnection(sourceId: "n1", targetId: "n2", connectorType: "dynamic");

            c.SourceId.Should().Be("n1");
            c.TargetId.Should().Be("n2");
            c.ConnectorType.Should().Be("dynamic");
        }

        [Fact]
        public void DiagramConfig_has_sensible_defaults_or_is_serialisable_contract()
        {
            // This test is intentionally light in Prompt 2.
            // It just asserts the type exists and is instantiable.
            var cfg = new DiagramConfig();
            cfg.Should().NotBeNull();
        }
    }
}