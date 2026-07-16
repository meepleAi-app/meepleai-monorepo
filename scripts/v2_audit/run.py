"""CLI: orchestrate the full audit pipeline.

Usage:
  python -m scripts.v2_audit.run [--dry-run] [--limit N]
  python -m scripts.v2_audit.run --check [--max-baseline N]

Phase A: parse matrix, validate paths.
Phase B: per row, run 4 dimension audits.
Phase C: write report + input snapshot.

--check runs Phase A ONLY as the "State Matrix Sync + Enforcement" gate: it
reports v2-migration-matrix.md rows whose component/mockup path no longer exists
(drift) as NON-BLOCKING GitHub Actions ::warning:: annotations + a step summary.
It exits 0 unless --max-baseline N is given AND drift exceeds N (opt-in strict
ceiling; the CI workflow runs without a baseline so it never blocks merge).
"""
from __future__ import annotations
from pathlib import Path
import argparse
import json
import os
import sys

from scripts.v2_audit.matrix_parser import parse_matrix_with_skipped
from scripts.v2_audit.component_inspector import inspect_component
from scripts.v2_audit.mockup_inspector import inspect_mockup
from scripts.v2_audit.nav_dimension import audit_nav
from scripts.v2_audit.structural_dimension import audit_structural
from scripts.v2_audit.token_dimension import audit_tokens
from scripts.v2_audit.props_dimension import audit_props
from scripts.v2_audit.report_builder import build_report

REPO_ROOT = Path(__file__).resolve().parents[2]
MATRIX_PATH = REPO_ROOT / "docs" / "for-developers" / "frontend" / "v2-migration-matrix.md"
MATRIX_REL = "docs/for-developers/frontend/v2-migration-matrix.md"
MOCKUPS_DIR = REPO_ROOT / "admin-mockups" / "design_files"
REPORT_PATH = REPO_ROOT / "docs" / "superpowers" / "specs" / "audit-report.md"
INPUT_SNAPSHOT_PATH = REPO_ROOT / "docs" / "superpowers" / "specs" / "audit-input-validated.json"


def classify_rows(rows, repo_root: Path = REPO_ROOT, mockups_dir: Path = MOCKUPS_DIR):
    """Partition matrix rows into (drift, valid).

    drift: list[(component, missing_path_str)] — a row is drift when its component
           path OR its mockup file no longer exists (a sibling `.jsx` is accepted
           as a fallback for a missing `.html`/other mockup).
    valid: list[(row, comp_path, mockup_path)] — rows ready for the deep audit.

    This is the single source of truth for the "matrix consistency" check shared
    by run_audit (Phase A) and check_matrix_sync (the CI gate).
    """
    drift: list[tuple[str, str]] = []
    valid: list = []
    for row in rows:
        comp_path = repo_root / row.path
        mockup_path = mockups_dir / row.mockup
        if not comp_path.exists():
            drift.append((row.component, row.path))
            continue
        if not mockup_path.exists():
            mockup_jsx = mockup_path.with_suffix(".jsx")
            if mockup_jsx.exists():
                mockup_path = mockup_jsx
            else:
                # Repo-relative, forward-slash path — symmetric with the
                # component-missing case (row.path) and CI-portable in reports.
                drift.append((row.component, mockup_path.relative_to(repo_root).as_posix()))
                continue
        valid.append((row, comp_path, mockup_path))
    return drift, valid


def run_audit(limit: int | None = None, dry_run: bool = False) -> dict:
    rows, skipped_done_lines = parse_matrix_with_skipped(MATRIX_PATH)
    if limit:
        rows = rows[:limit]
    print(f"Phase A: {len(rows)} done rows to audit")
    if skipped_done_lines:
        print(f"Phase A: {len(skipped_done_lines)} done rows SKIPPED by strict parser (see snapshot)")

    matrix_drift, valid_rows = classify_rows(rows)

    print(f"Phase A: {len(matrix_drift)} matrix drift entries; {len(valid_rows)} ready for audit")

    if dry_run:
        print("DRY-RUN: would audit", len(valid_rows), "components")
        return {"dry_run": True, "valid_rows": len(valid_rows), "drift": len(matrix_drift)}

    print("Phase B: running 4 dimension audits per row")
    all_findings = []

    # First pass: extract all snapshots
    inspections = []  # list of (row, comp_path, mockup_path, comp, mock, comp_text, mockup_text)
    for row, comp_path, mockup_path in valid_rows:
        comp = inspect_component(comp_path)
        mock = inspect_mockup(mockup_path)
        comp_text = comp_path.read_text(encoding="utf-8")
        mockup_text = mockup_path.read_text(encoding="utf-8")
        inspections.append((row, comp_path, mockup_path, comp, mock, comp_text, mockup_text))

    # Second pass: non-nav dimensions per component (structural, tokens, props)
    for row, comp_path, mockup_path, comp, mock, comp_text, mockup_text in inspections:
        all_findings.extend(audit_structural(comp, mock, row.route))
        all_findings.extend(audit_tokens(comp, comp_text, row.route))
        all_findings.extend(audit_props(comp, mockup_text, row.route))

    # Third pass: nav dimension aggregated by route
    from collections import defaultdict
    from scripts.v2_audit.component_inspector import ComponentSnapshot
    from scripts.v2_audit.mockup_inspector import MockupSnapshot

    by_route_comps: dict[str, list] = defaultdict(list)
    by_route_mock_destinations: dict[str, set] = defaultdict(set)
    by_route_component_names: dict[str, list[str]] = defaultdict(list)

    for row, comp_path, mockup_path, comp, mock, comp_text, mockup_text in inspections:
        by_route_comps[row.route].append(comp)
        by_route_mock_destinations[row.route].update(mock.link_destinations)
        by_route_component_names[row.route].append(comp.path.name)

    for route, comps in by_route_comps.items():
        # Synthesize an aggregated ComponentSnapshot for the route
        aggregated = ComponentSnapshot(path=Path(f"<route:{route}>"))
        for c in comps:
            aggregated.link_hrefs |= c.link_hrefs
            aggregated.router_calls |= c.router_calls
        # Synthesize aggregated MockupSnapshot
        agg_mock = MockupSnapshot(path=Path(f"<route:{route}>"))
        agg_mock.link_destinations = by_route_mock_destinations[route]

        # Run nav audit ONCE per route
        for finding in audit_nav(aggregated, agg_mock, route):
            # Override the component field with all components in this route
            finding.component = ", ".join(sorted(set(by_route_component_names[route])))
            all_findings.append(finding)

    INPUT_SNAPSHOT_PATH.parent.mkdir(parents=True, exist_ok=True)
    with INPUT_SNAPSHOT_PATH.open("w", encoding="utf-8", newline="\n") as snap:
        json.dump(
            {
                "matrix_path": str(MATRIX_PATH.relative_to(REPO_ROOT)),
                "total_rows": len(rows),
                "valid_rows": len(valid_rows),
                "skipped_done_rows": skipped_done_lines,
                "skipped_done_count": len(skipped_done_lines),
                "drift": [{"component": c, "path": p} for c, p in matrix_drift],
                "audited_components": [
                    {"component": r.component, "route": r.route, "path": r.path}
                    for r, _, _ in valid_rows
                ],
            },
            snap,
            indent=2,
        )

    stats = build_report(
        all_findings,
        out_path=REPORT_PATH,
        total_components=len(valid_rows),
        matrix_drift=matrix_drift,
    )
    print(f"Phase C: report written to {REPORT_PATH}")
    print(f"Stats: {stats}")
    return stats


