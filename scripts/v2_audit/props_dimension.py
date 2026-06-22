"""Props dimension audit: compare TS interface fields vs mockup data access patterns.

Most heuristic dimension. Primarily input for manual review.
"""
from __future__ import annotations
from typing import Iterator
import re

from scripts.v2_audit.component_inspector import ComponentSnapshot
from scripts.v2_audit.finding import Finding, Severity, Confidence, Dimension


# Detect data field access in mockup text: X.field (in template literals, JSX, etc.)
_FIELD_ACCESS_RE = re.compile(r"\b(\w+)\.(\w+)\b")


def _extract_mockup_data_fields(text: str) -> set[str]:
    """Return set of accessed fields like 'player.name', 'game.title'."""
    fields = set()
    for m in _FIELD_ACCESS_RE.finditer(text):
        obj, field = m.group(1), m.group(2)
        # Skip noise (window.X, document.X, Math.X, this.X, etc.)
        if obj.lower() in {"window", "document", "math", "this", "console", "process", "json"}:
            continue
        fields.add(f"{obj}.{field}")
    return fields


def audit_props(
    comp: ComponentSnapshot,
    mockup_text: str,
    route: str,
) -> Iterator[Finding]:
    # If component has no props interfaces, can't audit → LOW confidence flag
    if not comp.props_interfaces:
        yield Finding(
            dimension=Dimension.PROPS,
            severity=Severity.MINOR,
            confidence=Confidence.LOW,
            component=comp.path.name,
            route=route,
            description="No TS interface for props (cannot auto-audit)",
            evidence={"reason": "no_interface_found"},
        )
        return

    # Collect all prop field names across all interfaces in the component
    all_props_fields = set()
    for iface_fields in comp.props_interfaces.values():
        all_props_fields.update(iface_fields.keys())

    # Extract data fields accessed in mockup
    mockup_fields = _extract_mockup_data_fields(mockup_text)
    mockup_field_names = {f.split(".", 1)[1] for f in mockup_fields}

    # Missing fields: mockup uses them, component doesn't expose them
    missing = mockup_field_names - all_props_fields
    for field in sorted(missing):
        yield Finding(
            dimension=Dimension.PROPS,
            severity=Severity.IMPORTANT,
            confidence=Confidence.LOW,  # static analysis is heuristic here
            component=comp.path.name,
            route=route,
            description=f"Mockup accesses '{field}' but no matching prop in component",
            evidence={
                "missing_field": field,
                "component_props": sorted(all_props_fields),
                "mockup_field_accesses": sorted(mockup_field_names),
            },
        )
