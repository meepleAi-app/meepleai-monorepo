"""Structural dimension audit: semantic landmarks + heading hierarchy."""
from __future__ import annotations
from typing import Iterator

from scripts.v2_audit.component_inspector import ComponentSnapshot
from scripts.v2_audit.mockup_inspector import MockupSnapshot
from scripts.v2_audit.finding import Finding, Severity, Confidence, Dimension


# Landmarks considered Critical if missing
_CRITICAL_LANDMARKS = {"header", "main", "section"}
# Headings considered Important if mockup has them but component doesn't
_KEY_HEADINGS = {"h1", "h2"}
# Threshold: if component is missing more than 60% of mockup landmarks,
# mark all findings LOW confidence (component may be a legit subset)
_HIGH_DIVERGENCE_PCT = 0.60


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

    for h in sorted(missing_headings):
        if h in _KEY_HEADINGS:
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
