using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using VDG.Core.Models;
using VDG.Core.Vba;

namespace VDG.Core.Analysis
{
    /// <summary>
    /// Builds a call-graph diagram from VBA modules. Each procedure (Sub or Function)
    /// becomes a node in the resulting <see cref="DiagramModel"/>. Edges represent
    /// calls from one procedure to another. Built-in VBA functions are ignored.
    /// </summary>
    public static class ProcedureGraphBuilder
    {
        // Regex to capture Sub or Function declarations: captures the procedure name in group 2
        private static readonly Regex ProcDeclRegex = new(
            @"\b(Sub|Function)\s+(\w+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Regex to capture Call statements or implicit calls (e.g. Foo(args))
        private static readonly Regex CallRegex = new(
            @"\bCall\s+(\w+)\b|\b(\w+)\s*\(",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Generates a <see cref="DiagramModel"/> representing the call graph of the VBA project
        /// located at <paramref name="projectFilePath"/>. Each procedure is identified by
        /// "ModuleName.ProcedureName" and call relationships are captured as directed edges.
        /// </summary>
        /// <param name="gateway">Gateway used to extract modules from the project.</param>
        /// <param name="projectFilePath">Path to the VBA project file (.xlsm).</param>
        /// <returns>A diagram model describing procedures and calls.</returns>
        public static DiagramModel GenerateProcedureGraph(IVbeGateway gateway, string projectFilePath)
        {
            if (gateway == null) throw new ArgumentNullException(nameof(gateway));
            if (projectFilePath == null) throw new ArgumentNullException(nameof(projectFilePath));

            var modules = gateway.ExportModules(projectFilePath);
            var nodes = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
            var edges = new List<Edge>();

            foreach (var module in modules)
            {
                var declarations = ProcDeclRegex.Matches(module.Code ?? string.Empty);
                var procedures = new List<string>();

                // Record each procedure in this module
                foreach (Match decl in declarations)
                {
                    if (decl.Success)
                    {
                        var procName = decl.Groups[2].Value;
                        var nodeId = $"{module.Name}.{procName}";
                        if (!nodes.ContainsKey(nodeId))
                        {
                            var node = new Node(nodeId, nodeId)
                            {
                                Type = "Procedure"
                            };
                            nodes.Add(nodeId, node);
                        }
                        procedures.Add(procName);
                    }
                }

                // Scan calls within the module code
                var calls = CallRegex.Matches(module.Code ?? string.Empty);
                foreach (Match call in calls)
                {
                    string calledName = call.Groups[1].Success ? call.Groups[1].Value : call.Groups[2].Value;
                    if (string.IsNullOrWhiteSpace(calledName)) continue;
                    // Determine caller procedure by locating the closest enclosing declaration before the call
                    var callIndex = call.Index;
                    string? callerProc = null;
                    foreach (Match decl in declarations)
                    {
                        if (decl.Index < callIndex)
                        {
                            callerProc = decl.Groups[2].Value;
                        }
                        else
                        {
                            break;
                        }
                    }
                    // Only create an edge if caller is known
                    if (callerProc != null)
                    {
                        var sourceId = $"{module.Name}.{callerProc}";
                        // Attempt to resolve the called procedure by searching all modules
                        string? targetId = null;
                        foreach (var mod2 in modules)
                        {
                            // only consider calls to procedures we have declarations for
                            if (string.Equals(mod2.Name, module.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                // same module: if called name matches a declared procedure
                                foreach (Match decl2 in ProcDeclRegex.Matches(mod2.Code ?? string.Empty))
                                {
                                    if (string.Equals(decl2.Groups[2].Value, calledName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        targetId = $"{mod2.Name}.{decl2.Groups[2].Value}";
                                        break;
                                    }
                                }
                            }
                            if (targetId != null) break;
                        }
                        if (targetId != null)
                        {
                            var edgeId = $"{sourceId}->{targetId}";
                            edges.Add(new Edge(edgeId, sourceId, targetId));
                        }
                    }
                }
            }

            return new DiagramModel(nodes.Values, edges);
        }
    }
}