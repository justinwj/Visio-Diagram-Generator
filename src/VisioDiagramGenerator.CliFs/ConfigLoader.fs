namespace VisioDiagramGenerator.Cli

open System
open System.IO

module ConfigLoader =
  let defaultConfigPath =
    Path.Combine("shared","Config","samples","diagramConfig.sample.json")

  let defaultSchemaPath =
    Path.Combine("shared","Config","diagramConfig.schema.json")

  let readAllText (path:string) =
    let full = Path.GetFullPath path
    if not (File.Exists full) then failwithf "File not found: %s" full
    File.ReadAllText full

  let readConfigText (path:string) =
    let p = if String.IsNullOrWhiteSpace path then defaultConfigPath else path
    readAllText p

  let tryReadSchemaText (pathOpt:string option) =
    try
      let p = defaultSchemaPath
      if File.Exists p then Some (File.ReadAllText p) else None
    with _ -> None
