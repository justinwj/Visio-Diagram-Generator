using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using Microsoft.CSharp.RuntimeBinder;
using VDG.Core.Models;
using VDG.Core.Safety;
using VisioDiagramGenerator.Algorithms;

namespace VDG.CLI
{
    internal static class ExitCodes
    {
        public const int Ok = 0;
        public const int Usage = 64;
        public const int InvalidInput = 65;
        public const int VisioUnavailable = 69;
        public const int InternalError = 70;
    }

    internal sealed class UsageException : Exception
    {
        public UsageException(string message) : base(message) { }
    }

    internal static class Program
    {
        private const string CurrentSchemaVersion = "1.1";
        private static readonly string[] SupportedSchemaVersions = { "1.0", CurrentSchemaVersion };
        private const double DefaultNodeWidth = 1.8;
        private const double DefaultNodeHeight = 1.0;
        private const double Margin = 1.0;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        [STAThread]
        private static int Main(string[] args)
        {
            string? outputPath = null;

            try
            {
                if (args.Length < 2)
                {
                    throw new UsageException("Expected arguments: <input.diagram.json> <output.vsdx>");
                }

                var inputPath = Path.GetFullPath(args[0]);
                outputPath = Path.GetFullPath(args[1]);

                var model = LoadDiagramModel(inputPath);
                var layout = LayoutEngine.compute(model);

                EnsureDirectory(outputPath);
                RunVisio(model, layout, outputPath);
                DeleteErrorLog(outputPath);

                Console.WriteLine($"Saved diagram: {outputPath}");
                return ExitCodes.Ok;
            }
            catch (UsageException uex)
            {
                Console.Error.WriteLine($"usage: {uex.Message}");
                PrintUsage();
                return ExitCodes.Usage;
            }
            catch (FileNotFoundException fnf)
            {
                WriteErrorLog(outputPath, fnf);
                Console.Error.WriteLine($"input file not found: {fnf.FileName}");
                return ExitCodes.InvalidInput;
            }
            catch (JsonException jex)
            {
                WriteErrorLog(outputPath, jex);
                Console.Error.WriteLine($"invalid diagram JSON: {jex.Message}");
                return ExitCodes.InvalidInput;
            }
            catch (InvalidDataException idex)
            {
                WriteErrorLog(outputPath, idex);
                Console.Error.WriteLine($"invalid diagram: {idex.Message}");
                return ExitCodes.InvalidInput;
            }
            catch (COMException comEx)
            {
                WriteErrorLog(outputPath, comEx);
                Console.Error.WriteLine($"Visio automation error: {comEx.Message}");
                return ExitCodes.VisioUnavailable;
            }
            catch (Exception ex)
            {
                WriteErrorLog(outputPath, ex);
                Console.Error.WriteLine($"fatal: {ex.Message}");
                return ExitCodes.InternalError;
            }
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("VDG.CLI runner");
            Console.Error.WriteLine("Usage: VDG.CLI.exe <input.diagram.json> <output.vsdx>");
        }

