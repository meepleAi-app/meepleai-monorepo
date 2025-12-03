"""Application configuration settings"""
from pydantic_settings import BaseSettings, SettingsConfigDict
from pathlib import Path
from typing import Literal


class Settings(BaseSettings):
    """Application configuration with environment variable overrides"""

    # Logging
    log_level: Literal["DEBUG", "INFO", "WARNING", "ERROR"] = "INFO"

    # File Processing
    max_file_size: int = 52428800  # 50MB in bytes
    timeout: int = 30  # seconds
    temp_dir: Path = Path("/tmp/pdf-extraction")

    # Unstructured Configuration
    unstructured_strategy: Literal["fast", "hi_res"] = "fast"
    language: str = "ita"

    # Chunking Configuration
    chunk_max_characters: int = 2000
    chunk_overlap: int = 200

    # Quality Thresholds
    quality_threshold: float = 0.80
    min_chars_per_page: int = 500

    # Server Configuration
    host: str = "0.0.0.0"
    port: int = 8001
    workers: int = 4

    # Observability
    enable_metrics: bool = True
    enable_structured_logging: bool = True

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
    )


# Singleton instance
settings = Settings()
