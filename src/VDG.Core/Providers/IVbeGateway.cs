using System.Collections.Generic;

namespace VDG.Core.Providers
{
    /// <summary>
    /// Small seam over VBIDE to enable unit testing without Office.
    /// Implementations may wrap Microsoft.Vbe.Interop, but tests will mock this interface.
    /// </summary>
    public interface IVbeGateway
    {
        /// <summary>
        /// True when 'Trust access to the VBA project object model' is enabled and VBIDE is reachable.
        /// </summary>
        bool IsTrusted();

        /// <summary>
        /// Enumerate module names from the active project.
        /// </summary>
        IEnumerable<VbeModuleInfo> EnumerateModules();
    }

    /// <summary>
    /// POCO representing a VB module.
    /// </summary>
    public sealed record VbeModuleInfo(string Name);
}