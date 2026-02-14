"""
ISSUE-3776: Context Preservation Tests
Verify conversation history flows correctly across agent transitions.
"""

import pytest
from unittest.mock import AsyncMock, patch
from uuid import uuid4

from src.application.orchestrator import GameOrchestrator
from src.domain import GameAgentState, AgentType, IntentType, Message
from datetime import datetime


class TestContextPreservation:
    """Test suite for conversation context preservation."""

    @pytest.fixture
    def orchestrator(self):
        """Create orchestrator with mocked API client."""
        with patch('src.application.orchestrator.MeepleAIApiClient') as mock_client_class:
            mock_client = mock_client_class.return_value
            orchestrator = GameOrchestrator(api_client=mock_client)
            return orchestrator

    @pytest.mark.asyncio
    async def test_tutor_preserves_conversation_history(self, orchestrator):
        """Test Tutor node appends to conversation_history."""
        initial_history = [
            Message(role="user", content="Previous question", timestamp=datetime.utcnow())
        ]

        state = GameAgentState(
            game_id=uuid4(),
            session_id=uuid4(),
            user_query="How do I set up chess?",
            intent=IntentType.SETUP_QUESTION,
            conversation_history=initial_history.copy(),
        )

        # Mock API response
        orchestrator.api_client.tutor_query = AsyncMock(return_value={
            "response": "Here's how to set up chess",
            "confidence": 0.92,
            "citations": [],
        })

        result = await orchestrator._tutor_node(state)

        # Verify conversation history was extended
        assert "conversation_history" in result
        updated_history = result["conversation_history"]
        assert len(updated_history) == 3  # Initial + user message + assistant response
        assert updated_history[-2].role == "user"
        assert updated_history[-2].content == "How do I set up chess?"
        assert updated_history[-1].role == "assistant"
        assert updated_history[-1].metadata["agent"] == "tutor"

    @pytest.mark.asyncio
    async def test_decisore_preserves_conversation_history(self, orchestrator):
        """Test Decisore node appends to conversation_history."""
        initial_history = [
            Message(role="user", content="Previous analysis", timestamp=datetime.utcnow())
        ]

        state = GameAgentState(
            game_id=uuid4(),
            session_id=uuid4(),
            intent=IntentType.MOVE_SUGGESTION,
            conversation_history=initial_history.copy(),
        )

        # Mock API response
        orchestrator.api_client.decisore_analyze = AsyncMock(return_value={
            "suggestions": [{"move": {"notation": "e2-e4"}, "reasoning": "Control center"}],
            "overallConfidence": 0.88,
        })

        result = await orchestrator._decisore_node(state)

        # Verify conversation history was extended
        updated_history = result["conversation_history"]
        assert len(updated_history) == 2  # Initial + assistant response
        assert updated_history[-1].metadata["agent"] == "decisore"

    @pytest.mark.asyncio
    async def test_context_flows_across_agent_switch(self, orchestrator):
        """Test conversation context is preserved when switching agents."""
        # Start with Tutor
        state = GameAgentState(
            game_id=uuid4(),
            session_id=uuid4(),
            user_query="How do I set up chess?",
            intent=IntentType.SETUP_QUESTION,
            conversation_history=[],
        )

        orchestrator.api_client.tutor_query = AsyncMock(return_value={
            "response": "Place pieces on the back rank",
            "confidence": 0.90,
            "citations": [],
        })

        # Execute Tutor
        tutor_result = await orchestrator._tutor_node(state)

        # Create new state for Decisore with preserved history
        state_after_tutor = GameAgentState(
            game_id=state.game_id,
            session_id=state.session_id,
            user_query="Now suggest my first move",
            intent=IntentType.MOVE_SUGGESTION,
            conversation_history=tutor_result["conversation_history"],
        )

        orchestrator.api_client.decisore_analyze = AsyncMock(return_value={
            "suggestions": [{"move": {"notation": "e2-e4"}, "reasoning": "Control"}],
            "overallConfidence": 0.85,
        })

        # Execute Decisore
        decisore_result = await orchestrator._decisore_node(state_after_tutor)

        # Verify full conversation history preserved
        final_history = decisore_result["conversation_history"]
        assert len(final_history) == 3  # Tutor Q + Tutor A + Decisore A
        assert any("chess" in msg.content.lower() for msg in final_history)
        assert final_history[-1].metadata["agent"] == "decisore"

    @pytest.mark.asyncio
    async def test_empty_history_initializes_correctly(self, orchestrator):
        """Test empty conversation history is handled correctly."""
        state = GameAgentState(
            game_id=uuid4(),
            session_id=uuid4(),
            user_query="Test query",
            conversation_history=[],  # Empty history
        )

        orchestrator.api_client.tutor_query = AsyncMock(return_value={
            "response": "Test response",
            "confidence": 0.85,
            "citations": [],
        })

        result = await orchestrator._tutor_node(state)

        # Should create new history
        assert len(result["conversation_history"]) == 2  # User + Assistant

    @pytest.mark.asyncio
    async def test_history_metadata_preserved(self, orchestrator):
        """Test conversation history metadata (role, agent) is correct."""
        state = GameAgentState(
            game_id=uuid4(),
            session_id=uuid4(),
            user_query="Test",
            conversation_history=[],
        )

        orchestrator.api_client.tutor_query = AsyncMock(return_value={
            "response": "Answer",
            "confidence": 0.9,
            "citations": [],
        })

        result = await orchestrator._tutor_node(state)

        history = result["conversation_history"]
        user_msg = history[-2]
        assistant_msg = history[-1]

        assert user_msg.role == "user"
        assert user_msg.content == "Test"
        assert assistant_msg.role == "assistant"
        assert assistant_msg.metadata["agent"] == "tutor"
        assert assistant_msg.content == "Answer"
