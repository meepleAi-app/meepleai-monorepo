# SmolDocling FastAPI Service Dockerfile
# Multi-stage build: builder compiles deps, runtime keeps only what's needed
# Supports both GPU and CPU inference

# ============================================================
# Stage 1: Builder — compile Python packages in a slim image
# ============================================================
FROM python:3.11-slim AS builder

RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Create a virtual environment for clean COPY later
RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

COPY requirements.txt /tmp/requirements.txt
RUN pip install --no-cache-dir -r /tmp/requirements.txt

# Download model weights at build time (speeds up first startup)
ENV HF_HOME=/opt/model-cache
RUN python -c "from transformers import AutoProcessor, AutoModelForImageTextToText; \
    print('Downloading SmolDocling model...'); \
    AutoProcessor.from_pretrained('docling-project/SmolDocling-256M-preview'); \
    AutoModelForImageTextToText.from_pretrained('docling-project/SmolDocling-256M-preview'); \
    print('Model download complete')"

# ============================================================
# Stage 2: Runtime — CUDA base for GPU, no build-essential
# ============================================================
FROM nvidia/cuda:12.6.3-cudnn-runtime-ubuntu22.04

WORKDIR /app

# Install only runtime system dependencies (NO build-essential)
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
    # Cleanup
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Set Python version
RUN update-alternatives --install /usr/bin/python python /usr/bin/python3.11 1

# Create non-root user for security (before copying model so cache is user-owned)
RUN groupadd -r smoldocling && useradd -r -g smoldocling -m -d /home/smoldocling smoldocling \
    && mkdir -p /home/smoldocling/.cache/huggingface \
    && chown -R smoldocling:smoldocling /home/smoldocling

# Copy the pre-built virtual environment from builder
COPY --from=builder /opt/venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

# Copy pre-downloaded model weights from builder and set ownership
ENV HF_HOME=/home/smoldocling/.cache/huggingface
COPY --from=builder /opt/model-cache /home/smoldocling/.cache/huggingface
RUN chown -R smoldocling:smoldocling /home/smoldocling/.cache

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
