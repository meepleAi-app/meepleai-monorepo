from pathlib import Path
from scripts.v2_audit.token_dimension import audit_tokens
from scripts.v2_audit.component_inspector import ComponentSnapshot


def test_hardcoded_color_is_important():
    comp = ComponentSnapshot(
        path=Path("X.tsx"),
        tailwind_tokens={"bg-white", "text-foreground", "border-zinc-300"},
    )
    findings = list(audit_tokens(comp, file_text="", route="/x"))
    bad = [f for f in findings if "bg-white" in f.description or "zinc" in f.description]
    assert len(bad) >= 2
    assert all(f.severity == "important" for f in bad)


def test_eslint_disable_is_critical(tmp_path):
    f = tmp_path / "X.tsx"
    f.write_text(
        '/* eslint-disable local/no-hardcoded-color-utility */\n'
        'export const X = () => <div className="bg-white" />;',
        encoding="utf-8",
    )
    comp = ComponentSnapshot(path=f, tailwind_tokens={"bg-white"})
    findings = list(audit_tokens(comp, file_text=f.read_text(), route="/x"))
    critical = [f for f in findings if f.severity == "critical"]
    assert any("eslint-disable" in f.description for f in critical)


def test_text_white_without_paired_bg_is_minor():
    comp = ComponentSnapshot(path=Path("X.tsx"), tailwind_tokens={"text-white"})
    findings = list(audit_tokens(comp, file_text="<div className='text-white'>x</div>", route="/x"))
    minor = [f for f in findings if f.severity == "minor"]
    assert any("text-white" in f.description for f in minor)


def test_text_white_with_entity_bg_no_finding():
    comp = ComponentSnapshot(
        path=Path("X.tsx"),
        tailwind_tokens={"text-white", "bg-entity-game"},
    )
    text = '<div className="bg-entity-game text-white">x</div>'
    findings = list(audit_tokens(comp, file_text=text, route="/x"))
    minor_about_text_white = [
        f for f in findings if "text-white" in f.description and f.severity == "minor"
    ]
    assert len(minor_about_text_white) == 0
