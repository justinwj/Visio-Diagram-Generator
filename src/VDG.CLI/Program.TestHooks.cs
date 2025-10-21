using System.Collections.Generic;
using VDG.Core.Models;
using VisioDiagramGenerator.Algorithms;

namespace VDG.CLI
{
    internal static partial class Program
    {
        internal static (DiagramDataset Dataset, Dictionary<string, string> Overrides) BuildPagingDatasetForTests(
            DiagramModel model,
            LayoutResult layout)
        {
            var dataset = BuildPagingDataset(model, layout, out var overrides);
            return (dataset, overrides);
        }
    }
}
