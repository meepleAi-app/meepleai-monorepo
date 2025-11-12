"""Domain models for SmolDocling PDF extraction"""
from dataclasses import dataclass
from pathlib import Path
from typing import List, Dict, Any, Optional
from PIL import Image


@dataclass
class PageImage:
    """Represents a single page converted to image"""

    page_number: int
    image: Image.Image
    width: int
    height: int
    dpi: int

    @classmethod
    def from_pil_image(cls, page_number: int, image: Image.Image, dpi: int = 300):
        """Create from PIL Image"""
        return cls(
            page_number=page_number,
            image=image,
            width=image.width,
            height=image.height,
            dpi=dpi,
        )


@dataclass
class PageExtractionResult:
    """Result of processing a single page with SmolDocling"""

    page_number: int
    doctags_text: str  # Raw DocTags markup
    markdown_text: str  # Converted to Markdown
    char_count: int
    has_tables: bool
    has_equations: bool
    confidence_score: float  # VLM inference confidence (0-1)

    @property
    def is_empty(self) -> bool:
        """Check if page extraction is empty"""
        return len(self.doctags_text.strip()) == 0


@dataclass
class QualityScore:
    """Quality assessment for SmolDocling extraction"""

    total_score: float
    text_coverage_score: float
    layout_detection_score: float
    confidence_score: float
    page_coverage_score: float

    def meets_threshold(self, threshold: float) -> bool:
        """Check if quality meets minimum threshold"""
        return self.total_score >= threshold

    def to_dict(self) -> Dict[str, float]:
        """Convert to dictionary for API response"""
        return {
            "total_score": round(self.total_score, 2),
            "text_coverage_score": round(self.text_coverage_score, 2),
            "layout_detection_score": round(self.layout_detection_score, 2),
            "confidence_score": round(self.confidence_score, 2),
            "page_coverage_score": round(self.page_coverage_score, 2),
        }


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
class ExtractionResult:
    """Complete result of SmolDocling PDF extraction"""

    full_text: str
    markdown_text: str  # Full document in Markdown
    chunks: List[TextChunk]
    page_results: List[PageExtractionResult]
    page_count: int
    extraction_duration_ms: int
    quality_score: QualityScore

    @property
    def chunk_count(self) -> int:
        """Number of text chunks created"""
        return len(self.chunks)

    @property
    def has_tables(self) -> bool:
        """Check if any page contains tables"""
        return any(page.has_tables for page in self.page_results)

    @property
    def has_equations(self) -> bool:
        """Check if any page contains equations"""
        return any(page.has_equations for page in self.page_results)


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

        if self.file_size > 104857600:  # 100MB hard limit
            raise ValueError(f"PDF too large: {self.file_size} bytes")
