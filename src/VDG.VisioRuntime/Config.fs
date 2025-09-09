namespace VDG.VisioRuntime

open System
open System.IO
open Newtonsoft.Json
open VDG.Core

[<CLIMutable>]
type ModuleCfg = { name: string }

[<CLIMutable>]
type NodeCfg = { id: string; label: string }

[<CLIMutable>]
type ConnectorCfg = { from: string; ``to``: string; label: string option }

[<CLIMutable>]
type RootCfg =
    { modules: ModuleCfg list option
      nodes: NodeCfg list option
      connectors: ConnectorCfg list option
      saveAs: string option }

module Config =
    /// Load a Prompt 7 config JSON. Supports either { modules:[{name}] } or { nodes:[{id,label}] }.
    let load (path: string) : Node list * Connector list * string option =
        if not (File.Exists path) then
            failwithf "Config not found: %s" (Path.GetFullPath path)

        let json = File.ReadAllText path
        if System.String.IsNullOrWhiteSpace json then
            failwith "Config file is empty"

        let root = JsonConvert.DeserializeObject<RootCfg>(json)

        let nodes =
            match root.modules, root.nodes with
            | Some mods, _ ->
                mods |> List.map (fun m ->
                    { Id = m.name; Label = m.name; Children = []; Parent = None })
            | None, Some ns ->
                ns |> List.map (fun n ->
                    { Id = n.id; Label = n.label; Children = []; Parent = None })
            | _ -> []

        let connectors =
            match root.connectors with
            | None -> []
            | Some cs ->
                cs |> List.map (fun c ->
                    { From = c.from; To = c.``to``; Label = c.label })

        nodes, connectors, root.saveAs
