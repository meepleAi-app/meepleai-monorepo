# MeepleAI Embedding Service Dockerfile
# AI-09: Multi-language embeddings support (LOCAL)
# Multi-stage build: removes build-essential from runtime image

# === Stage 1: Build dependencies and download model ===
FROM python:3.11-slim AS builder

WORKDIR /build

# Install build tools needed for compiling Python packages
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Install Python dependencies into a virtual env for clean copy
RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Pre-download the model so runtime container starts fast
ENV HF_HOME=/model-cache
RUN python -c "from sentence_transformers import SentenceTransformer; SentenceTransformer('intfloat/multilingual-e5-large')"

# === Stage 2: Lean runtime image ===
FROM python:3.11-slim

WORKDIR /app

# Only curl needed at runtime (for healthcheck)
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r embedding && useradd -r -g embedding -m -d /home/embedding embedding \
    && mkdir -p /home/embedding/.cache/huggingface \
    && chown -R embedding:embedding /home/embedding

# Copy virtual env with all installed packages (no build tools needed)
COPY --from=builder /opt/venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

# Copy pre-downloaded model cache
ENV HF_HOME=/home/embedding/.cache/huggingface
COPY --from=builder --chown=embedding:embedding /model-cache /home/embedding/.cache/huggingface

# Copy application code
COPY main.py .

# Set ownership and switch to non-root user
RUN chown -R embedding:embedding /app
USER embedding

# Expose port
EXPOSE 8000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8000/health || exit 1

# Run the application
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000", "--log-level", "info"]
