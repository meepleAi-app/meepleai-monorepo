FROM python:3.11-slim

WORKDIR /app

# Install system dependencies for Unstructured
RUN apt-get update && apt-get install -y --no-install-recommends \
    # Core dependencies
    libmagic-dev \
    poppler-utils \
    tesseract-ocr \
    # Italian language support
    tesseract-ocr-ita \
    # OpenCV dependencies (required by unstructured-inference)
    libgl1 \
    libglib2.0-0 \
    # Build tools (if needed)
    gcc \
    g++ \
    # Cleanup
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Set Tesseract data directory
ENV TESSDATA_PREFIX=/usr/share/tesseract-ocr/4.00/tessdata

# Copy requirements first for better caching
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY src/ ./src/

# Create temp directory with proper permissions
RUN mkdir -p /tmp/pdf-extraction && chmod 777 /tmp/pdf-extraction

# Expose port
EXPOSE 8001

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD python -c "import requests; requests.get('http://localhost:8001/health').raise_for_status()"

# Run with Gunicorn + Uvicorn workers
CMD ["gunicorn", "src.main:app", \
     "--workers", "4", \
     "--worker-class", "uvicorn.workers.UvicornWorker", \
     "--bind", "0.0.0.0:8001", \
     "--timeout", "35", \
     "--access-logfile", "-", \
     "--error-logfile", "-"]
