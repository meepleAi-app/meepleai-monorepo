"""
ADR-016 Phase 4: Cross-Encoder Reranking Service

High-performance reranking service using BGE-reranker-v2-m3 model.
Provides semantic reranking for RAG retrieval results.

Features:
- Cross-encoder reranking with BAAI/bge-reranker-v2-m3
- Batch processing for efficiency
- Health check endpoint
- Model warmup on startup
- Configurable batch size and timeouts
"""

import logging
import os
import time
from contextlib import asynccontextmanager
from typing import Optional

from fastapi import FastAPI, HTTPException, Request, status
from pydantic import BaseModel, Field
from sentence_transformers import CrossEncoder
from slowapi import Limiter, _rate_limit_exceeded_handler
from slowapi.errors import RateLimitExceeded
from slowapi.util import get_remote_address

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)

# Configuration from environment
MODEL_NAME = os.getenv("MODEL_NAME", "BAAI/bge-reranker-v2-m3")
BATCH_SIZE = int(os.getenv("BATCH_SIZE", "32"))
MAX_LENGTH = int(os.getenv("MAX_LENGTH", "512"))
DEVICE = os.getenv("DEVICE", "cpu")
ENABLE_WARMUP = os.getenv("ENABLE_WARMUP", "true").lower() == "true"
RATE_LIMIT = os.getenv("RATE_LIMIT", "100/minute")  # Configurable rate limit

# Rate limiter initialization
limiter = Limiter(key_func=get_remote_address)

# Global model instance
model: Optional[CrossEncoder] = None
model_loaded: bool = False
startup_time: Optional[float] = None


class ChunkInput(BaseModel):
    """Input chunk for reranking."""
    id: str = Field(..., description="Unique chunk identifier")
    content: str = Field(..., description="Chunk text content")
    score: float = Field(default=0.0, description="Original retrieval score")
    metadata: dict = Field(default_factory=dict, description="Additional metadata")


class RerankRequest(BaseModel):
    """Rerank request payload."""
    query: str = Field(..., description="Search query for reranking context")
    chunks: list[ChunkInput] = Field(..., description="Chunks to rerank")
    top_k: Optional[int] = Field(default=None, description="Return top K results (None = all)")


class RerankResult(BaseModel):
    """Reranked chunk result."""
    id: str
    content: str
    original_score: float
    rerank_score: float
    metadata: dict


class RerankResponse(BaseModel):
    """Rerank response payload."""
    results: list[RerankResult]
    model: str
    processing_time_ms: float


class HealthResponse(BaseModel):
    """Health check response."""
    status: str
    model_loaded: bool
    model_name: str
    device: str
    uptime_seconds: Optional[float] = None


def load_model() -> CrossEncoder:
    """Load the cross-encoder model."""
    global model, model_loaded, startup_time

    logger.info(f"Loading cross-encoder model: {MODEL_NAME}")
    start = time.time()

    model = CrossEncoder(
        MODEL_NAME,
        max_length=MAX_LENGTH,
        device=DEVICE
    )

    load_time = time.time() - start
    model_loaded = True
    startup_time = time.time()
    logger.info(f"Model loaded successfully in {load_time:.2f}s on device: {DEVICE}")

    return model


def warmup_model():
    """Perform model warmup with sample inference."""
    if not ENABLE_WARMUP or model is None:
        return

    logger.info("Performing model warmup...")
    start = time.time()

    # Sample warmup inference
    warmup_pairs = [
        ("What are the rules?", "The game has specific rules for setup."),
        ("How do I win?", "Victory conditions include scoring points."),
    ]

    try:
        _ = model.predict(warmup_pairs)
        warmup_time = time.time() - start
        logger.info(f"Model warmup completed in {warmup_time:.2f}s")
    except Exception as e:
        logger.warning(f"Model warmup failed: {e}")


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan management."""
    # Startup
    load_model()
    warmup_model()
    yield
    # Shutdown
    logger.info("Shutting down reranker service")


app = FastAPI(
    title="Cross-Encoder Reranking Service",
    description="ADR-016 Phase 4: Semantic reranking for RAG retrieval",
    version="1.0.0",
    lifespan=lifespan
)

# Register rate limiter with app
app.state.limiter = limiter
app.add_exception_handler(RateLimitExceeded, _rate_limit_exceeded_handler)


@app.get("/health", response_model=HealthResponse)
async def health_check():
    """
    Health check endpoint.

    Returns model status and service health information.
    """
    uptime = None
    if startup_time:
        uptime = time.time() - startup_time

    return HealthResponse(
        status="healthy" if model_loaded else "unhealthy",
        model_loaded=model_loaded,
        model_name=MODEL_NAME,
        device=DEVICE,
        uptime_seconds=uptime
    )


@app.post("/rerank", response_model=RerankResponse)
@limiter.limit(RATE_LIMIT)
async def rerank(request: Request, rerank_request: RerankRequest):
    """
    Rerank chunks using cross-encoder model.

    Takes a query and list of chunks, returns chunks sorted by relevance score.
    Uses BAAI/bge-reranker-v2-m3 for high-quality semantic reranking.
    """
    if not model_loaded or model is None:
        raise HTTPException(
            status_code=status.HTTP_503_SERVICE_UNAVAILABLE,
            detail="Model not loaded"
        )

    if not rerank_request.chunks:
        return RerankResponse(
            results=[],
            model=MODEL_NAME,
            processing_time_ms=0.0
        )

    start = time.time()

    try:
        # Prepare query-chunk pairs for cross-encoder
        pairs = [(rerank_request.query, chunk.content) for chunk in rerank_request.chunks]

        # Compute reranking scores in batches
        scores = model.predict(pairs, batch_size=BATCH_SIZE, show_progress_bar=False)

        # Combine results with scores
        results = []
        for i, chunk in enumerate(rerank_request.chunks):
            results.append(RerankResult(
                id=chunk.id,
                content=chunk.content,
                original_score=chunk.score,
                rerank_score=float(scores[i]),
                metadata=chunk.metadata
            ))

        # Sort by rerank score (descending)
        results.sort(key=lambda x: x.rerank_score, reverse=True)

        # Apply top_k if specified
        if rerank_request.top_k is not None and rerank_request.top_k > 0:
            results = results[:rerank_request.top_k]

        processing_time_ms = (time.time() - start) * 1000

        logger.info(
            f"Reranked {len(rerank_request.chunks)} chunks in {processing_time_ms:.1f}ms"
        )

        return RerankResponse(
            results=results,
            model=MODEL_NAME,
            processing_time_ms=processing_time_ms
        )

    except Exception as e:
        logger.error(f"Reranking failed: {e}")
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Reranking failed: {str(e)}"
        )


@app.get("/")
async def root():
    """Root endpoint with service info."""
    return {
        "service": "Cross-Encoder Reranking Service",
        "version": "1.0.0",
        "model": MODEL_NAME,
        "endpoints": {
            "/health": "Health check",
            "/rerank": "Rerank chunks (POST)"
        }
    }


if __name__ == "__main__":
    import uvicorn

    port = int(os.getenv("PORT", "8003"))
    host = os.getenv("HOST", "0.0.0.0")

    uvicorn.run(app, host=host, port=port)