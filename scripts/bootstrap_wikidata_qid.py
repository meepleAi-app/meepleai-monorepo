#!/usr/bin/env python3
"""
Populates ``shared_games.wikidata_qid`` for catalog entries with a BGG ID via
the SPARQL ``wdt:P2339`` (BoardGameGeek game ID) property.

Issue: https://github.com/meepleAi-app/meepleai-monorepo/issues/2123
Spec : docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md
ADR  : docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md §5

The M8 single-entry orchestrator (#1823 EnrichCatalogCoverCommand) requires
``WikidataQid`` to be populated before it can fetch a P18 image from Wikidata.
This script is the one-shot bootstrap for legacy catalog rows that pre-date the
M8 freshness flow; Wave 3 M9 BackgroundService scheduler will keep the column
warm on an ongoing basis.

Usage::

    # Dry run (recommended first) — prints the proposed UPDATE count without
    # touching the DB.
    python scripts/bootstrap_wikidata_qid.py \\
        --connection-string "Host=...;Username=...;Database=meepleai" \\
        --dry-run

    # Live run — performs the UPDATE statements.
    python scripts/bootstrap_wikidata_qid.py \\
        --connection-string "Host=...;Username=...;Database=meepleai"

Notes:
- Wikimedia SPARQL rate limit is 1 request per second; the script sleeps
  ``--sparql-sleep-seconds`` (default 1.0) between batch calls.
- Batch size is bounded by the SPARQL ``VALUES`` clause length (default 50).
- The User-Agent header includes ``abuse@meepleai.app`` per ADR-059 §4.

Security: this script DOES NOT execute Python via YAML or any other vector. It
talks to PostgreSQL via psycopg2 and to the SPARQL endpoint via requests. Both
inputs (BGG IDs, the parsed QID) are validated as integers / strict
``^Q\\d+$`` strings before any SQL is built.
"""

from __future__ import annotations

import argparse
import re
import sys
import time
from dataclasses import dataclass
from typing import Iterable, Iterator

SPARQL_ENDPOINT = "https://query.wikidata.org/sparql"
USER_AGENT = "MeepleAI/1.0 (issue #2123; abuse@meepleai.app)"
QID_PATTERN = re.compile(r"^Q\d+$")


@dataclass(frozen=True)
class BgGameRow:
    """A row read from ``shared_games`` that is a candidate for QID bootstrap."""
    id: str  # UUID as string — we never operate on it numerically
    bgg_id: int


@dataclass
class BootstrapResult:
    candidates: int
    resolved: int
    updated: int

    @property
    def hit_rate(self) -> float:
        return self.resolved / self.candidates if self.candidates else 0.0


def chunked(iterable: list[BgGameRow], size: int) -> Iterator[list[BgGameRow]]:
    for i in range(0, len(iterable), size):
        yield iterable[i : i + size]


def build_sparql_query(bgg_ids: Iterable[int]) -> str:
    """
    Build a SPARQL VALUES query for the given BGG IDs.

    The IDs are inlined as strings (P2339 expects string literals) and escaped
    to digits-only by the caller — the regex below guards against any value
    that does not match ``^\\d+$``, raising before the request goes out.
    """
    safe = []
    for bid in bgg_ids:
        if not isinstance(bid, int) or bid <= 0:
            raise ValueError(f"refusing to inline non-positive BGG id: {bid!r}")
        safe.append(f'"{bid}"')
    values = " ".join(safe)
    return (
        "SELECT ?item ?bggId WHERE {\n"
        f"  VALUES ?bggId {{ {values} }}\n"
        "  ?item wdt:P2339 ?bggId .\n"
        "}"
    )


def parse_sparql_response(payload: dict) -> dict[int, str]:
    """
    Extract ``{bgg_id: qid}`` from a SPARQL JSON response. Skips bindings that
    cannot be parsed into a valid QID, returning the partial map and the
    caller will count the gap as "unresolved".
    """
    result: dict[int, str] = {}
    for binding in payload.get("results", {}).get("bindings", []):
        bgg_id_raw = binding.get("bggId", {}).get("value")
        qid_uri = binding.get("item", {}).get("value", "")
        try:
            bgg_id = int(bgg_id_raw)
        except (TypeError, ValueError):
            continue
        if not qid_uri:
            continue
        qid = qid_uri.rsplit("/", 1)[-1]
        if not QID_PATTERN.match(qid):
            continue
        # If a single BGG id maps to multiple Wikidata items, keep the first.
        result.setdefault(bgg_id, qid)
    return result


def fetch_candidates(cursor) -> list[BgGameRow]:
    cursor.execute(
        """
        SELECT id, bgg_id
        FROM shared_games
        WHERE bgg_id IS NOT NULL
          AND wikidata_qid IS NULL
          AND is_deleted = FALSE
        """
    )
    rows = cursor.fetchall()
    return [BgGameRow(id=str(r[0]), bgg_id=int(r[1])) for r in rows]


