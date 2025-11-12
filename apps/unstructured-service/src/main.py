"""FastAPI application entry point"""
import logging
import uuid
from contextlib import asynccontextmanager
from datetime import datetime
from fastapi import FastAPI, File, UploadFile, Form, HTTPException, status
from fastapi.responses import JSONResponse
from typing import Literal

from .config import settings
from .application import PdfExtractionService
from .api.schemas import (
    PdfExtractionResponse,
    TextChunkSchema,
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


# Lifespan context manager
@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan handler"""
    logger.info("Starting PDF Extraction Service")
    logger.info(f"Configuration: strategy={settings.unstructured_strategy}, language={settings.language}")
    yield
    logger.info("Shutting down PDF Extraction Service")


# Create FastAPI application
app = FastAPI(
    title="PDF Extraction Microservice",
    description="Unstructured PDF extraction service for MeepleAI",
    version="1.0.0",
    lifespan=lifespan,
)

# Initialize service
pdf_service = PdfExtractionService()


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


@app.exception_handler(FileNotFoundError)
async def file_not_found_handler(request, exc: FileNotFoundError):
    """Handle file not found errors (422)"""
    request_id = str(uuid.uuid4())
    logger.error(f"File not found [{request_id}]: {exc}")

    return JSONResponse(
        status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
        content=ErrorResponse(
            error=ErrorDetail(
                code="FILE_NOT_FOUND",
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
    responses={
        400: {"model": ErrorResponse, "description": "Invalid request"},
        413: {"model": ErrorResponse, "description": "File too large"},
        415: {"model": ErrorResponse, "description": "Unsupported media type"},
        422: {"model": ErrorResponse, "description": "Unprocessable PDF"},
        500: {"model": ErrorResponse, "description": "Internal server error"},
    },
    summary="Extract text and metadata from PDF",
    description="Extracts text using Unstructured library with semantic chunking",
)
async def extract_pdf(
    file: UploadFile = File(..., description="PDF file to extract"),
    strategy: Literal["fast", "hi_res"] = Form(
        default="fast", description="Extraction strategy"
    ),
    language: str = Form(default="ita", description="Document language"),
):
    """
    Extract text from PDF with semantic chunking

    Args:
        file: PDF file (multipart/form-data)
        strategy: Extraction strategy (fast or hi_res)
        language: Document language (ISO 639-3 code)

    Returns:
        PdfExtractionResponse with text, chunks, and quality score

    Raises:
        HTTPException: Various errors (400, 413, 415, 422, 500)
    """
    request_id = str(uuid.uuid4())
    logger.info(f"Extraction request [{request_id}]: file={file.filename}, strategy={strategy}, language={language}")

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

        # Create BytesIO object
        from io import BytesIO

        file_io = BytesIO(file_content)

        # Extract text
        result = await pdf_service.extract_async(
            file_content=file_io,
            filename=file.filename or "document.pdf",
            strategy=strategy,
            language=language,
        )

        # Build response
        response = PdfExtractionResponse(
            text=result.full_text,
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
                "strategy_used": strategy,
                "language": language,
                "detected_tables": result.table_count,
                "detected_structures": result.detected_structures,
                "quality_breakdown": result.quality_score.to_dict(),
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
    description="Returns service health status and component checks",
)
async def health_check():
    """
    Check service health

    Returns:
        HealthCheckResponse with status and component checks
    """
    try:
        # Check Unstructured library import
        try:
            from unstructured.partition.pdf import partition_pdf

            unstructured_status = "ok"
        except Exception as e:
            logger.error(f"Unstructured library check failed: {e}")
            unstructured_status = "error"

        # Check disk space
        import shutil

        disk_usage = shutil.disk_usage(settings.temp_dir)
        free_gb = disk_usage.free / (1024**3)
        disk_status = "ok" if free_gb > 1.0 else "warning" if free_gb > 0.5 else "error"

        # Check memory
        import psutil

        memory = psutil.virtual_memory()
        memory_status = (
            "ok"
            if memory.percent < 80
            else "warning"
            if memory.percent < 90
            else "error"
        )

        # Determine overall status
        statuses = [unstructured_status, disk_status, memory_status]
        overall_status = "healthy" if all(s == "ok" for s in statuses) else "unhealthy"

        return HealthCheckResponse(
            status=overall_status,
            timestamp=datetime.utcnow(),
            checks={
                "unstructured_library": unstructured_status,
                "disk_space": disk_status,
                "memory": memory_status,
            },
        )

    except Exception as e:
        logger.error(f"Health check failed: {e}", exc_info=True)
        return HealthCheckResponse(
            status="unhealthy",
            timestamp=datetime.utcnow(),
            checks={
                "unstructured_library": "error",
                "disk_space": "error",
                "memory": "error",
            },
        )


@app.get("/", summary="Root endpoint")
async def root():
    """Root endpoint with service information"""
    return {
        "service": "PDF Extraction Microservice",
        "version": "1.0.0",
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
