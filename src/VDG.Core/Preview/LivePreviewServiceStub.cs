using System;
using System.IO;
using System.Text;

namespace VDG.Core.Preview
{
    /// <summary>
    /// Stub implementation that creates a temp .vsdx (or copies an existing one) and returns
    /// a deterministic placeholder URL. Real upload (Graph/OneDrive) can plug in later.
    /// </summary>
    public sealed class LivePreviewServiceStub : ILivePreviewService
    {
        public string CreatePreview(string? vsdxPath = null, string? suggestedName = null)
        {
            string name = string.IsNullOrWhiteSpace(suggestedName)
                ? $"VDG_preview_{Guid.NewGuid():N}.vsdx"
                : (suggestedName!.EndsWith(".vsdx", StringComparison.OrdinalIgnoreCase) ? suggestedName! : suggestedName! + ".vsdx");

            string tempPath = Path.Combine(Path.GetTempPath(), name);

            try
            {
                if (!string.IsNullOrEmpty(vsdxPath) && File.Exists(vsdxPath))
                {
                    File.Copy(vsdxPath!, tempPath, overwrite: true);
                }
                else
                {
                    // Minimal placeholder file; validity as a real VSDX is not required for the stub.
                    if (!File.Exists(tempPath))
                    {
                        using var _ = File.Create(tempPath);
                    }
                }
            }
            catch (Exception ex)
            {
                // For the stub, errors should still result in a usable message rather than throwing.
                // We fallback to returning a URL with an error hint encoded; callers can still show it.
                string err = Convert.ToBase64String(Encoding.UTF8.GetBytes(ex.GetType().Name + ":" + ex.Message));
                return $"https://vdg.local/preview/error/{err}";
            }

            // Return a deterministic, local placeholder URL that callers can display/click.
            string id = Convert.ToBase64String(Encoding.UTF8.GetBytes(tempPath)).TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return $"https://vdg.local/preview/{id}";
        }
    }
}