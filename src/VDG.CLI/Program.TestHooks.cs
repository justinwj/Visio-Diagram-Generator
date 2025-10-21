using System;
using System.Collections.Generic;
using System.IO;
using VDG.Core.Models;
using VisioDiagramGenerator.Algorithms;

namespace VDG.CLI
{
    internal static partial class Program
    {
        internal static (DiagramDataset Dataset, Dictionary<string, string> Overrides, PlannerMetrics Metrics) BuildPagingDatasetForTests(
            DiagramModel model,
            LayoutResult layout)
        {
            var result = BuildPagingDataset(model, layout);
            return (result.Dataset, result.NodeSegmentOverrides, result.Metrics);
        }

        internal static string InvokePlannerSummaryForTests(PagePlan[]? pagePlans, int originalModules, int segmentCount, int splitModuleCount, double? avgSegmentsPerModule = null)
        {
            var metrics = new PlannerMetrics
            {
                OriginalModuleCount = originalModules,
                SegmentCount = segmentCount,
                SplitModuleCount = splitModuleCount,
                AverageSegmentsPerModule = avgSegmentsPerModule ?? (originalModules > 0 ? segmentCount / (double)originalModules : 0.0)
            };

            var originalOut = Console.Out;
            using var writer = new StringWriter();
            Console.SetOut(writer);
            try
            {
                EmitPlannerSummary(pagePlans, metrics);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            return writer.ToString().Trim();
        }
    }
}
