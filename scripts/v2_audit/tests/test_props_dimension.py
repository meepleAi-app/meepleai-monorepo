from pathlib import Path
from scripts.v2_audit.props_dimension import audit_props
from scripts.v2_audit.component_inspector import ComponentSnapshot


def test_missing_data_field_is_important():
    """Mockup displays player.name + player.wins; component only accepts name."""
    comp = ComponentSnapshot(
        path=Path("X.tsx"),
        props_interfaces={"PlayerCardProps": {"name": "string"}},
    )
    mockup_text = 'data-player="${player.name} ${player.wins} ${player.avatar}"'
    findings = list(audit_props(comp, mockup_text=mockup_text, route="/players"))
    descriptions = [f.description for f in findings]
    assert any("wins" in d for d in descriptions)
    assert any("avatar" in d for d in descriptions)


def test_no_findings_when_props_complete():
    comp = ComponentSnapshot(
        path=Path("X.tsx"),
        props_interfaces={
            "PlayerCardProps": {"name": "string", "avatar": "string", "wins": "number"}
        },
    )
    mockup_text = "${player.name} ${player.avatar} ${player.wins}"
    findings = list(audit_props(comp, mockup_text=mockup_text, route="/players"))
    assert len(findings) == 0


def test_low_confidence_when_no_interface():
    """Component has no Props interface → emit single LOW-confidence flag."""
    comp = ComponentSnapshot(path=Path("X.tsx"), props_interfaces={})
    mockup_text = "${player.name}"
    findings = list(audit_props(comp, mockup_text=mockup_text, route="/players"))
    assert len(findings) == 1
    assert findings[0].confidence == "low"
