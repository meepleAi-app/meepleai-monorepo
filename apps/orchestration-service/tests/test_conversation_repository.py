"""
ISSUE-3497: Integration tests for ConversationRepository
Tests PostgreSQL state persistence and retrieval.
"""

import pytest
from uuid import uuid4
from datetime import datetime
from unittest.mock import AsyncMock, MagicMock, patch

from src.infrastructure.conversation_repository import ConversationRepository
from src.domain import TutorState, Message


class TestConversationRepository:
    """Test suite for conversation persistence."""

    @pytest.fixture
    def repository(self):
        """Create repository instance."""
        return ConversationRepository(database_url="postgresql://test:test@localhost/test")

    @pytest.fixture
    def sample_state(self):
        """Create sample tutor state."""
        state = TutorState(
            game_id=uuid4(),
            session_id=uuid4(),
            user_query="How do I play?",
        )
        state.add_turn("How do I play?", "Here's how to play...")
        state.turn_count = 1
        return state

    @pytest.mark.asyncio
    async def test_save_state_serializes_correctly(self, repository, sample_state):
        """Test state serialization to database."""
        mock_pool = AsyncMock()
        mock_conn = AsyncMock()
        mock_pool.acquire.return_value.__aenter__.return_value = mock_conn

        repository.pool = mock_pool

        await repository.save_state(sample_state)

        # Verify execute was called with correct SQL
        mock_conn.execute.assert_called_once()
        call_args = mock_conn.execute.call_args[0]
        assert "INSERT INTO" in call_args[0]
        assert "ConversationMemory" in call_args[0]

    @pytest.mark.asyncio
    async def test_load_state_deserializes_correctly(self, repository):
        """Test state deserialization from database."""
        session_id = uuid4()
        game_id = uuid4()

        # Mock database row
        mock_row = {
            "GameId": game_id,
            "SessionId": session_id,
            "ConversationHistory": '[{"role": "user", "content": "Hello", "timestamp": "2026-02-04T10:00:00"}]',
            "Metadata": '{"turn_count": 1, "conversation_summary": null, "previous_topics": [], "intent": "setup"}',
            "CreatedAt": datetime.utcnow(),
        }

        mock_pool = AsyncMock()
        mock_conn = AsyncMock()
        mock_conn.fetchrow.return_value = mock_row
        mock_pool.acquire.return_value.__aenter__.return_value = mock_conn

        repository.pool = mock_pool

        state = await repository.load_state(session_id)

        assert state is not None
        assert state.session_id == session_id
        assert state.turn_count == 1
        assert len(state.conversation_history) == 1

    @pytest.mark.asyncio
    async def test_save_without_pool_logs_warning(self, repository, sample_state):
        """Test graceful handling when database unavailable."""
        repository.pool = None

        # Should not raise exception
        await repository.save_state(sample_state)

    @pytest.mark.asyncio
    async def test_load_without_pool_returns_none(self, repository):
        """Test graceful handling when database unavailable."""
        repository.pool = None

        state = await repository.load_state(uuid4())

        assert state is None

    @pytest.mark.asyncio
    async def test_load_nonexistent_session_returns_none(self, repository):
        """Test loading non-existent session."""
        mock_pool = AsyncMock()
        mock_conn = AsyncMock()
        mock_conn.fetchrow.return_value = None  # No row found
        mock_pool.acquire.return_value.__aenter__.return_value = mock_conn

        repository.pool = mock_pool

        state = await repository.load_state(uuid4())

        assert state is None
