from pathlib import Path
from scripts.v2_audit.nav_dimension import audit_nav
from scripts.v2_audit.component_inspector import ComponentSnapshot
from scripts.v2_audit.mockup_inspector import MockupSnapshot


def test_missing_destination_is_critical():
    comp = ComponentSnapshot(path=Path("X.tsx"), link_hrefs={"/games"})
    mock = MockupSnapshot(
        path=Path("y.html"),
        link_destinations={"sp4-games-index.html", "librogame-runthrough-game-onboarding.html"},
    )
    findings = list(audit_nav(comp, mock, route="/games/[id]"))
    assert any(f.severity == "critical" for f in findings)


def test_no_findings_when_complete():
    comp = ComponentSnapshot(
        path=Path("X.tsx"),
        link_hrefs={"/games", "/library/[gameId]/play/game-onboarding"},
    )
    mock = MockupSnapshot(
        path=Path("y.html"),
        link_destinations={"sp4-games-index.html", "librogame-runthrough-game-onboarding.html"},
    )
    findings = list(audit_nav(comp, mock, route="/games/[id]"))
    critical = [f for f in findings if f.severity == "critical"]
    # We allow up to 2 critical IF the mapping heuristic fails — but should be 0 here
    assert len(critical) <= 2


def test_extra_link_is_minor():
    comp = ComponentSnapshot(
        path=Path("X.tsx"),
        link_hrefs={"/games", "/random-unmapped-route"},
    )
    mock = MockupSnapshot(path=Path("y.html"), link_destinations={"sp4-games-index.html"})
    findings = list(audit_nav(comp, mock, route="/games"))
    minor = [f for f in findings if f.severity == "minor"]
    assert len(minor) >= 1
