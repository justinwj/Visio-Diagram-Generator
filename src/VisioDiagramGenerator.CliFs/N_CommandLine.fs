namespace VisioDiagramGenerator.CliFs

/// <summary>
/// Represents the possible commands accepted by the CLI.
/// </summary>
type Command =
    | Generate of modelPath: string * outputPath: string option * livePreview: bool
    | VbaAnalysis of projectPath: string * outputPath: string option * livePreview: bool
    | Export of vsdxPath: string * format: string * outputPath: string option
    | Help

/// <summary>
/// Provides basic parsing of command line arguments. This parser expects a verb as the first
/// argument (generate, vba-analysis, export) followed by required positional parameters and
/// optional flags (--output path, --live-preview). Unrecognised inputs result in Help.
/// </summary>
module CommandLine =
    let private tryGetFlag (flag: string) (args: string list) : bool * string list =
        match args with
        | f :: rest when f = flag -> true, rest
        | _ -> false, args

    let private tryGetOption (flag: string) (args: string list) : string option * string list =
        match args with
        | f :: value :: rest when f = flag -> Some value, rest
        | _ -> None, args

    /// Parses the provided array of arguments into a <see cref="Command"/>. If parsing fails
    /// the Help command is returned.
    let parse (argv: string[]) : Command =
        let args = argv |> Array.toList
        match args with
        | [] -> Help
        | verb :: tail ->
            match verb.ToLowerInvariant() with
            | "generate" ->
                match tail with
                | model :: rest ->
                    let outputOpt, rest1 = tryGetOption "--output" rest
                    let live, rest2 = tryGetFlag "--live-preview" rest1
                    // Ignore unknown arguments for now
                    Generate(model, outputOpt, live)
                | _ -> Help
            | "vba-analysis" ->
                match tail with
                | proj :: rest ->
                    let outputOpt, rest1 = tryGetOption "--output" rest
                    let live, rest2 = tryGetFlag "--live-preview" rest1
                    VbaAnalysis(proj, outputOpt, live)
                | _ -> Help
            | "export" ->
                match tail with
                | vsdx :: format :: rest ->
                    let outputOpt, _ = tryGetOption "--output" rest
                    Export(vsdx, format, outputOpt)
                | _ -> Help
            | _ -> Help