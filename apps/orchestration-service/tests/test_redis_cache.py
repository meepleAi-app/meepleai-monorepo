"""Tests for Redis cache."""

import pytest
from unittest.mock import AsyncMock, MagicMock, patch
from datetime import datetime, UTC

from src.infrastructure.redis_cache import IntentCache
from src.domain import IntentType


class TestIntentCache:
    """Test suite for Redis cache."""

    @pytest.fixture
    def cache(self):
        """Create cache instance."""
        return IntentCache(redis_url="redis://test:6379")

    @pytest.mark.asyncio
    async def test_get_returns_none_when_not_connected(self, cache):
        """Test get returns None when Redis not connected."""
        cache.client = None

        result = await cache.get("test_key")

        assert result is None

    @pytest.mark.asyncio
    async def test_set_does_nothing_when_not_connected(self, cache):
        """Test set does nothing when Redis not connected."""
        cache.client = None

        # Should not raise exception
        await cache.set("test_key", IntentType.SETUP_QUESTION, 0.9)

    @pytest.mark.asyncio
    async def test_get_success(self, cache):
        """Test successful cache retrieval."""
        mock_client = AsyncMock()
        mock_client.get.return_value = '{"intent": "setup", "confidence": 0.9}'
        cache.client = mock_client

        result = await cache.get("test query")

        assert result is not None
        assert result[0] == IntentType.SETUP_QUESTION
        assert result[1] == 0.9

    @pytest.mark.asyncio
    async def test_set_success(self, cache):
        """Test successful cache storage."""
        mock_client = AsyncMock()
        cache.client = mock_client

        await cache.set("test query", IntentType.SETUP_QUESTION, 0.85)

        mock_client.setex.assert_called_once()

    @pytest.mark.asyncio
    async def test_get_handles_json_decode_error(self, cache):
        """Test get handles invalid JSON."""
        mock_client = AsyncMock()
        mock_client.get.return_value = "invalid json"
        cache.client = mock_client

        result = await cache.get("test_key")

        assert result is None

    @pytest.mark.asyncio
    async def test_disconnect_when_connected(self, cache):
        """Test disconnect releases connection."""
        mock_client = AsyncMock()
        cache.client = mock_client

        await cache.disconnect()

        mock_client.aclose.assert_called_once()
    @pytest.mark.asyncio
    async def test_connect_success(self, cache):
        """Test successful Redis connection."""
        with patch('redis.asyncio.from_url', new_callable=AsyncMock) as mock_from_url:
            mock_client = AsyncMock()
            mock_client.ping = AsyncMock()
            mock_from_url.return_value = mock_client

            await cache.connect()

            assert cache.client == mock_client

    @pytest.mark.asyncio
    async def test_connect_failure(self, cache):
        """Test Redis connection failure."""
        with patch('redis.asyncio.from_url', new_callable=AsyncMock) as mock_from_url:
            mock_from_url.side_effect = Exception("Connection failed")

            await cache.connect()

            assert cache.client is None

    @pytest.mark.asyncio
    async def test_set_handles_errors(self, cache):
        """Test set handles errors gracefully."""
        mock_client = AsyncMock()
        mock_client.setex.side_effect = Exception("Redis error")
        cache.client = mock_client

        # Should not raise
        await cache.set("test", IntentType.SETUP_QUESTION, 0.9)

    def test_make_key_creates_hash(self, cache):
        """Test cache key generation."""
        key1 = cache._make_key("test query")
        key2 = cache._make_key("test query")
        key3 = cache._make_key("different query")

        assert key1 == key2  # Same query = same key
        assert key1 != key3  # Different query = different key
        assert key1.startswith("intent:classification:")
