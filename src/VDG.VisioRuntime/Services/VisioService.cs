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
    /// Concrete implementation of <see cref="IVisioService"/> for
    /// automating Microsoft Visio via COM.  This class owns at most one
    /// <see cref="Visio.Application"/> instance.  If an external instance
    /// is detected it will attach and never dispose it.  If no instance
    /// is found a new one is created and disposed when the service is
    /// disposed.  All short‑lived COM objects are released in
    /// <c>finally</c> blocks to avoid resource leaks.
    /// </summary>
    public sealed class VisioService : IVisioService
    {
        private Visio.Application? _app;
        private bool _ownsApp;

        // Cache for loaded stencil documents.  Stencils are keyed by
        // their original name or path, normalised to be case‑insensitive.
        private readonly Dictionary<string, Visio.Document> _stencilCache =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Attach to a running Visio instance or create a new one.  If an
        /// instance is created the service owns it and will quit it on
        /// disposal.  Otherwise the caller retains ownership of the
        /// external instance.
        /// </summary>
        /// <param name="visible">Whether the Visio UI should be shown.</param>
        public void AttachOrCreateVisio(bool visible = true)
        {
            if (_app != null)
            {
                // Already attached/created
                return;
            }

            try
            {
                // Attempt to attach to an existing instance.
                _app = (Visio.Application)Marshal.GetActiveObject("Visio.Application");
                _ownsApp = false;
            }
            catch
            {
                // If no instance is running create one and mark ownership.
                _app = new Visio.Application();
                _ownsApp = true;
            }

            _app.Visible = visible;
        }

        /// <summary>
        /// Ensure an active document and page exist.  Creates them if
        /// necessary.  Assumes <see cref="AttachOrCreateVisio"/> has been
        /// called previously.
        /// </summary>
        public void EnsureDocumentAndPage()
        {
            var app = _app ?? throw new InvalidOperationException("Visio application is not attached. Call AttachOrCreateVisio first.");

            Visio.Documents? docs = null;
            Visio.Document? doc = null;
            Visio.Pages? pages = null;
            Visio.Page? page = null;

            try
            {
                docs = app.Documents;

                // Ensure there is at least one document
                if (app.Documents.Count == 0)
                {
                    doc = docs.Add("");
                }
                else
                {
                    doc = app.ActiveDocument ?? docs.Add("");
                }

                pages = doc.Pages;

                page = app.ActivePage;
                if (page == null)
                {
                    page = pages.Count > 0 ? pages[1] : pages.Add();
                    if (app.ActiveWindow != null)
                    {
                        app.ActiveWindow.Page = page;
                    }
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

        /// <summary>
        /// Draw a basic shape centred at the specified coordinates.
        /// </summary>
        /// <inheritdoc />
        public int DrawShape(BasicShapeKind kind,
                             double centerXIn,
                             double centerYIn,
                             double widthIn,
                             double heightIn,
                             string? text = null)
        {
            var app = _app ?? throw new InvalidOperationException("Visio application is not attached.");
            Visio.Page? page = null;
            Visio.Shape? shape = null;

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
                        shape.CellsU["Rounding"].FormulaU = "0.15 in";
                        break;
                    case BasicShapeKind.Ellipse:
                        shape = page.DrawOval(left, bottom, right, top);
                        break;
                    default:
                        shape = page.DrawRectangle(left, bottom, right, top);
                        break;
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    shape.Text = text;
                }

                return shape.ID;
            }
            finally
            {
                Com.Release(ref shape);
                Com.Release(ref page);
            }
        }

        /// <summary>
        /// Draw a dynamic connector between two shapes.
        /// </summary>
        /// <inheritdoc />
        public int DrawConnector(int fromShapeId, int toShapeId, ConnectorKind kind = ConnectorKind.RightAngle)
        {
            var app = _app ?? throw new InvalidOperationException("Visio application is not attached.");
            Visio.Page? page = null;
            Visio.Shapes? shapes = null;
            Visio.Shape? from = null;
            Visio.Shape? to = null;
            Visio.Shape? connector = null;
            Visio.Cell? beginX = null;
            Visio.Cell? endX = null;

            try
            {
                page = app.ActivePage ?? throw new InvalidOperationException("No active page. Call EnsureDocumentAndPage() first.");
                shapes = page.Shapes;

                from = shapes.get_ItemFromID[fromShapeId];
                to = shapes.get_ItemFromID[toShapeId];

                // Use ConnectorToolDataObject to drop a dynamic connector without opening stencils
                var dataObj = app.ConnectorToolDataObject;
                connector = page.Drop(dataObj, 0, 0);

                // Glue begin and end points to the centre of shapes (PinX)
                beginX = connector.CellsU["BeginX"];
                endX = connector.CellsU["EndX"];
                beginX.GlueTo(from.CellsU["PinX"]);
                endX.GlueTo(to.CellsU["PinX"]);

                // Tweak connector style if requested
                switch (kind)
                {
                    case ConnectorKind.Straight:
                        // Attempt to remove routing jogs
                        connector.CellsU["LineRouteExt"].FormulaU = "0";
                        break;
                    case ConnectorKind.Curved:
                        // Rounded line caps for a curved look
                        connector.CellsU["LineCap"].FormulaU = "1";
                        break;
                    case ConnectorKind.RightAngle:
                    default:
                        // Default dynamic connector uses right angle routing
                        break;
                }

                return connector.ID;
            }
            finally
            {
                Com.Release(ref endX);
                Com.Release(ref beginX);
                Com.Release(ref connector);
                Com.Release(ref to);
                Com.Release(ref from);
                Com.Release(ref shapes);
                Com.Release(ref page);
            }
        }

        /// <summary>
        /// Load a stencil into the cache.  Stencils are opened in
        /// read‑only mode and docked so they do not prompt to save when
        /// Visio closes.  Duplicate loads are ignored.  Paths without
        /// extensions will be resolved using <see cref="ResolveStencilPath"/>.
        /// </summary>
        /// <inheritdoc />
        public void LoadStencil(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
                throw new ArgumentNullException(nameof(nameOrPath));

            var app = _app ?? throw new InvalidOperationException("Visio application is not attached.");
            if (_stencilCache.ContainsKey(nameOrPath))
            {
                return;
            }

            string full = ResolveStencilPath(app, nameOrPath);

            Visio.Document? stencil = null;
            try
            {
                short flags = (short)(Visio.VisOpenSaveArgs.visOpenDocked | Visio.VisOpenSaveArgs.visOpenRO);
                stencil = app.Documents.OpenEx(full, flags);
                _stencilCache[nameOrPath] = stencil;
            }
            catch
            {
                // Release if something failed; do not cache incomplete objects
                if (stencil != null)
                {
                    Com.Release(ref stencil);
                }
                throw;
            }
        }

        /// <summary>
        /// Drop a master from a stencil onto the page.  The stencil will
        /// be loaded on demand if necessary.
        /// </summary>
        /// <inheritdoc />
        public int DropMaster(string stencilNameOrPath,
                              string masterName,
                              double xIn,
                              double yIn,
                              double? wIn = null,
                              double? hIn = null,
                              string? text = null)
        {
            var app = _app ?? throw new InvalidOperationException("Visio application is not attached.");
            if (string.IsNullOrWhiteSpace(stencilNameOrPath))
                throw new ArgumentNullException(nameof(stencilNameOrPath));
            if (string.IsNullOrWhiteSpace(masterName))
                throw new ArgumentNullException(nameof(masterName));

            Visio.Page? page = null;
            Visio.Document? stencil = null;
            Visio.Master? master = null;
            Visio.Shape? shape = null;

            try
            {
                page = app.ActivePage ?? throw new InvalidOperationException("No active page. Call EnsureDocumentAndPage() first.");

                // Load stencil if not already present
                if (!_stencilCache.TryGetValue(stencilNameOrPath, out stencil))
                {
                    LoadStencil(stencilNameOrPath);
                    stencil = _stencilCache[stencilNameOrPath];
                }

                master = stencil.Masters.get_ItemU(masterName);
                shape = page.Drop(master, xIn, yIn);

                if (wIn.HasValue)
                {
                    shape.CellsU["Width"].ResultIU = wIn.Value;
                }
                if (hIn.HasValue)
                {
                    shape.CellsU["Height"].ResultIU = hIn.Value;
                }
                if (!string.IsNullOrWhiteSpace(text))
                {
                    shape.Text = text;
                }

                return shape.ID;
            }
            finally
            {
                Com.Release(ref shape);
                Com.Release(ref master);
                // Do not release stencil here; cached for reuse
                Com.Release(ref page);
            }
        }

        /// <summary>
        /// Set the text of a shape by ID.  Null or empty text clears the
        /// shape’s existing text.
        /// </summary>
        /// <inheritdoc />
        public void SetShapeText(int shapeId, string? text)
        {
            var app = _app ?? throw new InvalidOperationException("Visio application is not attached.");
            Visio.Page? page = null;
            Visio.Shapes? shapes = null;
            Visio.Shape? shape = null;
            try
            {
                page = app.ActivePage ?? throw new InvalidOperationException("No active page.");
                shapes = page.Shapes;
                shape = shapes.get_ItemFromID[shapeId];
                shape.Text = text ?? string.Empty;
            }
            finally
            {
                Com.Release(ref shape);
                Com.Release(ref shapes);
                Com.Release(ref page);
            }
        }

        /// <summary>
        /// Save the active document as a .vsdx file.  Normalises the
        /// extension.
        /// </summary>
        /// <inheritdoc />
        public void SaveAsVsdx(string fullPath)
        {
            var app = _app ?? throw new InvalidOperationException("Visio application is not attached.");
            if (string.IsNullOrWhiteSpace(fullPath))
                throw new ArgumentNullException(nameof(fullPath));
            var path = Path.ChangeExtension(fullPath, ".vsdx");

            Visio.Document? doc = null;
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

        /// <summary>
        /// Fit the active window view to the page.  This is useful for
        /// previewing the diagram after drawing.
        /// </summary>
        /// <inheritdoc />
        public void FitToPage()
        {
            var app = _app ?? throw new InvalidOperationException("Visio application is not attached.");
            Visio.Window? win = null;
            try
            {
                win = app.ActiveWindow;
                win?.ViewFit(Visio.VisWindowFit.visFitPage);
            }
            finally
            {
                Com.Release(ref win);
            }
        }

        /// <summary>
        /// Dispose the service.  If the service owns the Visio
        /// application it will be quit.  All loaded stencils are also
        /// released.
        /// </summary>
        public void Dispose()
        {
            // Release cached stencils
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
                try
                {
                    app.Quit();
                }
                catch
                {
                    // ignore errors quitting Visio
                }
                Com.Release(ref app);
            }

            // Encourage RCW cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// Resolve a stencil name into a full file system path.  If the
        /// supplied value already refers to an existing file it is returned
        /// unchanged.  Otherwise Visio’s stencil paths are searched and
        /// the first match is returned.  The search also attempts to
        /// append ".vssx" if no extension is present.
        /// </summary>
        /// <param name="app">The Visio application used to query stencil paths.</param>
        /// <param name="nameOrPath">Stencil name or path.</param>
        /// <returns>The resolved full path.</returns>
        private static string ResolveStencilPath(Visio.Application app, string nameOrPath)
        {
            // Absolute or relative file exists?  Return as is.
            if (File.Exists(nameOrPath))
            {
                return Path.GetFullPath(nameOrPath);
            }

            // Search Visio stencil paths
            string paths = app.StencilPaths;
            var directories = paths.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var dir in directories)
            {
                // candidate as provided
                var candidate = Path.Combine(dir, nameOrPath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                // candidate with .vssx extension if not present
                if (!Path.HasExtension(nameOrPath))
                {
                    var withExt = Path.Combine(dir, nameOrPath + ".vssx");
                    if (File.Exists(withExt))
                    {
                        return withExt;
                    }
                }
            }

            // Last resort: search relative to application base directory
            var baseDir = AppContext.BaseDirectory;
            var local = Path.Combine(baseDir, nameOrPath);
            if (File.Exists(local))
            {
                return local;
            }
            if (!Path.HasExtension(nameOrPath))
            {
                var localExt = Path.Combine(baseDir, nameOrPath + ".vssx");
                if (File.Exists(localExt))
                {
                    return localExt;
                }
            }

            throw new FileNotFoundException($"Stencil not found: '{nameOrPath}'.");
        }
    }
}