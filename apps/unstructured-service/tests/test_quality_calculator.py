"""Unit tests for QualityScoreCalculator"""
import pytest
from src.application.quality_calculator import QualityScoreCalculator
from src.domain.models import QualityScore


class TestQualityScoreCalculator:
    """Test quality score calculation logic"""

    def setup_method(self):
        """Setup test instance"""
        self.calculator = QualityScoreCalculator()

    def test_perfect_quality_score(self, mock_unstructured_elements):
        """Test perfect quality score (1.0)"""
        # Arrange
        full_text = "A" * 10000  # Lots of text (2000 chars/page for 5 pages)
        elements = mock_unstructured_elements * 10  # Many elements
        page_count = 5
        tables = mock_unstructured_elements[2:3] * 5  # 5 tables
        structures = ["Title", "Header", "Paragraph", "ListItem", "Table"]

        # Act
        score = self.calculator.calculate(full_text, elements, page_count, tables, structures)

        # Assert
        assert isinstance(score, QualityScore)
        assert score.total_score >= 0.90  # Very high quality
        assert score.text_coverage_score > 0.35  # Good text coverage
        assert score.structure_detection_score > 0.18  # Good structures
        assert score.table_detection_score >= 0.20  # Tables detected
        assert score.meets_threshold(0.80)

    def test_poor_quality_score(self):
        """Test poor quality score (< 0.40)"""
        # Arrange
        full_text = "A" * 50  # Very little text
        elements = []
        page_count = 1
        tables = []
        structures = []

        # Act
        score = self.calculator.calculate(full_text, elements, page_count, tables, structures)

        # Assert
        assert score.total_score < 0.40
        assert score.total_score >= 0.0
        assert not score.meets_threshold(0.80)

    def test_medium_quality_score(self):
        """Test medium quality score (0.60-0.80)"""
        # Arrange
        full_text = "A" * 2000  # Decent amount of text
        elements = [mock_element(category="Paragraph") for _ in range(10)]
        page_count = 2
        tables = [mock_element(category="Table")]
        structures = ["Paragraph", "Table"]

        # Act
        score = self.calculator.calculate(full_text, elements, page_count, tables, structures)

        # Assert
        assert 0.60 <= score.total_score < 0.80
        assert not score.meets_threshold(0.80)

    def test_text_coverage_calculation(self):
        """Test text coverage score component"""
        # Arrange
        text_abundant = "A" * 5000  # 1000 chars/page for 5 pages
        text_minimal = "A" * 250  # 50 chars/page for 5 pages

        # Act
        score_abundant = self.calculator._calculate_text_coverage(text_abundant, 5)
        score_minimal = self.calculator._calculate_text_coverage(text_minimal, 5)

        # Assert
        assert score_abundant > 0.5  # Good coverage
        assert score_minimal < 0.5  # Poor coverage
        assert 0.0 <= score_abundant <= 1.0
        assert 0.0 <= score_minimal <= 1.0

    def test_structure_detection_scoring(self):
        """Test structure detection score component"""
        # Arrange
        structures_rich = ["Title", "Header", "Paragraph", "ListItem"]
        structures_poor = ["Paragraph"]
        structures_empty = []

        # Act
        score_rich = self.calculator._calculate_structure_detection(structures_rich)
        score_poor = self.calculator._calculate_structure_detection(structures_poor)
        score_empty = self.calculator._calculate_structure_detection(structures_empty)

        # Assert
        assert score_rich >= 0.8  # All key structures
        assert 0.0 < score_poor < 0.5  # Only basic
        assert score_empty == 0.0  # No structures

    def test_table_detection_scoring(self):
        """Test table detection score component"""
        # Arrange
        many_tables = [mock_element(category="Table") for _ in range(5)]
        few_tables = [mock_element(category="Table")]
        no_tables = []

        # Act
        score_many = self.calculator._calculate_table_detection(many_tables, 10)
        score_few = self.calculator._calculate_table_detection(few_tables, 10)
        score_none = self.calculator._calculate_table_detection(no_tables, 10)

        # Assert
        assert score_many >= 0.8  # Excellent table detection
        assert 0.3 < score_few < 0.8  # Some tables
        assert score_none == 0.3  # No tables (neutral score)

    def test_page_coverage_calculation(self):
        """Test page coverage score component"""
        # Arrange
        elements_full = [
            mock_element(page=1),
            mock_element(page=2),
            mock_element(page=3),
        ]
        elements_partial = [
            mock_element(page=1),
            mock_element(page=3),  # Page 2 missing
        ]

        # Act
        score_full = self.calculator._calculate_page_coverage(elements_full, 3)
        score_partial = self.calculator._calculate_page_coverage(elements_partial, 3)

        # Assert
        assert score_full == 1.0  # All pages
        assert score_partial < 1.0  # Partial coverage
        assert score_partial >= 0.6  # At least 2/3 pages

    def test_quality_score_weights(self, mock_unstructured_elements):
        """Test that quality score weights sum correctly"""
        # Arrange
        full_text = "A" * 2000
        elements = mock_unstructured_elements
        page_count = 2
        tables = [mock_unstructured_elements[2]]
        structures = ["Title", "Paragraph", "Table"]

        # Act
        score = self.calculator.calculate(full_text, elements, page_count, tables, structures)

        # Assert - individual scores should sum to total (within rounding)
        calculated_total = (
            score.text_coverage_score
            + score.structure_detection_score
            + score.table_detection_score
            + score.page_coverage_score
        )
        assert abs(score.total_score - calculated_total) < 0.01  # Allow small rounding error

    def test_meets_threshold_method(self):
        """Test meets_threshold method"""
        # Arrange
        score_high = QualityScore(
            total_score=0.85,
            text_coverage_score=0.40,
            structure_detection_score=0.20,
            table_detection_score=0.15,
            page_coverage_score=0.10,
        )
        score_low = QualityScore(
            total_score=0.65,
            text_coverage_score=0.30,
            structure_detection_score=0.15,
            table_detection_score=0.10,
            page_coverage_score=0.10,
        )

        # Act & Assert
        assert score_high.meets_threshold(0.80)
        assert not score_low.meets_threshold(0.80)
        assert score_low.meets_threshold(0.60)

    def test_zero_pages_handling(self):
        """Test edge case with zero pages"""
        # Arrange
        full_text = ""
        elements = []
        page_count = 0
        tables = []
        structures = []

        # Act
        score = self.calculator.calculate(full_text, elements, page_count, tables, structures)

        # Assert
        assert score.total_score == 0.0
        assert score.text_coverage_score == 0.0
        assert score.page_coverage_score == 0.0


# Helper functions
def mock_element(category="Paragraph", page=1):
    """Create mock Unstructured element"""
    from dataclasses import dataclass
    from typing import Optional

    @dataclass
    class MockMetadata:
        page_number: int
        filename: Optional[str] = None

    @dataclass
    class MockElement:
        text: str
        category: str
        metadata: MockMetadata

    return MockElement(text=f"Mock {category} content", category=category, metadata=MockMetadata(page_number=page))
