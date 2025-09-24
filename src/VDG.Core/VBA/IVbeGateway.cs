using System.Collections.Generic;

namespace VDG.Core.Vba
{
    /// <summary>
    /// Abstraction over the Visual Basic for Applications (VBA) extensibility interface.
    /// Allows exporting modules from a VBA project for analysis without tightly coupling
    /// to the COM interfaces. Concrete implementations can wrap VBIDE or other providers.
    /// </summary>
    public interface IVbeGateway
    {
        /// <summary>
        /// Exports the modules contained in the specified VBA project file (.xlsm).
        /// Returns a collection of modules with their names and code text.
        /// </summary>
        /// <param name="projectFilePath">Path to a VBA enabled project (e.g. Excel .xlsm)</param>
        /// <returns>Enumeration of modules with their content.</returns>
        IEnumerable<VbaModule> ExportModules(string projectFilePath);
    }

    /// <summary>
    /// Simple representation of a VBA module for analysis purposes.
    /// </summary>
    public sealed class VbaModule
    {
        public string Name { get; }
        public string Code { get; }

        public VbaModule(string name, string code)
        {
            Name = name;
            Code = code;
        }
    }
}