def apply_qid_updates(cursor, mapping: dict[int, str], bgg_id_to_row_id: dict[int, str]) -> int:
    """
    Run the UPDATE statements. Returns the number of rows affected.

    The QID format is re-validated here as defense in depth: if any value
    slipped through ``parse_sparql_response`` it gets discarded before SQL.
    """
    updated = 0
    for bgg_id, qid in mapping.items():
        if not QID_PATTERN.match(qid):
            continue
        row_id = bgg_id_to_row_id.get(bgg_id)
        if row_id is None:
            continue
        cursor.execute(
            "UPDATE shared_games "
            "SET wikidata_qid = %s, wikidata_qid_last_verified_at = NOW() "
            "WHERE id = %s::uuid AND wikidata_qid IS NULL",
            (qid, row_id),
        )
        updated += cursor.rowcount or 0
    return updated


def run(
    connection_string: str,
    batch_size: int,
    sparql_sleep_seconds: float,
    sparql_timeout_seconds: float,
    dry_run: bool,
    *,
    psycopg2_module=None,
    requests_module=None,
) -> BootstrapResult:
    """
    Main entry point. ``psycopg2_module`` and ``requests_module`` are injected
    for unit-test seams; the production CLI imports them at the call site.
    """
    if psycopg2_module is None:
        import psycopg2 as psycopg2_module  # type: ignore[no-redef]
    if requests_module is None:
        import requests as requests_module  # type: ignore[no-redef]

    conn = psycopg2_module.connect(connection_string)
    try:
        with conn.cursor() as cur:
            candidates = fetch_candidates(cur)
        if not candidates:
            print("No candidates: every row with a BGG id already has a QID.")
            return BootstrapResult(candidates=0, resolved=0, updated=0)

        print(f"Found {len(candidates)} candidate row(s) to resolve via SPARQL.")
        bgg_id_to_row_id = {r.bgg_id: r.id for r in candidates}
        resolved_map: dict[int, str] = {}
        for batch in chunked(candidates, batch_size):
            ids = [r.bgg_id for r in batch]
            query = build_sparql_query(ids)
            try:
                response = requests_module.get(
                    SPARQL_ENDPOINT,
                    params={"query": query, "format": "json"},
                    headers={"User-Agent": USER_AGENT, "Accept": "application/sparql-results+json"},
                    timeout=sparql_timeout_seconds,
                )
                response.raise_for_status()
                batch_map = parse_sparql_response(response.json())
            except Exception as exc:  # noqa: BLE001 — print + continue per-batch
                print(f"  batch {ids[0]}..{ids[-1]} failed: {type(exc).__name__}: {exc}", file=sys.stderr)
                batch_map = {}
            print(f"  batch {ids[0]}..{ids[-1]}: {len(batch_map)}/{len(ids)} resolved")
            resolved_map.update(batch_map)
            time.sleep(sparql_sleep_seconds)

        result = BootstrapResult(
            candidates=len(candidates),
            resolved=len(resolved_map),
            updated=0,
        )
        if dry_run:
            print(
                f"\nDry-run: {result.resolved}/{result.candidates} would be updated "
                f"({result.hit_rate * 100:.1f}% hit rate). No DB writes performed."
            )
            return result

        with conn.cursor() as cur:
            result = BootstrapResult(
                candidates=len(candidates),
                resolved=len(resolved_map),
                updated=apply_qid_updates(cur, resolved_map, bgg_id_to_row_id),
            )
        conn.commit()
        print(
            f"\nDone: {result.updated}/{result.candidates} rows updated "
            f"({result.hit_rate * 100:.1f}% hit rate)."
        )
        return result
    finally:
        conn.close()


def _parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument(
        "--connection-string",
        required=True,
        help="psycopg2-style PostgreSQL connection string for the target environment.",
    )
    ap.add_argument(
        "--dry-run",
        action="store_true",
        help="Print the resolution rate without performing any UPDATE.",
    )
    ap.add_argument(
        "--batch-size",
        type=int,
        default=50,
        help="Number of BGG IDs per SPARQL VALUES clause (default 50).",
    )
    ap.add_argument(
        "--sparql-sleep-seconds",
        type=float,
        default=1.0,
        help="Sleep between SPARQL batches to respect the Wikimedia 1 req/sec policy (default 1.0).",
    )
    ap.add_argument(
        "--sparql-timeout-seconds",
        type=float,
        default=30.0,
        help="Per-batch SPARQL request timeout in seconds (default 30.0).",
    )
    return ap.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = _parse_args(argv)
    try:
        result = run(
            connection_string=args.connection_string,
            batch_size=args.batch_size,
            sparql_sleep_seconds=args.sparql_sleep_seconds,
            sparql_timeout_seconds=args.sparql_timeout_seconds,
            dry_run=args.dry_run,
        )
    except Exception as exc:  # noqa: BLE001 — top-level CLI guard
        print(f"bootstrap-wikidata-qid: ERROR {type(exc).__name__}: {exc}", file=sys.stderr)
        return 1
    return 0 if result.candidates == 0 or result.resolved > 0 else 2


if __name__ == "__main__":
    sys.exit(main())
