using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using VDG.VisioRuntime.Models;
using VDG.VisioRuntime.Services;

namespace VDG.VisioRuntime.Rendering
{
    /// <summary>
    /// Helper class to render diagrams from JSON specifications.  The
    /// static methods on this type parse the JSON into a
    /// <see cref="DiagramSpec"/> and then issue drawing commands via an
    /// <see cref="IVisioService"/>.  Basic auto‑layout is applied for
    /// nodes missing explicit coordinates.
    /// </summary>
    public static class VisioJsonRenderer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Render a diagram from a JSON file path.
        /// </summary>
        /// <param name="service">The Visio service used to draw.</param>
        /// <param name="jsonPath">Path to the JSON file.</param>
        public static void RenderFile(IVisioService service, string jsonPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
                throw new ArgumentNullException(nameof(jsonPath));
            var json = File.ReadAllText(jsonPath);
            RenderJson(service, json);
        }

        /// <summary>
        /// Render a diagram from a JSON string.
        /// </summary>
        /// <param name="service">The Visio service used to draw.</param>
        /// <param name="json">The JSON specification.</param>
        public static void RenderJson(IVisioService service, string json)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (json == null) throw new ArgumentNullException(nameof(json));

            var spec = JsonSerializer.Deserialize<DiagramSpec>(json, Options) ??
                       throw new InvalidDataException("Invalid diagram JSON specification.");

            // Ensure at least one page exists
            service.EnsureDocumentAndPage();

            // Load stencils
            if (spec.Stencils != null)
            {
                foreach (var s in spec.Stencils)
                {
                    service.LoadStencil(s);
                }
            }

            // Assign coordinates if missing
            AutoLayoutIfNeeded(spec);

            // Build nodes and map their IDs
            var idToShape = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in spec.Nodes)
            {
                int shapeId;
                if (!string.IsNullOrWhiteSpace(node.Master))
                {
                    var stencil = node.Stencil ?? spec.Stencils?.FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(stencil))
                    {
                        // Fallback to BASIC_U if nothing was provided
                        stencil = "BASIC_U.VSSX";
                    }
                    shapeId = service.DropMaster(stencil!, node.Master!, node.X!.Value, node.Y!.Value, node.W, node.H, node.Text);
                }
                else
                {
                    // Parse kind string; default to rectangle
                    var kind = ParseKind(node.Kind);
                    shapeId = service.DrawShape(kind, node.X!.Value, node.Y!.Value, node.W, node.H, node.Text);
                }
                idToShape[node.Id] = shapeId;
            }

            // Draw edges
            foreach (var edge in spec.Edges)
            {
                if (!idToShape.TryGetValue(edge.From, out var fromId))
                    throw new InvalidDataException($"Edge refers to unknown node '{edge.From}'.");
                if (!idToShape.TryGetValue(edge.To, out var toId))
                    throw new InvalidDataException($"Edge refers to unknown node '{edge.To}'.");

                var connectorId = service.DrawConnector(fromId, toId);
                if (!string.IsNullOrWhiteSpace(edge.Label))
                {
                    service.SetShapeText(connectorId, edge.Label);
                }
            }

            // Fit the view
            service.FitToPage();
        }

        /// <summary>
        /// Assign simple grid coordinates to nodes that do not specify
        /// explicit X and Y values.  Nodes that already have coordinates
        /// remain unaffected.
        /// </summary>
        /// <param name="spec">The diagram specification.</param>
        private static void AutoLayoutIfNeeded(DiagramSpec spec)
        {
            bool needs = spec.Nodes.Exists(n => !n.X.HasValue || !n.Y.HasValue);
            if (!needs) return;

            int count = spec.Nodes.Count;
            int cols = (int)Math.Ceiling(Math.Sqrt(count));
            int rows = (int)Math.Ceiling((double)count / cols);

            double x0 = 2.0;
            double y0 = 8.0;
            double xStep = 3.0;
            double yStep = 2.0;
            for (int i = 0; i < count; i++)
            {
                int r = i / cols;
                int c = i % cols;
                var node = spec.Nodes[i];
                if (!node.X.HasValue) node.X = x0 + c * xStep;
                if (!node.Y.HasValue) node.Y = y0 - r * yStep;
            }
        }

        /// <summary>
        /// Parse a free‑form kind string into a <see cref="BasicShapeKind"/>.
        /// Recognised values include "rectangle", "rounded", "rounded rectangle",
        /// "ellipse" and "oval".  Defaults to rectangle on unknown input.
        /// </summary>
        /// <param name="kind">The string kind.</param>
        /// <returns>The parsed enumeration value.</returns>
        private static BasicShapeKind ParseKind(string? kind)
        {
            return (kind ?? "rectangle").Trim().ToLowerInvariant() switch
            {
                "rounded" or "rounded rectangle" or "rounded-rectangle" => BasicShapeKind.RoundedRectangle,
                "ellipse" or "oval" => BasicShapeKind.Ellipse,
                _ => BasicShapeKind.Rectangle
            };
        }
    }
}