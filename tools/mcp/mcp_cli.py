#!/usr/bin/env python3
"""Utility CLI per collegarsi ai server MCP dockerizzati via Codex."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path
from typing import Any, Dict, Iterable, List, Tuple

REPO_ROOT = Path(__file__).resolve().parents[2]
CONFIG_PATH = REPO_ROOT / "mcp" / "servers.config.json"
PROTOCOL_VERSION = "2024-11-05"
CLIENT_INFO = {"name": "codex-cli", "version": "0.1"}


def load_config() -> Dict[str, Dict[str, Any]]:
    if not CONFIG_PATH.exists():
        raise SystemExit(f"Configurazione MCP assente: {CONFIG_PATH}")
    with CONFIG_PATH.open() as fh:
        return json.load(fh)


def build_parser(server_names: Iterable[str]) -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Client MCP minimale per eseguire tools/list e tools/call sui server docker",
    )
    parser.add_argument(
        "--debug-json",
        action="store_true",
        help="Mostra le risposte JSON raw (utile per troubleshooting)",
    )

    subparsers = parser.add_subparsers(dest="command", required=True)

    servers_parser = subparsers.add_parser(
        "servers", help="Elenca i server configurati e relativi container"
    )
    servers_parser.add_argument(
        "--verbose", action="store_true", help="Mostra anche la descrizione del server"
    )

    list_parser = subparsers.add_parser(
        "list", help="Esegue tools/list su un server MCP"
    )
    list_parser.add_argument("server", choices=server_names, help="Nome server MCP")

    call_parser = subparsers.add_parser(
        "call", help="Esegue tools/call su un server MCP"
    )
    call_parser.add_argument("server", choices=server_names, help="Nome server MCP")
    call_parser.add_argument("tool", help="Nome del tool da invocare")
    call_parser.add_argument(
        "--args",
        help="Payload JSON inline per gli arguments (es. '{\"task\":\"foo\"}')",
    )
    call_parser.add_argument(
        "--args-file",
        help="Percorso di un file JSON con gli arguments (usa '-' per stdin)",
    )

    return parser


def read_json_line(
    proc: subprocess.Popen[str], server: str, debug_logs: bool = False
) -> Dict[str, Any]:
    while True:
        line = proc.stdout.readline()
        if not line:
            raise RuntimeError(f"{server}: risposta vuota/connessione chiusa")
        stripped = line.strip()
        if not stripped:
            continue
        try:
            return json.loads(stripped)
        except json.JSONDecodeError:
            if debug_logs:
                print(f"[{server}] {stripped}", file=sys.stderr)


def send(proc: subprocess.Popen[str], message: Dict[str, Any]) -> None:
    proc.stdin.write(json.dumps(message) + "\n")
    proc.stdin.flush()


def start_session(
    server: str, conf: Dict[str, Any], debug_logs: bool
) -> Tuple[subprocess.Popen[str], int]:
    docker_cmd: List[str] = ["docker", "exec", "-i"]
    for key, value in (conf.get("env") or {}).items():
        docker_cmd += ["-e", f"{key}={value}"]
    docker_cmd.append(conf["container"])
    docker_cmd.extend(conf["command"])

    proc = subprocess.Popen(
        docker_cmd,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=sys.stderr,
        text=True,
    )
    if proc.stdin is None or proc.stdout is None:
        raise RuntimeError("Impossibile aprire stdin/stdout per docker exec")

    request_id = 1
    init_payload = {
        "jsonrpc": "2.0",
        "id": request_id,
        "method": "initialize",
        "params": {
            "protocolVersion": PROTOCOL_VERSION,
            "clientInfo": CLIENT_INFO,
            "capabilities": {},
        },
    }
    send(proc, init_payload)
    init_response = read_json_line(proc, server, debug_logs=debug_logs)
    if "error" in init_response:
        raise RuntimeError(f"{server}: initialize error: {init_response['error']}")

    send(proc, {"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}})
    request_id += 1
    return proc, request_id


def close_session(proc: subprocess.Popen[str]) -> None:
    try:
        if proc.stdin:
            proc.stdin.close()
    finally:
        proc.terminate()


def list_tools(server: str, conf: Dict[str, Any], debug: bool) -> None:
    proc, next_id = start_session(server, conf, debug_logs=debug)
    try:
        payload = {
            "jsonrpc": "2.0",
            "id": next_id,
            "method": "tools/list",
            "params": {},
        }
        send(proc, payload)
        response = read_json_line(proc, server, debug_logs=debug)
        if debug:
            print(json.dumps(response, indent=2))
            return

        if "error" in response:
            print(f"[{server}] Error: {response['error']}", file=sys.stderr)
            return

        tools = response.get("result", {}).get("tools", [])
        if not tools:
            print(f"{server}: nessun tool esposto.")
            return
        print(f"{server} ({len(tools)} tools)")
        for tool in tools:
            description = tool.get("description", "")
            print(f"- {tool['name']}: {description}")
    finally:
        close_session(proc)


def load_arguments(args_inline: str | None, args_file: str | None) -> Dict[str, Any]:
    if args_inline and args_file:
        raise SystemExit("Specifica --args o --args-file, non entrambi.")
    if args_file:
        if args_file == "-":
            return json.load(sys.stdin)
        with Path(args_file).open() as fh:
            return json.load(fh)
    if args_inline:
        return json.loads(args_inline)
    return {}


def call_tool(
    server: str,
    conf: Dict[str, Any],
    tool_name: str,
    arguments: Dict[str, Any],
    debug: bool,
) -> None:
    proc, next_id = start_session(server, conf, debug_logs=debug)
    try:
        payload = {
            "jsonrpc": "2.0",
            "id": next_id,
            "method": "tools/call",
            "params": {"name": tool_name, "arguments": arguments},
        }
        send(proc, payload)
        response = read_json_line(proc, server, debug_logs=debug)
        if debug:
            print(json.dumps(response, indent=2))
            return
        if "error" in response:
            print(json.dumps(response["error"], indent=2), file=sys.stderr)
            return
        result = response.get("result", {})
        content = result.get("content", [])
        if not content:
            print(json.dumps(result, indent=2))
            return
        for block in content:
            block_type = block.get("type")
            if block_type == "text":
                print(block.get("text", ""))
            else:
                print(json.dumps(block, indent=2))
    finally:
        close_session(proc)


def print_servers(config: Dict[str, Dict[str, Any]], verbose: bool) -> None:
    for name, conf in config.items():
        line = f"- {name}: {conf['container']} → {' '.join(conf['command'])}"
        if verbose and conf.get("description"):
            line += f"\n    {conf['description']}"
        print(line)


def main() -> None:
    config = load_config()
    server_names = sorted(config.keys())
    parser = build_parser(server_names)
    args = parser.parse_args()

    if args.command == "servers":
        print_servers(config, verbose=args.verbose)
        return

    server_conf = config[args.server]

    if args.command == "list":
        list_tools(args.server, server_conf, debug=args.debug_json)
    elif args.command == "call":
        arguments = load_arguments(args.args, args.args_file)
        call_tool(
            args.server,
            server_conf,
            args.tool,
            arguments,
            debug=args.debug_json,
        )
    else:
        parser.error(f"Comando non gestito: {args.command}")


if __name__ == "__main__":
    main()
