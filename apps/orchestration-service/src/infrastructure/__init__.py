"""Infrastructure layer for external integrations."""

from .redis_cache import IntentCache

__all__ = ["IntentCache"]
