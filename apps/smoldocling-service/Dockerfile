# SmolDocling FastAPI Service Dockerfile
# Supports both GPU and CPU inference

# Use CUDA base image for GPU support (can run CPU-only too)
# Pick a newer CUDA/LTS image to replace the soon-to-be-deprecated tag
FROM nvidia/cuda:12.6.3-cudnn-runtime-ubuntu22.04

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    # Python
    python3.11 \
    python3-pip \
    # HTTP client for health checks
    curl \
    # PDF to Image conversion
    poppler-utils \
    # Image processing
    libgl1 \
    libglib2.0-0 \
    # Build tools (for some pip packages)
    build-essential \
    # Cleanup
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Set Python version
RUN update-alternatives --install /usr/bin/python python /usr/bin/python3.11 1

# Create non-root user for security (before model download so cache is user-owned)
RUN groupadd -r smoldocling && useradd -r -g smoldocling -m -d /home/smoldocling smoldocling \
    && mkdir -p /home/smoldocling/.cache/huggingface \
    && chown -R smoldocling:smoldocling /home/smoldocling

# Copy requirements first for better caching
COPY requirements.txt .
RUN python -m pip install --no-cache-dir -r requirements.txt

# Download model weights at build time (optional - speeds up first startup)
# Cache to user-owned directory so non-root user can access at runtime
ENV HF_HOME=/home/smoldocling/.cache/huggingface
RUN python -c "from transformers import AutoProcessor, AutoModelForImageTextToText; \
    print('Downloading SmolDocling model...'); \
    AutoProcessor.from_pretrained('docling-project/SmolDocling-256M-preview'); \
    AutoModelForImageTextToText.from_pretrained('docling-project/SmolDocling-256M-preview'); \
    print('Model download complete')" \
    && chown -R smoldocling:smoldocling /home/smoldocling/.cache

# Copy application code
COPY src/ ./src/

# Create temp directory for PDF processing
RUN mkdir -p /tmp/pdf-processing && chown smoldocling:smoldocling /tmp/pdf-processing

# Set ownership
RUN chown -R smoldocling:smoldocling /app

# Switch to non-root user
USER smoldocling

# Expose port
EXPOSE 8002

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8002/health || exit 1

# Run with single worker (VLM models don't benefit from multiple workers)
# Use external load balancer for horizontal scaling instead
CMD ["uvicorn", "src.main:app", \
     "--host", "0.0.0.0", \
     "--port", "8002", \
     "--workers", "1", \
     "--timeout-keep-alive", "45"]
