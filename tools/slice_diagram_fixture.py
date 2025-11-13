#!/usr/bin/env python3
"""
Slice a diagram JSON down to the immediate fan-out of a single node.
Useful for creating small, reproducible fixtures from large samples
without rerunning the full VBA→IR→diagram toolchain.
"""

from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path
from typing import Iterable, Set


def load_json(path: Path) -> dict:
    with path.open() as handle:
        return json.load(handle)


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w") as handle:
        json.dump(payload, handle, indent=2)
        handle.write("\n")


def collect_containers(nodes: Iterable[dict]) -> Set[str]:
    ids: Set[str] = set()
    for node in nodes:
        cid = node.get("containerId")
        if cid:
            ids.add(cid)
    return ids


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Slice a diagram JSON down to one source node and its direct targets."
    )
    parser.add_argument("--source", required=True, help="Path to the source diagram JSON.")
    parser.add_argument("--target-id", required=True, help="Node ID to keep (plus direct fan-out).")
    parser.add_argument("--output", required=True, help="Where to write the sliced diagram JSON.")
    parser.add_argument(
        "--final",
        help="Optional repo-relative file that should receive the sliced output (copy of --output).",
    )
    parser.add_argument(
        "--slice-name",
        help="Metadata value for fixture.slice (defaults to --target-id).",
    )
    parser.add_argument(
        "--skip-lane-containers",
        action="store_true",
        help="Set layout.view.skipLaneContainers=true in the sliced fixture.",
    )
    args = parser.parse_args()

    source_path = Path(args.source).expanduser().resolve()
    output_path = Path(args.output).expanduser().resolve()
    final_path = Path(args.final).expanduser().resolve() if args.final else None

    data = load_json(source_path)
    nodes = data.get("nodes", [])
    edges = data.get("edges", [])
    containers = data.get("containers", [])

    keep_ids: Set[str] = {args.target_id}
    for edge in edges:
        if edge.get("sourceId") == args.target_id:
            target = edge.get("targetId")
            if target:
                keep_ids.add(target)

    changed = True
    while changed:
        changed = False
        for node in nodes:
            if node.get("id") in keep_ids:
                cid = node.get("containerId")
                if cid and cid not in keep_ids:
                    keep_ids.add(cid)
                    changed = True

    filtered_nodes = [node for node in nodes if node.get("id") in keep_ids]
    filtered_edges = [
        edge
        for edge in edges
        if edge.get("sourceId") == args.target_id and edge.get("targetId") in keep_ids
    ]
    container_ids = collect_containers(filtered_nodes)
    filtered_containers = [c for c in containers if c.get("id") in container_ids]

    data["nodes"] = filtered_nodes
    data["edges"] = filtered_edges
    data["containers"] = filtered_containers

    props = data.setdefault("metadata", {}).setdefault("properties", {})
    props["fixture.slice"] = args.slice_name or args.target_id
    props["fixture.generatedBy"] = "tools/slice_diagram_fixture.py"
    props["fixture.sourceDiagram"] = source_path.as_posix()
    if args.skip_lane_containers:
        props["layout.view.skipLaneContainers"] = "true"

    write_json(output_path, data)
    if final_path:
        final_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(output_path, final_path)


if __name__ == "__main__":
    main()
