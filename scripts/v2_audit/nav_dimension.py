"""Navigation dimension audit.

Compares ComponentSnapshot.link_hrefs (Next.js routes) vs
MockupSnapshot.link_destinations (mockup filenames).

Strategy: for each mockup destination, attempt to map it to a Next.js route
via a simple heuristic. Then compare against component link_hrefs.

For unmappable destinations: emit IMPORTANT-severity LOW-confidence finding.
For mapped destinations not present in component: CRITICAL.
For extra component routes not in mockup: MINOR.
"""
from __future__ import annotations
from typing import Iterator
import re

from scripts.v2_audit.component_inspector import ComponentSnapshot
from scripts.v2_audit.mockup_inspector import MockupSnapshot
from scripts.v2_audit.finding import Finding, Severity, Confidence, Dimension


def _mockup_to_route(mockup_dest: str) -> str | None:
    """Heuristic: map a mockup filename to a Next.js route.

    Returns None if no confident mapping found.
    """
    name = mockup_dest.replace(".html", "")
    # Common standalone mockups (checked first to override prefix patterns)
    KNOWN = {
        "sp4-dashboard": "/dashboard",
        "sp4-discover": "/discover",
        "sp4-library-desktop": "/library",
        "sp4-game-chat-tab": "/games/[id]/chat",
        "sp4-citation-pdf-viewer": "/kb/[id]/pdf",
        "notifications": "/notifications",
        "settings": "/settings",
        "onboarding": "/onboarding",
        "auth-flow": "/login",
        "index": "/",
    }
    if name in KNOWN:
        return KNOWN[name]
    # sp4-X-index → /X
    m = re.match(r"sp4-(\w+)-index$", name)
    if m:
        return f"/{m.group(1)}"
    # sp4-X-detail → /X/[id]
    m = re.match(r"sp4-(\w+)-detail$", name)
    if m:
        return f"/{m.group(1)}/[id]"
    # sp4-hub-X → /hub/X
    m = re.match(r"sp4-hub-(\w+)$", name)
    if m:
        return f"/hub/{m.group(1)}"
    # librogame-runthrough-X → /library/[gameId]/play/X
    m = re.match(r"librogame-runthrough-(\w+(?:-\w+)*)$", name)
    if m:
        return f"/library/[gameId]/play/{m.group(1)}"
    # sp7-game-night-X → /game-nights/[id]/X
    m = re.match(r"sp7-game-night-(\w+(?:-\w+)*)$", name)
    if m:
        return f"/game-nights/[id]/{m.group(1)}"
    # sp3-X → /X (public)
    m = re.match(r"sp3-(\w+(?:-\w+)*)$", name)
    if m:
        return f"/{m.group(1)}"
    return None


def audit_nav(
    comp: ComponentSnapshot,
    mock: MockupSnapshot,
    route: str,
) -> Iterator[Finding]:
    comp_hrefs = comp.link_hrefs | comp.router_calls
    mapped_destinations = {}  # route → mockup_dest
    unmappable = []

    for dest in mock.link_destinations:
        rt = _mockup_to_route(dest)
        if rt:
            mapped_destinations[rt] = dest
        else:
            unmappable.append(dest)

    # Critical: mockup destination not present in component
    for expected_route, mockup_dest in mapped_destinations.items():
        if expected_route not in comp_hrefs:
            # Allow prefix/superset matching (e.g. /games matches /games/X)
            matched = any(
                h == expected_route
                or h.startswith(expected_route + "/")
                or expected_route.startswith(h + "/")
                for h in comp_hrefs
            )
            if not matched:
                yield Finding(
                    dimension=Dimension.NAV,
                    severity=Severity.CRITICAL,
                    confidence=Confidence.HIGH,
                    component=comp.path.name,
                    route=route,
                    description=f"Missing Link to {expected_route} (mockup: {mockup_dest})",
                    evidence={
                        "expected_route": expected_route,
                        "mockup_dest": mockup_dest,
                        "component_hrefs": sorted(comp_hrefs),
                    },
                )

    # Low confidence: unmappable mockup destinations
    for dest in unmappable:
        yield Finding(
            dimension=Dimension.NAV,
            severity=Severity.IMPORTANT,
            confidence=Confidence.LOW,
            component=comp.path.name,
            route=route,
            description=f"Unmappable mockup destination: {dest}",
            evidence={"mockup_dest": dest},
        )

    # Minor: extra component links not in mockup
    mapped_routes = set(mapped_destinations.keys())
    for href in comp_hrefs:
        if href not in mapped_routes and not any(
            href.startswith(r + "/") or r.startswith(href + "/")
            for r in mapped_routes
        ):
            yield Finding(
                dimension=Dimension.NAV,
                severity=Severity.MINOR,
                confidence=Confidence.MEDIUM,
                component=comp.path.name,
                route=route,
                description=f"Component link not in mockup: {href}",
                evidence={"component_href": href, "mapped_routes": sorted(mapped_routes)},
            )