        private static DiagramModel LoadDiagramModel(string inputPath)
        {
            if (!File.Exists(inputPath))
            {
                throw new FileNotFoundException("Diagram model file not found.", inputPath);
            }

            var json = File.ReadAllText(inputPath);
            var dto = JsonSerializer.Deserialize<DiagramEnvelope>(json, JsonOptions)
                      ?? throw new InvalidDataException("Diagram JSON deserialized to null.");

            var schemaVersion = dto.SchemaVersion;
            if (!string.IsNullOrEmpty(schemaVersion) &&
                !SupportedSchemaVersions.Any(v => string.Equals(v, schemaVersion, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(string.Format(
                    "Unsupported schemaVersion '{0}'. Supported versions: {1}.",
                    schemaVersion,
                    string.Join(", ", SupportedSchemaVersions)));
            }

            if (dto.Nodes is null || dto.Nodes.Count == 0)
            {
                throw new InvalidDataException("Diagram contains no nodes.");
            }

            var nodes = new List<Node>(dto.Nodes.Count);
            foreach (var nodeDto in dto.Nodes)
            {
                if (string.IsNullOrWhiteSpace(nodeDto.Id))
                {
                    throw new InvalidDataException("Node is missing 'id'.");
                }

                if (string.IsNullOrWhiteSpace(nodeDto.Label))
                {
                    throw new InvalidDataException($"Node '{nodeDto.Id}' is missing 'label'.");
                }

                var nodeId = nodeDto.Id!;
                var nodeLabel = nodeDto.Label!;
                var node = new Node(nodeId, nodeLabel);

                if (!string.IsNullOrWhiteSpace(nodeDto.Type))
                {
                    node.Type = nodeDto.Type;
                }

                if (!string.IsNullOrWhiteSpace(nodeDto.GroupId))
                {
                    node.GroupId = nodeDto.GroupId;
                }

                if (nodeDto.Size is { Width: > 0, Height: > 0 })
                {
                    node.Size = new Size(nodeDto.Size.Width!.Value, nodeDto.Size.Height!.Value);
                }

                ApplyStyle(node.Style, nodeDto.Style);
                ApplyMetadata(node.Metadata, nodeDto.Metadata);

                nodes.Add(node);
            }

            var edges = new List<Edge>();
            if (dto.Edges != null)
            {
                foreach (var edgeDto in dto.Edges)
                {
                    if (string.IsNullOrWhiteSpace(edgeDto.SourceId) || string.IsNullOrWhiteSpace(edgeDto.TargetId))
                    {
                        throw new InvalidDataException($"Edge '{edgeDto.Id}' is missing source or target id.");
                    }

                    var sourceId = edgeDto.SourceId!;
                    var targetId = edgeDto.TargetId!;
                    var edgeId = string.IsNullOrWhiteSpace(edgeDto.Id)
                        ? string.Format("{0}->{1}", sourceId, targetId)
                        : edgeDto.Id!;

                    var edge = new Edge(edgeId, sourceId, targetId, edgeDto.Label)
                    {
                        Directed = edgeDto.Directed ?? true
                    };

                    ApplyStyle(edge.Style, edgeDto.Style);
                    ApplyMetadata(edge.Metadata, edgeDto.Metadata);

                    edges.Add(edge);
                }
            }

            var model = new DiagramModel(nodes, edges);
            if (dto.Metadata != null)
            {
                ApplyMetadata(model.Metadata, dto.Metadata.Properties);

                if (!string.IsNullOrWhiteSpace(dto.Metadata.Title))
                {
                    model.Metadata["title"] = dto.Metadata.Title!;
                }

                if (!string.IsNullOrWhiteSpace(dto.Metadata.Description))
                {
                    model.Metadata["description"] = dto.Metadata.Description!;
                }

                if (dto.Metadata.Tags != null && dto.Metadata.Tags.Count > 0)
                {
                    var cleaned = dto.Metadata.Tags
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .Select(tag => tag!.Trim())
                        .Where(tag => tag.Length > 0)
                        .ToArray();

                    if (cleaned.Length > 0)
                    {
                        model.Metadata["tags"] = string.Join(", ", cleaned);
                    }
                }
            }

            return model;
        }
        private static void ApplyStyle(ShapeStyle style, StyleDto? dto)
        {
            if (style == null || dto == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(dto.Fill))
            {
                style.FillColor = dto.Fill;
            }

            if (!string.IsNullOrWhiteSpace(dto.Stroke))
            {
                style.StrokeColor = dto.Stroke;
            }

            if (!string.IsNullOrWhiteSpace(dto.LinePattern))
            {
                style.LinePattern = dto.LinePattern;
            }
        }

        private static void ApplyMetadata(IDictionary<string, string> target, IDictionary<string, string>? source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var kvp in source)
            {
                if (!string.IsNullOrWhiteSpace(kvp.Key))
                {
                    target[kvp.Key] = kvp.Value ?? string.Empty;
                }
            }
        }

        private static void EnsureDirectory(string outputPath)
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void RunVisio(DiagramModel model, LayoutResult layout, string outputPath)
        {
            dynamic? app = null;
            dynamic? documents = null;
            dynamic? document = null;
            dynamic? pages = null;
            dynamic? page = null;

            var placements = new Dictionary<string, NodePlacement>(StringComparer.OrdinalIgnoreCase);

            try
            {
                app = CreateVisioApplication();
                app.Visible = false;

        private static void ApplyNodeStyle(Node node, dynamic shape)
        {
            if (node.Style == null || node.Style.IsDefault())
            {
                return;
            }

            if (TryParseColor(node.Style.FillColor, out var fill))
            {
                TrySetFormula(shape, "FillForegnd", $"RGB({fill.R},{fill.G},{fill.B})");
            }

            if (TryParseColor(node.Style.StrokeColor, out var stroke))
            {
                TrySetFormula(shape, "LineColor", $"RGB({stroke.R},{stroke.G},{stroke.B})");
            }

            ApplyLinePattern(shape, node.Style.LinePattern);
        }

        private static void ApplyEdgeStyle(Edge edge, dynamic line)
        {
            if (edge.Style == null || edge.Style.IsDefault())
            {
                return;
            }

            var strokeSource = string.IsNullOrWhiteSpace(edge.Style.StrokeColor)
                ? edge.Style.FillColor
                : edge.Style.StrokeColor;

            if (TryParseColor(strokeSource, out var stroke))
            {
                TrySetFormula(line, "LineColor", $"RGB({stroke.R},{stroke.G},{stroke.B})");
            }

            ApplyLinePattern(line, edge.Style.LinePattern);
        }

        private static void ApplyLinePattern(dynamic shape, string? pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return;
            }

            if (double.TryParse(pattern, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
            {
                TrySetResult(shape.CellsU["LinePattern"], numeric);
            }
            else
            {
                TrySetFormula(shape, "LinePattern", pattern);
            }
        }

        private static bool TryParseColor(string? value, out (int R, int G, int B) color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var span = value.Trim();

            if (span.StartsWith("#", StringComparison.Ordinal))
            {
                span = span.Substring(1);
            }

            if (span.Length != 6 && span.Length != 8)
            {
                return false;
            }

            var offset = span.Length - 6;

            if (!int.TryParse(span.Substring(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
                !int.TryParse(span.Substring(offset + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
                !int.TryParse(span.Substring(offset + 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                return false;
            }

            color = (r, g, b);
            return true;
        }

                app.AlertResponse = 7;

                documents = app.Documents;
                document = documents.Add("");
                pages = document.Pages;
                page = pages[1];

                if (layout.Nodes.Length == 0)
                {
                    throw new InvalidDataException("Layout produced zero nodes.");
                }

                ComputePlacements(model, layout, placements, page);
                DrawConnectors(model, placements, page);

                document.SaveAs(outputPath);
            }
            finally
            {
                if (document != null)
                {
                    try { document.Close(); } catch { /* ignore */ }
                }

                if (app != null)
                {
                    try { app.Quit(); } catch { /* ignore */ }
                }

                foreach (var placement in placements.Values)
                {
                    ReleaseCom(placement.Shape);
                }

                ReleaseCom(page);
                ReleaseCom(pages);
                ReleaseCom(document);
                ReleaseCom(documents);
                ReleaseCom(app);
            }
        }

        private static dynamic CreateVisioApplication()
        {
            var progType = Type.GetTypeFromProgID("Visio.Application");
            if (progType == null)
            {
                throw new COMException("Visio is not installed or registered on this machine.");
            }

            var instance = Activator.CreateInstance(progType);
            if (instance == null)
            {
                throw new COMException("Failed to create Visio.Application instance.");
            }

            return instance;
        }

        private static void ComputePlacements(DiagramModel model, LayoutResult layout, IDictionary<string, NodePlacement> placements, dynamic page)
        {
            var nodeMap = model.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.OrdinalIgnoreCase);

            double minLeft = double.MaxValue;
            double minBottom = double.MaxValue;
            double maxRight = double.MinValue;
            double maxTop = double.MinValue;

            foreach (var nodeLayout in layout.Nodes)
            {
                var width = nodeLayout.Size.HasValue && nodeLayout.Size.Value.Width > 0
                    ? nodeLayout.Size.Value.Width
                    : (float)DefaultNodeWidth;

                var height = nodeLayout.Size.HasValue && nodeLayout.Size.Value.Height > 0
                    ? nodeLayout.Size.Value.Height
                    : (float)DefaultNodeHeight;

                var left = nodeLayout.Position.X;
                var bottom = nodeLayout.Position.Y;

                minLeft = Math.Min(minLeft, left);
                minBottom = Math.Min(minBottom, bottom);
                maxRight = Math.Max(maxRight, left + width);
                maxTop = Math.Max(maxTop, bottom + height);
            }

            var offsetX = Margin - minLeft;
            var offsetY = Margin - minBottom;
            var pageWidth = Math.Max(1.0, (maxRight - minLeft) + (Margin * 2));
            var pageHeight = Math.Max(1.0, (maxTop - minBottom) + (Margin * 2));

            TrySetResult(page.PageSheet.CellsU["PageWidth"], pageWidth);
            TrySetResult(page.PageSheet.CellsU["PageHeight"], pageHeight);

            foreach (var nodeLayout in layout.Nodes)
            {
                if (!nodeMap.TryGetValue(nodeLayout.Id, out var node))
                {
                    continue;
                }

                var width = nodeLayout.Size.HasValue && nodeLayout.Size.Value.Width > 0
                    ? nodeLayout.Size.Value.Width
                    : (float)DefaultNodeWidth;

                var height = nodeLayout.Size.HasValue && nodeLayout.Size.Value.Height > 0
                    ? nodeLayout.Size.Value.Height
                    : (float)DefaultNodeHeight;

                var left = nodeLayout.Position.X + offsetX;
                var bottom = nodeLayout.Position.Y + offsetY;
                var right = left + width;
                var top = bottom + height;

                dynamic shape = page.DrawRectangle(left, bottom, right, top);
                shape.Text = node.Label;
                ApplyNodeStyle(node, shape);

                placements[nodeLayout.Id] = new NodePlacement(shape, left, bottom, width, height);
            }
        }

        private static void DrawConnectors(DiagramModel model, IDictionary<string, NodePlacement> placements, dynamic page)
        {
            foreach (var edge in model.Edges)
            {
                if (!placements.TryGetValue(edge.SourceId, out var source) ||
                    !placements.TryGetValue(edge.TargetId, out var target))
                {
                    continue;
                }

                dynamic line = page.DrawLine(source.CenterX, source.CenterY, target.CenterX, target.CenterY);

                if (!string.IsNullOrWhiteSpace(edge.Label))
                {
                    line.Text = edge.Label;
                }

                if (edge.Directed)
                {
                    TrySetFormula(line, "EndArrow", "5");
                }
                else
                {
                    TrySetFormula(line, "EndArrow", "0");
                }

                ApplyEdgeStyle(edge, line);
                ReleaseCom(line);
            }
        }

        private static void DeleteErrorLog(string outputPath)
        {
            var logPath = GetErrorLogPath(outputPath);
            if (File.Exists(logPath))
            {
                try { File.Delete(logPath); } catch { /* ignore */ }
            }
        }

        private static void WriteErrorLog(string? outputPath, Exception ex)
        {
            var logPath = outputPath != null
                ? GetErrorLogPath(outputPath)
                : Path.Combine(Path.GetTempPath(), "vdg.runner.error.log");

            try
            {
                var builder = new StringBuilder();
                builder.AppendLine(DateTimeOffset.UtcNow.ToString("O"));
                builder.AppendLine(ex.GetType().FullName);
                builder.AppendLine(ex.Message);
                builder.AppendLine(ex.StackTrace ?? string.Empty);

                var directory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(logPath, builder.ToString());
            }
            catch
            {
                // Swallow logging failures.
            }
        }

        private static string GetErrorLogPath(string outputPath)
        {
            var directory = Path.GetDirectoryName(outputPath);
            var fileName = Path.GetFileNameWithoutExtension(outputPath);
            var logName = string.IsNullOrEmpty(fileName) ? "vdg-runner" : fileName;
            return Path.Combine(directory ?? string.Empty, logName + ".error.log");
        }

        private static void TrySetFormula(dynamic shape, string cellName, string formula)
        {
            try
            {
                shape.CellsU[cellName].FormulaU = formula;
            }
            catch (RuntimeBinderException)
            {
            }
            catch (COMException)
            {
            }
        }

        private static void TrySetResult(dynamic cell, double value)
        {
            try
            {
                cell.ResultIU = value;
            }
            catch (RuntimeBinderException)
            {
            }
            catch (COMException)
            {
            }
        }

        private static void ReleaseCom(object? instance)
        {
            if (instance != null && Marshal.IsComObject(instance))
            {
                try { Marshal.FinalReleaseComObject(instance); } catch { /* ignore */ }
            }
        }

        private sealed class NodePlacement
        {
            public NodePlacement(dynamic shape, double left, double bottom, double width, double height)
            {
                Shape = shape;
                Left = left;
                Bottom = bottom;
                Width = width;
                Height = height;
            }

            public dynamic Shape { get; }
            public double Left { get; }
            public double Bottom { get; }
            public double Width { get; }
            public double Height { get; }
            public double CenterX => Left + (Width / 2.0);
            public double CenterY => Bottom + (Height / 2.0);
        }

        private sealed class DiagramEnvelope
        {
            [JsonPropertyName("schemaVersion")]
            public string? SchemaVersion { get; set; }

            [JsonPropertyName("metadata")]
            public DiagramMetadataDto? Metadata { get; set; }

            [JsonPropertyName("nodes")]
            public List<NodeDto>? Nodes { get; set; }

            [JsonPropertyName("edges")]
            public List<EdgeDto>? Edges { get; set; }
        }

        private sealed class DiagramMetadataDto
        {
            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }

            [JsonPropertyName("tags")]
            public List<string>? Tags { get; set; }

            [JsonPropertyName("properties")]
            public Dictionary<string, string>? Properties { get; set; }
        }

        private sealed class NodeDto
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("label")]
            public string? Label { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("groupId")]
            public string? GroupId { get; set; }

            [JsonPropertyName("size")]
            public SizeDto? Size { get; set; }

            [JsonPropertyName("style")]
            public StyleDto? Style { get; set; }

            [JsonPropertyName("metadata")]
            public Dictionary<string, string>? Metadata { get; set; }
        }

        private sealed class EdgeDto
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("sourceId")]
            public string? SourceId { get; set; }

            [JsonPropertyName("targetId")]
            public string? TargetId { get; set; }

            [JsonPropertyName("label")]
            public string? Label { get; set; }

            [JsonPropertyName("directed")]
            public bool? Directed { get; set; }

            [JsonPropertyName("style")]
            public StyleDto? Style { get; set; }

            [JsonPropertyName("metadata")]
            public Dictionary<string, string>? Metadata { get; set; }
        }

        private sealed class SizeDto
        {
            [JsonPropertyName("width")]
            public float? Width { get; set; }

            [JsonPropertyName("height")]
            public float? Height { get; set; }
        }

        private sealed class StyleDto
        {
            [JsonPropertyName("fill")]
            public string? Fill { get; set; }

            [JsonPropertyName("stroke")]
            public string? Stroke { get; set; }

            [JsonPropertyName("linePattern")]
            public string? LinePattern { get; set; }
        }

    }
}