def _write_step_summary(rows, skipped, valid, drift, max_baseline) -> None:
    """Write a Markdown summary to $GITHUB_STEP_SUMMARY (no-op outside Actions)."""
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if not summary_path:
        return
    lines = [
        "## 🔗 State Matrix Sync + Enforcement",
        "",
        f"- Matrix: `{MATRIX_REL}`",
        f"- `done` rows parsed: **{len(rows)}** ({len(skipped)} skipped by the strict parser)",
        f"- Valid (component + mockup both exist): **{len(valid)}**",
        (
            f"- Drifted (component/mockup missing): **{len(drift)}**"
            + (f" · baseline `{max_baseline}`" if max_baseline is not None else " · _non-blocking report_")
        ),
        "",
    ]
    if drift:
        lines.append("<details><summary>Drifted rows</summary>")
        lines.append("")
        lines.append("| Component | Missing path |")
        lines.append("|---|---|")
        for comp, path in drift:
            lines.append(f"| `{comp}` | `{path}` |")
        lines.append("")
        lines.append("</details>")
    else:
        lines.append("✅ No matrix drift.")
    lines.append("")
    with open(summary_path, "a", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")


def check_matrix_sync(max_baseline: int | None = None) -> int:
    """State Matrix Sync gate (Phase A only). Reports v2-migration-matrix.md drift.

    NON-BLOCKING by design: emits GitHub ::warning:: annotations + a step summary
    and returns 0, UNLESS max_baseline is set and drift exceeds it (opt-in strict
    ceiling). The CI workflow runs without a baseline, so a red status can only
    come from the module self-tests, never from drift — keeping this a warning.
    """
    rows, skipped = parse_matrix_with_skipped(MATRIX_PATH)
    drift, valid = classify_rows(rows)
    n_drift = len(drift)
    in_actions = os.environ.get("GITHUB_ACTIONS") == "true"

    print(
        f"State Matrix Sync: {len(rows)} 'done' rows parsed "
        f"({len(skipped)} skipped by strict parser); {len(valid)} valid, {n_drift} drifted."
    )
    for comp, path in drift[:50]:
        print(f"  DRIFT  {comp} -> {path}")
    if n_drift > 50:
        print(f"  ... and {n_drift - 50} more (see step summary).")

    if in_actions and n_drift:
        # GitHub caps rendered ::warning:: annotations (~10) — emit one headline
        # plus a handful of examples rather than spamming one per drift row.
        print(
            f"::warning file={MATRIX_REL}::State Matrix Sync: {n_drift} row(s) in "
            f"{MATRIX_REL} reference a component/mockup that no longer exists (drift). "
            f"Reconcile the matrix rows (see step summary)."
        )
        for comp, path in drift[:9]:
            print(f"::warning file={MATRIX_REL}::drift: {comp} -> {path}")

    _write_step_summary(rows, skipped, valid, drift, max_baseline)

    if max_baseline is not None and n_drift > max_baseline:
        print(
            f"::error file={MATRIX_REL}::State Matrix drift ({n_drift}) exceeds "
            f"baseline ({max_baseline})."
        )
        return 1
    return 0


def main(argv=None):
    p = argparse.ArgumentParser()
    p.add_argument("--limit", type=int, help="Audit only first N rows")
    p.add_argument("--dry-run", action="store_true", help="Phase A only")
    p.add_argument(
        "--check",
        action="store_true",
        help="State Matrix Sync gate: report matrix drift (Phase A) as non-blocking warnings",
    )
    p.add_argument(
        "--max-baseline",
        type=int,
        default=None,
        help="With --check: exit 1 if drift exceeds this ceiling (opt-in strict; omit for a non-blocking report)",
    )
    args = p.parse_args(argv)
    if args.check:
        return check_matrix_sync(max_baseline=args.max_baseline)
    run_audit(limit=args.limit, dry_run=args.dry_run)
    return 0


if __name__ == "__main__":
    sys.exit(main())
