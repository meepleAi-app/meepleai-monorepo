"""CLI: create GitHub issues from audit findings via `gh` CLI.

Usage:
  python -m scripts.v2_audit.create_issues [--dry-run]

Idempotent: checks for existing open issues with title `[audit] Route X: N findings`.
If exists, updates body via `gh issue edit`. Otherwise creates via `gh issue create`.
"""
from __future__ import annotations
from collections import defaultdict
from pathlib import Path
import argparse
import json
import subprocess
import sys

from scripts.v2_audit.matrix_parser import parse_matrix
from scripts.v2_audit.component_inspector import inspect_component, ComponentSnapshot
from scripts.v2_audit.mockup_inspector import inspect_mockup, MockupSnapshot
from scripts.v2_audit.nav_dimension import audit_nav
from scripts.v2_audit.structural_dimension import audit_structural
from scripts.v2_audit.token_dimension import audit_tokens
from scripts.v2_audit.props_dimension import audit_props
from scripts.v2_audit.confidence_scorer import eligible_for_issue
from scripts.v2_audit.issue_creator import build_issue_title, build_issue_body

REPO_ROOT = Path(__file__).resolve().parents[2]
MATRIX_PATH = REPO_ROOT / "docs" / "for-developers" / "frontend" / "v2-migration-matrix.md"
MOCKUPS_DIR = REPO_ROOT / "admin-mockups" / "design_files"


def _collect_findings_by_route() -> dict[str, dict]:
    """Re-run audits and group by route. Mirror the aggregation logic from run.py.

    Returns: { route: { "findings": [Finding], "mockups": set[str] } }
    """
    rows = list(parse_matrix(MATRIX_PATH, status_filter="done"))
    by_route: dict[str, dict] = defaultdict(lambda: {"findings": [], "mockups": set()})

    # First pass: inspect all components, run per-component dimensions
    inspections = []
    for row in rows:
        comp_path = REPO_ROOT / row.path
        mockup_path = MOCKUPS_DIR / row.mockup
        if not comp_path.exists():
            continue
        if not mockup_path.exists():
            jsx = mockup_path.with_suffix(".jsx")
            if jsx.exists():
                mockup_path = jsx
            else:
                continue

        comp = inspect_component(comp_path)
        mock = inspect_mockup(mockup_path)
        comp_text = comp_path.read_text(encoding="utf-8")
        mockup_text = mockup_path.read_text(encoding="utf-8")
        inspections.append((row, comp, mock, comp_text, mockup_text))

        by_route[row.route]["mockups"].add(row.mockup)

        # Per-component dimensions
        by_route[row.route]["findings"].extend(audit_structural(comp, mock, row.route))
        by_route[row.route]["findings"].extend(audit_tokens(comp, comp_text, row.route))
        by_route[row.route]["findings"].extend(audit_props(comp, mockup_text, row.route))

    # Second pass: aggregate nav by route
    by_route_comps: dict[str, list[ComponentSnapshot]] = defaultdict(list)
    by_route_mocks_dests: dict[str, set[str]] = defaultdict(set)
    by_route_comp_names: dict[str, list[str]] = defaultdict(list)
    for row, comp, mock, _, _ in inspections:
        by_route_comps[row.route].append(comp)
        by_route_mocks_dests[row.route].update(mock.link_destinations)
        by_route_comp_names[row.route].append(comp.path.name)

    for route, comps in by_route_comps.items():
        aggregated = ComponentSnapshot(path=Path(f"<route:{route}>"))
        for c in comps:
            aggregated.link_hrefs |= c.link_hrefs
            aggregated.router_calls |= c.router_calls
        agg_mock = MockupSnapshot(path=Path(f"<route:{route}>"))
        agg_mock.link_destinations = by_route_mocks_dests[route]

        for finding in audit_nav(aggregated, agg_mock, route):
            finding.component = ", ".join(sorted(set(by_route_comp_names[route])))
            by_route[route]["findings"].append(finding)

    return by_route


def _find_existing_issue(title: str) -> int | None:
    """Return issue number if an open issue with this title exists, else None."""
    try:
        # gh search needs exact title escaping; use --search with quotes
        result = subprocess.run(
            ["gh", "issue", "list", "--state", "open", "--search", title, "--json", "number,title", "--limit", "100"],
            capture_output=True, text=True, check=True, encoding="utf-8",
        )
        issues = json.loads(result.stdout)
        for issue in issues:
            if issue["title"] == title:
                return issue["number"]
    except subprocess.CalledProcessError as e:
        print(f"WARN: gh issue list failed: {e.stderr}")
    return None


def _create_or_update_issue(title: str, body: str, dry_run: bool) -> str:
    existing = _find_existing_issue(title)
    if dry_run:
        action = "update" if existing else "create"
        return f"[DRY-RUN] would {action} issue: {title}" + (f" (#{existing})" if existing else "")

    if existing:
        subprocess.run(
            ["gh", "issue", "edit", str(existing), "--body", body],
            check=True, encoding="utf-8",
        )
        return f"Updated #{existing}: {title}"
    else:
        result = subprocess.run(
            ["gh", "issue", "create",
             "--title", title,
             "--body", body,
             "--label", "audit-finding",
             "--label", "area/frontend",
             "--label", "v2-migration"],
            capture_output=True, text=True, check=True, encoding="utf-8",
        )
        return f"Created: {result.stdout.strip()}"


def main(argv=None):
    p = argparse.ArgumentParser()
    p.add_argument("--dry-run", action="store_true")
    args = p.parse_args(argv)

    by_route = _collect_findings_by_route()
    created_count = 0
    skipped_count = 0

    for route in sorted(by_route):
        data = by_route[route]
        eligible = [f for f in data["findings"] if eligible_for_issue(f)]
        if not eligible:
            skipped_count += 1
            continue

        title = build_issue_title(route, eligible)
        body = build_issue_body(eligible, mockups=sorted(data["mockups"]))
        result = _create_or_update_issue(title, body, args.dry_run)
        print(result)
        created_count += 1

    print(f"\nSummary: {created_count} issues processed, {skipped_count} routes had no eligible findings.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
