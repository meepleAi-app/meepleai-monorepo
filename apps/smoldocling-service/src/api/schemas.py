"""API request/response schemas (DTOs) for SmolDocling service"""
from typing import List, Dict, Any, Optional
from pydantic import BaseModel, Field
from datetime import datetime


# Response Schemas
class PageResultSchema(BaseModel):
    """Single page extraction result"""

    page_number: int = Field(ge=1)
    markdown_text: str
    char_count: int = Field(ge=0)
    has_tables: bool
    has_equations: bool
    confidence_score: float = Field(ge=0.0, le=1.0)


class TextChunkSchema(BaseModel):
    """Text chunk in response"""

    text: str
    page_number: int = Field(ge=1)
    element_type: Optional[str] = None
    metadata: Dict[str, Any] = Field(default_factory=dict)


class QualityBreakdownSchema(BaseModel):
    """Quality score breakdown"""

    text_coverage_score: float
    layout_detection_score: float
    confidence_score: float
    page_coverage_score: float


class PdfExtractionResponse(BaseModel):
    """Successful PDF extraction response"""

    text: str = Field(description="Full extracted text (plain)")
    markdown: str = Field(description="Full extracted text (Markdown with structure)")
    chunks: List[TextChunkSchema] = Field(description="Text chunks (page-based)")
    quality_score: float = Field(description="Overall quality (0.0-1.0)", ge=0.0, le=1.0)
    page_count: int = Field(description="Number of pages", ge=1)
    metadata: Dict[str, Any] = Field(description="Extraction metadata")


# Error Schemas (reuse from Unstructured for consistency)
class ErrorDetail(BaseModel):
    """Error detail information"""

    code: str
    message: str
    details: Optional[Dict[str, Any]] = None
    timestamp: str  # ISO 8601 format
    request_id: Optional[str] = None


class ErrorResponse(BaseModel):
    """Error response"""

    error: ErrorDetail


# Health Check Schema
class HealthCheckResponse(BaseModel):
    """Health check response"""

    status: str  # "healthy" or "unhealthy"
    timestamp: str  # ISO 8601 format
    checks: Dict[str, str]
    gpu_info: Optional[Dict[str, Any]] = None
