"""Quality score calculation for PDF extraction"""
import logging
from typing import List, Any

from ..domain.models import QualityScore
from ..config import settings

logger = logging.getLogger(__name__)


class QualityScoreCalculator:
    """Calculates quality scores for PDF extraction results"""

    def __init__(self):
        self.settings = settings

    def calculate(
        self,
        full_text: str,
        elements: List[Any],
        page_count: int,
        tables: List[Any],
        detected_structures: List[str],
    ) -> QualityScore:
        """
        Calculate comprehensive quality score (0.0-1.0)

        Scoring breakdown:
        - Text coverage: 40% (sufficient text extracted)
        - Structure detection: 20% (titles, headers, lists detected)
        - Table detection: 20% (tables identified)
        - Page coverage: 20% (all pages processed)

        Args:
            full_text: Complete extracted text
            elements: List of Unstructured elements
            page_count: Number of pages in PDF
            tables: List of detected tables
            detected_structures: List of structure types found

        Returns:
            QualityScore object with breakdown
        """
        # Calculate individual scores
        text_coverage = self._calculate_text_coverage(full_text, page_count)
        structure_detection = self._calculate_structure_detection(detected_structures)
        table_detection = self._calculate_table_detection(tables, page_count)
        page_coverage = self._calculate_page_coverage(elements, page_count)

        # Calculate total score (weighted sum)
        total = (
            text_coverage * 0.4  # 40%
            + structure_detection * 0.2  # 20%
            + table_detection * 0.2  # 20%
            + page_coverage * 0.2  # 20%
        )

        quality_score = QualityScore(
            total_score=round(total, 2),
            text_coverage_score=round(text_coverage, 2),
            structure_detection_score=round(structure_detection, 2),
            table_detection_score=round(table_detection, 2),
            page_coverage_score=round(page_coverage, 2),
        )

        logger.info(
            f"Quality score calculated: {quality_score.total_score:.2f} "
            f"(text={text_coverage:.2f}, struct={structure_detection:.2f}, "
            f"table={table_detection:.2f}, pages={page_coverage:.2f})"
        )

        return quality_score

    def _calculate_text_coverage(self, text: str, page_count: int) -> float:
        """
        Calculate text coverage score (0.0-1.0)

        Score based on characters per page:
        - < min_chars_per_page: 0.0 (poor)
        - min_chars_per_page to 2x: linear scale
        - >= 2x min_chars_per_page: 1.0 (excellent)
        """
        if page_count == 0:
            return 0.0

        chars_per_page = len(text) / page_count
        min_chars = self.settings.min_chars_per_page
        ideal_chars = min_chars * 2

        if chars_per_page < min_chars:
            # Poor extraction
            score = chars_per_page / min_chars * 0.5  # Max 0.5 if below threshold
        elif chars_per_page >= ideal_chars:
            # Excellent extraction
            score = 1.0
        else:
            # Linear interpolation between min and ideal
            score = 0.5 + (chars_per_page - min_chars) / (ideal_chars - min_chars) * 0.5

        return min(score, 1.0)

    def _calculate_structure_detection(self, detected_structures: List[str]) -> float:
        """
        Calculate structure detection score (0.0-1.0)

        Score based on key structure types detected:
        - Title, Header: +0.3 each
        - NarrativeText, Paragraph: +0.2
        - ListItem: +0.2
        """
        score = 0.0
        structure_set = set(detected_structures)

        # Key structures with weights
        if "Title" in structure_set:
            score += 0.3
        if "Header" in structure_set:
            score += 0.3
        if "NarrativeText" in structure_set or "Paragraph" in structure_set:
            score += 0.2
        if "ListItem" in structure_set:
            score += 0.2

        return min(score, 1.0)

    def _calculate_table_detection(self, tables: List[Any], page_count: int) -> float:
        """
        Calculate table detection score (0.0-1.0)

        Score based on tables per page:
        - 0 tables: 0.3 (document may not have tables)
        - 1-3 tables: linear scale to 0.8
        - 4+ tables: 1.0 (excellent table detection)
        """
        table_count = len(tables)

        if table_count == 0:
            # No tables detected - may be legitimate if document has no tables
            return 0.3  # Neutral score
        elif table_count <= 3:
            # Linear interpolation 1-3 tables -> 0.5-0.8
            return 0.5 + (table_count / 3) * 0.3
        else:
            # Many tables detected - excellent
            return 1.0

    def _calculate_page_coverage(self, elements: List[Any], page_count: int) -> float:
        """
        Calculate page coverage score (0.0-1.0)

        Score based on pages with extracted content:
        - All pages processed: 1.0
        - Partial coverage: proportional
        """
        if page_count == 0:
            return 0.0

        # Get unique page numbers from elements
        pages_with_content = set()
        for element in elements:
            if hasattr(element, "metadata") and hasattr(element.metadata, "page_number"):
                pages_with_content.add(element.metadata.page_number)

        coverage_ratio = len(pages_with_content) / page_count
        return coverage_ratio
