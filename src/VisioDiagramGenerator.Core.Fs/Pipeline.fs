namespace VDG.Core

/// Represents a diagram node with an identifier, label text and optional parent relationship.
type Node =
    { Id: string
      /// Display text for the node.
      Label: string
      /// Children of this node (by id).
      Children: string list
      /// Optional parent id.
      Parent: string option }

/// Represents a connector between two nodes with an optional label.
type Connector =
    { From: string
      To: string
      Label: string option }

/// Low‑level build command.  The pipeline will convert loaded configuration into a flat list of
/// commands that the diagram builder executes.
type Command =
    | AddNode of Node
    | AddConnector of Connector

/// Functions for building command lists from domain objects.
module Pipeline =
    /// Build a list of commands from nodes and connectors in the order nodes then connectors.
    let buildCommands (nodes: Node list) (connectors: Connector list) : Command list =
        let nodeCommands = nodes |> List.map AddNode
        let connectorCommands = connectors |> List.map AddConnector
        nodeCommands @ connectorCommands