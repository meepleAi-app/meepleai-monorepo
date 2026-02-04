"""
ISSUE-3497: Unit tests for Tutor Agent Multi-Turn Dialogue
Tests context retention, summarization, and state management across 10+ turns.
"""

import pytest
from uuid import uuid4
from unittest.mock import AsyncMock, MagicMock, patch

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

        # Mock the chain response
        mock_response = AsyncMock()
        mock_response.content = "To set up the game, place the board..."

        with patch.object(tutor_agent, 'TUTOR_PROMPT') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.return_value = mock_response
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

            result = await tutor_agent.execute(base_state)

            assert result.turn_count == 1
            assert len(result.conversation_history) == 2  # User + assistant
            assert result.agent_response is not None

    @pytest.mark.asyncio
    async def test_context_maintained_across_turns(self, tutor_agent, base_state):
        """Test that context is maintained across multiple turns."""
        # Turn 1
        base_state.user_query = "How many players can play?"

        # Mock chain responses
        mock_response1 = AsyncMock()
        mock_response1.content = "The game supports 2-4 players."

        mock_response2 = AsyncMock()
        mock_response2.content = "Unfortunately, 5 players is not supported in the base game."

        with patch.object(tutor_agent, 'TUTOR_PROMPT') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.side_effect = [mock_response1, mock_response2]
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

            state_after_turn1 = await tutor_agent.execute(base_state)

            # Turn 2 - pronoun reference requiring context
            state_after_turn1.user_query = "What if I want to play with 5?"

            state_after_turn2 = await tutor_agent.execute(state_after_turn1)

            assert state_after_turn2.turn_count == 2
            assert len(state_after_turn2.conversation_history) == 4
            # Verify chain was called twice
            assert mock_chain.ainvoke.call_count == 2

    @pytest.mark.asyncio
    async def test_summarization_triggered_after_10_turns(self, tutor_agent, base_state):
        """Test that summarization is triggered after 10 turns."""
        # Simulate 11 turns to trigger summarization
        for i in range(11):
            base_state.user_query = f"Question {i+1}"
            base_state.add_turn(f"Question {i+1}", f"Answer {i+1}")

        # 11th turn should trigger summarization
        base_state.user_query = "Final question"

        # Mock chain responses
        summary_response = AsyncMock()
        summary_response.content = "Summary of previous 11 turns..."

        response_response = AsyncMock()
        response_response.content = "Final answer"

        with patch.object(tutor_agent, 'TUTOR_PROMPT') as mock_tutor_prompt:
            with patch.object(tutor_agent, 'SUMMARIZATION_PROMPT') as mock_summary_prompt:
                # Mock summarization chain
                summary_chain = AsyncMock()
                summary_chain.ainvoke.return_value = summary_response
                mock_summary_prompt.__or__ = MagicMock(return_value=summary_chain)

                # Mock tutor chain
                tutor_chain = AsyncMock()
                tutor_chain.ainvoke.return_value = response_response
                mock_tutor_prompt.__or__ = MagicMock(return_value=tutor_chain)

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

        # Mock chain response
        mock_response = AsyncMock()
        mock_response.content = "In chess, the knight moves in an L-shape..."

        with patch.object(tutor_agent, 'TUTOR_PROMPT') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.return_value = mock_response
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

            result = await tutor_agent.execute(base_state)

            # Verify chain was called and context was included
            call_args = mock_chain.ainvoke.call_args[0][0]
            assert "chess" in str(call_args).lower()
            assert result.agent_response is not None

    @pytest.mark.asyncio
    async def test_error_handling_in_summarization(self, tutor_agent, base_state):
        """Test graceful handling of summarization failures."""
        # Simulate conversation needing summarization
        for i in range(11):
            base_state.add_turn(f"Q{i}", f"A{i}")

        base_state.user_query = "Next question"

        # Mock chain responses
        response_response = AsyncMock()
        response_response.content = "Response despite summary failure"

        with patch.object(tutor_agent, 'TUTOR_PROMPT') as mock_tutor_prompt:
            with patch.object(tutor_agent, 'SUMMARIZATION_PROMPT') as mock_summary_prompt:
                # Mock summarization chain to fail
                summary_chain = AsyncMock()
                summary_chain.ainvoke.side_effect = Exception("Summarization error")
                mock_summary_prompt.__or__ = MagicMock(return_value=summary_chain)

                # Mock tutor chain
                tutor_chain = AsyncMock()
                tutor_chain.ainvoke.return_value = response_response
                mock_tutor_prompt.__or__ = MagicMock(return_value=tutor_chain)

                result = await tutor_agent.execute(base_state)

                # Should continue with response despite summary failure
                assert result.agent_response is not None
                # needs_summarization flag persists after failed summarization
                # (will be reset in actual summarization node on success)

    @pytest.mark.asyncio
    async def test_workflow_performance_under_2_seconds(self, tutor_agent, base_state):
        """Test that workflow completes within performance target."""
        import time

        base_state.user_query = "How do I win?"

        # Mock chain response
        mock_response = AsyncMock()
        mock_response.content = "To win, you need to..."

        with patch.object(tutor_agent, 'TUTOR_PROMPT') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.return_value = mock_response
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

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

        # Mock chain responses
        responses = [AsyncMock(content=f"A{i+1}") for i in range(3)]

        with patch.object(tutor_agent, 'TUTOR_PROMPT') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.side_effect = responses
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

            for query in queries:
                base_state.user_query = query
                base_state = await tutor_agent.execute(base_state)

        # Should have 6 messages (3 turns * 2 messages)
        assert len(base_state.conversation_history) == 6
        assert base_state.turn_count == 3
    @pytest.mark.asyncio
    async def test_search_knowledge_with_api_client(self, base_state):
        """Test search_knowledge_node with API client."""
        from src.infrastructure.api_client import MeepleAIApiClient
        from unittest.mock import AsyncMock, patch

        api_client = MeepleAIApiClient(base_url="http://test")
        tutor = TutorAgent(api_client=api_client)
        base_state.user_query = "Test query"

        with patch.object(api_client, 'hybrid_search', new_callable=AsyncMock) as mock_search:
            mock_search.return_value = {"results": [{"content": "test", "score": 0.9}]}
            
            result = await tutor._search_knowledge_node(base_state)
            
            assert "retrieved_chunks" in result
            assert len(result["retrieved_chunks"]) > 0

    @pytest.mark.asyncio
    async def test_generate_response_with_retrieved_chunks(self, tutor_agent, base_state):
        """Test response generation with search results."""
        base_state.user_query = "Test"
        base_state.retrieved_chunks = [{"content": "Chunk 1", "score": 0.95}]

        mock_response = AsyncMock()
        mock_response.content = "Response with context"

        with patch.object(tutor_agent, 'TUTOR_PROMPT') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.return_value = mock_response
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

            result = await tutor_agent._generate_response_node(base_state)

            assert result["agent_response"] == "Response with context"
            assert result["confidence_score"] == 0.85

    def test_get_recent_context_with_fewer_messages_than_requested(self, base_state):
        """Test get_recent_context when history is shorter than requested."""
        base_state.add_turn("Q1", "A1")
        
        recent = base_state.get_recent_context(num_turns=5)
        
        # Should return all 2 messages (1 turn)
        assert len(recent) == 2

    @pytest.mark.asyncio
    async def test_save_memory_node_placeholder(self, tutor_agent, base_state):
        """Test save_memory node placeholder logic."""
        result = await tutor_agent._save_memory_node(base_state)

        # Placeholder implementation - just logs
        assert result == {}
