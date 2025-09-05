namespace VisioDiagramGenerator.Cli

open System

module Program =
  [<EntryPoint>]
  let main argv =
    match ArgParser.parse argv with
    | Help None ->
        printfn "%s" ArgParser.usage
        int ExitCode.Usage
    | Help (Some err) ->
        eprintfn "Error: %s" err
        printfn "%s" ArgParser.usage
        int ExitCode.Usage
    | Validate (cfgPath, strictMode) ->
        try
          let cfgText = ConfigLoader.readConfigText cfgPath
          let schemaTextOpt = ConfigLoader.tryReadSchemaText None
          let strict = match strictMode with | StrictMode.Strict -> true | _ -> false
          let report = Validation.validate cfgText schemaTextOpt strict
          Validation.print report
          if report.IsValid then int ExitCode.Ok else int ExitCode.Invalid
        with ex ->
          eprintfn "fatal: %s" ex.Message
          int ExitCode.Invalid
