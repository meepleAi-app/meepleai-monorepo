"""
ISSUE-3776: Fallback Strategy Tests
Verify retry, timeout, and agent fallback mechanisms.
"""

import pytest
from unittest.mock import AsyncMock, MagicMock
import httpx
import asyncio

from src.application.fallback_strategy import FallbackStrategy
from src.domain import AgentType


class TestFallbackStrategy:
    """Test suite for fallback strategies."""

    @pytest.fixture
    def strategy(self):
        """Create fallback strategy."""
        return FallbackStrategy()

    @pytest.mark.asyncio
    async def test_execute_with_retry_success_first_attempt(self, strategy):
        """Test successful execution on first attempt."""
        mock_func = AsyncMock(return_value={"result": "success"})

        result = await strategy.execute_with_retry(mock_func, arg1="test")

        assert result == {"result": "success"}
        assert mock_func.call_count == 1

    @pytest.mark.asyncio
    async def test_execute_with_retry_success_after_failure(self, strategy):
        """Test success after initial failures (retry works)."""
        mock_func = AsyncMock()
        # Fail twice, succeed on third
        mock_func.side_effect = [
            httpx.HTTPError("Error 1"),
            httpx.HTTPError("Error 2"),
            {"result": "success"},
        ]

        result = await strategy.execute_with_retry(mock_func)

        assert result == {"result": "success"}
        assert mock_func.call_count == 3

    @pytest.mark.asyncio
    async def test_execute_with_retry_exhausts_retries(self, strategy):
        """Test all retries exhausted raises exception."""
        mock_func = AsyncMock()
        mock_func.side_effect = httpx.HTTPError("Persistent error")

        with pytest.raises(httpx.HTTPError, match="Persistent error"):
            await strategy.execute_with_retry(mock_func)

        assert mock_func.call_count == 3  # MAX_RETRIES

    @pytest.mark.asyncio
    async def test_exponential_backoff_timing(self, strategy):
        """Test exponential backoff delays are applied."""
        mock_func = AsyncMock()
        mock_func.side_effect = httpx.HTTPError("Fail")

        start_time = asyncio.get_event_loop().time()

        with pytest.raises(httpx.HTTPError):
            await strategy.execute_with_retry(mock_func)

        elapsed = asyncio.get_event_loop().time() - start_time

        # Total delay should be approximately 1s + 2s = 3s (not waiting after 3rd attempt)
        # Allow ±0.5s tolerance for test execution overhead
        assert 2.5 < elapsed < 4.0

    def test_get_timeout_fallback_tutor(self, strategy):
        """Test timeout fallback for Tutor agent."""
        fallback = strategy.get_timeout_fallback(AgentType.TUTOR)

        assert "agent_response" in fallback
        assert "knowledge base" in fallback["agent_response"].lower()
        assert fallback["current_agent"] == AgentType.TUTOR
        assert fallback["confidence_score"] == 0.0
        assert fallback["error"] == "timeout_fallback"

    def test_get_timeout_fallback_arbitro(self, strategy):
        """Test timeout fallback for Arbitro agent."""
        fallback = strategy.get_timeout_fallback(AgentType.ARBITRO)

        assert "validate" in fallback["agent_response"].lower()
        assert fallback["current_agent"] == AgentType.ARBITRO

    def test_get_timeout_fallback_decisore(self, strategy):
        """Test timeout fallback for Decisore agent."""
        fallback = strategy.get_timeout_fallback(AgentType.DECISORE)

        assert "strategic" in fallback["agent_response"].lower()
        assert fallback["current_agent"] == AgentType.DECISORE

    def test_get_agent_fallback_decisore_to_arbitro(self, strategy):
        """Test Decisore falls back to Arbitro."""
        fallback_agent = strategy.get_agent_fallback(AgentType.DECISORE)

        assert fallback_agent == AgentType.ARBITRO

    def test_get_agent_fallback_tutor_none(self, strategy):
        """Test Tutor has no agent fallback."""
        fallback_agent = strategy.get_agent_fallback(AgentType.TUTOR)

        assert fallback_agent is None

    def test_get_agent_fallback_arbitro_none(self, strategy):
        """Test Arbitro has no agent fallback (last resort)."""
        fallback_agent = strategy.get_agent_fallback(AgentType.ARBITRO)

        assert fallback_agent is None

    @pytest.mark.asyncio
    async def test_execute_with_fallback_primary_success(self, strategy):
        """Test execute_with_fallback uses primary when successful."""
        primary = AsyncMock(return_value={"agent_response": "primary result"})
        fallback_func = AsyncMock(return_value={"agent_response": "fallback result"})

        result = await strategy.execute_with_fallback(
            primary,
            fallback_func,
            AgentType.DECISORE
        )

        assert result["agent_response"] == "primary result"
        primary.assert_called_once()
        fallback_func.assert_not_called()

    @pytest.mark.asyncio
    async def test_execute_with_fallback_uses_fallback_on_failure(self, strategy):
        """Test execute_with_fallback uses fallback when primary fails."""
        primary = AsyncMock(side_effect=httpx.HTTPError("Primary failed"))
        fallback_func = AsyncMock(return_value={"agent_response": "fallback result"})

        result = await strategy.execute_with_fallback(
            primary,
            fallback_func,
            AgentType.DECISORE
        )

        assert result["agent_response"] == "fallback result"
        assert primary.call_count == 3  # Retried
        fallback_func.assert_called_once()

    @pytest.mark.asyncio
    async def test_execute_with_fallback_returns_timeout_when_all_fail(self, strategy):
        """Test timeout fallback when both primary and fallback fail."""
        primary = AsyncMock(side_effect=httpx.HTTPError("Primary failed"))
        fallback_func = AsyncMock(side_effect=httpx.HTTPError("Fallback failed"))

        result = await strategy.execute_with_fallback(
            primary,
            fallback_func,
            AgentType.TUTOR
        )

        assert result["error"] == "timeout_fallback"
        assert result["confidence_score"] == 0.0
