namespace VisioDiagramGenerator.CliFs

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Threading.Tasks
open VDG.Core.Analysis
open VDG.Core.Models
open VDG.Core.Vba
open VisioDiagramGenerator.Algorithms

// DTOs for basic JSON model serialisation
type NodeDto =
    { id: string
      label: string
      ``type``: string option }

type EdgeDto =
    { id: string
      sourceId: string
      targetId: string
      label: string option }

type DiagramDto =
    { schemaVersion: string option
      nodes: NodeDto array
      edges: EdgeDto array }

module private ModelLoader =

    let private schemaVersionValue = "1.0"

    let private applyNodeMetadata (dto: NodeDto) (node: Node) : Node =
        match dto.``type`` with
        | Some t when not (String.IsNullOrWhiteSpace t) ->
            node.Type <- t
            node
        | _ -> node

    let toModel (dto: DiagramDto) : DiagramModel =
        let nodes =
            dto.nodes
            |> Array.map (fun n -> Node(n.id, n.label) |> applyNodeMetadata n)

        let edges =
            dto.edges
            |> Array.map (fun e -> Edge(e.id, e.sourceId, e.targetId, Option.toObj e.label))

        DiagramModel(nodes, edges)

    let loadFromJson (path: string) : DiagramModel =
        if String.IsNullOrWhiteSpace(path) then
            invalidArg (nameof path) "Model path must be provided."

        let json = File.ReadAllText(path)
        let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)

        let dto = JsonSerializer.Deserialize<DiagramDto>(json, options)
        match dto.schemaVersion with
        | Some v when not (String.Equals(v, schemaVersionValue, StringComparison.OrdinalIgnoreCase)) ->
            invalidOp (sprintf "Unsupported schemaVersion '%s'. Expected '%s'." v schemaVersionValue)
        | _ -> ()
        toModel dto

    let fromModel (model: DiagramModel) : DiagramDto =
        let nodes =
            model.Nodes
            |> Seq.map (fun n ->
                let nodeType =
                    match Option.ofObj n.Type with
                    | Some t when not (String.IsNullOrWhiteSpace t) -> Some t
                    | _ -> None

                { id = n.Id
                  label = n.Label
                  ``type`` = nodeType })
            |> Seq.toArray

        let edges =
            model.Edges
            |> Seq.map (fun e ->
                let label =
                    match Option.ofObj e.Label with
                    | Some l when not (String.IsNullOrWhiteSpace l) -> Some l
                    | _ -> None

                { id = e.Id
                  sourceId = e.SourceId
                  targetId = e.TargetId
                  label = label })
            |> Seq.toArray

        { schemaVersion = Some schemaVersionValue; nodes = nodes; edges = edges }

    let saveToJson (path: string) (model: DiagramModel) =
        let dto = fromModel model
        match Path.GetDirectoryName(path) with
        | null
        | "" -> ()
        | dir -> Directory.CreateDirectory(dir) |> ignore

        let options = JsonSerializerOptions(WriteIndented = true)
        let json = JsonSerializer.Serialize(dto, options)
        File.WriteAllText(path, json)

