// src/VDG.Core.Contracts/Templates/TemplateDefinitions.cs
using System;
using System.Collections.Generic;

namespace VDG.Core.Contracts.Templates
{
    public readonly record struct MasterKey(string StencilKey, string NameU);
    public readonly record struct MasterInfo(string StencilKey, string Name, string NameU);
    public sealed record StencilSpec(string Key, string Path);
    public sealed record TemplateSpec(
        string TemplatePath,
        IReadOnlyList<StencilSpec> Stencils,
        IReadOnlyDictionary<string, string> ShapeMapping,
        string? ThemeName = null,
        string? ThemePath = null,
        int? ThemeVariant = null,
        string? FallbackMaster = null,
        string NameUMatch = "strict"
    );

    public interface ITemplateManager : IDisposable
    {
        void Prepare(TemplateSpec spec);
        bool TryResolveMaster(string logicalType, out MasterKey key);
        IEnumerable<MasterInfo> ListStencilMasters(int take = 25);
    }
}
