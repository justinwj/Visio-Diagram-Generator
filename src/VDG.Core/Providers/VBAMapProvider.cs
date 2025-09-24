using System;
using System.Collections.Generic;
using VDG.Core.Models;
using VDG.Core.Vba;

namespace VDG.Core.Providers
{
    /// <summary>
    /// VBA-aware provider: when VBIDE access is trusted, emits one DiagramItem per module.
    /// On any error or when trust is disabled, cleanly falls back to the supplied provider.
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
                foreach (var module in _vbe.EnumerateModules())
                {
                    var name = module?.Name;
                    var label = string.IsNullOrWhiteSpace(name) ? "Module" : name!;

                    items.Add(new DiagramItem(
                        Id: Guid.NewGuid().ToString(),
                        TypeName: "Module",
                        Label: label,
                        X: 0,
                        Y: 0));
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

                // First cut: no edges from VBIDE; rely on fallback if it supplies edges.
                return Array.Empty<DiagramConnection>();
            }
            catch
            {
                return _fallback.GetConnections();
            }
        }
    }
}
