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

    @pytest.mark.asyncio
    async def test_tutor_query_success(self, client):
        """Test successful Tutor query call."""
        mock_response = MagicMock()
        mock_response.json = MagicMock(return_value={
            "response": "Here's how to set up chess",
            "agentType": "tutor",
            "confidence": 0.92,
            "citations": ["rulebook.pdf"],
            "executionTimeMs": 150.5
        })
        mock_response.raise_for_status = MagicMock()

        async def async_post(*args, **kwargs):
            return mock_response

        with patch.object(client.client, 'post', side_effect=async_post) as mock_post:
            result = await client.tutor_query(
                game_id=uuid4(),
                session_id=uuid4(),
                query="How do I set up chess?"
            )

            assert result["response"] == "Here's how to set up chess"
            assert result["confidence"] == 0.92
            assert len(result["citations"]) == 1

    @pytest.mark.asyncio
    async def test_tutor_query_handles_errors(self, client):
        """Test Tutor query error handling."""
        with patch.object(client.client, 'post', new_callable=AsyncMock) as mock_post:
            mock_post.side_effect = httpx.HTTPError("API error")

            with pytest.raises(httpx.HTTPError):
                await client.tutor_query(
                    game_id=uuid4(),
                    session_id=uuid4(),
                    query="test"
                )

    @pytest.mark.asyncio
    async def test_decisore_analyze_success(self, client):
        """Test successful Decisore analyze call."""
        mock_response = MagicMock()
        mock_response.json = MagicMock(return_value={
            "suggestions": [
                {"move": {"notation": "e2-e4"}, "reasoning": "Control center"},
                {"move": {"notation": "d2-d4"}, "reasoning": "Open lines"}
            ],
            "overallConfidence": 0.88,
            "executionTimeMs": 2100.3
        })
        mock_response.raise_for_status = MagicMock()

        async def async_post(*args, **kwargs):
            return mock_response

        with patch.object(client.client, 'post', side_effect=async_post) as mock_post:
            result = await client.decisore_analyze(
                session_id=uuid4(),
                player_name="White",
                analysis_depth="standard",
                max_suggestions=3
            )

            assert len(result["suggestions"]) == 2
            assert result["overallConfidence"] == 0.88

    @pytest.mark.asyncio
    async def test_decisore_analyze_handles_errors(self, client):
        """Test Decisore analyze error handling."""
        with patch.object(client.client, 'post', new_callable=AsyncMock) as mock_post:
            mock_post.side_effect = httpx.TimeoutException("Request timed out")

            with pytest.raises(httpx.TimeoutException):
                await client.decisore_analyze(
                    session_id=uuid4(),
                    player_name="Black"
                )