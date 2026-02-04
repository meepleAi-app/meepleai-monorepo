"""
ISSUE-3496: Redis Cache for Intent Classification
Caches frequent query patterns for performance optimization.
"""

import hashlib
import json
import logging
from typing import Optional, Tuple

import redis.asyncio as redis

from ..config import settings
from ..domain import IntentType

logger = logging.getLogger(__name__)


class IntentCache:
    """
    Redis-based cache for intent classifications.

    Target: >60% cache hit rate for frequent queries.
    TTL: 1 hour for cached classifications.
    """

    def __init__(self, redis_url: Optional[str] = None):
        """
        Initialize Redis cache.

        Args:
            redis_url: Redis connection URL (defaults to settings)
        """
        self.redis_url = redis_url or settings.redis_url
        self.client: Optional[redis.Redis] = None
        self.ttl_seconds = 3600  # 1 hour
        self.key_prefix = "intent:classification:"

    async def connect(self) -> None:
        """Establish Redis connection."""
        try:
            self.client = await redis.from_url(
                self.redis_url,
                encoding="utf-8",
                decode_responses=True,
            )
            # Test connection
            await self.client.ping()
            logger.info("✅ Redis cache connected successfully")
        except Exception as e:
            logger.error(f"Failed to connect to Redis: {e}")
            self.client = None

    async def disconnect(self) -> None:
        """Close Redis connection."""
        if self.client:
            await self.client.aclose()
            logger.info("Redis cache disconnected")

    async def get(self, query: str) -> Optional[Tuple[IntentType, float]]:
        """
        Get cached classification for query.

        Args:
            query: User query text

        Returns:
            Tuple of (IntentType, confidence) if cached, None otherwise
        """
        if not self.client:
            return None

        try:
            cache_key = self._make_key(query)
            cached_json = await self.client.get(cache_key)

            if not cached_json:
                return None

            # Parse cached value
            data = json.loads(cached_json)
            intent = IntentType(data["intent"])
            confidence = float(data["confidence"])

            logger.debug(f"Cache HIT: {query[:50]}... → {intent.value}")
            return intent, confidence

        except Exception as e:
            logger.warning(f"Cache read error: {e}")
            return None

    async def set(self, query: str, intent: IntentType, confidence: float) -> None:
        """
        Cache classification result.

        Args:
            query: User query text
            intent: Classified intent
            confidence: Confidence score
        """
        if not self.client:
            return

        try:
            cache_key = self._make_key(query)
            value = json.dumps({
                "intent": intent.value,
                "confidence": confidence,
            })

            await self.client.setex(cache_key, self.ttl_seconds, value)
            logger.debug(f"Cached: {query[:50]}... → {intent.value}")

        except Exception as e:
            logger.warning(f"Cache write error: {e}")

    def _make_key(self, query: str) -> str:
        """
        Create cache key from query.

        Uses SHA256 hash to handle variable-length queries and
        avoid special characters in Redis keys.
        """
        # Normalize query: lowercase, strip whitespace
        normalized = query.lower().strip()

        # Hash for consistent key length
        query_hash = hashlib.sha256(normalized.encode()).hexdigest()[:16]

        return f"{self.key_prefix}{query_hash}"
