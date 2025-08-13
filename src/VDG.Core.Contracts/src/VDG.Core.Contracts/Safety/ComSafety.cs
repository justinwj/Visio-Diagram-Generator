using System.Runtime.InteropServices;
using VDG.Core.Logging;

namespace VDG.Core.Safety;

/// <summary>Helpers for working with COM objects safely.</summary>
public static class ComSafety
{
    /// <summary>Releases a COM object if it is a COM RCW.</summary>
    public static void Release(object? comObject, ILogger? logger = null)
    {
        try
        {
            if (comObject is not null && Marshal.IsComObject(comObject))
            {
                Marshal.FinalReleaseComObject(comObject);
                logger?.Debug($"Released COM object of type {comObject.GetType().FullName}.");
            }
        }
        catch (Exception ex)
        {
            logger?.Warn($"Failed to release COM object: {ex.Message}");
        }
    }

    /// <summary>Executes <paramref name=\"body\"/> and guarantees release of the COM object built by <paramref name=\"factory\"/>.</summary>
    public static TOut Using<TCom, TOut>(Func<TCom> factory, Func<TCom, TOut> body, ILogger? logger = null)
        where TCom : class
    {
        var com = factory();
        try
        {
            return body(com);
        }
        finally
        {
            Release(com, logger);
        }
    }

    /// <summary>Async variant of <see cref=\"Using\">Using</see>.</summary>
    public static async Task<TOut> UsingAsync<TCom, TOut>(Func<TCom> factory, Func<TCom, Task<TOut>> body, ILogger? logger = null)
        where TCom : class
    {
        var com = factory();
        try
        {
            return await body(com).ConfigureAwait(false);
        }
        finally
        {
            Release(com, logger);
        }
    }
}
