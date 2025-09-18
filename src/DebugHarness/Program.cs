using System;
using System.Collections.Generic;
using VDG.Core;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("VDG Debug Harness (headless)");

        var providers = new IMapProvider[]
        {
            new InlineProvider(new []
            {
                new DiagramItem("A","Box","A",0,0),
                new DiagramItem("B","Box","B",0,0)
            },
            new []
            {
                new DiagramConnection("A","B","line")
            })
        };

        var pipeline = new Pipeline(providers, new GridLayoutAlgorithm());
        var cmds = pipeline.BuildCommands();
        Console.WriteLine($"Commands: {cmds.Count} (shapes+connectors)");
    }
}

internal sealed class InlineProvider : IMapProvider
{
    private readonly IReadOnlyList<DiagramItem> _items;
    private readonly IReadOnlyList<DiagramConnection> _conns;
    public InlineProvider(IReadOnlyList<DiagramItem> items, IReadOnlyList<DiagramConnection> conns)
    {
        _items = items; _conns = conns;
    }
    public IReadOnlyList<DiagramItem> GetItems() => _items;
    public IReadOnlyList<DiagramConnection> GetConnections() => _conns;
}