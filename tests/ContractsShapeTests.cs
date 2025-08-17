using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using VDG.Core.Core; // Pipeline / DrawCommand namespaces per your folder map
using VDG.Core.Layouts;
using VDG.Core.Models;
using VDG.Core.Providers;
using Xunit;

namespace VDG.Core.Tests
{
    // Tiny fake provider & layout to validate interfaces exist and are consumable
    internal sealed class TestProvider : IMapProvider
    {
        public IReadOnlyList<DiagramItem> GetItems() => Array.Empty<DiagramItem>();
        public IReadOnlyList<DiagramConnection> GetConnections() => Array.Empty<DiagramConnection>();
    }

    internal sealed class PassthroughLayout : ILayoutAlgorithm
    {
        public IReadOnlyList<DiagramItem> Compute(
            IReadOnlyList<DiagramItem> items,
            IReadOnlyList<DiagramConnection> _)
            => items; // no-op
    }

    public class ContractsShapeTests
    {
        [Fact]
        public void IMapProvider_and_ILayoutAlgorithm_are_available_and_type_safe()
        {
            var p = new TestProvider();
            var l = new PassthroughLayout();

            p.Should().BeAssignableTo<IMapProvider>();
            l.Should().BeAssignableTo<ILayoutAlgorithm>();
        }

        [Fact]
        public void DrawCommand_contract_is_constructible_and_carries_kind_and_data()
        {
            var item = new DiagramItem("id", "t", "lbl", 0, 0);
            var cmd = new DrawCommand(kind: "shape", data: item);

            cmd.Kind.Should().Be("shape");
            cmd.Data.Should().BeSameAs(item);
        }

        [Fact]
        public void Pipeline_contract_exists_and_BuildCommands_method_is_discoverable()
        {
            // Don’t assert behavior in Prompt 2; we only validate the public surface is present.
            var providers = new List<IMapProvider> { new TestProvider() };
            var layout = new PassthroughLayout();

            var pipeline = new Pipeline(providers, layout);
            pipeline.Should().NotBeNull();

            // Reflection check to avoid binding to not-yet-implemented logic:
            var mi = typeof(Pipeline).GetMethod("BuildCommands");
            mi.Should().NotBeNull("Pipeline must expose BuildCommands()");
        }
    }
}
