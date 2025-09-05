namespace VisioDiagramGenerator.Cli

open System
open System.IO
open System.Text
open System.Text.Json

type ExitCode =
  | Ok = 0
  | Invalid = 2
  | Usage = 64

type Severity = | Err | Warn

type ValidationIssue = {
  Level : Severity
  Path  : string
  Message : string
}

type ValidationReport = {
  IsValid : bool
  Issues : ValidationIssue list
}

module private JsonUtil =
  let tryParse (json:string) =
    try
      use _doc = JsonDocument.Parse(json)
      Ok ()
    with ex ->
      Error ex.Message

  let getTopLevelPropertyNames (json:string) =
    use doc = JsonDocument.Parse(json)
    doc.RootElement.EnumerateObject()
    |> Seq.map (fun p -> p.Name)
    |> Set.ofSeq

  let getSchemaAllowedTopLevelProps (schemaText:string) =
    try
      use sd = JsonDocument.Parse(schemaText)
      let root = sd.RootElement
      let mutable props = Unchecked.defaultof<JsonElement>
      if root.TryGetProperty("properties", &props) then
        if props.ValueKind = JsonValueKind.Object then
          props.EnumerateObject()
          |> Seq.map (fun p -> p.Name)
          |> Set.ofSeq
        else Set.empty
      else Set.empty
    with _ -> Set.empty

module Validation =
  let private writeReportToTemp (report:ValidationReport) =
    try
      let path = Path.Combine(Path.GetTempPath(), "vdg-config-errors.json")
      let opts = JsonSerializerOptions(WriteIndented = true)
      let payload =
        {| valid = report.IsValid
           issues =
             report.Issues
             |> List.map (fun i ->
               {| level = match i.Level with | Severity.Err -> "error" | Severity.Warn -> "warn"
                  path = i.Path
                  message = i.Message |})
           timestamp = DateTimeOffset.UtcNow |}
      let json = JsonSerializer.Serialize(payload, opts)
      File.WriteAllText(path, json, Encoding.UTF8)
      Some path
    with _ -> None

  let validate (configText:string) (schemaTextOpt:string option) (strictUnknown:bool) : ValidationReport =
    match JsonUtil.tryParse configText with
    | Error msg ->
        let rep =
          { IsValid = false
            Issues = [ { Level = Severity.Err; Path = "$"; Message = $"JSON parse error: {msg}" } ] }
        ignore (writeReportToTemp rep)
        rep
    | Ok () ->
        // unknown top-level properties (best-effort)
        let unknownIssues =
          match schemaTextOpt with
          | None -> []
          | Some schemaText ->
              let allowed = JsonUtil.getSchemaAllowedTopLevelProps schemaText
              if allowed.Count = 0 then [] else
              let present = JsonUtil.getTopLevelPropertyNames configText
              let unknown = Set.difference present allowed
              if Set.isEmpty unknown then [] else
                let level = if strictUnknown then Severity.Err else Severity.Warn
                unknown
                |> Seq.map (fun name -> { Level = level; Path = "$." + name; Message = "Unknown property (top-level)" })
                |> List.ofSeq

        let isValid = unknownIssues |> List.exists (fun i -> i.Level = Severity.Err) |> not
        let rep = { IsValid = isValid; Issues = unknownIssues }
        if not rep.IsValid then ignore (writeReportToTemp rep)
        rep

  let print (report:ValidationReport) =
    if report.Issues.IsEmpty then
      printfn "Config is VALID."
    else
      let header = sprintf "%-6s | %-24s | %s" "Level" "Path" "Message"
      let rule = String.replicate header.Length "-"
      printfn "%s" header
      printfn "%s" rule
      for i in report.Issues do
        let lvl = match i.Level with | Severity.Err -> "ERROR" | Severity.Warn -> "WARN"
        printfn "%-6s | %-24s | %s" lvl i.Path i.Message
