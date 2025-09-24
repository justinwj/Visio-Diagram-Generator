using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

class InspectLoadContext : AssemblyLoadContext
{
    private readonly string _directory;

    public InspectLoadContext(string assemblyPath) : base(isCollectible: true)
    {
        _directory = Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var candidate = Path.Combine(_directory, assemblyName.Name + ".dll");
        if (File.Exists(candidate))
        {
            return LoadFromAssemblyPath(candidate);
        }

        return null;
    }
}

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: InspectTests <assembly>");
            return;
        }

        var assemblyPath = Path.GetFullPath(args[0]);
        if (!File.Exists(assemblyPath))
        {
            Console.WriteLine($"not found: {assemblyPath}");
            return;
        }

        var alc = new InspectLoadContext(assemblyPath);
        var assembly = alc.LoadFromAssemblyPath(assemblyPath);

        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                Console.WriteLine($"{type.FullName}::{method.Name}({string.Join(",", Array.ConvertAll(method.GetParameters(), p => p.ParameterType.FullName))}) -> {method.ReturnType.FullName}");
            }
        }

        alc.Unload();
    }
}
