using System;
using System.Collections.Generic;

using VDG.Core.Contracts; // single import: types live directly under Contracts

namespace VDG.Core.Providers
{
    /// <summary>
    /// Optional provider that uses VBIDE when trust allows; otherwise falls back to a text-export provider.
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
                    items.Add(new DiagramItem(
                        id: Guid.NewGuid().ToString(),
                        typeName: "Module",
                        label: m.Name,
                        x: 0, y: 0
                    ));
                }
                return items;
            }
            catch
            {
                return _fallback.GetItems();
            }
        }

        public IReadOnlyList<DiagramConnection> GetConnections()
        {
            try
            {
                if (!_vbe.IsTrusted())
                    return _fallback.GetConnections();
                return Array.Empty<DiagramConnection>();
            }
            catch
            {
                return _fallback.GetConnections();
            }
        }
    }
}
