using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using VDG.Core.Core;
using VDG.Core.Core.Interop; // IVisioService under Interop per repo map
using VDG.Core.Models;

namespace VDG.Core.Tests
{
    // No-op fake that satisfies the interface without COM
    internal sealed class FakeVisioService : IVisioService
    {
        public string DrawShape(string master, double x, double y, string label) => Guid.NewGuid().ToString();
        public void DrawConnector(string srcId, string trgId, string connectorType) { /* no-op */ }
        public void Dispose() { /* no-op */ }
    }

    public class DiagramBuilderContractsTests
    {
        [Fact]
        public void DiagramBuilder_accepts_IVisioService_dependency()
        {
            using var fake = new FakeVisioService();
            var builder = new DiagramBuilder(fake);

            builder.Should().NotBeNull();
        }

        [Fact]
        public void DiagramBuilder_Execute_accepts_empty_command_list_without_throwing()
        {
            using var fake = new FakeVisioService();
            var builder = new DiagramBuilder(fake);

            var cmds = Array.Empty<DrawCommand>();
            var act = () => builder.Execute(cmds);

            act.Should().NotThrow(); // Prompt-2 contract: tolerate empty/no-op input
        }
    }
}