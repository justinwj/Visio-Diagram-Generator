namespace VisioDiagramGenerator.CliFs

open System
open System.IO
open System.Threading.Tasks

module LivePreview =

    let private getEnv (name: string) =
        match Environment.GetEnvironmentVariable(name) with
        | null
        | "" -> None
        | value -> Some value

    /// Placeholder upload implementation. Real Graph integration will be restored once the SDK surface is
    /// stabilised for F# callers. The current implementation validates inputs and reports the simulated
    /// outcome so that callers receive deterministic feedback during development.
    let uploadLivePreview (filePath: string) : Task<string> =
        task {
            if String.IsNullOrWhiteSpace(filePath) then
                invalidArg (nameof filePath) "File path is required."

            if not (File.Exists(filePath)) then
                invalidOp ($"File not found: {filePath}")

            let message =
                match getEnv "VDG_GRAPH_CLIENT_ID", getEnv "VDG_GRAPH_TENANT_ID" with
                | Some _, Some _ ->
                    $"Live preview upload is not yet available in this preview build. VSDX saved at {filePath}."
                | _ ->
                    "Graph credentials not configured. Skipping live preview upload."

            return message
        }
