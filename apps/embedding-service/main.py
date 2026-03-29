"""
MeepleAI Embedding Service - Local Multilingual Embeddings

FastAPI microservice providing multilingual embeddings using sentence-transformers.
Uses intfloat/multilingual-e5-large model for 5 languages: EN, IT, DE, FR, ES.

AI-09: Multi-language embeddings support (LOCAL implementation)
"""

import asyncio
import logging
import os
from contextlib import asynccontextmanager
from functools import partial
from typing import List

import torch
import threading
from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse, PlainTextResponse
import uuid, time
from pydantic import BaseModel, Field
from sentence_transformers import SentenceTransformer

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)

# Model configuration — configurable via environment variable
# Supported models:
#   intfloat/multilingual-e5-large  (1024 dim, ~560M params, higher quality)
#   intfloat/multilingual-e5-base   (768 dim, ~278M params, ~2x faster)
#   intfloat/multilingual-e5-small  (384 dim, ~118M params, ~4x faster)
ALLOWED_MODELS = {
    "intfloat/multilingual-e5-large",
    "intfloat/multilingual-e5-base",
    "intfloat/multilingual-e5-small",
}
MODEL_NAME = os.environ.get("EMBEDDING_MODEL", "intfloat/multilingual-e5-large")
if MODEL_NAME not in ALLOWED_MODELS:
    raise ValueError(
        f"EMBEDDING_MODEL={MODEL_NAME!r} not in allowed models: {ALLOWED_MODELS}"
    )
SUPPORTED_LANGUAGES = ["en", "it", "de", "fr", "es"]
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"
# Hard guard to prevent runaway memory use on very long inputs
MAX_TEXT_CHARS = 4096
MAX_TOTAL_CHARS = 200_000

# Global model instance
model: SentenceTransformer | None = None
metrics_lock = threading.Lock()
metrics = {
    "embed_requests_total": 0,
    "embed_failures_total": 0,
    "embed_duration_ms_sum": 0.0,
    "embed_total_chars_sum": 0.0,
}


# Request/Response models
class EmbeddingRequest(BaseModel):
    """Request model for embedding generation"""
    texts: List[str] = Field(..., min_length=1, max_length=100, description="Texts to embed (1-100)")
    language: str = Field(..., pattern="^(en|it|de|fr|es)$", description="ISO 639-1 language code")


class EmbeddingResponse(BaseModel):
    """Response model for embedding generation"""
    embeddings: List[List[float]] = Field(..., description="Embedding vectors")
    model: str = Field(..., description="Model name used")
    dimension: int = Field(..., description="Embedding dimension")
    count: int = Field(..., description="Number of embeddings generated")


