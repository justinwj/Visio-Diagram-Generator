using System.Collections.Generic;

namespace VDG.Core.Vba
{
    /// <summary>
    /// Abstraction over the Visual Basic for Applications (VBA) extensibility interface.
    /// Allows callers to query trust state, enumerate available modules, and export module
    /// contents for deeper analysis without tightly coupling to COM types.
    /// </summary>
    public interface IVbeGateway
    {
        /// <summary>
        /// True when "Trust access to the VBA project object model" is enabled and the host
        /// application exposes its VBIDE automation surface.
        /// </summary>
        bool IsTrusted();

        /// <summary>
        /// Enumerate modules that are currently available in the host project. Implementations
        /// may omit <see cref="VbaModule.Code"/> content when the intent is discovery only.
        /// </summary>
        IEnumerable<VbaModule> EnumerateModules();

        /// <summary>
        /// Exports the modules contained in the specified VBA project file (e.g. *.xlsm) and
        /// returns their code so that downstream analyzers can construct call graphs.
        /// </summary>
        /// <param name="projectFilePath">Path to a VBA-enabled project.</param>
        IEnumerable<VbaModule> ExportModules(string projectFilePath);
    }

    /// <summary>
    /// Simple representation of a VBA module for analysis purposes.
    /// </summary>
    public sealed class VbaModule
    {
        public string Name { get; }
        public string? Code { get; }

        public VbaModule(string name, string? code)
        {
            Name = name;
            Code = code;
        }
    }
}
