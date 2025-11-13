"""
MeepleAI Embedding Service - Local Multilingual Embeddings

FastAPI microservice providing multilingual embeddings using sentence-transformers.
Uses intfloat/multilingual-e5-large model for 5 languages: EN, IT, DE, FR, ES.

AI-09: Multi-language embeddings support (LOCAL implementation)
"""

import logging
from contextlib import asynccontextmanager
from typing import List

import torch
from fastapi import FastAPI, HTTPException
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field
from sentence_transformers import SentenceTransformer

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)

# Model configuration
MODEL_NAME = "intfloat/multilingual-e5-large"
SUPPORTED_LANGUAGES = ["en", "it", "de", "fr", "es"]
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"

# Global model instance
model: SentenceTransformer | None = None


# Request/Response models
class EmbeddingRequest(BaseModel):
    """Request model for embedding generation"""
    texts: List[str] = Field(..., min_items=1, max_items=100, description="Texts to embed (1-100)")
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

    try:
        logger.info(f"Generating embeddings for {len(request.texts)} texts in language: {request.language}")

        # Generate embeddings
        # Note: multilingual-e5-large uses instruction prefix for better quality
        # Format: "query: <text>" for queries, "passage: <text>" for documents
        # We'll use "passage:" prefix as we're embedding PDF chunks
        prefixed_texts = [f"passage: {text}" for text in request.texts]

        embeddings = model.encode(
            prefixed_texts,
            convert_to_numpy=True,
            show_progress_bar=False,
            normalize_embeddings=True  # L2 normalization for better similarity search
        )

        # Convert to list of lists for JSON serialization
        embeddings_list = embeddings.tolist()

        logger.info(f"Successfully generated {len(embeddings_list)} embeddings")

        return EmbeddingResponse(
            embeddings=embeddings_list,
            model=MODEL_NAME,
            dimension=len(embeddings_list[0]),
            count=len(embeddings_list)
        )

    except Exception as e:
        logger.error(f"Error generating embeddings: {e}")
        raise HTTPException(status_code=500, detail=f"Error generating embeddings: {str(e)}")


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
