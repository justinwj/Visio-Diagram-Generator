#nullable enable
using System.Runtime.InteropServices;

namespace VDG.VisioRuntime.Interop
{
    /// <summary>
    /// Helper methods for safely releasing COM objects.
    /// </summary>
    internal static class Com
    {
        /// <summary>
        /// Release a COM object and clear the reference. No-op for non‑COM objects.
        /// </summary>
        public static void Release<T>(ref T com)
        {
            if (com is object o && Marshal.IsComObject(o))
            {
                try { Marshal.FinalReleaseComObject(o); } catch { /* ignore */ }
            }
            com = default!;
        }
    }
}
