namespace VisioDiagramGenerator.Cli

open System

type StrictMode = | WarnUnknown | Strict

type Command =
  | Validate of configPath:string * strict:StrictMode
  | Generate of configPath:string * runner:string option * timeoutSec:int option * buffered:bool * passThrough:string list
  | Help of error:string option

module ArgParser =

  let private usageText = """vdg <command> [options]
Commands
  validate   Validate a diagram config JSON.
  generate   Generate a diagram via the Visio runner (net48).

Options (validate)
  --config <path>
  --strict | --warn-unknown

Options (generate)
  --config <path>
  --runner <path>      Override runner exe path
  --timeout <seconds>  Default 600
  --buffered           Buffer runner output; default is live streaming
  --                   All arguments after `--` are passed to the runner as-is
Examples
  vdg validate --config shared/Config/samples/diagramConfig.sample.json
  vdg generate --config shared/Config/samples/diagramConfig.sample.json -- --saveAs out.vsdx --open
"""
  let usage = usageText

  let parse (argv:string[]) : Command =
    let rec parseValidate (cfg, strict) xs =
      match xs with
      | "--config"::p::r -> parseValidate (Some p, strict) r
      | "--strict"::r    -> parseValidate (cfg, Strict) r
      | "--warn-unknown"::r -> parseValidate (cfg, WarnUnknown) r
      | [] -> cfg |> Option.map (fun p -> Validate(p, strict)) |> Option.defaultValue (Help (Some "Missing --config <path> for validate"))
      | flag::_ -> Help (Some (sprintf "Unknown option '%s'" flag))

    let rec parseGenerate (cfg, runner, timeout, buffered, passthrough, afterDash) xs =
      match xs with
      | [] ->
          match cfg with
          | Some c -> Generate(c, runner, timeout, buffered, List.rev passthrough)
          | None -> Help (Some "Missing --config <path> for generate")
      | "--"::rest ->
          // everything after -- is pass-through
          parseGenerate (cfg, runner, timeout, buffered, List.rev passthrough @ rest, true) []
      | "--config"::p::r when not afterDash -> parseGenerate (Some p, runner, timeout, buffered, passthrough, afterDash) r
      | "--runner"::p::r when not afterDash -> parseGenerate (cfg, Some p, timeout, buffered, passthrough, afterDash) r
      | "--timeout"::s::r when not afterDash ->
          match System.Int32.TryParse s with
          | true, v -> parseGenerate (cfg, runner, Some v, buffered, passthrough, afterDash) r
          | _ -> Help (Some "Invalid --timeout value")
      | "--buffered"::r when not afterDash -> parseGenerate (cfg, runner, timeout, true, passthrough, afterDash) r
      | flag::_ when not afterDash -> Help (Some (sprintf "Unknown option '%s'" flag))
      | _ -> Help (Some "Internal parse error") // should not happen

    match argv |> Array.toList with
    | [] -> Help None
    | "validate"::rest -> parseValidate (None, WarnUnknown) rest
    | "generate"::rest -> parseGenerate (None, None, None, false, [], false) rest
    | _ -> Help None
