"""Tests for run.classify_rows — the matrix-drift classification that powers
both the deep audit (Phase A) and the State Matrix Sync gate (#2998)."""
from scripts.v2_audit.matrix_parser import MatrixRow
from scripts.v2_audit.run import classify_rows


def _row(component: str, path: str, mockup: str) -> MatrixRow:
    return MatrixRow(
        mockup=mockup, component=component, path=path, route="/x", status="done", pr="#1"
    )


def test_classify_valid_row(tmp_path):
    repo = tmp_path
    mockups = repo / "mock"
    mockups.mkdir()
    (repo / "comp.tsx").write_text("x", encoding="utf-8")
    (mockups / "m.html").write_text("x", encoding="utf-8")

    drift, valid = classify_rows([_row("Good", "comp.tsx", "m.html")], repo_root=repo, mockups_dir=mockups)

    assert drift == []
    assert len(valid) == 1
    row, comp_path, mockup_path = valid[0]
    assert row.component == "Good"
    assert comp_path == repo / "comp.tsx"
    assert mockup_path == mockups / "m.html"


def test_classify_drift_missing_component(tmp_path):
    repo = tmp_path
    mockups = repo / "mock"
    mockups.mkdir()
    (mockups / "m.html").write_text("x", encoding="utf-8")  # mockup exists, component does not

    drift, valid = classify_rows([_row("NoComp", "missing.tsx", "m.html")], repo_root=repo, mockups_dir=mockups)

    assert valid == []
    assert drift == [("NoComp", "missing.tsx")]  # reports the (repo-relative) component path


def test_classify_drift_missing_mockup(tmp_path):
    repo = tmp_path
    mockups = repo / "mock"
    mockups.mkdir()
    (repo / "comp.tsx").write_text("x", encoding="utf-8")  # component exists, mockup does not

    drift, valid = classify_rows([_row("NoMock", "comp.tsx", "gone.html")], repo_root=repo, mockups_dir=mockups)

    assert valid == []
    assert len(drift) == 1
    comp, path = drift[0]
    assert comp == "NoMock"
    assert path == "mock/gone.html"  # repo-relative, forward-slash


def test_classify_jsx_fallback_for_missing_html(tmp_path):
    repo = tmp_path
    mockups = repo / "mock"
    mockups.mkdir()
    (repo / "comp.tsx").write_text("x", encoding="utf-8")
    (mockups / "m.jsx").write_text("x", encoding="utf-8")  # .html missing, sibling .jsx present

    drift, valid = classify_rows([_row("JsxFallback", "comp.tsx", "m.html")], repo_root=repo, mockups_dir=mockups)

    assert drift == []
    assert len(valid) == 1
    assert valid[0][2].name == "m.jsx"  # resolved to the .jsx sibling


def test_classify_mixed_batch(tmp_path):
    repo = tmp_path
    mockups = repo / "mock"
    mockups.mkdir()
    (repo / "ok.tsx").write_text("x", encoding="utf-8")
    (mockups / "ok.html").write_text("x", encoding="utf-8")

    rows = [
        _row("Ok", "ok.tsx", "ok.html"),
        _row("BadComp", "nope.tsx", "ok.html"),
        _row("BadMock", "ok.tsx", "nope.html"),
    ]
    drift, valid = classify_rows(rows, repo_root=repo, mockups_dir=mockups)

    assert len(valid) == 1
    assert len(drift) == 2
    assert {c for c, _ in drift} == {"BadComp", "BadMock"}
