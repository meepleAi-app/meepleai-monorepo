from pathlib import Path
from scripts.mockup_demo.apply_map import parse_nav_map


def test_parse_skips_todo_rows(tmp_path):
    """Rows with destination 'TODO' are skipped."""
    map_file = tmp_path / "nav-map.md"
    map_file.write_text(
        "| File | Selector | Text | Destination | Confidence |\n"
        "|---|---|---|---|---|\n"
        "| `a.html` | `a:L1:Foo` | Foo | `b.html` | 0.95 |\n"
        "| `c.html` | `a:L2:Bar` | Bar | `TODO` | 0.50 |\n",
        encoding="utf-8",
    )
    rows = parse_nav_map(map_file)
    assert len(rows) == 1
    assert rows[0][0] == "a.html"
    assert rows[0][3] == "b.html"


def test_parse_skips_out_of_scope_rows(tmp_path):
    """Rows with destination 'OUT_OF_SCOPE' are skipped."""
    map_file = tmp_path / "nav-map.md"
    map_file.write_text(
        "| File | Selector | Text | Destination | Confidence |\n"
        "|---|---|---|---|---|\n"
        "| `a.html` | `a:L1:Foo` | Foo | `OUT_OF_SCOPE` | 0.95 |\n",
        encoding="utf-8",
    )
    rows = parse_nav_map(map_file)
    assert len(rows) == 0


def test_parse_returns_all_fields(tmp_path):
    """Each row tuple has (filename, selector, text, destination)."""
    map_file = tmp_path / "nav-map.md"
    map_file.write_text(
        "| File | Selector | Text | Destination | Confidence |\n"
        "|---|---|---|---|---|\n"
        "| `index.html` | `button:L42:Play` | Play | `library.html` | 0.95 |\n",
        encoding="utf-8",
    )
    rows = parse_nav_map(map_file)
    assert rows[0] == ("index.html", "button:L42:Play", "Play", "library.html")
