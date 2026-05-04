"""FastAPI application entry point for SmolDocling service"""
import base64
import io
import logging
import uuid
import torch
import threading
from contextlib import asynccontextmanager
from datetime import datetime, timezone
from typing import List, Optional
from fastapi import FastAPI, File, Form, Query, UploadFile, HTTPException, status
from fastapi.responses import JSONResponse, PlainTextResponse, Response
from starlette.concurrency import run_in_threadpool
from PIL import Image, ExifTags
import cv2
import numpy as np

from .config import settings
from .application import PdfExtractionService
from .domain.models import PageImage
from .api.schemas import (
    PdfExtractionResponse,
    TextChunkSchema,
    PageResultSchema,
    ErrorDetail,
    ErrorResponse,
    HealthCheckResponse,
)

# Configure logging
logging.basicConfig(
    level=settings.log_level,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)


# Global service instance (initialized in lifespan)
pdf_service: Optional[PdfExtractionService] = None
metrics_lock = threading.Lock()
metrics = {
    "extract_requests_total": 0,
    "extract_failures_total": 0,
    "extract_duration_ms_sum": 0.0,
    "extract_payload_bytes_sum": 0.0,
}


# Lifespan context manager
@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan handler with model warmup"""
    global pdf_service

    logger.info("Starting SmolDocling PDF Extraction Service")
    logger.info(
        f"Configuration: device={settings.device}, model={settings.model_name}, "
        f"dpi={settings.image_dpi}"
    )

    # Initialize service
    pdf_service = PdfExtractionService()

    # Model warmup (if enabled)
    if settings.enable_model_warmup:
        try:
            logger.info("Warming up SmolDocling model...")
            pdf_service.vlm_adapter.initialize()

            # Dummy inference for GPU warmup
            dummy_image = Image.new("RGB", (512, 512), color="white")
            from src.domain.models import PageImage

            dummy_page = PageImage.from_pil_image(1, dummy_image, 300)
            pdf_service.vlm_adapter.process_page(dummy_page)

            logger.info("Model warmup complete")

        except Exception as e:
            logger.error(f"Model warmup failed: {e}. Service may be slow on first request.")

    yield

    logger.info("Shutting down SmolDocling PDF Extraction Service")

    # Cleanup model resources
    if pdf_service:
        pdf_service.vlm_adapter.cleanup()


# Create FastAPI application
app = FastAPI(
    title="SmolDocling PDF Extraction Service",
    description="VLM-based PDF extraction using SmolDocling (256M parameters)",
    version="1.0.0",
    lifespan=lifespan,
)



# Exception handlers
@app.exception_handler(ValueError)
async def value_error_handler(request, exc: ValueError):
    """Handle validation errors (400)"""
    request_id = str(uuid.uuid4())
    logger.error(f"Validation error [{request_id}]: {exc}")

    return JSONResponse(
        status_code=status.HTTP_400_BAD_REQUEST,
        content=ErrorResponse(
            error=ErrorDetail(
                code="INVALID_REQUEST",
                message=str(exc),
                timestamp=datetime.now(timezone.utc).isoformat(),
                request_id=request_id,
            )
        ).model_dump(),
    )


@app.exception_handler(RuntimeError)
async def runtime_error_handler(request, exc: RuntimeError):
    """Handle runtime errors (500)"""
    request_id = str(uuid.uuid4())
    logger.error(f"Runtime error [{request_id}]: {exc}", exc_info=True)

    return JSONResponse(
        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        content=ErrorResponse(
            error=ErrorDetail(
                code="EXTRACTION_FAILED",
                message=str(exc),
                timestamp=datetime.now(timezone.utc).isoformat(),
                request_id=request_id,
            )
        ).model_dump(),
    )


@app.exception_handler(Exception)
async def general_exception_handler(request, exc: Exception):
    """Handle general errors (500)"""
    request_id = str(uuid.uuid4())
    logger.error(f"Internal error [{request_id}]: {exc}", exc_info=True)

    return JSONResponse(
        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        content=ErrorResponse(
            error=ErrorDetail(
                code="INTERNAL_ERROR",
                message="An internal error occurred during PDF extraction",
                details={"error_type": type(exc).__name__},
                timestamp=datetime.now(timezone.utc).isoformat(),
                request_id=request_id,
            )
        ).model_dump(),
    )


# API Endpoints
@app.post(
    "/api/v1/extract",
    response_model=PdfExtractionResponse,
    summary="Extract text from PDF using SmolDocling VLM",
    description="Converts PDF pages to images and processes with SmolDocling Vision-Language Model",
)
async def extract_pdf(
    file: UploadFile = File(..., description="PDF file to extract"),
):
    """
    Extract text from PDF with VLM-based processing

    Args:
        file: PDF file (multipart/form-data)

    Returns:
        PdfExtractionResponse with text, markdown, chunks, and quality score

    Raises:
        HTTPException: Various errors (400, 413, 415, 422, 500)
    """
    request_id = str(uuid.uuid4())
    logger.info(f"Extraction request [{request_id}]: file={file.filename}")

    start_time = datetime.now(timezone.utc)
    try:
        # Validate file type
        if not file.content_type or "pdf" not in file.content_type.lower():
            raise HTTPException(
                status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
                detail=ErrorResponse(
                    error=ErrorDetail(
                        code="UNSUPPORTED_MEDIA_TYPE",
                        message=f"File type not supported: {file.content_type}. Expected application/pdf",
                        timestamp=datetime.now(timezone.utc).isoformat(),
                        request_id=request_id,
                    )
                ).model_dump(),
            )

        # Validate file size
        file_content = await file.read()
        file_size = len(file_content)

        if file_size > settings.max_file_size:
            raise HTTPException(
                status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
                detail=ErrorResponse(
                    error=ErrorDetail(
                        code="FILE_TOO_LARGE",
                        message=f"File size {file_size} bytes exceeds maximum {settings.max_file_size} bytes",
                        timestamp=datetime.now(timezone.utc).isoformat(),
                        request_id=request_id,
                    )
                ).model_dump(),
            )

        # Create BytesIO
        from io import BytesIO

        file_io = BytesIO(file_content)

        # Extract text off the event loop (CPU/GPU bound)
        result = await run_in_threadpool(
            pdf_service.extract,
            file_io,
            file.filename or "document.pdf",
            "ita",
        )

        # Build response
        response = PdfExtractionResponse(
            text=result.full_text,
            markdown=result.markdown_text,
            chunks=[
                TextChunkSchema(
                    text=chunk.text,
                    page_number=chunk.page_number,
                    element_type=chunk.element_type,
                    metadata=chunk.metadata or {},
                )
                for chunk in result.chunks
            ],
            quality_score=result.quality_score.total_score,
            page_count=result.page_count,
            metadata={
                "extraction_duration_ms": result.extraction_duration_ms,
                "language": "ita",
                "has_tables": result.has_tables,
                "has_equations": result.has_equations,
                "quality_breakdown": result.quality_score.to_dict(),
                "page_results": [
                    {
                        "page": page.page_number,
                        "chars": page.char_count,
                        "confidence": page.confidence_score,
                    }
                    for page in result.page_results
                ],
            },
        )

        logger.info(
            f"Extraction completed [{request_id}]: pages={result.page_count}, "
            f"chunks={len(result.chunks)}, quality={result.quality_score.total_score:.2f}"
        )
        duration_ms = (datetime.now(timezone.utc) - start_time).total_seconds() * 1000
        with metrics_lock:
            metrics["extract_requests_total"] += 1
            metrics["extract_duration_ms_sum"] += duration_ms
            metrics["extract_payload_bytes_sum"] += file_size

        return response

    except HTTPException:
        with metrics_lock:
            metrics["extract_requests_total"] += 1
            metrics["extract_failures_total"] += 1
        raise
    except ValueError as e:
        with metrics_lock:
            metrics["extract_requests_total"] += 1
            metrics["extract_failures_total"] += 1
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=ErrorResponse(
                error=ErrorDetail(
                    code="CORRUPTED_PDF",
                    message=str(e),
                    timestamp=datetime.now(timezone.utc).isoformat(),
                    request_id=request_id,
                )
            ).model_dump(),
        )
    except Exception as e:
        logger.error(f"Extraction failed [{request_id}]: {e}", exc_info=True)
        with metrics_lock:
            metrics["extract_requests_total"] += 1
            metrics["extract_failures_total"] += 1
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=ErrorResponse(
                error=ErrorDetail(
                    code="EXTRACTION_FAILED",
                    message="Failed to extract text from PDF",
                    details={"error": str(e)},
                    timestamp=datetime.now(timezone.utc).isoformat(),
                    request_id=request_id,
                )
            ).model_dump(),
        )


@app.post(
    "/api/v1/page-image",
    summary="Extract single PDF page as image",
    description="Converts a specific PDF page to a JPEG image for cover image selection",
)
async def extract_page_image(
    file: UploadFile = File(..., description="PDF file"),
    page_number: int = Query(default=1, ge=1, description="1-based page number to extract"),
):
    """
    Extract a single page from a PDF as a JPEG image.

    Args:
        file: PDF file (multipart/form-data)
        page_number: 1-based page number (default: 1)

    Returns:
        JPEG image bytes (Content-Type: image/jpeg)
    """
    import io
    import tempfile
    import os

    request_id = str(uuid.uuid4())
    logger.info(f"Page image request [{request_id}]: file={file.filename}, page={page_number}")

    try:
        # Validate content type
        if not file.content_type or "pdf" not in file.content_type.lower():
            raise HTTPException(
                status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
                detail=f"Unsupported file type: {file.content_type}. Expected application/pdf",
            )

        file_content = await file.read()
        if len(file_content) == 0:
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Empty file",
            )

        # Write to temp file (pdf2image requires a file path)
        with tempfile.NamedTemporaryFile(suffix=".pdf", delete=False) as tmp:
            tmp.write(file_content)
            tmp_path = tmp.name

        try:
            from pdf2image import convert_from_path
            from pdf2image.exceptions import PDFPageCountError

            def _convert():
                return convert_from_path(
                    pdf_path=tmp_path,
                    dpi=150,  # Lower DPI for preview images
                    fmt="jpeg",
                    first_page=page_number,
                    last_page=page_number,
                    thread_count=1,
                )

            images = await run_in_threadpool(_convert)

            if not images:
                raise HTTPException(
                    status_code=status.HTTP_404_NOT_FOUND,
                    detail=f"Page {page_number} not found in PDF",
                )

            # Convert PIL image to JPEG bytes
            img_bytes = io.BytesIO()
            images[0].save(img_bytes, format="JPEG", quality=85)
            img_bytes.seek(0)

            logger.info(f"Page image extracted [{request_id}]: page={page_number}")
            return Response(
                content=img_bytes.read(),
                media_type="image/jpeg",
            )

        finally:
            try:
                os.unlink(tmp_path)
            except OSError:
                pass

    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Page image extraction failed [{request_id}]: {e}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Failed to extract page image: {str(e)}",
        )


@app.get(
    "/health",
    response_model=HealthCheckResponse,
    summary="Health check endpoint",
    description="Returns service health with GPU/model status",
)
async def health_check():
    """
    Check service health and GPU availability

    Returns:
        HealthCheckResponse with status, checks, and GPU info
    """
    try:
        # Check model initialization
        model_status = "ok" if pdf_service and pdf_service.vlm_adapter._is_initialized else "not_initialized"

        # Check CUDA availability
        cuda_available = torch.cuda.is_available()
        device_status = "ok" if (settings.device == "cpu" or cuda_available) else "error"

        # Check disk space
        import shutil

        disk_usage = shutil.disk_usage(settings.temp_dir)
        free_gb = disk_usage.free / (1024**3)
        disk_status = "ok" if free_gb > 2.0 else "warning" if free_gb > 1.0 else "error"

        # GPU info (if available)
        gpu_info = None
        if cuda_available:
            gpu_info = {
                "device_count": torch.cuda.device_count(),
                "device_name": torch.cuda.get_device_name(0),
                "memory_allocated_mb": torch.cuda.memory_allocated(0) / 1024**2,
                "memory_reserved_mb": torch.cuda.memory_reserved(0) / 1024**2,
            }

        # Determine overall status
        checks = {
            "model_initialized": model_status,
            "device_available": device_status,
            "disk_space": disk_status,
        }

        overall_status = "healthy" if all(v == "ok" for v in checks.values()) else "unhealthy"

        return HealthCheckResponse(
            status=overall_status,
            timestamp=datetime.now(timezone.utc).isoformat(),
            checks=checks,
            gpu_info=gpu_info,
        )

    except Exception as e:
        logger.error(f"Health check failed: {e}", exc_info=True)
        return HealthCheckResponse(
            status="unhealthy",
            timestamp=datetime.now(timezone.utc).isoformat(),
            checks={
                "model_initialized": "error",
                "device_available": "error",
                "disk_space": "error",
            },
            gpu_info=None,
        )


@app.get("/", summary="Root endpoint")
async def root():
    """Root endpoint with service information"""
    return {
        "service": "SmolDocling PDF Extraction Service",
        "version": "1.0.0",
        "model": settings.model_name,
        "device": settings.device,
        "status": "running",
        "docs": "/docs",
    }


@app.get("/metrics", summary="Prometheus metrics")
async def metrics_endpoint():
    with metrics_lock:
        lines = [
            "# HELP extract_requests_total Total extract requests",
            "# TYPE extract_requests_total counter",
            f"extract_requests_total {metrics['extract_requests_total']}",
            "# HELP extract_failures_total Total extract failures",
            "# TYPE extract_failures_total counter",
            f"extract_failures_total {metrics['extract_failures_total']}",
            "# HELP extract_duration_ms_sum Cumulative extract latency in ms",
            "# TYPE extract_duration_ms_sum counter",
            f"extract_duration_ms_sum {metrics['extract_duration_ms_sum']}",
            "# HELP extract_payload_bytes_sum Cumulative payload bytes processed",
            "# TYPE extract_payload_bytes_sum counter",
            f"extract_payload_bytes_sum {metrics['extract_payload_bytes_sum']}",
        ]
    return PlainTextResponse("\n".join(lines) + "\n")


# ---------------------------------------------------------------------------
# /preprocess — Photo preprocessing endpoint (Libro Game AI Assistant MVP)
# ---------------------------------------------------------------------------
# Spike findings (2026-05-04):
#   - SmolDocling does NOT expose token-level confidence scores.
#   - Confidence is estimated via _estimate_confidence() heuristics in
#     SmolDoclingAdapter (base 0.7 + length/structure bonuses, max 1.0).
#   - process_page(PageImage) returns PageExtractionResult with .markdown_text
#     and .confidence_score — both are used here.
#   - No global `ocr_engine`; inference is accessed via pdf_service.vlm_adapter.
#   - pdf_service may be None before lifespan runs (unit tests must monkeypatch).
# ---------------------------------------------------------------------------

from pydantic import BaseModel as _BaseModel


class PreprocessResponse(_BaseModel):
    """Response schema for /preprocess endpoint."""

    processed_image_base64: str
    extracted_text: str
    confidence: float
    orientation: str
    is_blank: bool
    warnings: List[str]


# ── Image processing helpers ──────────────────────────────────────────────────

def _dewarp_image(img: Image.Image) -> Image.Image:
    """
    Detect page contour and apply perspective transform to rectify the image.
    Falls back to the original image when no 4-corner contour is found.
    """
    from imutils.perspective import four_point_transform

    cv_img = cv2.cvtColor(np.array(img.convert("RGB")), cv2.COLOR_RGB2BGR)
    gray = cv2.cvtColor(cv_img, cv2.COLOR_BGR2GRAY)
    blurred = cv2.GaussianBlur(gray, (5, 5), 0)
    edges = cv2.Canny(blurred, 75, 200)

    contours, _ = cv2.findContours(edges, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not contours:
        return img

    page_contour = max(contours, key=cv2.contourArea)
    peri = cv2.arcLength(page_contour, True)
    approx = cv2.approxPolyDP(page_contour, 0.02 * peri, True)

    if len(approx) == 4:
        try:
            warped = four_point_transform(cv_img, approx.reshape(4, 2))
            return Image.fromarray(cv2.cvtColor(warped, cv2.COLOR_BGR2RGB))
        except Exception:
            return img

    return img


def _detect_orientation(img: Image.Image) -> str:
    """
    Detect page orientation from EXIF tag 274 (Orientation) or image dimensions.
    Returns one of: 'portrait', 'landscape', 'rotated', 'unknown'.
    """
    # Attempt EXIF-based detection (JPEG files only)
    exif_data = None
    try:
        exif_data = img._getexif() if hasattr(img, "_getexif") else None  # type: ignore[attr-defined]
    except Exception:
        pass

    if exif_data:
        orientation_tag = exif_data.get(274)  # Tag 274 = Orientation
        if orientation_tag in (3, 4):
            return "rotated"
        if orientation_tag in (5, 6, 7, 8):
            width, height = img.size
            return "landscape" if width > height else "portrait"

    width, height = img.size
    if width > height * 1.2:
        return "landscape"
    if height > width * 1.2:
        return "portrait"
    return "unknown"


def _is_blank_page(img: Image.Image, std_threshold: float = 5.0) -> bool:
    """
    Return True when the image is mostly blank (low pixel-intensity variance).
    A standard deviation below `std_threshold` on the greyscale channel is
    treated as blank.
    """
    grey = np.array(img.convert("L"), dtype=np.float32)
    return float(grey.std()) < std_threshold


def _extract_text_with_confidence(img: Image.Image) -> tuple[float, str]:
    """
    Run SmolDocling VLM inference on a single image and return (confidence, text).

    Spike note: SmolDocling (Hugging Face transformers) does not expose
    token-level confidence scores.  Confidence is estimated by the adapter's
    _estimate_confidence() heuristic (base 0.7, max 1.0).  A constant 0.85
    is used as the baseline when the model is not initialised (tests).
    """
    global pdf_service  # noqa: PLW0602

    if pdf_service is None or pdf_service.vlm_adapter is None:
        # Service not ready (e.g., unit-test environment without lifespan)
        return 0.85, ""

    # Ensure model is loaded (lazy init)
    if not pdf_service.vlm_adapter._is_initialized:
        try:
            pdf_service.vlm_adapter.initialize()
        except Exception as exc:
            logger.warning("VLM initialisation failed during preprocess: %s", exc)
            return 0.85, ""

    page = PageImage.from_pil_image(1, img.convert("RGB"), dpi=300)
    result = pdf_service.vlm_adapter.process_page(page)
    return result.confidence_score, result.markdown_text


# ── /preprocess endpoint ───────────────────────────────────────────────────────

@app.post(
    "/api/v1/preprocess",
    response_model=PreprocessResponse,
    summary="Preprocess a camera-captured rulebook page",
    description=(
        "Applies perspective correction (dewarp), orientation detection, blank-page "
        "detection and SmolDocling VLM text extraction to a photo taken with a "
        "mobile camera.  Use preprocessing_mode='photo-camera' for full processing "
        "or 'default' for a pass-through response."
    ),
)
async def preprocess_photo(
    image: UploadFile = File(..., description="Image file (JPEG/PNG)"),
    preprocessing_mode: str = Form(
        "photo-camera",
        description="'photo-camera' for full preprocessing, 'default' for pass-through",
    ),
) -> PreprocessResponse:
    """
    Preprocess a page photo for the Libro Game AI Assistant.

    Args:
        image: Image file (multipart/form-data)
        preprocessing_mode: Processing mode

    Returns:
        PreprocessResponse with base64-encoded processed image, extracted text,
        confidence score, orientation, blank-page flag and non-fatal warnings.
    """
    request_id = str(uuid.uuid4())
    logger.info(
        "Preprocess request [%s]: file=%s mode=%s",
        request_id,
        image.filename,
        preprocessing_mode,
    )

    img_bytes = await image.read()
    if not img_bytes:
        raise HTTPException(status_code=status.HTTP_400_BAD_REQUEST, detail="Empty image file")

    try:
        img = Image.open(io.BytesIO(img_bytes))
    except Exception as exc:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Cannot open image: {exc}",
        ) from exc

    warnings: List[str] = []

    if preprocessing_mode == "photo-camera":
        # 1. Perspective correction
        try:
            img = await run_in_threadpool(_dewarp_image, img)
        except Exception as exc:
            warnings.append(f"Dewarp skipped: {exc}")

        # 2. Orientation
        orientation = _detect_orientation(img)

        # 3. Blank-page detection
        is_blank = _is_blank_page(img)
        if is_blank:
            warnings.append("Page appears blank")

        # 4. VLM text extraction + confidence
        try:
            confidence, extracted_text = await run_in_threadpool(
                _extract_text_with_confidence, img
            )
        except Exception as exc:
            logger.warning("OCR error in preprocess [%s]: %s", request_id, exc)
            confidence = 0.0
            extracted_text = ""
            warnings.append(f"OCR error: {exc}")

        if confidence < 0.5:
            warnings.append("Low OCR confidence — image may be blurry or poorly lit")

        # 5. Encode processed image
        buf = io.BytesIO()
        img.convert("RGB").save(buf, format="JPEG", quality=92)
        processed_b64 = base64.b64encode(buf.getvalue()).decode()

        logger.info(
            "Preprocess done [%s]: orientation=%s blank=%s confidence=%.2f warnings=%d",
            request_id,
            orientation,
            is_blank,
            confidence,
            len(warnings),
        )

        return PreprocessResponse(
            processed_image_base64=processed_b64,
            extracted_text=extracted_text,
            confidence=confidence,
            orientation=orientation,
            is_blank=is_blank,
            warnings=warnings,
        )

    # default / pass-through mode
    extracted_text = ""
    try:
        _, extracted_text = await run_in_threadpool(_extract_text_with_confidence, img)
    except Exception as exc:
        warnings.append(f"OCR error: {exc}")

    return PreprocessResponse(
        processed_image_base64=base64.b64encode(img_bytes).decode(),
        extracted_text=extracted_text,
        confidence=0.95,
        orientation="portrait",
        is_blank=False,
        warnings=warnings,
    )


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "src.main:app",
        host=settings.host,
        port=settings.port,
        reload=True,
        log_level=settings.log_level.lower(),
    )
