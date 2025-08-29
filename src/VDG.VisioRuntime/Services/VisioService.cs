#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using VDG.VisioRuntime.Interop;
using Visio = Microsoft.Office.Interop.Visio;

namespace VDG.VisioRuntime.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IVisioService"/> for Visio COM automation.
    /// Manages a single Visio.Application; releases all short‑lived COM RCWs.
    /// </summary>
    public sealed class VisioService : IVisioService
    {
        private Visio.Application _app;
        private bool _ownsApp;

        // Cache opened stencils (case‑insensitive key).
        private readonly Dictionary<string, Visio.Document> _stencilCache =
            new(StringComparer.OrdinalIgnoreCase);

        public void AttachOrCreateVisio(bool visible = true)
        {
            if (_app != null) return;

            try
            {
                _app = (Visio.Application)Marshal.GetActiveObject("Visio.Application");
                _ownsApp = false;
            }
            catch
            {
                _app = new Visio.Application();
                _ownsApp = true;
            }

            _app.Visible = visible;
        }

        public void EnsureDocumentAndPage()
        {
            var app = _app ?? throw new InvalidOperationException("Visio not attached. Call AttachOrCreateVisio first.");

            Visio.Documents docs = null;
            Visio.Document doc = null;
            Visio.Pages pages = null;
            Visio.Page page = null;

            try
            {
                docs = app.Documents;
                doc = app.Documents.Count == 0 ? docs.Add("") : (app.ActiveDocument ?? docs.Add(""));
                pages = doc.Pages;

                page = app.ActivePage;
                if (page == null)
                {
                    page = pages.Count > 0 ? pages[1] : pages.Add();
                    if (app.ActiveWindow != null) app.ActiveWindow.Page = page;
                }
            }
            finally
            {
                Com.Release(ref page);
                Com.Release(ref pages);
                Com.Release(ref doc);
                Com.Release(ref docs);
            }
        }

        public int DrawShape(BasicShapeKind kind, double centerXIn, double centerYIn,
                             double widthIn, double heightIn, string text = null)
        {
            var app = _app ?? throw new InvalidOperationException("Visio not attached.");
            Visio.Page page = null;
            Visio.Shape shape = null;
            Visio.Cell rounding = null;

            try
            {
                page = app.ActivePage ?? throw new InvalidOperationException("No active page. Call EnsureDocumentAndPage() first.");

                double halfW = Math.Abs(widthIn) / 2.0;
                double halfH = Math.Abs(heightIn) / 2.0;
                double left = centerXIn - halfW;
                double bottom = centerYIn - halfH;
                double right = centerXIn + halfW;
                double top = centerYIn + halfH;

                switch (kind)
                {
                    case BasicShapeKind.Rectangle:
                        shape = page.DrawRectangle(left, bottom, right, top);
                        break;

                    case BasicShapeKind.RoundedRectangle:
                        shape = page.DrawRectangle(left, bottom, right, top);
                        rounding = shape.get_CellsU("Rounding");
                        rounding.FormulaU = "0.15 in";
                        break;

                    case BasicShapeKind.Ellipse:
                        shape = page.DrawOval(left, bottom, right, top);
                        break;

                    default:
                        shape = page.DrawRectangle(left, bottom, right, top);
                        break;
                }

                if (!string.IsNullOrWhiteSpace(text))
                    shape.Text = text;

                return shape.ID;
            }
            finally
            {
                Com.Release(ref rounding);
                Com.Release(ref shape);
                Com.Release(ref page);
            }
        }

        public int DrawConnector(int fromShapeId, int toShapeId, ConnectorKind kind = ConnectorKind.RightAngle)
        {
            var app = _app ?? throw new InvalidOperationException("Visio not attached.");
            Visio.Page page = null;
            Visio.Shapes shapes = null;
            Visio.Shape from = null, to = null, connector = null;
            Visio.Cell beginX = null, endX = null, fromPinX = null, toPinX = null;
            Visio.Cell lineRouteExt = null, lineCap = null;

            try
            {
                page = app.ActivePage ?? throw new InvalidOperationException("No active page. Call EnsureDocumentAndPage() first.");
                shapes = page.Shapes;

                // Correct: get_ItemFromID is a method, not an indexer
                from = shapes.get_ItemFromID(fromShapeId);
                to   = shapes.get_ItemFromID(toShapeId);

                // Drop dynamic connector without opening stencils
                var dataObj = app.ConnectorToolDataObject;
                connector = page.Drop(dataObj, 0, 0);

                // Glue begin/end to shape centers (PinX)
                beginX   = connector.get_CellsU("BeginX");
                endX     = connector.get_CellsU("EndX");
                fromPinX = from.get_CellsU("PinX");
                toPinX   = to.get_CellsU("PinX");

                beginX.GlueTo(fromPinX);
                endX.GlueTo(toPinX);

                // Optional styling
                switch (kind)
                {
                    case ConnectorKind.Straight:
                        lineRouteExt = connector.get_CellsU("LineRouteExt");
                        lineRouteExt.FormulaU = "0";
                        break;

                    case ConnectorKind.Curved:
                        lineCap = connector.get_CellsU("LineCap");
                        lineCap.FormulaU = "1";
                        break;

                    case ConnectorKind.RightAngle:
                    default:
                        // default dynamic routing already right‑angle
                        break;
                }

                return connector.ID;
            }
            finally
            {
                Com.Release(ref lineCap);
                Com.Release(ref lineRouteExt);
                Com.Release(ref toPinX);
                Com.Release(ref fromPinX);
                Com.Release(ref endX);
                Com.Release(ref beginX);
                Com.Release(ref connector);
                Com.Release(ref to);
                Com.Release(ref from);
                Com.Release(ref shapes);
                Com.Release(ref page);
            }
        }

        public void LoadStencil(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
                throw new ArgumentNullException(nameof(nameOrPath));

            var app = _app ?? throw new InvalidOperationException("Visio not attached.");
            if (_stencilCache.ContainsKey(nameOrPath)) return;

            string full = ResolveStencilPath(app, nameOrPath);

            Visio.Document stencil = null;
            try
            {
                short flags = (short)(Visio.VisOpenSaveArgs.visOpenDocked | Visio.VisOpenSaveArgs.visOpenRO);
                stencil = app.Documents.OpenEx(full, flags);
                _stencilCache[nameOrPath] = stencil;
            }
            catch
            {
                if (stencil != null) Com.Release(ref stencil);
                throw;
            }
        }

        public int DropMaster(string stencilNameOrPath, string masterName,
                              double xIn, double yIn, double? wIn = null, double? hIn = null, string text = null)
        {
            var app = _app ?? throw new InvalidOperationException("Visio not attached.");
            if (string.IsNullOrWhiteSpace(stencilNameOrPath)) throw new ArgumentNullException(nameof(stencilNameOrPath));
            if (string.IsNullOrWhiteSpace(masterName))        throw new ArgumentNullException(nameof(masterName));

            Visio.Page page = null;
            Visio.Document stencil = null;
            Visio.Master master = null;
            Visio.Shape shape = null;
            Visio.Cell width = null, height = null;

            try
            {
                page = app.ActivePage ?? throw new InvalidOperationException("No active page. Call EnsureDocumentAndPage() first.");

                if (!_stencilCache.TryGetValue(stencilNameOrPath, out stencil))
                {
                    LoadStencil(stencilNameOrPath);
                    stencil = _stencilCache[stencilNameOrPath];
                }

                master = stencil.Masters.get_ItemU(masterName);
                shape  = page.Drop(master, xIn, yIn);

                if (wIn.HasValue)
                {
                    width = shape.get_CellsU("Width");
                    width.ResultIU = wIn.Value;
                }
                if (hIn.HasValue)
                {
                    height = shape.get_CellsU("Height");
                    height.ResultIU = hIn.Value;
                }
                if (!string.IsNullOrWhiteSpace(text))
                {
                    shape.Text = text;
                }

                return shape.ID;
            }
            finally
            {
                Com.Release(ref height);
                Com.Release(ref width);
                Com.Release(ref shape);
                Com.Release(ref master);
                // stencil remains cached
                Com.Release(ref page);
            }
        }

        public void SetShapeText(int shapeId, string text)
        {
            var app = _app ?? throw new InvalidOperationException("Visio not attached.");
            Visio.Page page = null;
            Visio.Shapes shapes = null;
            Visio.Shape shape = null;

            try
            {
                page = app.ActivePage ?? throw new InvalidOperationException("No active page.");
                shapes = page.Shapes;
                shape  = shapes.get_ItemFromID(shapeId);
                shape.Text = text ?? string.Empty;
            }
            finally
            {
                Com.Release(ref shape);
                Com.Release(ref shapes);
                Com.Release(ref page);
            }
        }

        public void SaveAsVsdx(string fullPath)
        {
            var app = _app ?? throw new InvalidOperationException("Visio not attached.");
            if (string.IsNullOrWhiteSpace(fullPath)) throw new ArgumentNullException(nameof(fullPath));
            var path = Path.ChangeExtension(fullPath, ".vsdx");

            Visio.Document doc = null;
            try
            {
                doc = app.ActiveDocument ?? throw new InvalidOperationException("No active document to save.");
                doc.SaveAs(path);
            }
            finally
            {
                Com.Release(ref doc);
            }
        }

        public void FitToPage()
        {
            var app = _app ?? throw new InvalidOperationException("Visio not attached.");
            Visio.Window win = null;
            try
            {
                win = app.ActiveWindow;
                if (win != null)
                {
                    // Correct: ViewFit is a property, not a method
                    win.ViewFit = (short)Visio.VisWindowFit.visFitPage;
                }
            }
            finally
            {
                Com.Release(ref win);
            }
        }

        public void Dispose()
        {
            foreach (var key in _stencilCache.Keys.ToList())
            {
                var doc = _stencilCache[key];
                _stencilCache.Remove(key);
                Com.Release(ref doc);
            }

            if (_ownsApp && _app != null)
            {
                var app = _app;
                _app = null;
                try { app.Quit(); } catch { /* ignore */ }
                Com.Release(ref app);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private static string ResolveStencilPath(Visio.Application app, string nameOrPath)
        {
            if (File.Exists(nameOrPath)) return Path.GetFullPath(nameOrPath);

            string paths = app.StencilPaths;
            var directories = paths.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var dir in directories)
            {
                var candidate = Path.Combine(dir, nameOrPath);
                if (File.Exists(candidate)) return candidate;

                if (!Path.HasExtension(nameOrPath))
                {
                    var withExt = Path.Combine(dir, nameOrPath + ".vssx");
                    if (File.Exists(withExt)) return withExt;
                }
            }

            var baseDir = AppContext.BaseDirectory;
            var local = Path.Combine(baseDir, nameOrPath);
            if (File.Exists(local)) return local;

            if (!Path.HasExtension(nameOrPath))
            {
                var localExt = Path.Combine(baseDir, nameOrPath + ".vssx");
                if (File.Exists(localExt)) return localExt;
            }

            throw new FileNotFoundException($"Stencil not found: '{nameOrPath}'.");
        }
    }
}
