"""Structural dimension audit: semantic landmarks + heading hierarchy."""
from __future__ import annotations
from typing import Iterator
import functools
import re
from pathlib import Path

from scripts.v2_audit.component_inspector import ComponentSnapshot
from scripts.v2_audit.mockup_inspector import MockupSnapshot
from scripts.v2_audit.finding import Finding, Severity, Confidence, Dimension
from scripts.v2_audit.nav_dimension import _route_to_filesystem_path


# Landmarks considered Critical if missing
_CRITICAL_LANDMARKS = {"header", "main", "section"}
# Headings considered Important if mockup has them but component doesn't
_KEY_HEADINGS = {"h1", "h2"}
# Threshold: if component is missing more than 60% of mockup landmarks,
# mark all findings LOW confidence (component may be a legit subset)
_HIGH_DIVERGENCE_PCT = 0.60

_H1_RE = re.compile(r"<h1\b", re.IGNORECASE)


@functools.lru_cache(maxsize=None)
def _route_tree_has_h1(route: str) -> bool:
    """Return True if any TSX file in the route's filesystem tree contains <h1>.

    Skips test files (.test.tsx, .stories.tsx). Returns False if route folder
    doesn't exist.
    """
    path = _route_to_filesystem_path(route)
    if path is None:
        return False
    for tsx in path.glob("**/*.tsx"):
        name = tsx.name
        if name.endswith(".test.tsx") or name.endswith(".stories.tsx"):
            continue
        try:
            text = tsx.read_text(encoding="utf-8")
        except OSError:
            continue
        if _H1_RE.search(text):
            return True
    return False


def audit_structural(
    comp: ComponentSnapshot,
    mock: MockupSnapshot,
    route: str,
) -> Iterator[Finding]:
    missing_landmarks = mock.landmarks - comp.landmarks
    missing_headings = mock.headings - comp.headings

    # Calculate divergence ratio
    if mock.landmarks:
        divergence = len(missing_landmarks) / len(mock.landmarks)
    else:
        divergence = 0.0
    confidence = Confidence.LOW if divergence > _HIGH_DIVERGENCE_PCT else Confidence.MEDIUM

    for lm in sorted(missing_landmarks):
        severity = Severity.CRITICAL if lm in _CRITICAL_LANDMARKS else Severity.MINOR
        yield Finding(
            dimension=Dimension.STRUCTURAL,
            severity=severity,
            confidence=confidence,
            component=comp.path.name,
            route=route,
            description=f"Missing semantic landmark: <{lm}>",
            evidence={
                "missing": lm,
                "mockup_landmarks": sorted(mock.landmarks),
                "component_landmarks": sorted(comp.landmarks),
            },
        )

    parent_has_h1 = _route_tree_has_h1(route)

    for h in sorted(missing_headings):
        if h in _KEY_HEADINGS:
            # Suppress h1 finding if parent (page/layout) already has h1 (WCAG: single h1 per page)
            if h == "h1" and parent_has_h1:
                continue
            yield Finding(
                dimension=Dimension.STRUCTURAL,
                severity=Severity.IMPORTANT,
                confidence=confidence,
                component=comp.path.name,
                route=route,
                description=f"Missing heading: <{h}>",
                evidence={
                    "missing": h,
                    "mockup_headings": sorted(mock.headings),
                    "component_headings": sorted(comp.headings),
                },
            )
