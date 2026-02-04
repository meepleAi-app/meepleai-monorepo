"""
ISSUE-3495: Unit tests for LangGraph orchestrator.
Tests routing logic and state transitions.
"""

import pytest
from uuid import uuid4

from src.domain import GameAgentState, IntentType, AgentType, Message
from src.application import GameOrchestrator


class TestGameOrchestrator:
    """Test suite for LangGraph orchestrator."""

    @pytest.fixture
    def orchestrator(self):
        """Create orchestrator instance."""
        return GameOrchestrator()

    @pytest.fixture
    def base_state(self):
        """Create base game state."""
        return GameAgentState(
            game_id=uuid4(),
            session_id=uuid4(),
        )

    @pytest.mark.asyncio
    async def test_classify_setup_question(self, orchestrator, base_state):
        """Test intent classification for setup questions."""
        base_state.user_query = "How do I set up the game?"

        result = await orchestrator.execute(base_state)

        assert result.intent == IntentType.SETUP_QUESTION
        assert result.current_agent == AgentType.TUTOR
        assert result.agent_response is not None
        assert "TUTOR" in result.agent_response

    @pytest.mark.asyncio
    async def test_classify_rules_question(self, orchestrator, base_state):
        """Test intent classification for rules questions."""
        base_state.user_query = "What are the legal moves for this piece?"

        result = await orchestrator.execute(base_state)

        assert result.intent == IntentType.RULES_QUESTION
        assert result.current_agent == AgentType.TUTOR
        assert result.agent_response is not None

    @pytest.mark.asyncio
    async def test_classify_move_suggestion(self, orchestrator, base_state):
        """Test routing to Decisore for move suggestions."""
        base_state.user_query = "What's the best move in this situation?"

        result = await orchestrator.execute(base_state)

        assert result.intent == IntentType.MOVE_SUGGESTION
        assert result.current_agent == AgentType.DECISORE
        assert "DECISORE" in result.agent_response

    @pytest.mark.asyncio
    async def test_arbitro_route_with_pending_move(self, orchestrator, base_state):
        """Test routing to Arbitro when pending move exists."""
        from src.domain import Move

        base_state.pending_move = Move(move_notation="e2-e4", player="white")

        result = await orchestrator.execute(base_state)

        assert result.current_agent == AgentType.ARBITRO
        assert "ARBITRO" in result.agent_response

    @pytest.mark.asyncio
    async def test_workflow_sets_confidence_score(self, orchestrator, base_state):
        """Test that workflow sets confidence scores."""
        base_state.user_query = "Explain the game rules"

        result = await orchestrator.execute(base_state)

        assert result.confidence_score is not None
        assert 0.0 <= result.confidence_score <= 1.0

    @pytest.mark.asyncio
    async def test_workflow_handles_errors_gracefully(self, orchestrator):
        """Test error handling in workflow execution."""
        # Create invalid state
        invalid_state = GameAgentState(
            game_id=uuid4(),
            session_id=uuid4(),
            user_query="test query",
        )

        # Should not raise exception
        result = await orchestrator.execute(invalid_state)

        # Should complete with response
        assert result.agent_response is not None
    @pytest.mark.asyncio
    async def test_tutor_route_placeholder_response(self, orchestrator, base_state):
        """Test tutor agent placeholder response."""
        from unittest.mock import AsyncMock, MagicMock, patch

        base_state.current_agent = "tutor"
        base_state.user_query = "Test query"

        # Mock intent classifier
        with patch.object(orchestrator.intent_classifier, 'prompt') as mock_prompt:
            mock_chain = AsyncMock()
            mock_response = AsyncMock()
            mock_response.content = "Intent: SETUP\nConfidence: 0.9"
            mock_chain.ainvoke.return_value = mock_response
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

            result = await orchestrator._tutor_route_node(base_state)

            assert "agent_response" in result
            assert result["confidence_score"] >= 0.0

    def test_route_decision_defaults_to_tutor(self, base_state):
        """Test route_decision defaults to tutor for unknown intents."""
        from src.application.orchestrator import GameOrchestrator
        
        orch = GameOrchestrator()
        base_state.intent = None  # Unknown intent
        
        result = orch._route_decision(base_state)
        
        assert result == "tutor"
