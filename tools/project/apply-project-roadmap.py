#!/usr/bin/env python3
"""
Populate Phase / Branch / Checkpoint fields in the GitHub Project board
based on issue labels contained in the roadmap.
"""

from __future__ import annotations

import json
import subprocess
import sys
from dataclasses import dataclass
from typing import Dict, Iterable, List, Optional, Sequence

PROJECT_OWNER = "DegrassiAaron"
PROJECT_NUMBER = "4"
PROJECT_ID = "PVT_kwHOAOeY484BIC8G"

PHASE_FIELD_ID = "PVTSSF_lAHOAOeY484BIC8Gzg4n0-0"
BRANCH_FIELD_ID = "PVTSSF_lAHOAOeY484BIC8Gzg4n0-8"
CHECKPOINT_FIELD_ID = "PVTSSF_lAHOAOeY484BIC8Gzg4n1Lc"

PHASE_OPTIONS = {
    "Phase 1  Sprint 1": "55420e41",
    "Phase 2  Sprint 2": "3224c7ad",
    "Phase 3  BGAI Month 1-2": "a06adde7",
    "Phase 4  BGAI Month 3-4": "37a56910",
    "Phase 5  BGAI Month 5-6": "cbb4a436",
    "Admin  FASE 1": "219a0d48",
    "Admin  FASE 2-4": "84e5351c",
    "Frontend Epics": "e4588418",
    "FE-IMP": "1ddffc2e",
    "Ops/Test": "c87fd7b5",
}

BRANCH_OPTIONS = {
    "Main": "9a7f6f9f",
    "Frontend": "82c1cb39",
    "Backend": "96178022",
}

CHECKPOINT_OPTIONS = {
    "CP1 Sprint 1-2": "35ef9354",
    "CP2 Month 1-2": "88aba175",
    "CP3 Month 3-4": "bac01793",
    "CP4 Month 5-6": "f5f30a50",
}

CHECKPOINT_FOR_PHASE = {
    "Phase 1  Sprint 1": "CP1 Sprint 1-2",
    "Phase 2  Sprint 2": "CP1 Sprint 1-2",
    "Phase 3  BGAI Month 1-2": "CP2 Month 1-2",
    "Phase 4  BGAI Month 3-4": "CP3 Month 3-4",
    "Phase 5  BGAI Month 5-6": "CP4 Month 5-6",
    "Admin  FASE 1": "CP4 Month 5-6",
    "Admin  FASE 2-4": "CP4 Month 5-6",
    "Frontend Epics": "CP4 Month 5-6",
    "FE-IMP": "CP4 Month 5-6",
    "Ops/Test": "CP4 Month 5-6",
}


@dataclass
class ProjectItem:
    item_id: str
    number: int
    title: str
    labels: List[str]
    milestone: Optional[str]


def run_gh(args: Sequence[str]) -> str:
    """Run a gh command and return stdout."""
    completed = subprocess.run(
        ["gh", *args],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )
    if completed.returncode != 0:
        sys.stderr.write(completed.stdout)
        sys.stderr.write(completed.stderr)
        raise RuntimeError(f"Command failed: gh {' '.join(args)}")
    return completed.stdout


def load_items() -> List[ProjectItem]:
    """Fetch all project items as ProjectItem objects."""
    output = run_gh(
        [
            "project",
            "item-list",
            PROJECT_NUMBER,
            "--owner",
            PROJECT_OWNER,
            "--limit",
            "200",
            "--format",
            "json",
        ]
    )
    payload = json.loads(output)
    items: List[ProjectItem] = []
    for entry in payload.get("items", []):
        content = entry.get("content") or {}
        number = content.get("number")
        if number is None:
            # Skip draft cards
            continue
        title = content.get("title") or entry.get("title") or ""
        labels = entry.get("labels") or []
        milestone = entry.get("milestone") or {}
        items.append(
            ProjectItem(
                item_id=entry["id"],
                number=number,
                title=title,
                labels=[str(label).lower() for label in labels],
                milestone=milestone.get("title") if milestone else None,
            )
        )
    return sorted(items, key=lambda item: item.number)


