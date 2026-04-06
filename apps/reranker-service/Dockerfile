# ADR-016 Phase 4: Cross-Encoder Reranking Service
# Multi-stage build for optimized image size

FROM python:3.11-slim AS builder

WORKDIR /app

# Install build dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Install Python dependencies
COPY requirements.txt .
RUN pip wheel --no-cache-dir --no-deps --wheel-dir /app/wheels -r requirements.txt

# Pre-download ML model in builder to avoid cold-start delay
ENV HF_HOME=/model-cache
RUN pip install --no-cache /app/wheels/* \
    && python -c "from sentence_transformers import CrossEncoder; CrossEncoder('BAAI/bge-reranker-v2-m3')"

# Production stage
FROM python:3.11-slim

WORKDIR /app

# Create non-root user for security
RUN groupadd -r reranker && useradd -r -g reranker -m -d /home/reranker reranker && mkdir -p /home/reranker/.cache/huggingface && chown -R reranker:reranker /home/reranker

# Install runtime dependencies only
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy wheels from builder stage
COPY --from=builder /app/wheels /wheels
COPY --from=builder /app/requirements.txt .

# Install dependencies from wheels (faster, no compilation)
RUN pip install --no-cache /wheels/*

# Copy pre-downloaded model from builder
COPY --from=builder --chown=reranker:reranker /model-cache /home/reranker/.cache/huggingface

# Copy application code
COPY main.py .

# Set ownership
RUN chown -R reranker:reranker /app

# Switch to non-root user
USER reranker

# Environment variables with sensible defaults
ENV HF_HOME=/home/reranker/.cache/huggingface \
    MODEL_NAME=BAAI/bge-reranker-v2-m3 \
    BATCH_SIZE=32 \
    MAX_LENGTH=512 \
    DEVICE=cpu \
    ENABLE_WARMUP=true \
    PORT=8003 \
    HOST=0.0.0.0 \
    PYTHONUNBUFFERED=1

# Expose the service port
EXPOSE 8003

# Health check
HEALTHCHECK --interval=30s --timeout=10s --retries=3 --start-period=120s \
    CMD curl -f http://localhost:8003/health || exit 1

# Run the service
CMD ["python", "main.py"]
