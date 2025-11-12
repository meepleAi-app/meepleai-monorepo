"""Quality score calculation for SmolDocling extraction"""
import logging
from typing import List

from ..domain.models import QualityScore, PageExtractionResult
from ..config import settings

logger = logging.getLogger(__name__)


class QualityScoreCalculator:
    """Calculates quality scores for SmolDocling VLM extraction results"""

    def __init__(self):
        self.settings = settings

    def calculate(
        self,
        full_text: str,
        page_results: List[PageExtractionResult],
        expected_page_count: int,
    ) -> QualityScore:
        """
        Calculate comprehensive quality score (0.0-1.0)

        Scoring breakdown for Stage 2 (VLM-based):
        - Text coverage: 40% (sufficient text extracted)
        - Layout detection: 30% (tables, equations, structure from VLM)
        - Confidence: 20% (VLM inference confidence)
        - Page coverage: 10% (all pages processed)

        Args:
            full_text: Complete extracted text
            page_results: List of per-page results
            expected_page_count: Expected number of pages

        Returns:
            QualityScore object with breakdown
        """
        # Calculate individual scores
        text_coverage = self._calculate_text_coverage(full_text, expected_page_count)
        layout_detection = self._calculate_layout_detection(page_results)
        confidence = self._calculate_average_confidence(page_results)
        page_coverage = self._calculate_page_coverage(page_results, expected_page_count)

        # Calculate total score (weighted sum)
        total = (
            text_coverage * 0.4  # 40%
            + layout_detection * 0.3  # 30% (increased from Unstructured due to VLM focus)
            + confidence * 0.2  # 20% (new - VLM confidence)
            + page_coverage * 0.1  # 10% (reduced - VLM processes all pages)
        )

        quality_score = QualityScore(
            total_score=round(total, 2),
            text_coverage_score=round(text_coverage, 2),
            layout_detection_score=round(layout_detection, 2),
            confidence_score=round(confidence, 2),
            page_coverage_score=round(page_coverage, 2),
        )

        logger.info(
            f"Quality score calculated: {quality_score.total_score:.2f} "
            f"(text={text_coverage:.2f}, layout={layout_detection:.2f}, "
            f"confidence={confidence:.2f}, pages={page_coverage:.2f})"
        )

        return quality_score

    def _calculate_text_coverage(self, text: str, page_count: int) -> float:
        """Calculate text coverage score (0.0-1.0)"""
        if page_count == 0:
            return 0.0

        chars_per_page = len(text) / page_count
        min_chars = self.settings.min_chars_per_page  # 300 for VLM
        ideal_chars = min_chars * 3  # 900 chars/page ideal

        if chars_per_page < min_chars:
            # Poor extraction
            score = chars_per_page / min_chars * 0.5
        elif chars_per_page >= ideal_chars:
            # Excellent extraction
            score = 1.0
        else:
            # Linear interpolation
            score = 0.5 + (chars_per_page - min_chars) / (ideal_chars - min_chars) * 0.5

        return min(score, 1.0)

    def _calculate_layout_detection(self, page_results: List[PageExtractionResult]) -> float:
        """
        Calculate layout detection score (0.0-1.0)

        VLM-specific scoring based on detected elements:
        - Tables detected: +0.4
        - Equations detected: +0.3
        - Structure (sections, paragraphs): +0.3
        """
        if not page_results:
            return 0.0

        score = 0.0

        # Tables detected
        pages_with_tables = sum(1 for page in page_results if page.has_tables)
        if pages_with_tables > 0:
            score += 0.4

        # Equations detected
        pages_with_equations = sum(1 for page in page_results if page.has_equations)
        if pages_with_equations > 0:
            score += 0.3

        # Structure detected (check for DocTags markup)
        pages_with_structure = sum(
            1
            for page in page_results
            if "</section>" in page.doctags_text or "</paragraph>" in page.doctags_text
        )
        if pages_with_structure > 0:
            score += 0.3

        return min(score, 1.0)

    def _calculate_average_confidence(
        self, page_results: List[PageExtractionResult]
    ) -> float:
        """Calculate average confidence score across all pages"""
        if not page_results:
            return 0.0

        # Filter out empty pages (confidence 0.0)
        non_empty_pages = [p for p in page_results if not p.is_empty]

        if not non_empty_pages:
            return 0.0

        avg_confidence = sum(p.confidence_score for p in non_empty_pages) / len(
            non_empty_pages
        )
        return round(avg_confidence, 2)

    def _calculate_page_coverage(
        self, page_results: List[PageExtractionResult], expected_page_count: int
    ) -> float:
        """Calculate page coverage score (0.0-1.0)"""
        if expected_page_count == 0:
            return 0.0

        # Count non-empty pages
        successful_pages = sum(1 for page in page_results if not page.is_empty)

        coverage = successful_pages / expected_page_count
        return round(coverage, 2)