def determine_phase(item: ProjectItem) -> Optional[str]:
    """Return the roadmap phase name for the issue."""
    labels = set(item.labels)
    title = item.title.lower()
    milestone = (item.milestone or "").lower()

    if title.startswith("fe-imp"):
        return "FE-IMP"
    if title.startswith("[frontend-") or ("frontend" in labels and "epic" in labels):
        return "Frontend Epics"
    if "fase-1-dashboard" in labels:
        return "Admin  FASE 1"
    if {"fase-2-infrastructure", "fase-3-management", "fase-4-advanced"} & labels:
        return "Admin  FASE 2-4"
    if "sprint-1" in labels:
        return "Phase 1  Sprint 1"
    if "sprint-2" in labels:
        return "Phase 2  Sprint 2"
    if "sprint-3" in labels:
        return "Phase 3  BGAI Month 1-2"
    if "sprint-4" in labels:
        return "Phase 4  BGAI Month 3-4"
    if "sprint-5" in labels:
        return "Phase 5  BGAI Month 5-6"
    if "[sprint-1]" in title:
        return "Phase 1  Sprint 1"
    if "[sprint-2]" in title:
        return "Phase 2  Sprint 2"
    if "[sprint-3]" in title:
        return "Phase 3  BGAI Month 1-2"
    if "[sprint-4]" in title:
        return "Phase 4  BGAI Month 3-4"
    if "[sprint-5]" in title:
        return "Phase 5  BGAI Month 5-6"
    if {"month-1", "month-2"} & labels:
        return "Phase 3  BGAI Month 1-2"
    if {"month-3", "month-4"} & labels:
        return "Phase 4  BGAI Month 3-4"
    if {"month-5", "month-6"} & labels:
        return "Phase 5  BGAI Month 5-6"
    if "month 1" in milestone or "month 2" in milestone:
        return "Phase 3  BGAI Month 1-2"
    if "month 3" in milestone or "month 4" in milestone:
        return "Phase 4  BGAI Month 3-4"
    if "month 5" in milestone or "month 6" in milestone:
        return "Phase 5  BGAI Month 5-6"
    if (
        "testing" in labels
        or "qa" in labels
        or "ci-cd" in labels
        or any("security" in label for label in labels)
        or title.startswith("[testing")
        or title.startswith("[ci/cd")
        or title.startswith("test(")
        or "test " in title
        or "security" in title
    ):
        return "Ops/Test"
    if "main" in labels:
        return "Ops/Test"
    return None


def determine_branch(item: ProjectItem) -> str:
    """Infer active branch from labels."""
    labels = set(item.labels)
    if "frontend" in labels:
        return "Frontend"
    if "backend" in labels:
        return "Backend"
    if "main" in labels:
        return "Main"
    return "Main"


def determine_checkpoint(phase_name: str) -> Optional[str]:
    """Checkpoint is derived directly from the phase."""
    return CHECKPOINT_FOR_PHASE.get(phase_name)


def set_single_select(item_id: str, field_id: str, option_id: str) -> None:
    """Set a project field to the given option."""
    run_gh(
        [
            "project",
            "item-edit",
            "--id",
            item_id,
            "--project-id",
            PROJECT_ID,
            "--field-id",
            field_id,
            "--single-select-option-id",
            option_id,
        ]
    )


def main() -> int:
    items = load_items()
    phase_counts: Dict[str, int] = {}
    missing_phase: List[ProjectItem] = []

    for item in items:
        phase_name = determine_phase(item)
        if not phase_name:
            missing_phase.append(item)
            continue
        branch_name = determine_branch(item)
        checkpoint_name = determine_checkpoint(phase_name)

        set_single_select(item.item_id, PHASE_FIELD_ID, PHASE_OPTIONS[phase_name])
        set_single_select(item.item_id, BRANCH_FIELD_ID, BRANCH_OPTIONS[branch_name])
        if checkpoint_name:
            set_single_select(
                item.item_id,
                CHECKPOINT_FIELD_ID,
                CHECKPOINT_OPTIONS[checkpoint_name],
            )

        phase_counts[phase_name] = phase_counts.get(phase_name, 0) + 1
        print(
            f"[# {item.number}] Phase={phase_name} Branch={branch_name} "
            f"Checkpoint={checkpoint_name}"
        )

    if missing_phase:
        sys.stderr.write("\n[WARN] Unable to map phase for the following issues:\n")
        for item in missing_phase:
            sys.stderr.write(f"  - #{item.number} {item.title}\n")
        return 1

    print("\nSummary by phase:")
    for phase_name, count in sorted(phase_counts.items()):
        print(f"  {phase_name}: {count} items")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
