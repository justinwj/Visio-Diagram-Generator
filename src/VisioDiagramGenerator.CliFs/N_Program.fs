namespace VisioDiagramGenerator.CliFs

open System
open System.IO
open System.Text.Json
open System.Threading.Tasks
open VDG.Core.Models
open VDG.Core.Analysis
open VisioDiagramGenerator.Algorithms
open VisioDiagramGenerator.CliFs

// DTOs for basic JSON model deserialization
type NodeDto = { id: string; label: string; ``type``: string option }
type EdgeDto = { id: string; sourceId: string; targetId: string; label: string option }
type DiagramDto = { nodes: NodeDto array; edges: EdgeDto array }

module private ModelLoader =
    let toModel (dto: DiagramDto) : DiagramModel =
        let nodes = dto.nodes |> Array.map (fun n -> Node(n.id, n.label, Type = n.``type``))
        let edges = dto.edges |> Array.map (fun e -> Edge(e.id, e.sourceId, e.targetId, e.label))
        DiagramModel(nodes, edges)
    /// Reads a JSON file into a DiagramModel.
    let loadFromJson (path: string) : DiagramModel =
        let json = File.ReadAllText(path)
        let dto = JsonSerializer.Deserialize<DiagramDto>(json, JsonSerializerOptions(PropertyNameCaseInsensitive = true))
        toModel dto

module private Runner =
    /// Persists the layout result to a VSDX file. For now this creates an empty file with
    /// a simple text representation; real Visio rendering is deferred to the net48 runner.
    let saveVsdx (layout: LayoutResult) (path: string) : unit =
        // Serialize layout to JSON as a placeholder
        let json = JsonSerializer.Serialize(layout)
        File.WriteAllText(path, json)

    /// Executes the net48 runner. Currently this is a stub that simply writes the layout to VSDX.
    let runRunner (model: DiagramModel) (outputPath: string) : Task =
        task {
            // Compute layout using our F# algorithm
            let layout = LayoutEngine.compute model
            saveVsdx layout outputPath
            return ()
        }

open CommandLine

module Program =
    [<EntryPoint>]
    let main argv =
        let cmd = CommandLine.parse argv
        let exitCodeTask =
            match cmd with
            | Help ->
                Console.WriteLine("Visio Diagram Generator CLI")
                Console.WriteLine("Usage:")
                Console.WriteLine("  generate <model.json> [--output <out.vsdx>] [--live-preview]")
                Console.WriteLine("  vba-analysis <project.xlsm> [--output <out.vsdx>] [--live-preview]")
                Console.WriteLine("  export <diagram.vsdx> <format> [--output <out.file>]")
                Task.FromResult(0)
            | Generate(modelPath, outputOpt, live) ->
                task {
                    try
                        let model = ModelLoader.loadFromJson modelPath
                        let outPath = outputOpt |> Option.defaultValue (Path.ChangeExtension(modelPath, ".vsdx"))
                        do! Runner.runRunner model outPath
                        if live then
                            let! url = LivePreview.uploadLivePreview outPath
                            Console.WriteLine($"Preview link: {url}")
                        return 0
                    with ex ->
                        Console.Error.WriteLine($"Error: {ex.Message}")
                        return 1
                }
            | VbaAnalysis(projectPath, outputOpt, live) ->
                task {
                    try
                        // Use a dummy gateway to indicate analysis unimplemented; throw error
                        Console.Error.WriteLine("VBA analysis requires a concrete IVbeGateway implementation.")
                        return 1
                    with ex ->
                        Console.Error.WriteLine($"Error: {ex.Message}")
                        return 1
                }
            | Export(vsdxPath, format, outputOpt) ->
                task {
                    Console.Error.WriteLine("Export functionality not implemented yet.")
                    return 1
                }
        exitCodeTask.GetAwaiter().GetResult()