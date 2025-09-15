using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Office.Interop.Visio;
using VDG.Core.Contracts.Templates;

namespace VDG.VisioRuntime.Templates
{
    public sealed class TemplateManagerImpl : ITemplateManager
    {
        private Application? _app;
        private Document? _drawing;

        private readonly Dictionary<string, Document> _stencils =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, Dictionary<string, Master>> _masters =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, MasterKey> _map =
            new(StringComparer.Ordinal);

        public void Prepare(TemplateSpec spec)
        {
            if (_app != null) throw new InvalidOperationException("TemplateManager already prepared.");
            if (spec is null) throw new ArgumentNullException(nameof(spec));

            // Start Visio (STA runner)
            _app = new Application();

            // Resolve .vstx and open hidden
            var templatePath = ResolveExistingFile(spec.TemplatePath);
            _drawing = _app.Documents.OpenEx(templatePath, (short)VisOpenSaveArgs.visOpenHidden);

            EnsurePage();
            ApplyThemeIfRequested(spec);

            // Load stencils hidden + read-only and cache masters by NameU
            foreach (var stencil in spec.Stencils)
            {
                var stencilPath = ResolveExistingFile(stencil.Path);
                var doc = _app.Documents.OpenEx(stencilPath,
                    (short)(VisOpenSaveArgs.visOpenHidden | VisOpenSaveArgs.visOpenRO));

                _stencils[stencil.Key] = doc;

                var comparer = spec.NameUMatch.Equals("caseInsensitive", StringComparison.OrdinalIgnoreCase)
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal;

                var byNameU = new Dictionary<string, Master>(comparer);
                foreach (Master m in doc.Masters)
                    if (!byNameU.ContainsKey(m.NameU)) byNameU[m.NameU] = m;

                _masters[stencil.Key] = byNameU;
            }

            // Build logicalType -> MasterKey map ("Key!NameU")
            foreach (var kv in spec.ShapeMapping)
            {
                var parts = kv.Value.Split('!');
                if (parts.Length != 2)
                    throw new ArgumentException($"ShapeMapping '{kv.Key}' must be 'StencilKey!NameU'.");

                var stencilKey = parts[0];
                var nameU = parts[1];

                if (!_masters.TryGetValue(stencilKey, out var masters) || !masters.ContainsKey(nameU))
                {
                    var known = string.Join(", ", _masters.Keys.OrderBy(k => k));
                    throw new KeyNotFoundException(
                        $"Master '{kv.Value}' not found for logicalType '{kv.Key}'. Loaded stencils: {known}");
                }

                _map[kv.Key] = new MasterKey(stencilKey, nameU);
            }

            if (!string.IsNullOrWhiteSpace(spec.FallbackMaster))
            {
                var parts = spec.FallbackMaster!.Split('!');
                if (parts.Length == 2 &&
                    _masters.TryGetValue(parts[0], out var masters) &&
                    masters.ContainsKey(parts[1]))
                {
                    _map["__fallback__"] = new MasterKey(parts[0], parts[1]);
                }
            }
        }

        private static string ResolveExistingFile(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                throw new ArgumentException("Path is null/empty.", nameof(rawPath));

            // Normalize + expand environment variables
            var raw = Environment.ExpandEnvironmentVariables(rawPath.Trim().Trim('"').Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));

            // If absolute and exists -> return
            if (Path.IsPathRooted(raw) && File.Exists(raw))
                return Path.GetFullPath(raw);

            // Candidate roots to try for relative paths
            var candidates = new List<string>();

            // 1) Current working dir
            candidates.Add(Path.Combine(Environment.CurrentDirectory, raw));

            // 2) EXE directory
            var exeDir = AppContext.BaseDirectory;
            candidates.Add(Path.Combine(exeDir, raw));

            // 3) Walk up to 8 parents from EXE dir
            var dir = new DirectoryInfo(exeDir);
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
            {
                candidates.Add(Path.Combine(dir.FullName, raw));
            }

            // 4) If path starts with "shared" (repo convention), also probe "repoRoot/shared/..."
            //    by walking up until we find "src" and then trying its parent as repoRoot.
            dir = new DirectoryInfo(exeDir);
            for (int i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
            {
                if (string.Equals(dir.Name, "src", StringComparison.OrdinalIgnoreCase) && dir.Parent != null)
                {
                    var repoRoot = dir.Parent.FullName;
                    candidates.Add(Path.Combine(repoRoot, raw));
                    break;
                }
            }

            foreach (var c in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(c))
                    return Path.GetFullPath(c);
            }

            var msg = "File not found: '" + rawPath + "'. Tried:\n  - " + string.Join("\n  - ", candidates);
            throw new FileNotFoundException(msg);
        }

        private void EnsurePage()
        {
            if (_app == null) return;

            if (_app.ActiveDocument is null)
                _app.Documents.Add("");

            if (_app.ActivePage is null)
                _app.ActiveDocument.Pages.Add();
        }

        private void ApplyThemeIfRequested(TemplateSpec spec)
        {
            if (_app == null) return;
            var page = _app.ActivePage;
            if (page == null) return;

            // Either a built-in theme name or .thmx path (path can be relative)
            string? theme = null;
            if (!string.IsNullOrWhiteSpace(spec.ThemePath))
            {
                try { theme = ResolveExistingFile(spec.ThemePath!); } catch { /* ignore if not found */ }
            }
            if (theme == null && !string.IsNullOrWhiteSpace(spec.ThemeName))
                theme = spec.ThemeName!;

            if (theme == null) return;

            // Visio applies themes at the Page level; Document.* members don’t expose ApplyTheme.
            try { page.ApplyTheme(theme); } catch { /* Visio version without ApplyTheme → ignore */ }

            // Best‑effort variant (1..4) via late binding; harmless if not available
            var v = spec.ThemeVariant.GetValueOrDefault();
            if (v >= 1 && v <= 4)
            {
                try
                {
                    var variants = page.GetType().InvokeMember(
                        "ThemeVariants",
                        System.Reflection.BindingFlags.GetProperty,
                        null, page, null);

                    if (variants != null)
                    {
                        var variantObj = variants.GetType().InvokeMember(
                            "Item",
                            System.Reflection.BindingFlags.GetProperty,
                            null, variants, new object[] { (short)(v - 1) });
                        variantObj?.GetType().GetMethod("Apply")?.Invoke(variantObj, null);
                    }
                }
                catch { /* optional, non-fatal */ }
            }
        }

        public bool TryResolveMaster(string logicalType, out MasterKey key)
        {
            if (_app == null) throw new InvalidOperationException("Prepare must be called first.");

            if (_map.TryGetValue(logicalType, out key)) return true;
            if (_map.TryGetValue("__fallback__", out key)) return true;

            key = default;
            return false;
        }

        public IEnumerable<MasterInfo> ListStencilMasters(int take = 25)
        {
            foreach (var s in _masters)
                foreach (var m in s.Value.Take(take))
                    yield return new MasterInfo(s.Key, m.Value.Name, m.Value.NameU);
        }

        public void Dispose()
        {
            foreach (var d in _stencils.Values)
            {
                try { d.Close(); } catch { }
                Marshal.FinalReleaseComObject(d);
            }
            _stencils.Clear();
            _masters.Clear();

            if (_drawing != null) { try { _drawing.Close(); } catch { } Marshal.FinalReleaseComObject(_drawing); _drawing = null; }
            if (_app != null) { try { _app.Quit(); } catch { } Marshal.FinalReleaseComObject(_app); _app = null; }
        }
    }
}
