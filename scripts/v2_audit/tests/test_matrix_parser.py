from scripts.v2_audit.matrix_parser import parse_matrix, MatrixRow


def test_parse_only_done_rows(sample_matrix_row_path):
    rows = list(parse_matrix(sample_matrix_row_path, status_filter="done"))
    assert len(rows) == 3  # 3 done in fixture, 1 pending excluded
    assert all(r.status == "done" for r in rows)


def test_row_fields(sample_matrix_row_path):
    rows = list(parse_matrix(sample_matrix_row_path, status_filter="done"))
    hero = next(r for r in rows if r.component == "GameDetailHero")
    assert hero.mockup == "sp4-game-detail.jsx"
    assert hero.path == "apps/web/src/components/features/game-detail/GameDetailHero.tsx"
    assert hero.route == "/games/[id]"
    assert hero.pr == "#702"


def test_no_filter_returns_all_rows(sample_matrix_row_path):
    rows = list(parse_matrix(sample_matrix_row_path, status_filter=None))
    assert len(rows) == 4
