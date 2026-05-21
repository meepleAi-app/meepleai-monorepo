"""Tests for clickable_extractor — TDD Step 1 (red)."""
from scripts.mockup_demo.clickable_extractor import extract_clickables, Clickable


def test_extract_html_anchors_and_buttons(sample_html_path):
    items = list(extract_clickables(sample_html_path))
    assert any(it.text.strip() == "Dashboard" and it.tag == "a" for it in items)
    assert any(it.text.strip() == "Games" and it.tag == "a" for it in items)
    assert any(it.text.strip() == "Avvia libro game" and it.tag == "button" for it in items)
    assert any("game-card" in (it.classes or "") for it in items)


def test_clickable_has_required_fields(sample_html_path):
    items = list(extract_clickables(sample_html_path))
    for it in items:
        assert it.file_path == sample_html_path
        assert it.tag in ("a", "button", "div", "li", "span")
        assert it.line_number > 0
        assert it.snippet  # exact source snippet for later locating


def test_extract_jsx_clickables(sample_jsx_path):
    items = list(extract_clickables(sample_jsx_path))
    assert any(it.text.strip() == "Dashboard" and it.kind == "jsx" for it in items)
    assert any(it.text.strip() == "Avvia libro game" and it.on_click_existing is not None for it in items)
    assert any("game-card" in (it.classes or "") for it in items)


def test_extract_jsx_with_double_brace_style(tmp_path):
    """Regression: real mockups use style={{...}} which broke v1 regex."""
    from scripts.mockup_demo.clickable_extractor import extract_clickables
    f = tmp_path / "double_brace.jsx"
    f.write_text(
        '<a href="#" style={{ padding: "6px 12px", color: "red" }}>Dashboard</a>\n'
        '<button style={{ display: "flex" }} onClick={() => go()}>Play</button>',
        encoding="utf-8",
    )
    items = list(extract_clickables(f))
    assert any(it.text.strip() == "Dashboard" and it.tag == "a" for it in items)
    assert any(it.text.strip() == "Play" and it.tag == "button" for it in items)
