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

        internal static string InvokePlannerSummaryForTests(
            PagePlan[]? pagePlans,
            int originalModules,
            int segmentCount,
            int splitModuleCount,
            double? avgSegmentsPerModule = null,
            DiagnosticsSummary? diagnosticsSummary = null)
        {
            var metrics = new PlannerMetrics
            {
                OriginalModuleCount = originalModules,
                SegmentCount = segmentCount,
                SplitModuleCount = splitModuleCount,
                AverageSegmentsPerModule = avgSegmentsPerModule ?? (originalModules > 0 ? segmentCount / (double)originalModules : 0.0)
            };

            var stats = BuildPlannerSummaryStats(pagePlans);
            lock (ConsoleRedirectLock)
            {
                var originalOut = Console.Out;
                using var writer = new StringWriter();
                Console.SetOut(writer);
                try
                {
                    PrintPlannerSummary(stats, metrics, diagnosticsSummary);
                }
                finally
                {
                    Console.SetOut(originalOut);
                }

                return writer.ToString().Trim();
            }
        }

        internal static (PlannerSummaryStats Stats, DiagnosticsSummary Diagnostics) RunDiagnosticsForTests(
            DiagramModel model,
            LayoutResult layout,
            PagePlan[]? pagePlans,
            PageSplitOptions? options,
            PlannerMetrics metrics,
            DiagramDataset? dataset = null,
            IEnumerable<string>? filteredModules = null)
        {
            var stats = BuildPlannerSummaryStats(pagePlans);
            DiagnosticsSummary diagnostics;
            lock (ConsoleRedirectLock)
            {
                var originalOut = Console.Out;
                var originalErr = Console.Error;
                using var suppressedOut = new StringWriter();
                using var suppressedErr = new StringWriter();
                Console.SetOut(suppressedOut);
                Console.SetError(suppressedErr);
                try
                {
                    diagnostics = EmitDiagnostics(model, layout, null, null, pagePlans, options, metrics, stats, dataset, filteredModules);
                }
                finally
                {
                    Console.SetOut(originalOut);
                    Console.SetError(originalErr);
                }
            }
            return (stats, diagnostics);
        }

        internal static bool ShouldPaginateForTests(DiagramModel model, LayoutResult layout) => ShouldPaginate(model, layout);

        internal static DiagramModel LoadDiagramModelForTests(string inputPath) => LoadDiagramModel(inputPath);

        internal static ViewModeValidationResult AnalyzeViewModeContentForTests(DiagramModel model) => AnalyzeViewModeContent(model);

        internal static LayoutResult ComputeViewModeLayoutForTests(DiagramModel model) => ComputeViewModeLayout(model);
    }
}
