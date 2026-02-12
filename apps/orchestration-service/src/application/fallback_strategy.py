"""
ISSUE-3776: Fallback Strategies for Multi-Agent Orchestration
Graceful degradation and error recovery mechanisms.
"""

import asyncio
import logging
from typing import Any, Callable, Optional

import httpx

from ..domain import AgentType, GameAgentState

logger = logging.getLogger(__name__)


class FallbackStrategy:
    """
    Implements graceful fallback strategies for agent failures.

    Strategies:
    1. Retry with exponential backoff (transient failures)
    2. Timeout fallback (generic response on prolonged delays)
    3. Agent fallback (route to alternative agent on persistent failures)
    """

    # Exponential backoff delays (seconds)
    RETRY_DELAYS = [1.0, 2.0, 4.0]
    MAX_RETRIES = 3

    # Fallback responses by agent type
    FALLBACK_RESPONSES = {
        AgentType.TUTOR: "I'm having trouble accessing the knowledge base right now. Please try your question again in a moment, or rephrase it.",
        AgentType.ARBITRO: "I'm unable to validate this move at the moment. Please ensure the move notation is correct and try again.",
        AgentType.DECISORE: "Strategic analysis is temporarily unavailable. Try asking for move validation instead, or retry in a moment.",
    }

    # Agent fallback chain (if primary fails, try alternative)
    AGENT_FALLBACKS = {
        AgentType.DECISORE: AgentType.ARBITRO,  # Strategy fails → basic validation
        AgentType.TUTOR: None,  # No fallback for Tutor
        AgentType.ARBITRO: None,  # Arbitro is last resort
    }

    async def execute_with_retry(
        self,
        agent_func: Callable[..., Any],
        *args: Any,
        **kwargs: Any,
    ) -> Any:
        """
        Execute agent function with exponential backoff retry.

        Args:
            agent_func: Async agent function to call
            *args: Positional arguments for agent_func
            **kwargs: Keyword arguments for agent_func

        Returns:
            Agent function result

        Raises:
            Exception: If all retries exhausted
        """
        last_exception = None

        for attempt in range(self.MAX_RETRIES):
            try:
                logger.debug(f"Attempt {attempt + 1}/{self.MAX_RETRIES}")
                result = await agent_func(*args, **kwargs)

                if attempt > 0:
                    logger.info(f"Retry successful on attempt {attempt + 1}")

                return result

            except (httpx.HTTPError, httpx.TimeoutException, asyncio.TimeoutError) as e:
                last_exception = e
                logger.warning(
                    f"Attempt {attempt + 1} failed: {e}. "
                    f"Retrying in {self.RETRY_DELAYS[attempt]}s..."
                )

                # Sleep before retry (except on last attempt)
                if attempt < self.MAX_RETRIES - 1:
                    await asyncio.sleep(self.RETRY_DELAYS[attempt])

        # All retries exhausted
        logger.error(f"All {self.MAX_RETRIES} retries exhausted. Last error: {last_exception}")
        raise last_exception  # type: ignore

    def get_timeout_fallback(self, agent_type: AgentType) -> dict[str, Any]:
        """
        Get fallback response for timeout scenarios.

        Args:
            agent_type: Agent that timed out

        Returns:
            Fallback state dict with generic response
        """
        fallback_response = self.FALLBACK_RESPONSES.get(
            agent_type,
            "The AI agent is temporarily unavailable. Please try again in a moment."
        )

        logger.warning(f"Timeout fallback activated for {agent_type}")

        return {
            "agent_response": fallback_response,
            "current_agent": agent_type,
            "confidence_score": 0.0,
            "citations": [],
            "error": "timeout_fallback",
        }

    def get_agent_fallback(
        self,
        failed_agent: AgentType,
    ) -> Optional[AgentType]:
        """
        Get alternative agent to use when primary agent fails.

        Args:
            failed_agent: Agent that failed

        Returns:
            Alternative agent type, or None if no fallback available
        """
        fallback_agent = self.AGENT_FALLBACKS.get(failed_agent)

        if fallback_agent:
            logger.info(f"Agent fallback: {failed_agent} → {fallback_agent}")
            return fallback_agent

        logger.debug(f"No agent fallback available for {failed_agent}")
        return None

    async def execute_with_fallback(
        self,
        primary_func: Callable[..., Any],
        fallback_func: Optional[Callable[..., Any]],
        agent_type: AgentType,
        *args: Any,
        **kwargs: Any,
    ) -> dict[str, Any]:
        """
        Execute agent function with automatic fallback on failure.

        Args:
            primary_func: Primary agent function
            fallback_func: Optional fallback agent function
            agent_type: Primary agent type
            *args: Arguments for agent functions
            **kwargs: Keyword arguments for agent functions

        Returns:
            Agent response dict (from primary or fallback)
        """
        try:
            # Try primary with retry
            return await self.execute_with_retry(primary_func, *args, **kwargs)

        except Exception as e:
            logger.error(f"Primary agent {agent_type} failed after retries: {e}")

            # Try fallback agent if available
            if fallback_func:
                try:
                    logger.info(f"Attempting fallback agent for {agent_type}")
                    return await fallback_func(*args, **kwargs)
                except Exception as fallback_error:
                    logger.error(f"Fallback also failed: {fallback_error}")

            # Return timeout fallback response
            return self.get_timeout_fallback(agent_type)
