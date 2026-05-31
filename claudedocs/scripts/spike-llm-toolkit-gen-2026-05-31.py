"""
Spike LLM Production — Toolkit AI Generation (Wingspan)
========================================================

Replica fedele del flow di GenerateToolkitFromKbHandler.cs ma con:
- Input: 5 excerpts Wingspan hard-coded (fedeli al rulebook, surrogato dell'hybrid search)
- LLM: chiamata reale OpenRouter con il modello configurato nel secret
- Output: JSON salvato in claudedocs/ per audit + comparison vs Claude Opus surrogato

Setup:
- Legge OPENROUTER_API_KEY + OPENROUTER_DEFAULT_MODEL da infra/secrets/openrouter.secret
- Usa requests (già installato)

Uso:
    python claudedocs/scripts/spike-llm-toolkit-gen-2026-05-31.py
    python claudedocs/scripts/spike-llm-toolkit-gen-2026-05-31.py --model deepseek/deepseek-chat
"""

import argparse
import json
import os
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import requests

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
SECRET_FILE = REPO_ROOT / "infra" / "secrets" / "openrouter.secret"
OUTPUT_DIR = REPO_ROOT / "claudedocs"

# Fedele a apps/api/src/Api/BoundedContexts/GameToolkit/Application/Commands/ToolkitExtractionPrompts.cs
SYSTEM_PROMPT = """You are an expert board game rules analyst. Analyze the provided rulebook excerpts
and extract mechanical components needed to configure a digital game toolkit.

For each component type, output structured JSON. Include ONLY components explicitly
mentioned or strongly implied by the rules provided. Do not invent components.

DICE: Extract dice types (D4/D6/D8/D10/D12/D20/D100/Custom), quantities, custom faces.
COUNTERS: Extract resources, tokens, points (min/max values, IsPerPlayer=true if tracked per player).
TIMERS: Extract time limits, turn timers (DurationSeconds, AutoStart, IsPerPlayer).
SCORING: Extract victory conditions, points dimensions, ranking vs points system.
TURN ORDER: Extract round structure (RoundRobin/Sequential) and phase names if present.
EXCLUDED: List tool types NOT needed with a brief rule-based justification.

Rules:
- Set IsPerPlayer=true for anything tracked individually (e.g., player resources, player timers)
- Use min=0 for counters unless rules specify negative values
- DurationSeconds=0 if no explicit time limit is mentioned
- Reasoning must cite the rule text that led to each decision
- Return ONLY valid JSON — no markdown fences, no extra text

JSON schema: AiToolkitSuggestionDto with fields: ToolkitName, DiceTools, CounterTools,
TimerTools, ScoringTemplate, TurnTemplate, Overrides, Reasoning, ExcludedTools."""

# Identico ai 5 excerpt del spike teorico (fedeli al rulebook Wingspan)
WINGSPAN_EXCERPTS = [
    {
        "score": 0.92,
        "query": "dice requirements types quantity faces rolling",
        "text": (
            "Wingspan includes 5 custom 6-sided food dice. Each die has 6 faces depicting food "
            "types: invertebrate (worm), seed, fruit, fish, rodent (mouse), and a wild face "
            "showing two food types (player's choice). Dice are placed in the birdfeeder "
            "dice tower and re-rolled when fewer than 2 unique food types remain."
        ),
    },
    {
        "score": 0.88,
        "query": "counters resources tokens points tracking per player",
        "text": (
            "Each player has their own player mat with 3 habitat rows: forest, grassland, "
            "wetland. Players track: eggs (small wooden tokens placed on bird cards, max "
            "capacity per bird = 1-6), food tokens (taken from birdfeeder, stored on "
            "player mat), and action cubes (8 cubes per player to mark chosen actions on "
            "their player mat each round). Points are tallied only at game end."
        ),
    },
    {
        "score": 0.71,
        "query": "timer time limit turn duration countdown",
        "text": (
            "There is no time limit per turn or per round. Wingspan is a turn-based game "
            "without timers. Players may take as long as they need to make decisions, "
            "though casual play typically lasts 40-70 minutes total for 1-5 players."
        ),
    },
    {
        "score": 0.95,
        "query": "scoring points victory conditions ranking dimensions",
        "text": (
            "At game end, each player scores points in 6 categories:\n"
            "- Birds: variable points printed on each bird card (1-9 points)\n"
            "- Bonus cards: variable points per personal bonus card objectives\n"
            "- End-of-round goals: 5/2/1 or 4/2/1 points per round based on rank in goal\n"
            "- Eggs: 1 point per egg laid\n"
            "- Food cached on bird cards: 1 point per food cached\n"
            "- Tucked cards (drawn cards tucked under birds for ability): 1 point each\n"
            "Highest total wins. Ties broken by most bonus card points, then by most food."
        ),
    },
    {
        "score": 0.83,
        "query": "turn order round phases sequence players",
        "text": (
            "Wingspan is played over 4 rounds. Round 1: each player has 8 turns. Round 2: "
            "7 turns. Round 3: 6 turns. Round 4: 5 turns. Turn order is round-robin "
            "clockwise starting with the first player. On each turn, a player chooses "
            "exactly 1 of 4 actions (play bird / get food / lay eggs / draw cards), then "
            "play passes to the next player. End-of-round goals are scored after the "
            "last turn of each round."
        ),
    },
]


def load_secret() -> tuple[str, str]:
    if not SECRET_FILE.exists():
        sys.exit(f"FATAL: secret not found at {SECRET_FILE}")
    cfg = {}
    for raw in SECRET_FILE.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        k, v = line.split("=", 1)
        cfg[k.strip()] = v.strip()
    api_key = cfg.get("OPENROUTER_API_KEY", "")
    model = cfg.get("OPENROUTER_DEFAULT_MODEL", "meta-llama/llama-3.3-70b-instruct:free")
    if not api_key or api_key.startswith("sk-or-v1-change"):
        sys.exit("FATAL: OPENROUTER_API_KEY missing or unset")
    return api_key, model


