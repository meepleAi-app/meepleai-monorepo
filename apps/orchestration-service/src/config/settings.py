"""
ISSUE-3495: Service Configuration
Pydantic settings for environment-based configuration.
"""

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Orchestration service configuration."""

    # Service configuration
    log_level: str = Field(default="INFO", description="Logging level")
    port: int = Field(default=8004, description="Service port")

    # Dependent services
    embedding_service_url: str = Field(
        default="http://embedding-service:8000",
        description="Embedding service URL"
    )
    reranker_service_url: str = Field(
        default="http://reranker-service:8003",
        description="Reranker service URL"
    )
    unstructured_service_url: str = Field(
        default="http://unstructured-service:8001",
        description="Unstructured service URL"
    )
    smoldocling_service_url: str = Field(
        default="http://smoldocling-service:8002",
        description="SmolDocling service URL"
    )

    # Database
    database_url: str = Field(
        default="postgresql://meepleai:meepleai@postgres:5432/meepleai",
        description="PostgreSQL connection string"
    )

    # Redis
    redis_url: str = Field(
        default="redis://redis:6379/0",
        description="Redis connection string"
    )

    # Qdrant
    qdrant_url: str = Field(
        default="http://qdrant:6333",
        description="Qdrant vector store URL"
    )
    qdrant_api_key: str = Field(default="", description="Qdrant API key")

    # LLM Provider
    openrouter_api_key: str = Field(default="", description="OpenRouter API key")
    openrouter_base_url: str = Field(
        default="https://openrouter.ai/api/v1",
        description="OpenRouter base URL"
    )

    # LangGraph configuration
    langgraph_timeout: int = Field(default=30, description="Workflow timeout in seconds")
    max_workflow_depth: int = Field(default=10, description="Maximum workflow recursion depth")
    enable_checkpointing: bool = Field(default=True, description="Enable state checkpointing")

    # Monitoring
    metrics_enabled: bool = Field(default=True, description="Enable Prometheus metrics")

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=False,
        extra="ignore",
    )


# Global settings instance
settings = Settings()
