namespace VisioDiagramGenerator.CliFs

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Threading.Tasks
open VDG.Core.Analysis
open VDG.Core.Models
open VDG.Core.Vba
open VisioDiagramGenerator.Algorithms

// DTOs for basic JSON model serialisation
type SizeDto =
    { width: float32
      height: float32 }

type StyleDto =
    { fill: string option
      stroke: string option
      linePattern: string option }

type NodeDto =
    { id: string
      label: string
      ``type``: string option
      groupId: string option
      size: SizeDto option
      style: StyleDto option
      metadata: IDictionary<string, string> option }

type EdgeDto =
    { id: string option
      sourceId: string
      targetId: string
      label: string option
      directed: bool option
      style: StyleDto option
      metadata: IDictionary<string, string> option }

type DiagramMetadataDto =
    { title: string option
      description: string option
      tags: string array option
      properties: IDictionary<string, string> option }

type DiagramDto =
    { schemaVersion: string option
      metadata: DiagramMetadataDto option
      nodes: NodeDto array
      edges: EdgeDto array }

module private ModelLoader =

    let private schemaVersionValue = "1.1"
    let private supportedSchemaVersions = [| "1.0"; schemaVersionValue |]

    let private isSupportedVersion (value: string) =
        supportedSchemaVersions
        |> Array.exists (fun v -> String.Equals(v, value, StringComparison.OrdinalIgnoreCase))

    let private applyStyle (styleDto: StyleDto option) (style: ShapeStyle) =
        match styleDto with
        | Some dto ->
            match dto.fill with
            | Some fill when not (String.IsNullOrWhiteSpace fill) -> style.FillColor <- fill
            | _ -> ()

            match dto.stroke with
            | Some stroke when not (String.IsNullOrWhiteSpace stroke) -> style.StrokeColor <- stroke
            | _ -> ()

            match dto.linePattern with
            | Some pattern when not (String.IsNullOrWhiteSpace pattern) -> style.LinePattern <- pattern
            | _ -> ()
        | None -> ()

    let private applyMetadata (metadataOpt: IDictionary<string, string> option) (target: IDictionary<string, string>) =
        match metadataOpt with
        | Some metadata ->
            for KeyValue(k, v) in metadata do
                if not (String.IsNullOrWhiteSpace k) then
                    target[k] <- v
        | None -> ()

    let private applyNodeMetadata (dto: NodeDto) (node: Node) : Node =
        match dto.``type`` with
        | Some t when not (String.IsNullOrWhiteSpace t) -> node.Type <- t
        | _ -> ()

        match dto.groupId with
        | Some g when not (String.IsNullOrWhiteSpace g) -> node.GroupId <- g
        | _ -> ()

        match dto.size with
        | Some size when size.width > 0f && size.height > 0f ->
            node.Size <- Nullable(Size(size.width, size.height))
        | _ -> ()

        applyMetadata dto.metadata node.Metadata
        applyStyle dto.style node.Style
        node

    let private applyEdgeMetadata (dto: EdgeDto) (edge: Edge) : Edge =
        dto.directed
        |> Option.iter (fun directed -> edge.Directed <- directed)

        applyMetadata dto.metadata edge.Metadata
        applyStyle dto.style edge.Style
        edge

    let private normaliseNode (dto: NodeDto) : Node =
        Node(dto.id, dto.label) |> applyNodeMetadata dto

    let private normaliseEdge (dto: EdgeDto) : Edge =
        let edgeId =
            match dto.id with
            | Some id when not (String.IsNullOrWhiteSpace id) -> id
            | _ -> sprintf "%s->%s" dto.sourceId dto.targetId

        Edge(edgeId, dto.sourceId, dto.targetId, Option.toObj dto.label)
        |> applyEdgeMetadata dto

    let toModel (dto: DiagramDto) : DiagramModel =
        let nodes = dto.nodes |> Array.map normaliseNode
        let edges = dto.edges |> Array.map normaliseEdge
        let model = DiagramModel(nodes, edges)

        dto.metadata
        |> Option.iter (fun meta ->
            applyMetadata meta.properties model.Metadata

            meta.title
            |> Option.iter (fun title -> model.Metadata["title"] <- title)

            meta.description
            |> Option.iter (fun description -> model.Metadata["description"] <- description)

            meta.tags
            |> Option.iter (fun tags ->
                let cleaned =
                    tags
                    |> Array.map (fun t -> if isNull t then String.Empty else t.Trim())
                    |> Array.filter (fun t -> not (String.IsNullOrWhiteSpace t))

                if cleaned.Length > 0 then
                    let joined = String.Join(", ", cleaned)
                    model.Metadata["tags"] <- joined))

        model

    let loadFromJson (path: string) : DiagramModel =
        if String.IsNullOrWhiteSpace(path) then
            invalidArg (nameof path) "Model path must be provided."

        let json = File.ReadAllText(path)
        let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)

        let dto =
            let parsed = JsonSerializer.Deserialize<DiagramDto>(json, options)
            if obj.ReferenceEquals(parsed, null) then
                invalidOp "Diagram JSON deserialized to null."
            else
                parsed

        match dto.schemaVersion with
        | Some version when not (isSupportedVersion version) ->
            let supported = String.Join(", ", supportedSchemaVersions)
            invalidOp (sprintf "Unsupported schemaVersion '%s'. Supported versions: %s." version supported)
        | _ -> ()

        toModel dto

    let private toStyleDto (style: ShapeStyle) : StyleDto option =
        if obj.ReferenceEquals(style, null) || style.IsDefault() then None
        else
            let fill = Option.ofObj style.FillColor |> Option.filter (fun s -> not (String.IsNullOrWhiteSpace s))
            let stroke = Option.ofObj style.StrokeColor |> Option.filter (fun s -> not (String.IsNullOrWhiteSpace s))
            let line = Option.ofObj style.LinePattern |> Option.filter (fun s -> not (String.IsNullOrWhiteSpace s))

            if fill.IsNone && stroke.IsNone && line.IsNone then None
            else Some { fill = fill; stroke = stroke; linePattern = line }

    let private toSizeDto (size: Nullable<Size>) : SizeDto option =
        if size.HasValue && size.Value.Width > 0f && size.Value.Height > 0f then
            let value = size.Value
            Some { width = value.Width; height = value.Height }
        else
            None

    let private toMetadataDictionary (metadata: IDictionary<string, string>) : IDictionary<string, string> option =
        if metadata.Count = 0 then None
        else
            let copy = Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase)
            Some (copy :> IDictionary<string, string>)

    let private splitTags (value: string) : string array option =
        if String.IsNullOrWhiteSpace(value) then None
        else
            value.Split([| ','; ';' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun t -> t.Trim())
            |> Array.filter (fun t -> t.Length > 0)
            |> fun tags -> if tags.Length = 0 then None else Some tags

    let fromModel (model: DiagramModel) : DiagramDto =
        let nodes =
            model.Nodes
            |> Seq.map (fun n ->
                let nodeType = Option.ofObj n.Type |> Option.filter (fun t -> not (String.IsNullOrWhiteSpace t))
                let groupId = Option.ofObj n.GroupId |> Option.filter (fun g -> not (String.IsNullOrWhiteSpace g))
                { id = n.Id
                  label = n.Label
                  ``type`` = nodeType
                  groupId = groupId
                  size = toSizeDto n.Size
                  style = toStyleDto n.Style
                  metadata = toMetadataDictionary n.Metadata })
            |> Seq.toArray

        let edges =
            model.Edges
            |> Seq.map (fun e ->
                let label = Option.ofObj e.Label |> Option.filter (fun l -> not (String.IsNullOrWhiteSpace l))
                let style = toStyleDto e.Style
                let metadata = toMetadataDictionary e.Metadata
                let directed = if e.Directed then None else Some false

                { id = Some e.Id
                  sourceId = e.SourceId
                  targetId = e.TargetId
                  label = label
                  directed = directed
                  style = style
                  metadata = metadata })
            |> Seq.toArray

        let metadataDto =
            if model.Metadata.Count = 0 then None
            else
                let tryGet key =
                    match model.Metadata.TryGetValue key with
                    | true, value when not (String.IsNullOrWhiteSpace value) -> Some value
                    | _ -> None

                let title = tryGet "title"
                let description = tryGet "description"
                let tags = tryGet "tags" |> Option.bind splitTags

                let properties = Dictionary<string, string>(model.Metadata, StringComparer.OrdinalIgnoreCase)
                title |> Option.iter (fun _ -> properties.Remove "title" |> ignore)
                description |> Option.iter (fun _ -> properties.Remove "description" |> ignore)
                match tags with
                | Some _ -> properties.Remove "tags" |> ignore
                | None -> ()

                let propertiesOpt =
                    if properties.Count = 0 then None
                    else Some (properties :> IDictionary<string, string>)

                if title.IsNone && description.IsNone && tags.IsNone && propertiesOpt.IsNone then None
                else Some { title = title; description = description; tags = tags; properties = propertiesOpt }

        { schemaVersion = Some schemaVersionValue
          metadata = metadataDto
          nodes = nodes
          edges = edges }

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

    let private runnerLogPath (outputPath: string) =
        let directory = Path.GetDirectoryName(outputPath)
        let fileName = Path.GetFileNameWithoutExtension(outputPath)
        let logName =
            if String.IsNullOrWhiteSpace(fileName) then
                "vdg-runner"
            else
                fileName
        if String.IsNullOrWhiteSpace(directory) then
            logName + ".error.log"
        else
            Path.Combine(directory, logName + ".error.log")

    let runRunner (model: DiagramModel) (outputPath: string) : Task<unit> =
        task {
            let skipRunner =
                match Environment.GetEnvironmentVariable("VDG_SKIP_RUNNER") with
                | null -> false
                | value when value.Equals("1", StringComparison.OrdinalIgnoreCase) -> true
                | value when value.Equals("true", StringComparison.OrdinalIgnoreCase) -> true
                | _ -> false

            if skipRunner then
                ensureDirectory outputPath
                File.WriteAllText(outputPath, "Runner skipped (VDG_SKIP_RUNNER)")
            else
                let tempModelPath = Path.ChangeExtension(Path.GetTempFileName(), ".json")
                let logPath = runnerLogPath outputPath
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
                    psi.RedirectStandardOutput <- true
                    psi.RedirectStandardError <- true

                    use proc =
                        match Process.Start(psi) with
                        | null -> failwith "Failed to start runner process."
                        | p -> p

                    let stdOutTask = proc.StandardOutput.ReadToEndAsync()
                    let stdErrTask = proc.StandardError.ReadToEndAsync()

                    do! proc.WaitForExitAsync()

                    let! stdOut = stdOutTask
                    let! stdErr = stdErrTask

                    if not (String.IsNullOrWhiteSpace stdOut) then
                        Console.Write(stdOut)

                    if not (String.IsNullOrWhiteSpace stdErr) then
                        Console.Error.Write(stdErr)

                    if proc.ExitCode <> 0 then
                        if File.Exists(logPath) then
                            Console.Error.WriteLine($"Runner error log: {logPath}")
                        else
                            Console.Error.WriteLine("Runner failed; no error log was produced.")

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

    [<EntryPoint; STAThread>]
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











