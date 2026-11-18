# SmolDocling FastAPI Service Dockerfile
# Multi-stage build: builder compiles deps, runtime keeps only what's needed
# Supports both GPU and CPU inference

# ============================================================
# Stage 1: Builder — compile Python packages in a slim image
# ============================================================
FROM python:3.11-slim AS builder

# #4997: apt-get update and install are idempotent, so a short outage of the archive is retried.
RUN apt-get -o Acquire::Retries=5 update && apt-get -o Acquire::Retries=5 install -y --no-install-recommends \
    build-essential \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Create a virtual environment for clean COPY later
RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

COPY requirements.txt /tmp/requirements.txt
ENV PIP_DEFAULT_TIMEOUT=300
RUN pip install --no-cache-dir --retries 5 -r /tmp/requirements.txt

# Download model weights at build time (speeds up first startup)
ENV HF_HOME=/opt/model-cache
RUN python -c "from transformers import AutoProcessor, AutoModelForImageTextToText; \
    print('Downloading SmolDocling model...'); \
    AutoProcessor.from_pretrained('docling-project/SmolDocling-256M-preview'); \
    AutoModelForImageTextToText.from_pretrained('docling-project/SmolDocling-256M-preview'); \
    print('Model download complete')"

# ============================================================
# Stage 2: Runtime — CUDA base for GPU. Keeps build-essential OUT of the runtime
# image (the #248 size optimization), EXCEPT the minimal gcc + headers that Triton's
# runtime JIT needs on the GPU path — installed below with a rationale comment (#3435).
# ============================================================
FROM nvidia/cuda:12.6.3-cudnn-runtime-ubuntu22.04

WORKDIR /app

# Install runtime system dependencies. Python comes from the deadsnakes PPA so
# we get a *current* Python 3.11.x — the Ubuntu 22.04 default `python3.11`
# package on this CUDA base is the fossil **3.11.0rc1** (Aug 2022 release
# candidate), which is missing `sys.get_int_max_str_digits` (added only in the
# 3.11.0 final). torch >= 2.12 `polyfills/sys.py:31` decorates
# `@substitute_in_graph(sys.get_int_max_str_digits, ...)` unconditionally for
# Python 3.11+, so on rc1 the module raises AttributeError mid-init. The
# partially-loaded module leaves `sys.intern` already registered in
# `VariableBuilder._id_dispatch`, so any subsequent `import torch._dynamo`
# attempt (transformers does several via lazy loaders) then crashes with the
# user-visible "Duplicate dispatch rule for sys.intern" instead of the real
# AttributeError. Pinning a *current* 3.11.x via deadsnakes resolves both.
#
# DEBIAN_FRONTEND=noninteractive prevents `python3.11-distutils` (pulled via
# deadsnakes) from blocking on the `tzdata` geographic-area prompt. Verified
# locally 2026-06-10: without it the build hung at `Geographic area:`.
#
# #4997: the PPA is a fixed apt source, with its signing key in the repository
# (apt/deadsnakes-ppa.asc). `add-apt-repository` asked the Launchpad API for the
# key on every build, and that call failed (503, timeouts, closed connections),
# turning red every PR that rebuilds this image. The key is the one Launchpad
# publishes for ~deadsnakes/+archive/ubuntu/ppa, fingerprint
# F23C 5A6C F475 9775 95C8  9F51 BA69 3236 6A75 5776 (check with
# `gpg --show-keys apt/deadsnakes-ppa.asc`); the sha256 below pins the file, so a
# changed key is a deliberate edit of both. ca-certificates comes first, from the
# Ubuntu archive, because the PPA is served over https.
ENV DEBIAN_FRONTEND=noninteractive
COPY apt/deadsnakes-ppa.asc /etc/apt/keyrings/deadsnakes-ppa.asc
RUN printf '%s  %s\n' ee12f00ee17b7de1c3529279e12cd025731be85ac4404bbaeea08a12d4f9da23 \
        /etc/apt/keyrings/deadsnakes-ppa.asc > /tmp/deadsnakes-ppa.sha256 \
    && sha256sum -c /tmp/deadsnakes-ppa.sha256 \
    && rm /tmp/deadsnakes-ppa.sha256 \
    && apt-get -o Acquire::Retries=5 update \
    && apt-get -o Acquire::Retries=5 install -y --no-install-recommends ca-certificates \
    && . /etc/os-release \
    && echo "deb [signed-by=/etc/apt/keyrings/deadsnakes-ppa.asc] https://ppa.launchpadcontent.net/deadsnakes/ppa/ubuntu ${VERSION_CODENAME} main" \
        > /etc/apt/sources.list.d/deadsnakes-ppa.list \
    && apt-get -o Acquire::Retries=5 update \
    && apt-get -o Acquire::Retries=5 install -y --no-install-recommends \
    # Python 3.11 (deadsnakes — current 3.11.x, not Ubuntu's stale rc1)
    python3.11 \
    python3.11-distutils \
    python3-pip \
    # HTTP client for health checks
    curl \
    # PDF to Image conversion
    poppler-utils \
    # Image processing (OpenCV + Pillow runtime)
    libgl1 \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    # Triton JIT toolchain (#3435 SP3): torch>=2.12 JIT-compiles Triton CUDA kernels
    # (e.g. bmm_outer_product) at RUNTIME inside model.generate(). Triton shells out to
    # gcc to build a `cuda_utils.c` glue module that #includes <Python.h>, so it needs
    # BOTH a host C compiler (gcc + libc6-dev) AND the Python dev headers
    # (python3.11-dev → /usr/include/python3.11/Python.h). Without them every GPU
    # inference fails per-page and the adapter swallows it → empty extraction (0 chunks).
    # ptxas is already bundled in triton/backends/nvidia. GPU-only overhead, small; the
    # CPU path never hits the Triton kernel.
    gcc \
    libc6-dev \
    python3.11-dev \
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
# Fix venv shebang: builder uses /opt/venv/bin/python but runtime has python3.11 elsewhere
RUN ln -sf /usr/bin/python3.11 /opt/venv/bin/python && \
    ln -sf /usr/bin/python3.11 /opt/venv/bin/python3 && \
    ln -sf /usr/bin/python3.11 /opt/venv/bin/python3.11
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
