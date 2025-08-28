using System;

namespace VDG.VisioRuntime.Services
{
    /// <summary>
    /// Enumeration for the basic shapes that can be drawn without
    /// stencils.  Coordinates and sizes are expressed in inches.
    /// </summary>
    public enum BasicShapeKind
    {
        Rectangle,
        RoundedRectangle,
        Ellipse
    }

    /// <summary>
    /// Enumeration for connector styles.  Right angle routing is the
    /// default in Visio.  Additional kinds may be added later.
    /// </summary>
    public enum ConnectorKind
    {
        RightAngle,
        Straight,
        Curved
    }

    /// <summary>
    /// Abstraction over the Visio COM model.  This interface exposes
    /// high‑level operations for creating and manipulating diagrams.  It
    /// hides all COM interop details from callers and ensures proper
    /// resource cleanup via <see cref="IDisposable"/>.
    /// </summary>
    public interface IVisioService : IDisposable
    {
        /// <summary>
        /// Attach to a running instance of Visio or create a new one if
        /// none exists.  Visibility can be controlled via the
        /// <paramref name="visible"/> parameter.
        /// </summary>
        /// <param name="visible">Whether the Visio UI should be shown.</param>
        void AttachOrCreateVisio(bool visible = true);

        /// <summary>
        /// Ensure that there is an active document and page to draw on.
        /// If no document or page exists they will be created.
        /// </summary>
        void EnsureDocumentAndPage();

        /// <summary>
        /// Draw a basic shape centred at <paramref name="centerXIn"/>,
        /// <paramref name="centerYIn"/> measured in inches.
        /// </summary>
        /// <param name="kind">The kind of shape to draw.</param>
        /// <param name="centerXIn">X coordinate of the shape centre in inches.</param>
        /// <param name="centerYIn">Y coordinate of the shape centre in inches.</param>
        /// <param name="widthIn">Width of the shape in inches.</param>
        /// <param name="heightIn">Height of the shape in inches.</param>
        /// <param name="text">Optional text label.</param>
        /// <returns>The ID of the created shape.</returns>
        int DrawShape(BasicShapeKind kind,
                      double centerXIn,
                      double centerYIn,
                      double widthIn,
                      double heightIn,
                      string? text = null);

        /// <summary>
        /// Create a dynamic connector between two shapes by ID.
        /// </summary>
        /// <param name="fromShapeId">The ID of the shape where the connector begins.</param>
        /// <param name="toShapeId">The ID of the shape where the connector ends.</param>
        /// <param name="kind">Optional connector style.</param>
        /// <returns>The ID of the created connector.</returns>
        int DrawConnector(int fromShapeId, int toShapeId, ConnectorKind kind = ConnectorKind.RightAngle);

        /// <summary>
        /// Load a stencil document by name or path.  If the stencil has
        /// already been loaded this call is a no‑op.  The stencil is
        /// cached within the service until disposed.  Names without an
        /// extension will be resolved through Visio’s stencil search paths
        /// (for example "BASIC_U.VSSX").
        /// </summary>
        /// <param name="nameOrPath">Stencil filename or full path.</param>
        void LoadStencil(string nameOrPath);

        /// <summary>
        /// Drop a master shape from a stencil onto the page at the given
        /// coordinates.  Optional width, height and text can be applied
        /// after dropping.  The stencil will be automatically loaded
        /// if not already present.
        /// </summary>
        /// <param name="stencilNameOrPath">Stencil filename or path.</param>
        /// <param name="masterName">Name of the master to drop.</param>
        /// <param name="xIn">X coordinate in inches.</param>
        /// <param name="yIn">Y coordinate in inches.</param>
        /// <param name="wIn">Optional width in inches.</param>
        /// <param name="hIn">Optional height in inches.</param>
        /// <param name="text">Optional text for the shape.</param>
        /// <returns>The ID of the created shape.</returns>
        int DropMaster(string stencilNameOrPath,
                       string masterName,
                       double xIn,
                       double yIn,
                       double? wIn = null,
                       double? hIn = null,
                       string? text = null);

        /// <summary>
        /// Set the text of an existing shape by ID.  Passing null will
        /// clear any existing text.
        /// </summary>
        /// <param name="shapeId">ID of the shape.</param>
        /// <param name="text">New text for the shape.</param>
        void SetShapeText(int shapeId, string? text);

        /// <summary>
        /// Save the active document as a VSDX file.  The extension will
        /// automatically be normalised to ".vsdx".
        /// </summary>
        /// <param name="fullPath">Full path for the output file.</param>
        void SaveAsVsdx(string fullPath);

        /// <summary>
        /// Fit the active window view to the current page.  Useful for
        /// previewing diagrams before saving or exporting.
        /// </summary>
        void FitToPage();
    }
}