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
