# ADR-016 Phase 4: Cross-Encoder Reranking Service
# Multi-stage build for optimized image size

FROM python:3.11-slim as builder

WORKDIR /app

# Install build dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Install Python dependencies
COPY requirements.txt .
RUN pip wheel --no-cache-dir --no-deps --wheel-dir /app/wheels -r requirements.txt

# Production stage
FROM python:3.11-slim

WORKDIR /app

# Create non-root user for security
RUN groupadd -r reranker && useradd -r -g reranker reranker

# Install runtime dependencies only
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy wheels from builder stage
COPY --from=builder /app/wheels /wheels
COPY --from=builder /app/requirements.txt .

# Install dependencies from wheels (faster, no compilation)
RUN pip install --no-cache /wheels/*

# Copy application code
COPY main.py .
COPY tests/ ./tests/

# Set ownership
RUN chown -R reranker:reranker /app

# Switch to non-root user
USER reranker

# Environment variables with sensible defaults
ENV MODEL_NAME=BAAI/bge-reranker-v2-m3 \
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
