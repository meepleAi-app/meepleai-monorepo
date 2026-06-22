from pathlib import Path
from scripts.mockup_demo.validate import extract_targets, bfs_reachable


def test_extract_targets_html(tmp_path):
    f = tmp_path / "a.html"
    f.write_text(
        '<a href="b.html">b</a> '
        '<button onclick="window.location.href=\'c.html\'">c</button>',
        encoding="utf-8"
    )
    targets = extract_targets(f)
    assert "b.html" in targets
    assert "c.html" in targets


def test_extract_targets_jsx(tmp_path):
    f = tmp_path / "a.jsx"
    f.write_text("onClick={() => { window.location.href = 'd.html' }}", encoding="utf-8")
    targets = extract_targets(f)
    assert "d.html" in targets


def test_bfs_reachable(tmp_path):
    (tmp_path / "index.html").write_text('<a href="a.html">a</a>', encoding="utf-8")
    (tmp_path / "a.html").write_text('<a href="b.html">b</a>', encoding="utf-8")
    (tmp_path / "b.html").write_text('end', encoding="utf-8")
    (tmp_path / "orphan.html").write_text('orphan', encoding="utf-8")
    reachable, total = bfs_reachable(tmp_path, "index.html")
    assert "index.html" in reachable
    assert "a.html" in reachable
    assert "b.html" in reachable
    assert "orphan.html" not in reachable
