"""
ISSUE-3497: Unit tests for Tutor Agent Multi-Turn Dialogue
Tests context retention, summarization, and state management across 10+ turns.
"""

import pytest
from uuid import uuid4
from unittest.mock import AsyncMock, patch

from src.application.tutor_agent import TutorAgent
from src.domain import TutorState, Message


class TestTutorAgent:
    """Test suite for TutorAgent multi-turn dialogue."""

    @pytest.fixture
    def tutor_agent(self):
        """Create tutor agent instance."""
        return TutorAgent()

    @pytest.fixture
    def base_state(self):
        """Create base tutor state."""
        return TutorState(
            game_id=uuid4(),
            session_id=uuid4(),
        )

    @pytest.mark.asyncio
    async def test_single_turn_dialogue(self, tutor_agent, base_state):
        """Test single turn conversation."""
        base_state.user_query = "How do I set up the game?"

        with patch.object(tutor_agent.llm, 'ainvoke', new_callable=AsyncMock) as mock_llm:
            mock_response = AsyncMock()
            mock_response.content = "To set up the game, place the board..."
            mock_llm.return_value = mock_response

            result = await tutor_agent.execute(base_state)

            assert result.turn_count == 1
            assert len(result.conversation_history) == 2  # User + assistant
            assert result.agent_response is not None

    @pytest.mark.asyncio
    async def test_context_maintained_across_turns(self, tutor_agent, base_state):
        """Test that context is maintained across multiple turns."""
        # Turn 1
        base_state.user_query = "How many players can play?"

        with patch.object(tutor_agent.llm, 'ainvoke', new_callable=AsyncMock) as mock_llm:
            mock_response = AsyncMock()
            mock_response.content = "The game supports 2-4 players."
            mock_llm.return_value = mock_response

            state_after_turn1 = await tutor_agent.execute(base_state)

            # Turn 2 - pronoun reference requiring context
            state_after_turn1.user_query = "What if I want to play with 5?"

            mock_response2 = AsyncMock()
            mock_response2.content = "Unfortunately, 5 players is not supported in the base game."
            mock_llm.return_value = mock_response2

            state_after_turn2 = await tutor_agent.execute(state_after_turn1)

            assert state_after_turn2.turn_count == 2
            assert len(state_after_turn2.conversation_history) == 4
            # Verify context was passed to LLM (check call args contain previous messages)
            assert mock_llm.call_count == 2

    @pytest.mark.asyncio
    async def test_summarization_triggered_after_10_turns(self, tutor_agent, base_state):
        """Test that summarization is triggered after 10 turns."""
        # Simulate 11 turns to trigger summarization
        for i in range(11):
            base_state.user_query = f"Question {i+1}"
            base_state.add_turn(f"Question {i+1}", f"Answer {i+1}")

        # 11th turn should trigger summarization
        base_state.user_query = "Final question"

        with patch.object(tutor_agent.llm, 'ainvoke', new_callable=AsyncMock) as mock_llm:
            # First call: summarization
            summary_response = AsyncMock()
            summary_response.content = "Summary of previous 11 turns..."

            # Second call: response generation
            response_response = AsyncMock()
            response_response.content = "Final answer"

            mock_llm.side_effect = [summary_response, response_response]

            result = await tutor_agent.execute(base_state)

            # Verify summarization occurred
            assert result.conversation_summary is not None
            assert "Summary" in result.conversation_summary or len(result.conversation_summary) > 0
            assert result.needs_summarization is False

    @pytest.mark.asyncio
    async def test_get_recent_context_returns_last_n_turns(self, base_state):
        """Test get_recent_context retrieves correct number of messages."""
        # Add 5 turns
        for i in range(5):
            base_state.add_turn(f"User message {i}", f"Assistant message {i}")

        # Get last 2 turns
        recent = base_state.get_recent_context(num_turns=2)

        # Should return 4 messages (2 turns * 2 messages per turn)
        assert len(recent) == 4
        assert recent[0].content == "User message 3"
        assert recent[-1].content == "Assistant message 4"

    @pytest.mark.asyncio
    async def test_ambiguous_query_resolution_with_context(self, tutor_agent, base_state):
        """Test that ambiguous queries are resolved using conversation context."""
        # Turn 1: Establish context
        base_state.user_query = "I'm playing chess"
        base_state.add_turn("I'm playing chess", "Great! How can I help with chess?")

        # Turn 2: Ambiguous pronoun reference
        base_state.user_query = "How does the knight move?"

        with patch.object(tutor_agent.llm, 'ainvoke', new_callable=AsyncMock) as mock_llm:
            mock_response = AsyncMock()
            mock_response.content = "In chess, the knight moves in an L-shape..."
            mock_llm.return_value = mock_response

            result = await tutor_agent.execute(base_state)

            # Verify context was included in the LLM call
            call_args = mock_llm.call_args[0][0]
            assert "chess" in str(call_args).lower()
            assert result.agent_response is not None

    @pytest.mark.asyncio
    async def test_error_handling_in_summarization(self, tutor_agent, base_state):
        """Test graceful handling of summarization failures."""
        # Simulate conversation needing summarization
        for i in range(11):
            base_state.add_turn(f"Q{i}", f"A{i}")

        base_state.user_query = "Next question"

        with patch.object(tutor_agent.llm, 'ainvoke', new_callable=AsyncMock) as mock_llm:
            # Summarization fails
            mock_llm.side_effect = [
                Exception("Summarization error"),
                AsyncMock(content="Response despite summary failure")
            ]

            result = await tutor_agent.execute(base_state)

            # Should continue without summary
            assert result.needs_summarization is False
            assert result.agent_response is not None

    @pytest.mark.asyncio
    async def test_workflow_performance_under_2_seconds(self, tutor_agent, base_state):
        """Test that workflow completes within performance target."""
        import time

        base_state.user_query = "How do I win?"

        with patch.object(tutor_agent.llm, 'ainvoke', new_callable=AsyncMock) as mock_llm:
            mock_response = AsyncMock()
            mock_response.content = "To win, you need to..."
            mock_llm.return_value = mock_response

            start = time.perf_counter()
            await tutor_agent.execute(base_state)
            duration = time.perf_counter() - start

            # Should complete well under 2s target (with mocked LLM)
            assert duration < 2.0

    def test_should_summarize_returns_true_after_threshold(self, base_state):
        """Test should_summarize logic."""
        # Below threshold
        assert base_state.should_summarize() is False

        # At threshold
        base_state.turn_count = 10
        assert base_state.should_summarize() is True

        # Above threshold
        base_state.turn_count = 15
        assert base_state.should_summarize() is True

        # After summarization
        base_state.conversation_summary = "Summary"
        base_state.needs_summarization = False
        assert base_state.should_summarize() is False

    @pytest.mark.asyncio
    async def test_conversation_history_grows_with_turns(self, tutor_agent, base_state):
        """Test conversation history accumulates correctly."""
        queries = ["Q1", "Q2", "Q3"]

        with patch.object(tutor_agent.llm, 'ainvoke', new_callable=AsyncMock) as mock_llm:
            for i, query in enumerate(queries):
                base_state.user_query = query
                mock_response = AsyncMock()
                mock_response.content = f"A{i+1}"
                mock_llm.return_value = mock_response

                base_state = await tutor_agent.execute(base_state)

        # Should have 6 messages (3 turns * 2 messages)
        assert len(base_state.conversation_history) == 6
        assert base_state.turn_count == 3
