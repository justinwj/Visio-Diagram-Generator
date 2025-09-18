using System;
using System.Collections.Generic;
using System.Linq;
using VDG.Core;
using VDG.Core.Models;

namespace VDG.Core.Providers
{
    /// <summary>
    /// Optional provider that uses VBIDE when trust allows; otherwise falls back to a text-export provider.
    /// No COM is touched in unit tests; we inject IVbeGateway and a fallback IMapProvider.
    /// </summary>
    public sealed class VBAMapProvider : IMapProvider
    {
        private readonly IVbeGateway _vbe;
        private readonly IMapProvider _fallback;

        public VBAMapProvider(IVbeGateway vbe, IMapProvider fallback)
        {
            _vbe = vbe ?? throw new ArgumentNullException(nameof(vbe));
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
        }

        public IReadOnlyList<DiagramItem> GetItems()
        {
            try
            {
                if (!_vbe.IsTrusted())
                    return _fallback.GetItems();

                var items = new List<DiagramItem>();
                foreach (var m in _vbe.EnumerateModules())
                {
                    // Represent each module as a node; richer graph (procedures/calls) can be layered later.
                    items.Add(new DiagramItem(Guid.NewGuid().ToString(), typeName: "Module", label: m.Name, x: 0, y: 0));
                }
                return items;
            }
            catch
            {
                // On any error, fall back to the underlying provider.
                return _fallback.GetItems();
            }
        }

        public IReadOnlyList<DiagramConnection> GetConnections()
        {
            try
            {
                if (!_vbe.IsTrusted())
                    return _fallback.GetConnections();
                // First cut: no call graph edges from VBIDE; rely on fallback if it supplies edges.
                return Array.Empty<DiagramConnection>();
            }
            catch
            {
                return _fallback.GetConnections();
            }
        }
    }
}