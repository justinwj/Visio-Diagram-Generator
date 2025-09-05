namespace VisioDiagramGenerator.Cli

type StrictMode =
  | WarnUnknown
  | Strict

type Command =
  | Validate of configPath:string * strict:StrictMode
  | Help of error:string option

module ArgParser =
  let private usageText = """vdg <command> [options]

Commands
  validate   Validate a diagram config JSON (schema-warn for unknown top-level props).

Options
  --config <path>       Path to diagramConfig.json
  --strict              Fail on unknown props
  --warn-unknown        Warn only on unknown props (default)

Examples
  vdg validate --config shared/Config/samples/diagramConfig.sample.json
"""

  let usage = usageText

  let parse (argv: string[]) : Command =
    let rec parseValidate (cfg:string option, strict:StrictMode) (xs:string list) =
      match xs with
      | "--config"::p::rest -> parseValidate (Some p, strict) rest
      | "--strict"::rest -> parseValidate (cfg, Strict) rest
      | "--warn-unknown"::rest -> parseValidate (cfg, WarnUnknown) rest
      | [] ->
          match cfg with
          | Some p -> Validate(p, strict)
          | None -> Help (Some "Missing --config <path> for validate")
      | flag::_ -> Help (Some $"Unknown option '{flag}'")

    match argv |> Array.toList with
    | [] -> Help None
    | "validate"::rest -> parseValidate (None, WarnUnknown) rest
    | _ -> Help None
