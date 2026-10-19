# MeepleAI Unstructured PDF Extraction Service
# BGAI-001-v2: Multi-stage build removes gcc/g++ from runtime image

# === Stage 1: Build Python wheels ===
FROM python:3.11-slim AS builder

WORKDIR /build

# Install build tools for compiling native extensions
RUN apt-get update && apt-get install -y --no-install-recommends \
    gcc \
    g++ \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Build all Python packages into a virtual env
RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

COPY requirements.txt .
ENV PIP_DEFAULT_TIMEOUT=300
RUN pip install --no-cache-dir --retries 5 -r requirements.txt

# Pre-download NLTK datasets
ENV NLTK_DATA=/nltk_data
RUN python -m nltk.downloader -d /nltk_data punkt punkt_tab averaged_perceptron_tagger

# === Stage 2: Lean runtime image ===
FROM python:3.11-slim

WORKDIR /app

# Install only RUNTIME system dependencies (no gcc/g++/build-essential)
# OCR languages (#4435): tesseract-ocr pulls in eng (and osd); ita and deu are added here. They must
# cover the API's PdfProcessing:Extractor:Unstructured:Language set (default eng+ita+deu): Tesseract
# asked for a single language it has no data for fails the whole OCR ("couldn't load any languages").
RUN apt-get update && apt-get install -y --no-install-recommends \
    libmagic-dev \
    poppler-utils \
    tesseract-ocr \
    tesseract-ocr-ita \
    tesseract-ocr-deu \
    libgl1 \
    libglib2.0-0 \
    curl \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Set Tesseract data directory. Debian bookworm and trixie (Tesseract 5) install the packs under
# 5/tessdata; the old 4.00 path did not exist and Tesseract 5.5 ignored it with a warning (#4435).
# Check after a deploy: `docker exec meepleai-unstructured tesseract --list-langs` → eng, ita, deu.
ENV TESSDATA_PREFIX=/usr/share/tesseract-ocr/5/tessdata

# Create non-root user for security
RUN groupadd -r unstructured && useradd -r -g unstructured -m -d /home/unstructured unstructured

# Copy virtual env with pre-built packages (no compilation needed)
COPY --from=builder /opt/venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

# Copy pre-downloaded NLTK data
ENV NLTK_DATA=/home/unstructured/nltk_data
COPY --from=builder --chown=unstructured:unstructured /nltk_data /home/unstructured/nltk_data

# Copy application code
COPY src/ ./src/

# Create temp directory with proper permissions
RUN mkdir -p /tmp/pdf-extraction && chown unstructured:unstructured /tmp/pdf-extraction

# Set ownership
RUN chown -R unstructured:unstructured /app

# Switch to non-root user
USER unstructured

# Expose port
EXPOSE 8001

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8001/health || exit 1

# Run with Gunicorn + Uvicorn workers
CMD ["gunicorn", "src.main:app", \
     "--workers", "2", \
     "--worker-class", "uvicorn.workers.UvicornWorker", \
     "--bind", "0.0.0.0:8001", \
     "--timeout", "300", \
     "--access-logfile", "-", \
     "--error-logfile", "-"]
