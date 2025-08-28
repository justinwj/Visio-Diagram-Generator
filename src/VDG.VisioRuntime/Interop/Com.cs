using System;
using System.Runtime.InteropServices;

namespace VDG.VisioRuntime.Interop
{
    /// <summary>
    /// Helper methods for safely releasing COM objects.  Visio automation
    /// uses COM under the hood and without proper cleanup RCWs can leak
    /// resources and leave hidden processes.  This helper centralises
    /// calls to <see cref="Marshal.FinalReleaseComObject"/> and guards
    /// against null references and non‑COM objects.
    /// </summary>
    internal static class Com
    {
        /// <summary>
        /// Release a COM object and set the reference to null.  If the
        /// supplied object is not a COM wrapper this method is a no‑op.
        /// </summary>
        /// <typeparam name="T">The type of the COM wrapper.</typeparam>
        /// <param name="obj">A reference to the COM wrapper.  After
        /// returning this reference is set to null.</param>
        public static void Release<T>(ref T obj) where T : class
        {
            var tmp = obj;
            obj = null;
            if (tmp == null)
                return;

            if (Marshal.IsComObject(tmp))
            {
                try
                {
                    Marshal.FinalReleaseComObject(tmp);
                }
                catch
                {
                    // swallow exceptions – COM objects sometimes throw
                    // when their lifetime has already ended.  This
                    // helper should never throw.
                }
            }
        }
    }
}