using System;

namespace VDG.Core.Preview
{
    /// <summary>
    /// Contract for creating a quick browser preview link for a generated diagram without requiring Visio.
    /// Implementations must not perform COM or network I/O in this stub (Prompt 9).
    /// </summary>
    public interface ILivePreviewService
    {
        /// <summary>
        /// Saves or copies a .vsdx into the temp folder and returns a placeholder web URL string.
        /// If <paramref name="vsdxPath"/> is null or missing, a minimal placeholder file is created.
        /// No COM and no network calls are made.
        /// </summary>
        /// <param name="vsdxPath">An existing .vsdx to copy; optional.</param>
        /// <param name="suggestedName">Optional file name to use for the temp .vsdx.</param>
        /// <returns>A placeholder URL string suitable for logging or display.</returns>
        string CreatePreview(string? vsdxPath = null, string? suggestedName = null);
    }
}