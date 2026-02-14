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
    async def test_classify_move_validation(self, orchestrator, base_state):
        """Test routing to Arbitro for move validation queries."""
        base_state.user_query = "Is this move legal?"

        result = await orchestrator.execute(base_state)

        assert result.intent == IntentType.MOVE_VALIDATION
        assert result.current_agent == AgentType.ARBITRO

    @pytest.mark.asyncio
    async def test_classify_general_query(self, orchestrator, base_state):
        """Test routing to Tutor for general queries."""
        base_state.user_query = "Tell me about this game"

        result = await orchestrator.execute(base_state)

        assert result.intent == IntentType.GENERAL
        assert result.current_agent == AgentType.TUTOR

    @pytest.mark.asyncio
    async def test_agent_switching_mid_conversation(self, orchestrator, base_state):
        """Test agent switching detection and routing."""
        # Start with Tutor context
        base_state.user_query = "Actually, switch to Decisore for strategy"
        base_state.current_agent = AgentType.TUTOR

        # Mock decisore API
        from unittest.mock import AsyncMock
        orchestrator.api_client.decisore_analyze = AsyncMock(return_value={
            "suggestions": [{"move": {"notation": "e2-e4"}, "reasoning": "Control"}],
            "overallConfidence": 0.85,
        })

        result = await orchestrator.execute(base_state)

        # Should have switched to Decisore
        assert result.current_agent == AgentType.DECISORE
        assert result.next_agent == AgentType.DECISORE

    def test_route_entry_direct_to_tutor_with_intent(self, orchestrator):
        """Test direct routing when intent already classified."""
        state = GameAgentState(
            game_id=uuid4(),
            session_id=uuid4(),
            intent=IntentType.SETUP_QUESTION,
        )

        route = orchestrator._route_entry(state)

        assert route == "tutor"

    def test_route_entry_direct_to_arbitro_with_pending_move(self, orchestrator):
        """Test priority routing to Arbitro for pending moves."""
        from src.domain import Move

        state = GameAgentState(
            game_id=uuid4(),
            session_id=uuid4(),
            pending_move=Move(move_notation="e2-e4", player="white"),
            intent=IntentType.SETUP_QUESTION,  # Even with different intent
        )

        route = orchestrator._route_entry(state)

        # Pending move takes priority
        assert route == "arbitro"

    def test_route_entry_defaults_to_classify(self, orchestrator):
        """Test default routing to classification when no clear signal."""
        state = GameAgentState(
            game_id=uuid4(),
            session_id=uuid4(),
            user_query="ambiguous query",
        )

        route = orchestrator._route_entry(state)

        assert route == "classify"