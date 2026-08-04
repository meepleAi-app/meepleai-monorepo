"""
Tests for QualityScoreCalculator — layout detection must ignore empty pages (issue #3435).

Regression guard for the code-review finding: _calculate_layout_detection counted
has_tables/has_equations/structure over ALL pages, unlike _calculate_average_confidence
and _calculate_page_coverage which filter is_empty. After the DocTags decode fix
(is_empty keyed on markdown_text + _convert_to_markdown returning "" on failure), a page
whose conversion failed is dropped from the corpus but its has_tables/doctags_text would
still inflate the layout score. Layout detection now filters is_empty too.
"""
from src.application.quality_calculator import QualityScoreCalculator
from src.domain.models import PageExtractionResult, STRUCTURE_TAGS


def _page(markdown, doctags, has_tables=False, has_equations=False):
    return PageExtractionResult(
        page_number=1,
        doctags_text=doctags,
        markdown_text=markdown,
        char_count=len(markdown),
        has_tables=has_tables,
        has_equations=has_equations,
        confidence_score=0.8,
    )


def test_layout_detection_ignores_empty_pages():
    calc = QualityScoreCalculator()
    # markdown conversion failed → markdown_text "" → is_empty True, but has_tables=True
    # from doctags_text (metadata computed before conversion). Must NOT inflate the score.
    dropped = _page(markdown="", doctags="<doctag><otsl><fcel>x<nl></otsl></doctag>", has_tables=True)
    assert dropped.is_empty is True
    assert calc._calculate_layout_detection([dropped]) == 0.0


def test_layout_detection_counts_non_empty_table_page():
    calc = QualityScoreCalculator()
    real = _page(markdown="| a | b |", doctags="<doctag><otsl><fcel>a<nl></otsl></doctag>", has_tables=True)
    assert real.is_empty is False
    assert calc._calculate_layout_detection([real]) >= 0.4  # table bonus applies


def test_layout_detection_structure_bonus_uses_shared_tags():
    calc = QualityScoreCalculator()
    # a non-empty page with a real structure tag gets the structure bonus
    page = _page(markdown="Regole", doctags="<doctag><text>Regole</text></doctag>")
    assert "<text>" in STRUCTURE_TAGS
    assert "<list_item>" not in STRUCTURE_TAGS  # dropped: docling does not parse it (#3435)
    assert calc._calculate_layout_detection([page]) >= 0.3
