"""API request/response schemas (DTOs)"""
from typing import List, Dict, Any, Optional, Literal
from pydantic import BaseModel, Field, ConfigDict
from datetime import datetime


# Request Schemas
class PdfExtractionRequest(BaseModel):
    """Request for PDF extraction (multipart form data)"""

    strategy: Literal["fast", "hi_res"] = Field(
        default="fast", description="Unstructured extraction strategy"
    )
    language: str = Field(default="ita", description="Document language (ISO 639-3)")


# Response Schemas
class TextChunkSchema(BaseModel):
    """Text chunk in response"""

    text: str = Field(description="Chunk text content")
    page_number: int = Field(description="Page number (1-indexed)", ge=1)
    element_type: Optional[str] = Field(
        default=None, description="Element type (Title, Paragraph, etc.)"
    )
    metadata: Dict[str, Any] = Field(default_factory=dict, description="Additional metadata")


class BBoxSchema(BaseModel):
    """Normalized bounding box of an element (SP-B #3406, epic #3403).

    Fractions in [0,1] of the page layout, top-left origin (y grows downward), so
    the value is independent of strategy (fast vs hi_res), DPI, and viewer zoom.
    """

    x: float = Field(description="Left edge, normalized 0..1")
    y: float = Field(description="Top edge, normalized 0..1 (top-left origin)")
    width: float = Field(description="Width, normalized 0..1")
    height: float = Field(description="Height, normalized 0..1")


class ElementSchema(BaseModel):
    """Raw partition element (pre-chunking), preserving its structural category."""

    text: str = Field(description="Element text content")
    page_number: int = Field(description="Page number (1-indexed)", ge=1)
    category: Optional[str] = Field(
        default=None, description="Raw Unstructured category (Title, NarrativeText, Table, …)"
    )
    bbox: Optional[BBoxSchema] = Field(
        default=None,
        description="Normalized bounding box [0,1] top-left; None if the extractor emitted no coordinates",
    )


class QualityBreakdownSchema(BaseModel):
    """Quality score breakdown"""

    text_coverage_score: float = Field(description="Text coverage score (0-0.4)")
    structure_detection_score: float = Field(description="Structure detection score (0-0.2)")
    table_detection_score: float = Field(description="Table detection score (0-0.2)")
    page_coverage_score: float = Field(description="Page coverage score (0-0.2)")


class PdfExtractionResponse(BaseModel):
    """Successful PDF extraction response"""

    text: str = Field(description="Full extracted text")
    chunks: List[TextChunkSchema] = Field(description="Semantic text chunks")
    elements: List[ElementSchema] = Field(
        default_factory=list, description="Raw partition elements with structural category"
    )
    quality_score: float = Field(description="Overall quality score (0.0-1.0)", ge=0.0, le=1.0)
    page_count: int = Field(description="Number of pages", ge=1)
    metadata: Dict[str, Any] = Field(description="Extraction metadata")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "text": "Terraforming Mars is a board game...",
                "chunks": [
                    {
                        "text": "Setup instructions...",
                        "page_number": 1,
                        "element_type": "Title",
                        "metadata": {"char_count": 150},
                    }
                ],
                "quality_score": 0.85,
                "page_count": 20,
                "metadata": {
                    "extraction_duration_ms": 1250,
                    "strategy_used": "fast",
                    "language": "ita",
                    "detected_tables": 3,
                    "detected_structures": ["Title", "Paragraph", "Table"],
                    "quality_breakdown": {
                        "text_coverage_score": 0.40,
                        "structure_detection_score": 0.18,
                        "table_detection_score": 0.15,
                        "page_coverage_score": 0.20,
                    },
                },
            }
        }
    )


# Error Schemas
class ErrorDetail(BaseModel):
    """Error detail information"""

    code: str = Field(description="Error code")
    message: str = Field(description="Error message")
    details: Optional[Dict[str, Any]] = Field(default=None, description="Additional error details")
    timestamp: str = Field(description="Error timestamp (ISO 8601)")
    request_id: Optional[str] = Field(default=None, description="Request ID for tracing")


class ErrorResponse(BaseModel):
    """Error response"""

    error: ErrorDetail

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "error": {
                    "code": "EXTRACTION_FAILED",
                    "message": "Failed to extract text from PDF",
                    "details": {"reason": "Corrupted PDF structure"},
                    "timestamp": "2025-01-15T10:30:00Z",
                    "request_id": "550e8400-e29b-41d4-a716-446655440000",
                }
            }
        }
    )


# Health Check Schema
class HealthCheckResponse(BaseModel):
    """Health check response"""

    status: Literal["healthy", "unhealthy"] = Field(description="Service health status")
    timestamp: str = Field(description="Check timestamp (ISO 8601)")
    checks: Dict[str, str] = Field(description="Individual health checks")

    model_config = ConfigDict(
        json_schema_extra={
            "example": {
                "status": "healthy",
                "timestamp": "2025-01-15T10:30:00Z",
                "checks": {
                    "unstructured_library": "ok",
                    "disk_space": "ok",
                    "memory": "ok",
                },
            }
        }
    )
