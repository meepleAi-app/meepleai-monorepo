"""Domain models for PDF extraction"""
from dataclasses import dataclass
from pathlib import Path
from typing import List, Dict, Any, Optional


@dataclass
class TextChunk:
    """Represents a chunk of extracted text"""

    text: str
    page_number: int
    element_type: Optional[str] = None
    metadata: Optional[Dict[str, Any]] = None

    def __post_init__(self):
        if self.metadata is None:
            self.metadata = {}


@dataclass
class QualityScore:
    """Quality assessment for PDF extraction"""

    total_score: float
    text_coverage_score: float
    structure_detection_score: float
    table_detection_score: float
    page_coverage_score: float

    def meets_threshold(self, threshold: float) -> bool:
        """Check if quality meets minimum threshold"""
        return self.total_score >= threshold

    def to_dict(self) -> Dict[str, float]:
        """Convert to dictionary for API response"""
        return {
            "total_score": self.total_score,
            "text_coverage_score": self.text_coverage_score,
            "structure_detection_score": self.structure_detection_score,
            "table_detection_score": self.table_detection_score,
            "page_coverage_score": self.page_coverage_score,
        }


@dataclass
class ExtractionResult:
    """Result of PDF text extraction"""

    full_text: str
    chunks: List[TextChunk]
    page_count: int
    elements: List[Any]  # Unstructured Element objects
    tables: List[Any]
    detected_structures: List[str]
    extraction_duration_ms: int
    quality_score: QualityScore

    @property
    def chunk_count(self) -> int:
        """Number of text chunks created"""
        return len(self.chunks)

    @property
    def table_count(self) -> int:
        """Number of tables detected"""
        return len(self.tables)


@dataclass
class PdfDocument:
    """Represents a PDF document to be processed"""

    file_path: Path
    file_size: int
    language: str = "ita"

    def validate(self) -> None:
        """Validate PDF document"""
        if not self.file_path.exists():
            raise FileNotFoundError(f"PDF file not found: {self.file_path}")

        if not self.file_path.is_file():
            raise ValueError(f"Path is not a file: {self.file_path}")

        if self.file_path.suffix.lower() != ".pdf":
            raise ValueError(f"File is not a PDF: {self.file_path}")

        if self.file_size <= 0:
            raise ValueError(f"Invalid file size: {self.file_size}")
