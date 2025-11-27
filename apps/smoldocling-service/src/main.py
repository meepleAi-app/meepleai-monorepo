"""FastAPI application entry point for SmolDocling service"""
import logging
import uuid
import torch
from contextlib import asynccontextmanager
from datetime import datetime
from typing import Optional
from fastapi import FastAPI, File, UploadFile, HTTPException, status
from fastapi.responses import JSONResponse
from PIL import Image

from .config import settings
from .application import PdfExtractionService
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
                timestamp=datetime.utcnow().isoformat(),
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
                timestamp=datetime.utcnow().isoformat(),
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
                timestamp=datetime.utcnow().isoformat(),
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

    try:
        # Validate file type
        if not file.content_type or "pdf" not in file.content_type.lower():
            raise HTTPException(
                status_code=status.HTTP_415_UNSUPPORTED_MEDIA_TYPE,
                detail=ErrorResponse(
                    error=ErrorDetail(
                        code="UNSUPPORTED_MEDIA_TYPE",
                        message=f"File type not supported: {file.content_type}. Expected application/pdf",
                        timestamp=datetime.utcnow().isoformat(),
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
                        timestamp=datetime.utcnow().isoformat(),
                        request_id=request_id,
                    )
                ).model_dump(),
            )

        # Create BytesIO
        from io import BytesIO

        file_io = BytesIO(file_content)

        # Extract text
        result = await pdf_service.extract_async(
            file_content=file_io, filename=file.filename or "document.pdf", language="ita"
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

        return response

    except HTTPException:
        raise
    except ValueError as e:
        raise HTTPException(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            detail=ErrorResponse(
                error=ErrorDetail(
                    code="CORRUPTED_PDF",
                    message=str(e),
                    timestamp=datetime.utcnow().isoformat(),
                    request_id=request_id,
                )
            ).model_dump(),
        )
    except Exception as e:
        logger.error(f"Extraction failed [{request_id}]: {e}", exc_info=True)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=ErrorResponse(
                error=ErrorDetail(
                    code="EXTRACTION_FAILED",
                    message="Failed to extract text from PDF",
                    details={"error": str(e)},
                    timestamp=datetime.utcnow().isoformat(),
                    request_id=request_id,
                )
            ).model_dump(),
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
            timestamp=datetime.utcnow().isoformat(),
            checks=checks,
            gpu_info=gpu_info,
        )

    except Exception as e:
        logger.error(f"Health check failed: {e}", exc_info=True)
        return HealthCheckResponse(
            status="unhealthy",
            timestamp=datetime.utcnow().isoformat(),
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


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "src.main:app",
        host=settings.host,
        port=settings.port,
        reload=True,
        log_level=settings.log_level.lower(),
    )
