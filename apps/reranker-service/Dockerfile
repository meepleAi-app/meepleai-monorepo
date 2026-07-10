# ADR-016 Phase 4: Cross-Encoder Reranking Service
# Multi-stage build for optimized image size

FROM python:3.11-slim AS builder

WORKDIR /app

# Install build dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Install Python dependencies into a virtual env for a clean copy into the
# runtime stage (#2794). The previous `pip wheel` + copy-to-/wheels approach
# left ~409MB of wheel files behind in the final image (they were installed but
# never removed). The venv-copy pattern (mirrors embedding-service) carries only
# the installed packages, no wheel artifacts.
RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"
ENV PIP_DEFAULT_TIMEOUT=300

COPY requirements.txt .
# Install CPU-only torch FIRST from the official PyTorch CPU index (#2794).
# On aarch64 the default `pip install torch` bundles ~2.9GB of unused NVIDIA
# CUDA libraries (nvidia_cublas/cudnn/nccl/...); this server is GPU-less and the
# service runs DEVICE=cpu, so they are dead weight. Pre-installing the CPU wheel
# leaves torch already satisfied, so the requirements install below never pulls
# the CUDA variant (and requirements.txt stays unchanged for CI test jobs).
# torch IS pinned below; hadolint misreads the --index-url value as an unpinned positional package (DL3013 false positive)
# hadolint ignore=DL3013
RUN pip install --no-cache-dir --retries 5 torch==2.12.1 --index-url https://download.pytorch.org/whl/cpu
RUN pip install --no-cache-dir --retries 5 -r requirements.txt

# Pre-download ML model in builder to avoid cold-start delay
ENV HF_HOME=/model-cache
RUN python -c "from sentence_transformers import CrossEncoder; CrossEncoder('BAAI/bge-reranker-v2-m3')"

# Production stage
FROM python:3.11-slim

WORKDIR /app

# Create non-root user for security
RUN groupadd -r reranker && useradd -r -g reranker -m -d /home/reranker reranker && mkdir -p /home/reranker/.cache/huggingface && chown -R reranker:reranker /home/reranker

# Install runtime dependencies only
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Copy the virtual env with all installed packages (no build tools, no wheels)
COPY --from=builder /opt/venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

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
