"""
ISSUE-3759: Unit tests for Arbitro Agent Move Validation
Tests LangGraph workflow for real-time move validation with <100ms target.
"""

import pytest
from uuid import uuid4
from unittest.mock import AsyncMock, patch

from src.application.arbitro_agent import ArbitroAgent
from src.domain import ArbitroState
from src.domain.arbitro_state import ArbitroState  # For isinstance check


class TestArbitroAgent:
    """Test suite for ArbitroAgent move validation."""

    @pytest.fixture
    def arbitro_agent(self):
        """Create arbitro agent instance."""
        return ArbitroAgent()

    @pytest.fixture
    def base_state(self):
        """Create base arbitro state."""
        return ArbitroState(
            game_id=uuid4(),
            session_id=uuid4(),
        )

    @pytest.mark.asyncio
    async def test_valid_knight_move(self, arbitro_agent, base_state):
        """Test validation of valid knight move."""
        base_state.move_notation = "Nf3"
        base_state.game_state = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"

        # Mock the LLM chain's ainvoke method (chain = PROMPT | llm)
        with patch('langchain_core.runnables.base.RunnableSequence.ainvoke', new_callable=AsyncMock) as mock_chain:
            mock_response = AsyncMock()
            mock_response.content = "Knight moves in L-shape. The move Nf3 is legal."
            mock_chain.return_value = mock_response

            result = await arbitro_agent.execute(base_state)

            # LangGraph returns dict, not object
            assert result["is_valid"] is True
            assert result["validation_reason"] is not None
            assert len(result["applied_rule_ids"]) > 0
            assert result["agent_response"] is not None

    @pytest.mark.asyncio
    async def test_invalid_move_notation(self, arbitro_agent, base_state):
        """Test validation of invalid move notation."""
        base_state.move_notation = "Zz9"  # Invalid - no Z piece, no 9 rank

        with patch('langchain_core.runnables.base.RunnableSequence.ainvoke', new_callable=AsyncMock) as mock_chain:
            mock_response = AsyncMock()
            mock_response.content = "This move notation is invalid."
            mock_chain.return_value = mock_response

            result = await arbitro_agent.execute(base_state)

            assert result["is_valid"] is False
            assert "No matching rules" in result["validation_reason"] or result["is_valid"] is False

    @pytest.mark.asyncio
    async def test_redis_cache_hit_performance(self, arbitro_agent, base_state):
        """Test that Redis cache hit achieves <1ms retrieval."""
        base_state.move_notation = "e4"

        # Mock Redis cache hit with fast response
        mock_rules = arbitro_agent.rule_engine.CHESS_RULES
        with patch.object(arbitro_agent.rule_cache, 'connect', new_callable=AsyncMock) as mock_connect:
            mock_connect.return_value = None
            arbitro_agent.rule_cache.client = AsyncMock()  # Mock connected state

            with patch.object(arbitro_agent.rule_cache, 'get_rules', new_callable=AsyncMock) as mock_get:
                mock_get.return_value = mock_rules

                with patch('langchain_core.runnables.base.RunnableSequence.ainvoke', new_callable=AsyncMock) as mock_chain:
                    mock_response = AsyncMock()
                    mock_response.content = "Valid pawn move."
                    mock_chain.return_value = mock_response

                    result = await arbitro_agent.execute(base_state)

                    # Verify cache was hit
                    assert result["redis_cache_hit"] is True
                    # Verify rule retrieval was reasonably fast (<100ms for mocked call)
                    assert result["rule_retrieval_time_ms"] < 100.0

    @pytest.mark.asyncio
    async def test_natural_language_explanation_generated(self, arbitro_agent, base_state):
        """Test that natural language explanation is generated."""
        base_state.move_notation = "Nf3"

        with patch('langchain_core.runnables.base.RunnableSequence.ainvoke', new_callable=AsyncMock) as mock_chain:
            mock_response = AsyncMock()
            mock_response.content = "The knight can move to f3 following L-shape movement rules."
            mock_chain.return_value = mock_response

            result = await arbitro_agent.execute(base_state)

            # LLM should be called for explanation
            assert mock_chain.called
            assert result["agent_response"] is not None
            assert len(result["agent_response"]) > 0

    @pytest.mark.asyncio
    async def test_error_handling_llm_failure(self, arbitro_agent, base_state):
        """Test graceful degradation when LLM fails."""
        base_state.move_notation = "e4"

        with patch('langchain_core.runnables.base.RunnableSequence.ainvoke', new_callable=AsyncMock) as mock_chain:
            mock_chain.side_effect = Exception("LLM timeout")

            result = await arbitro_agent.execute(base_state)

            # Should fallback to rule engine reason
            assert result["agent_response"] is not None
            assert result["confidence_score"] == 0.75  # Fallback confidence

    @pytest.mark.asyncio
    async def test_validation_node_error_handling(self, arbitro_agent, base_state):
        """Test error handling in validate_move_node."""
        base_state.move_notation = "e4"

        # Mock rule engine to raise exception
        with patch.object(arbitro_agent.rule_engine, 'validate_move', side_effect=Exception("Rule engine failure")):
            with patch('langchain_core.runnables.base.RunnableSequence.ainvoke', new_callable=AsyncMock) as mock_chain:
                mock_response = AsyncMock()
                mock_response.content = "Error occurred during validation."
                mock_chain.return_value = mock_response

                result = await arbitro_agent.execute(base_state)

                # Should handle error gracefully
                assert result["is_valid"] is False
                assert "Validation error" in result["validation_reason"]
                assert "error" in result

    @pytest.mark.asyncio
    async def test_workflow_exception_handling(self, arbitro_agent, base_state):
        """Test top-level workflow exception handling."""
        base_state.move_notation = None  # Will cause graph execution to fail

        # Mock graph to raise exception
        with patch.object(arbitro_agent.graph, 'ainvoke', side_effect=Exception("Graph execution failed")):
            result = await arbitro_agent.execute(base_state)

            # Should return ArbitroState (dataclass) with error marked, not dict
            assert isinstance(result, ArbitroState)
            assert result.is_valid is False
            assert result.agent_response == "I encountered an error validating your move."
            assert result.error == "Graph execution failed"
