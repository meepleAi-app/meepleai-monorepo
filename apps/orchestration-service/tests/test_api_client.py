"""Tests for MeepleAI API Client."""

import pytest
from unittest.mock import AsyncMock, MagicMock, patch
from uuid import uuid4
import httpx

from src.infrastructure.api_client import MeepleAIApiClient


class TestMeepleAIApiClient:
    """Test suite for API client."""

    @pytest.fixture
    def client(self):
        """Create API client."""
        return MeepleAIApiClient(base_url="http://test:8080")

    def test_client_initialization(self, client):
        """Test client initializes with base URL."""
        assert client.base_url == "http://test:8080"
        assert client.client is not None

    def test_intent_to_strategy_mapping(self, client):
        """Test intent maps to retrieval strategy."""
        assert client._intent_to_strategy("setup") == "temporal_scoring"
        assert client._intent_to_strategy("rules") == "hybrid_search"
        assert client._intent_to_strategy("general") == "capability_matching"
        assert client._intent_to_strategy("unknown") == "hybrid_search"

    @pytest.mark.asyncio
    async def test_hybrid_search_success(self, client):
        """Test successful hybrid search call."""
        # Create proper mock response
        mock_response = MagicMock()
        mock_response.json = MagicMock(return_value={
            "results": [{"content": "test", "score": 0.9}]
        })
        mock_response.raise_for_status = MagicMock()

        async def async_post(*args, **kwargs):
            return mock_response

        with patch.object(client.client, 'post', side_effect=async_post) as mock_post:
            result = await client.hybrid_search(
                game_id=uuid4(),
                query="test query",
                intent_type="setup"
            )

            assert "results" in result
            assert len(result["results"]) == 1

    @pytest.mark.asyncio
    async def test_hybrid_search_handles_errors(self, client):
        """Test hybrid search error handling."""
        with patch.object(client.client, 'post', new_callable=AsyncMock) as mock_post:
            mock_post.side_effect = httpx.HTTPError("Network error")

            with pytest.raises(httpx.HTTPError):
                await client.hybrid_search(
                    game_id=uuid4(),
                    query="test",
                    intent_type="setup"
                )

    @pytest.mark.asyncio
    async def test_client_close(self, client):
        """Test client close."""
        with patch.object(client.client, 'aclose', new_callable=AsyncMock) as mock_close:
            await client.close()
            mock_close.assert_called_once()
