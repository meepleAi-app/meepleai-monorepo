#!/usr/bin/env python3
"""
Strips BGG-hosted image URL fields from catalog seed manifests for ToS compliance.

Issue: https://github.com/meepleAi-app/meepleai-monorepo/issues/2123
Spec : docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md

The catalog enrichment pipeline (#1823 M3-M8) replaces BGG-hosted covers with
self-hosted R2 variants generated from Wikidata + Wikimedia Commons. This script
strips the now-obsolete BGG asset fields from the YAML seed manifests under
``apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/``:

- ``imageUrl``
- ``thumbnailUrl``
- ``fallbackImageUrl``
- ``fallbackThumbnailUrl``
- ``bggEnhanced`` (legacy flag — replaced by presence/absence of ``description``)

Usage::

    python scripts/scrub_bgg_manifest.py \\
        apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/dev.yml \\
        apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/staging.yml \\
        apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/prod.yml

Output one line per file: ``<path>: <game_count> games, <stripped_count> fields removed``.
Exits 0 on success, 1 on parse/IO error. Idempotent: running twice on the same
file removes zero additional fields.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path
from ruamel.yaml import YAML

STRIPPED_FIELDS = frozenset(
    {
        "imageUrl",
        "thumbnailUrl",
        "fallbackImageUrl",
        "fallbackThumbnailUrl",
        "bggEnhanced",
    }
)


def _parse_safely(parser: YAML, text: str):
    """
    SECURITY WRAPPER. ``ruamel.yaml.YAML(typ='rt', pure=True)`` is the
    documented-safe variant: it does NOT execute Python via ``!!python/object``
    tags — a ``ConstructorError`` is raised if the input ever contains such a
    tag. Round-trip parsing is preferred over PyYAML ``safe_load`` + ``safe_dump``
    because it preserves comments, anchors, and indentation, so the 12k-line
    ``prod.yml`` diff stays human-reviewable for PR review.

    The vulnerable PyYAML ``yaml.load`` API is intentionally NOT imported
    anywhere in this script.
    """
    return parser.load(text)  # safe: typ='rt' + pure=True; see docstring above


def _make_safe_parser() -> YAML:
    parser = YAML(typ="rt", pure=True)
    parser.preserve_quotes = True
    # Match the indentation convention of the existing manifests in the repo:
    # sequence dashes aligned with the parent key (no extra indent), e.g.
    #   catalog:
    #     games:
    #     - title: …       <-- 2-space mapping, 2-space sequence, 0 offset
    #       bggId: 13
    parser.indent(mapping=2, sequence=2, offset=0)
    parser.width = 4096  # avoid line wrapping on long descriptions
    return parser


def scrub(path: Path) -> tuple[int, int]:
    """
    Strip ``STRIPPED_FIELDS`` from every game entry in ``path``.

    Returns ``(game_count, stripped_count)``. The file is rewritten in place.

    Raises :class:`FileNotFoundError` if ``path`` does not exist;
    :class:`ruamel.yaml.YAMLError` (or subclasses) on parse failure;
    :class:`OSError` on read/write failure.
    """
    parser = _make_safe_parser()
    text = path.read_text(encoding="utf-8")
    data = _parse_safely(parser, text)

    if data is None:
        return 0, 0

    games = data.get("catalog", {}).get("games", []) if isinstance(data, dict) else []
    stripped_count = 0
    game_count = 0
    for game in games:
        game_count += 1
        if not hasattr(game, "keys"):
            continue
        for key in list(game.keys()):
            if key in STRIPPED_FIELDS:
                del game[key]
                stripped_count += 1

    with path.open("w", encoding="utf-8", newline="") as out:
        parser.dump(data, out)
    return game_count, stripped_count


def _parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("paths", nargs="+", help="Manifest YAML files to scrub")
    return ap.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = _parse_args(argv)
    total_failures = 0
    for raw in args.paths:
        path = Path(raw)
        try:
            games, stripped = scrub(path)
        except FileNotFoundError as exc:
            print(f"{raw}: ERROR not found ({exc})", file=sys.stderr)
            total_failures += 1
            continue
        except Exception as exc:  # noqa: BLE001 — print and continue per-file
            print(f"{raw}: ERROR {type(exc).__name__}: {exc}", file=sys.stderr)
            total_failures += 1
            continue
        print(f"{raw}: {games} games, {stripped} fields removed")
    return 0 if total_failures == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
