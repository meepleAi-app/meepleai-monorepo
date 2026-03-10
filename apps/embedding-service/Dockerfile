# MeepleAI Embedding Service Dockerfile
# AI-09: Multi-language embeddings support (LOCAL)

FROM python:3.11-slim

# Set working directory
WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security (before model download so cache is user-owned)
RUN groupadd -r embedding && useradd -r -g embedding -m -d /home/embedding embedding \
    && mkdir -p /home/embedding/.cache/huggingface \
    && chown -R embedding:embedding /home/embedding

# Copy requirements and install Python dependencies
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Pre-download the model to speed up container startup
# Cache to user-owned directory so non-root user can access at runtime
ENV HF_HOME=/home/embedding/.cache/huggingface
RUN python -c "from sentence_transformers import SentenceTransformer; SentenceTransformer('intfloat/multilingual-e5-large')" \
    && chown -R embedding:embedding /home/embedding/.cache

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