class HealthResponse(BaseModel):
    """Response model for health check"""
    status: str = Field(..., description="Service status")
    model: str = Field(..., description="Loaded model name")
    device: str = Field(..., description="Compute device (cpu/cuda)")
    supported_languages: List[str] = Field(..., description="Supported language codes")


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Lifecycle manager - load model on startup, cleanup on shutdown"""
    global model

    logger.info(f"Loading model {MODEL_NAME} on device {DEVICE}...")
    try:
        model = SentenceTransformer(MODEL_NAME, device=DEVICE)
        logger.info(f"Model loaded successfully. Embedding dimension: {model.get_sentence_embedding_dimension()}")
    except Exception as e:
        logger.error(f"Failed to load model: {e}")
        raise

    yield

    # Cleanup
    logger.info("Shutting down embedding service...")
    model = None


# FastAPI app
app = FastAPI(
    title="MeepleAI Embedding Service",
    description="Local multilingual embeddings using sentence-transformers",
    version="1.0.0",
    lifespan=lifespan
)


@app.get("/health", response_model=HealthResponse, tags=["Health"])
async def health_check():
    """
    Health check endpoint for readiness probes

    Returns model status and configuration
    """
    if model is None:
        raise HTTPException(status_code=503, detail="Model not loaded")

    return HealthResponse(
        status="healthy",
        model=MODEL_NAME,
        device=DEVICE,
        supported_languages=SUPPORTED_LANGUAGES
    )


@app.post("/embeddings", response_model=EmbeddingResponse, tags=["Embeddings"])
async def generate_embeddings(request: EmbeddingRequest):
    """
    Generate embeddings for input texts

    Supports 5 languages: EN, IT, DE, FR, ES
    Uses intfloat/multilingual-e5-large model

    Args:
        request: EmbeddingRequest with texts and language code

    Returns:
        EmbeddingResponse with embedding vectors

    Raises:
        HTTPException: 503 if model not loaded, 400 for invalid input
    """
    if model is None:
        raise HTTPException(status_code=503, detail="Model not loaded")

    # Validate language
    if request.language not in SUPPORTED_LANGUAGES:
        raise HTTPException(
            status_code=400,
            detail=f"Unsupported language: {request.language}. Supported: {SUPPORTED_LANGUAGES}"
        )

    start = time.time()
    try:
        # Validate text lengths (defense-in-depth; pydantic only limits count)
        total_chars = sum(len(t) for t in request.texts)
        if any(len(t) > MAX_TEXT_CHARS for t in request.texts):
            raise HTTPException(
                status_code=400,
                detail=f"Each text must be <= {MAX_TEXT_CHARS} characters"
            )
        if total_chars > MAX_TOTAL_CHARS:
            raise HTTPException(
                status_code=400,
                detail=f"Total payload too large ({total_chars} chars > {MAX_TOTAL_CHARS})"
            )

        logger.info(f"Generating embeddings for {len(request.texts)} texts in language: {request.language}")

        # Note: multilingual-e5-large uses instruction prefix for better quality
        # Format: "query: <text>" for queries, "passage: <text>" for documents
        # We'll use "passage:" prefix as we're embedding PDF chunks
        prefixed_texts = [f"passage: {text}" for text in request.texts]

        loop = asyncio.get_running_loop()
        encode_fn = partial(
            model.encode,
            prefixed_texts,
            convert_to_numpy=True,
            show_progress_bar=False,
            normalize_embeddings=True  # L2 normalization for better similarity search
        )
        embeddings = await loop.run_in_executor(None, encode_fn)

        # Convert to list of lists for JSON serialization
        embeddings_list = embeddings.tolist()

        duration_ms = int((time.time() - start) * 1000)
        logger.info(f"Successfully generated {len(embeddings_list)} embeddings in {duration_ms}ms")
        with metrics_lock:
            metrics["embed_requests_total"] += 1
            metrics["embed_duration_ms_sum"] += duration_ms
            metrics["embed_total_chars_sum"] += total_chars

        return EmbeddingResponse(
            embeddings=embeddings_list,
            model=MODEL_NAME,
            dimension=len(embeddings_list[0]),
            count=len(embeddings_list)
        )

    except Exception as e:
        logger.error(f"Error generating embeddings: {e}")
        request_id = str(uuid.uuid4())
        with metrics_lock:
            metrics["embed_requests_total"] += 1
            metrics["embed_failures_total"] += 1
        return JSONResponse(
            status_code=500,
            content=ErrorResponse(
                error={
                    "code": "EMBEDDING_FAILED",
                    "message": str(e),
                    "request_id": request_id,
                }
            ).model_dump(),
        )


@app.get("/metrics", tags=["Metrics"])
async def metrics_endpoint():
    """
    Minimal Prometheus-style metrics (text format).
    """
    with metrics_lock:
        lines = [
            "# HELP embed_requests_total Total embedding requests",
            "# TYPE embed_requests_total counter",
            f"embed_requests_total {metrics['embed_requests_total']}",
            "# HELP embed_failures_total Total embedding failures",
            "# TYPE embed_failures_total counter",
            f"embed_failures_total {metrics['embed_failures_total']}",
            "# HELP embed_duration_ms_sum Cumulative embedding latency in ms",
            "# TYPE embed_duration_ms_sum counter",
            f"embed_duration_ms_sum {metrics['embed_duration_ms_sum']}",
            "# HELP embed_total_chars_sum Cumulative characters processed",
            "# TYPE embed_total_chars_sum counter",
            f"embed_total_chars_sum {metrics['embed_total_chars_sum']}",
        ]
    return PlainTextResponse("\n".join(lines) + "\n")


@app.get("/", tags=["Info"])
async def root():
    """Root endpoint with service info"""
    return {
        "service": "MeepleAI Embedding Service",
        "version": "1.0.0",
        "model": MODEL_NAME,
        "supported_languages": SUPPORTED_LANGUAGES,
        "endpoints": {
            "health": "/health",
            "embeddings": "/embeddings",
            "docs": "/docs"
        }
    }


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000, log_level="info")
# Error schema
class ErrorResponse(BaseModel):
    error: dict


# Unified error handler
@app.exception_handler(Exception)
async def unhandled_exception_handler(request: Request, exc: Exception):
    request_id = str(uuid.uuid4())
    logger.error(f"Unhandled error [{request_id}]: {exc}", exc_info=True)
    return JSONResponse(
        status_code=500,
        content=ErrorResponse(
            error={
                "code": "INTERNAL_ERROR",
                "message": "Unexpected error",
                "request_id": request_id,
            }
        ).model_dump(),
    )
