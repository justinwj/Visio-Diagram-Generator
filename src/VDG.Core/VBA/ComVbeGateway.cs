using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace VDG.Core.Vba
{
    /// <summary>
    /// COM-backed implementation of <see cref="IVbeGateway"/> that automates Excel to
    /// inspect VBA projects. Designed to fail gracefully when automation is unavailable or
    /// when access to the VBA object model is not trusted by the host.
    /// </summary>
    public sealed class ComVbeGateway : IVbeGateway, IDisposable
    {
        /// <summary>
        /// Create a hidden Excel application instance. Returns <c>null</c> when automation is unavailable.
        /// </summary>
        private static dynamic? AcquireExcelInstance(out bool createdNew)
        {
            createdNew = false;
            var excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null)
            {
                return null;
            }

            try
            {
                var instance = Activator.CreateInstance(excelType);
                createdNew = true;
                return instance;
            }
            catch
            {
                return null;
            }
        }

        private static void ReleaseCom(object? instance)
        {
            if (instance != null && Marshal.IsComObject(instance))
            {
                try
                {
                    Marshal.FinalReleaseComObject(instance);
                }
                catch
                {
                    // Ignore release errors.
                }
            }
        }

        private static void ShutdownExcel(dynamic? excel, bool createdNew)
        {
            if (excel == null)
            {
                return;
            }

            try
            {
                if (createdNew)
                {
                    try
                    {
                        excel.Quit();
                    }
                    catch
                    {
                        // Ignore failures while quitting.
                    }
                }
            }
            finally
            {
                ReleaseCom(excel);
            }
        }

        public bool IsTrusted()
        {
            dynamic? excel = null;
            bool created = false;
            dynamic? workbooks = null;
            dynamic? workbook = null;

            try
            {
                excel = AcquireExcelInstance(out created);
                if (excel == null)
                {
                    return false;
                }

                excel.Visible = false;
                excel.DisplayAlerts = false;

                workbooks = excel.Workbooks;
                workbook = workbooks.Add();

                try
                {
                    var vbProject = workbook.VBProject;
                    ReleaseCom(vbProject);
                    return true;
                }
                catch (COMException)
                {
                    return false;
                }
            }
            catch (COMException)
            {
                return false;
            }
            finally
            {
                if (workbook != null)
                {
                    try
                    {
                        workbook.Close(false);
                    }
                    catch
                    {
                        // Ignore close failures.
                    }
                }

                ReleaseCom(workbook);
                ReleaseCom(workbooks);
                ShutdownExcel(excel, created);
            }
        }

        public IEnumerable<VbaModule> EnumerateModules()
        {
            dynamic? excel = null;
            bool created = false;
            dynamic? workbooks = null;
            dynamic? workbook = null;
            dynamic? vbe = null;
            var modules = new List<VbaModule>();

            try
            {
                excel = AcquireExcelInstance(out created);
                if (excel == null)
                {
                    return modules;
                }

                excel.Visible = false;
                excel.DisplayAlerts = false;

                workbooks = excel.Workbooks;
                workbook = workbooks.Add();

                vbe = workbook.VBProject;
                foreach (var component in vbe.VBComponents)
                {
                    try
                    {
                        var name = SafeComponentName(component);
                        modules.Add(new VbaModule(name, null));
                    }
                    finally
                    {
                        ReleaseCom(component);
                    }
                }

                return modules;
            }
            catch (COMException)
            {
                return Array.Empty<VbaModule>();
            }
            finally
            {
                if (workbook != null)
                {
                    try
                    {
                        workbook.Close(false);
                    }
                    catch
                    {
                        // Ignore close failures.
                    }
                }

                ReleaseCom(vbe);
                ReleaseCom(workbook);
                ReleaseCom(workbooks);
                ShutdownExcel(excel, created);
            }
        }

        public IEnumerable<VbaModule> ExportModules(string projectFilePath)
        {
            if (string.IsNullOrWhiteSpace(projectFilePath))
            {
                throw new ArgumentNullException(nameof(projectFilePath));
            }

            if (!File.Exists(projectFilePath))
            {
                throw new FileNotFoundException("VBA project file not found.", projectFilePath);
            }

            dynamic? excel = null;
            bool created = false;
            dynamic? workbooks = null;
            dynamic? workbook = null;
            dynamic? vbProject = null;
            var result = new List<VbaModule>();

            try
            {
                excel = AcquireExcelInstance(out created);
                if (excel == null)
                {
                    throw new InvalidOperationException("Excel automation is unavailable on this host.");
                }

                excel.Visible = false;
                excel.DisplayAlerts = false;

                workbooks = excel.Workbooks;
                workbook = workbooks.Open(projectFilePath, ReadOnly: true);

                try
                {
                    vbProject = workbook.VBProject;
                }
                catch (COMException ex)
                {
                    throw new InvalidOperationException(
                        "Unable to access the VBA project. Ensure 'Trust access to the VBA project object model' is enabled.",
                        ex);
                }

                foreach (var component in vbProject.VBComponents)
                {
                    dynamic? codeModule = null;
                    try
                    {
                        codeModule = component.CodeModule;
                        int lineCount = codeModule.CountOfLines;
                        string code = lineCount > 0 ? codeModule.Lines(1, lineCount) ?? string.Empty : string.Empty;
                        var name = SafeComponentName(component);
                        result.Add(new VbaModule(name, code));
                    }
                    finally
                    {
                        ReleaseCom(codeModule);
                        ReleaseCom(component);
                    }
                }

                return result;
            }
            finally
            {
                if (workbook != null)
                {
                    try
                    {
                        workbook.Close(false);
                    }
                    catch
                    {
                        // Ignore close failures.
                    }
                }

                ReleaseCom(vbProject);
                ReleaseCom(workbook);
                ReleaseCom(workbooks);
                ShutdownExcel(excel, created);
            }
        }

        private static string SafeComponentName(dynamic component)
        {
            try
            {
                var nameObj = component?.Name;
                var name = nameObj as string;
                return string.IsNullOrWhiteSpace(name) ? "Module" : name!;
            }
            catch
            {
                return "Module";
            }
        }

        public void Dispose()
        {
            // Stateless – nothing to dispose. Interface provided so callers can use "use"/"using".
        }
    }
}
