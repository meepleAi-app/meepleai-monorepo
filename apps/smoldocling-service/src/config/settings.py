"""Application configuration settings for SmolDocling service"""
from pydantic_settings import BaseSettings, SettingsConfigDict
from pathlib import Path
from typing import Literal


class Settings(BaseSettings):
    """Application configuration with environment variable overrides"""

    # Logging
    log_level: Literal["DEBUG", "INFO", "WARNING", "ERROR"] = "INFO"

    # File Processing
    max_file_size: int = 52428800  # 50MB in bytes
    max_pages_per_request: int = 20  # Prevent excessive GPU/CPU usage
    timeout: int = 60  # seconds (longer than Unstructured due to VLM inference)
    temp_dir: Path = Path("/tmp/pdf-processing")
    model_cache_dir: Path = Path(
        __import__("os").environ.get("HF_HOME", "/home/smoldocling/.cache/huggingface")
    )

    # SmolDocling VLM Configuration
    device: Literal["cuda", "cpu", "auto"] = "auto"  # auto = use CUDA if available
    model_name: str = "docling-project/SmolDocling-256M-preview"
    max_new_tokens: int = 2048  # Max tokens to generate per page
    torch_dtype: Literal["bfloat16", "float16", "float32"] = "bfloat16"

    # PDF to Image Conversion
    image_dpi: int = 300  # Higher DPI = better OCR but slower
    image_format: Literal["PNG", "JPEG"] = "PNG"

    # Quality Thresholds (lower than Stage 1 since this is fallback)
    quality_threshold: float = 0.70
    min_chars_per_page: int = 300  # VLM may extract less due to layout focus

    # Crop-table discrimination (issue #3435 SP3 — Option B crop-discriminator)
    # The <otsl> gate is authoritative; these only contain cost / cap the degenerate loop.
    crop_prefilter_enabled: bool = True  # cheap colorfulness reject BEFORE the VLM
    # Hasler-Süsstrunk colorfulness above which a crop is rejected as an illustration WITHOUT
    # running the VLM. Uncalibrated: a conservative (high) default keeps false-rejects of real
    # tables (even ones with a tinted header) rare — but a sufficiently colourful table WILL be
    # rejected here (the <otsl> gate only sees crops that reach the VLM). Tunable; the
    # per-request `prefilter` flag disables it entirely.
    crop_prefilter_colorfulness_threshold: float = 40.0
    crop_max_new_tokens: int = 512  # crops are small (~76-300 tok); cap well below full-page 2048
    crop_rep_stop_window: int = 24  # trailing generated tokens inspected for the repetition early-stop
    crop_rep_stop_max_distinct: int = 2  # <= this many distinct ids in the window => degenerate loop

    # Server Configuration
    host: str = "0.0.0.0"
    port: int = 8002
    workers: int = 1  # VLM models: single worker, scale horizontally

    # Observability
    enable_metrics: bool = True
    enable_structured_logging: bool = True

    # Performance Optimization
    enable_model_warmup: bool = True  # Warm up on startup
    batch_processing: bool = False  # Batch multiple pages (experimental)
    test_mode: bool = False  # Shortcut pipeline for integration tests

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
    )


# Singleton instance
settings = Settings()