def build_user_prompt(game_title: str, excerpts: list[dict]) -> str:
    parts = [
        f"[{i + 1}] (score={c['score']:.2f})\n{c['text']}"
        for i, c in enumerate(excerpts)
    ]
    return (
        f"Game: {game_title}\n\n"
        f"Rulebook excerpts:\n{chr(10).join(p + chr(10) + chr(10) + '---' + chr(10) for p in parts[:-1])}"
        f"{parts[-1]}\n\n"
        f"Extract the toolkit configuration for \"{game_title}\" from the excerpts above."
    )


def call_openrouter(api_key: str, model: str, system: str, user: str, timeout: int = 60) -> dict:
    response = requests.post(
        "https://openrouter.ai/api/v1/chat/completions",
        headers={
            "Authorization": f"Bearer {api_key}",
            "HTTP-Referer": "https://github.com/meepleAi-app/meepleai-monorepo",
            "X-Title": "MeepleAI Spike - Toolkit AI Gen",
            "Content-Type": "application/json",
        },
        json={
            "model": model,
            "messages": [
                {"role": "system", "content": system},
                {"role": "user", "content": user},
            ],
            "response_format": {"type": "json_object"},
            "temperature": 0.0,
            "max_tokens": 2000,
        },
        timeout=timeout,
    )
    response.raise_for_status()
    return response.json()


def main() -> int:
    parser = argparse.ArgumentParser(description="Spike LLM toolkit gen")
    parser.add_argument("--model", default=None, help="Override model (default: from secret)")
    args = parser.parse_args()

    print("=" * 70)
    print("Spike LLM Production - Toolkit AI Generation (Wingspan)")
    print("=" * 70)

    api_key, default_model = load_secret()
    model = args.model or default_model
    print(f"Model:         {model}")
    print(f"Excerpts:      {len(WINGSPAN_EXCERPTS)} (5 categorie hybrid search simulate)")

    user_prompt = build_user_prompt("Wingspan", WINGSPAN_EXCERPTS)
    print(f"User prompt:   {len(user_prompt)} chars")
    print(f"System prompt: {len(SYSTEM_PROMPT)} chars")
    print()

    t0 = time.time()
    print("Calling OpenRouter...")
    try:
        resp = call_openrouter(api_key, model, SYSTEM_PROMPT, user_prompt)
    except requests.HTTPError as e:
        print(f"FATAL HTTP: {e.response.status_code} {e.response.text[:500]}")
        return 1
    except Exception as e:
        print(f"FATAL: {type(e).__name__}: {e}")
        return 1
    elapsed = time.time() - t0
    print(f"Response in {elapsed:.1f}s")
    print()

    content = resp["choices"][0]["message"]["content"]
    usage = resp.get("usage", {})

    # Try to parse the JSON output
    parsed = None
    parse_err = None
    try:
        parsed = json.loads(content)
    except json.JSONDecodeError as e:
        parse_err = str(e)
        # Try stripping markdown fences just in case
        stripped = content.strip().lstrip("`").lstrip("json").strip().rstrip("`").strip()
        try:
            parsed = json.loads(stripped)
            parse_err = f"recovered after stripping fences (original: {parse_err})"
        except json.JSONDecodeError:
            pass

    print("=" * 70)
    print("USAGE")
    print("=" * 70)
    print(f"Prompt tokens:     {usage.get('prompt_tokens', '?')}")
    print(f"Completion tokens: {usage.get('completion_tokens', '?')}")
    print(f"Total tokens:      {usage.get('total_tokens', '?')}")
    print()

    print("=" * 70)
    print("RAW OUTPUT (first 2000 chars)")
    print("=" * 70)
    print(content[:2000])
    if len(content) > 2000:
        print(f"... [truncated, {len(content) - 2000} more chars]")
    print()

    if parsed:
        print("=" * 70)
        print("PARSED JSON (top-level keys)")
        print("=" * 70)
        for k, v in parsed.items():
            if isinstance(v, list):
                print(f"  {k}: list({len(v)} items)")
            elif isinstance(v, dict):
                print(f"  {k}: dict({len(v)} keys: {list(v.keys())})")
            else:
                vs = str(v)
                print(f"  {k}: {vs[:80]}{'...' if len(vs) > 80 else ''}")
    else:
        print(f"PARSE FAILED: {parse_err}")

    # Save full output
    timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H%M%SZ")
    model_slug = model.replace("/", "_").replace(":", "_")
    out_path = OUTPUT_DIR / f"spike-llm-output-wingspan-{model_slug}-{timestamp}.json"
    out_path.write_text(
        json.dumps(
            {
                "metadata": {
                    "model": model,
                    "timestamp_utc": timestamp,
                    "elapsed_seconds": round(elapsed, 2),
                    "usage": usage,
                    "parse_success": parsed is not None,
                    "parse_error": parse_err,
                },
                "input": {
                    "system_prompt_chars": len(SYSTEM_PROMPT),
                    "user_prompt_chars": len(user_prompt),
                    "excerpts_count": len(WINGSPAN_EXCERPTS),
                },
                "raw_content": content,
                "parsed_json": parsed,
            },
            indent=2,
            ensure_ascii=False,
        ),
        encoding="utf-8",
    )
    print()
    print(f"Full output saved: {out_path.relative_to(REPO_ROOT)}")
    return 0 if parsed else 2


if __name__ == "__main__":
    sys.exit(main())
