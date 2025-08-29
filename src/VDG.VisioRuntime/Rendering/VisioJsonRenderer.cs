#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using VDG.VisioRuntime.Models;
using VDG.VisioRuntime.Services;

namespace VDG.VisioRuntime.Rendering
{
    /// <summary>
    /// Render diagrams from a JSON spec using IVisioService.
    /// </summary>
    public static class VisioJsonRenderer
    {
        public static void RenderJsonFromFile(IVisioService service, string path)
        {
            if (service is null) throw new ArgumentNullException(nameof(service));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            var json = File.ReadAllText(path);
            RenderJson(service, json);
        }

        public static void RenderJson(IVisioService service, string json)
        {
            if (service is null) throw new ArgumentNullException(nameof(service));
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentNullException(nameof(json));

            var spec = JsonConvert.DeserializeObject<DiagramSpec>(json)
                       ?? throw new InvalidDataException("Invalid diagram JSON.");

            // Load stencils up front (optional)
            if (spec.Stencils != null)
            {
                foreach (var s in spec.Stencils)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                        service.LoadStencil(s);
                }
            }

            // Draw nodes (null-safe iteration)
            var idToShape = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in (spec.Nodes ?? new List<NodeSpec>()))
            {
                var x = node.X ?? 0.0;
                var y = node.Y ?? 0.0;
                var w = node.W <= 0 ? 2.0 : node.W;
                var h = node.H <= 0 ? 1.0 : node.H;

                int id;
                if (!string.IsNullOrWhiteSpace(node.Master))
                {
                    // Drop by master; allow per-node stencil override
                    var stencil = node.Stencil ?? (spec.Stencils != null && spec.Stencils.Length > 0 ? spec.Stencils[0] : null);
                    if (string.IsNullOrWhiteSpace(stencil))
                        throw new InvalidDataException($"Node '{node.Id}' specifies a master but no stencil is available.");
                    id = service.DropMaster(stencil!, node.Master!, x, y, w, h, node.Text);
                }
                else
                {
                    var kind = ParseKind(node.Kind);
                    id = service.DrawShape(kind, x, y, w, h, node.Text);
                }

                if (!string.IsNullOrWhiteSpace(node.Id))
                    idToShape[node.Id] = id;
            }

            // Draw edges (null-safe iteration)
            foreach (var edge in (spec.Edges ?? new List<EdgeSpec>()))
            {
                if (!idToShape.TryGetValue(edge.From, out var fromId))
                    throw new InvalidDataException($"Edge refers to unknown node '{edge.From}'.");
                if (!idToShape.TryGetValue(edge.To, out var toId))
                    throw new InvalidDataException($"Edge refers to unknown node '{edge.To}'.");

                var connKind = ConnectorKind.RightAngle; // future: parse edge.Kind
                var cid = service.DrawConnector(fromId, toId, connKind);
                if (!string.IsNullOrWhiteSpace(edge.Label))
                    service.SetShapeText(cid, edge.Label);
            }

            service.FitToPage();
        }

        private static BasicShapeKind ParseKind(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return BasicShapeKind.Rectangle;
            return value!.Trim().ToLowerInvariant() switch
            {
                "rectangle" or "rect" => BasicShapeKind.Rectangle,
                "rounded" or "roundedrectangle" or "roundrect" => BasicShapeKind.RoundedRectangle,
                "ellipse" or "oval" => BasicShapeKind.Ellipse,
                _ => BasicShapeKind.Rectangle
            };
        }

    }
}
