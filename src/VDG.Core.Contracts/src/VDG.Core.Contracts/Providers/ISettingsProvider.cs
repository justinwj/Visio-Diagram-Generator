namespace VDG.Core.Providers;

/// <summary>Provides configuration or settings for the pipeline/builder.</summary>
public interface ISettingsProvider
{
    /// <summary>Try to get a setting value by key.</summary>
    bool TryGet(string key, out string? value);
}