module private Runner =

    let private ensureDirectory (outputPath: string) =
        match Path.GetDirectoryName(outputPath) with
        | null
        | "" -> ()
        | dir -> Directory.CreateDirectory(dir) |> ignore

    let runRunner (model: DiagramModel) (outputPath: string) : Task<unit> =
        task {
            let tempModelPath = Path.ChangeExtension(Path.GetTempFileName(), ".json")
            try
                let modelJson = JsonSerializer.Serialize(ModelLoader.fromModel model)
                File.WriteAllText(tempModelPath, modelJson)

                ensureDirectory outputPath

                let candidateInArtifacts = Path.Combine("artifacts", "runner", "VDG.CLI.exe")
                let runnerExe =
                    if File.Exists(candidateInArtifacts) then
                        candidateInArtifacts
                    else
                        "VDG.CLI.exe"

                if not (File.Exists(runnerExe)) then
                    failwith ($"Runner executable not found at {runnerExe}")

                let psi =
                    ProcessStartInfo(
                        FileName = runnerExe,
                        Arguments = $"\"{tempModelPath}\" \"{outputPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    )

                use proc =
                    match Process.Start(psi) with
                    | null -> failwith "Failed to start runner process."
                    | p -> p

                do! proc.WaitForExitAsync()

                if proc.ExitCode <> 0 then
                    failwith ($"Runner process failed with exit code {proc.ExitCode}")
            finally
                if File.Exists(tempModelPath) then
                    File.Delete(tempModelPath)
        }

open CommandLine

module Program =

    let mutable private gatewayFactory: unit -> IVbeGateway = fun () -> new ComVbeGateway() :> IVbeGateway

    let internal setGatewayFactory factory = gatewayFactory <- factory

    let private printHelp () =
        Console.WriteLine("Visio Diagram Generator CLI")
        Console.WriteLine("Usage:")
        Console.WriteLine("  generate <model.json> [--output <out.vsdx>] [--live-preview]")
        Console.WriteLine("  vba-analysis <project.xlsm> [--output <out.json>] [--live-preview]")
        Console.WriteLine("  export <diagram.vsdx> <format> [--output <out.file>]")

    let private disposeIfNeeded (gateway: IVbeGateway) =
        match gateway with
        | :? IDisposable as disp -> disp.Dispose()
        | _ -> ()

    [<EntryPoint>]
    let main (argv: string array) : int =
        let cmd = CommandLine.parse argv

        let exitCodeTask : Task<int> =
            match cmd with
            | Help ->
                printHelp ()
                Task.FromResult 0

            | Generate(modelPath, outputOpt, live) ->
                task {
                    try
                        let model = ModelLoader.loadFromJson modelPath
                        let outputPath =
                            outputOpt
                            |> Option.defaultValue (Path.ChangeExtension(modelPath, ".vsdx"))

                        do! Runner.runRunner model outputPath

                        if live then
                            let! url = LivePreview.uploadLivePreview outputPath
                            Console.WriteLine($"Preview link: {url}")

                        return 0
                    with ex ->
                        Console.Error.WriteLine($"Error: {ex.Message}")
                        return 1
                }

            | VbaAnalysis(projectPath, outputOpt, live) ->
                task {
                    try
                        if String.IsNullOrWhiteSpace(projectPath) then
                            invalidArg (nameof projectPath) "Project path must be provided."

                        let fullPath = Path.GetFullPath(projectPath)
                        if not (File.Exists(fullPath)) then
                            raise (FileNotFoundException($"File not found: {fullPath}", fullPath))

                        let gateway = gatewayFactory()
                        let model =
                            try
                                ProcedureGraphBuilder.GenerateProcedureGraph(gateway, fullPath)
                            finally
                                disposeIfNeeded gateway

                        let outputPath =
                            outputOpt
                            |> Option.defaultValue (Path.ChangeExtension(fullPath, ".diagram.json"))

                        ModelLoader.saveToJson outputPath model
                        Console.WriteLine($"Analysis exported: {outputPath}")

                        if live then
                            Console.Error.WriteLine("Live preview is not supported for VBA analysis yet.")

                        return 0
                    with ex ->
                        Console.Error.WriteLine($"VBA analysis error: {ex.Message}")
                        return 1
                }

            | Export(vsdxPath, format, outputOpt) ->
                task {
                    try
                        let outputPath = outputOpt |> Option.defaultValue (Path.ChangeExtension(vsdxPath, format))

                        match format.ToLowerInvariant() with
                        | "png"
                        | "pdf"
                        | "svg" ->
                            match Path.GetDirectoryName(outputPath) with
                            | null
                            | "" -> ()
                            | dir -> Directory.CreateDirectory(dir) |> ignore

                            File.Copy(vsdxPath, outputPath, true)
                            Console.WriteLine($"Exported to: {outputPath}")
                            return 0
                        | _ ->
                            Console.Error.WriteLine($"Unsupported export format: {format}")
                            return 1
                    with ex ->
                        Console.Error.WriteLine($"Export error: {ex.Message}")
                        return 1
                }

        exitCodeTask.GetAwaiter().GetResult()






