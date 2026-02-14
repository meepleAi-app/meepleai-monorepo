"""
ISSUE-3776: Agent Switching Tests
Verify mid-conversation agent switching logic.
"""

import pytest
from uuid import uuid4

from src.application.switch_detector import SwitchAgentDetector
from src.domain import AgentType, GameAgentState, IntentType


class TestSwitchAgentDetector:
    """Test suite for agent switching detection."""

    @pytest.fixture
    def detector(self):
        """Create switch detector."""
        return SwitchAgentDetector()

    @pytest.fixture
    def state(self):
        """Create test state."""
        return GameAgentState(
            game_id=uuid4(),
            session_id=uuid4(),
            user_query="test query",
            intent=IntentType.GENERAL,
            current_agent=AgentType.TUTOR,
        )

    def test_detect_switch_to_arbitro(self, detector):
        """Test switching to Arbitro agent."""
        query = "Actually, let me switch to Arbitro to validate this move"
        result = detector.detect_switch(query, AgentType.TUTOR)
        assert result == AgentType.ARBITRO

    def test_detect_switch_to_decisore(self, detector):
        """Test switching to Decisore agent."""
        query = "Can Decisore suggest a better move instead?"
        result = detector.detect_switch(query, AgentType.ARBITRO)
        assert result == AgentType.DECISORE

    def test_detect_switch_to_tutor(self, detector):
        """Test switching to Tutor agent."""
        query = "Wait, let Tutor explain the rules first"
        result = detector.detect_switch(query, AgentType.DECISORE)
        assert result == AgentType.TUTOR

    def test_no_switch_without_trigger(self, detector):
        """Test no switch detected without trigger phrase."""
        query = "What about the tutor?"  # Mentions tutor but no switch trigger
        result = detector.detect_switch(query, AgentType.ARBITRO)
        assert result is None

    def test_no_switch_to_same_agent(self, detector):
        """Test no switch when requesting current agent."""
        query = "Switch to Tutor please"
        result = detector.detect_switch(query, AgentType.TUTOR)
        # Should return None because already on Tutor
        assert result is None

    def test_switch_with_ask_instead(self, detector):
        """Test switch detection with 'ask instead' phrase."""
        query = "Ask Decisore instead"
        result = detector.detect_switch(query, AgentType.TUTOR)
        assert result == AgentType.DECISORE

    def test_switch_case_insensitive(self, detector):
        """Test switch detection is case-insensitive."""
        query = "SWITCH TO ARBITRO"
        result = detector.detect_switch(query, AgentType.TUTOR)
        assert result == AgentType.ARBITRO

    def test_multi_agent_detection(self, detector):
        """Test detection of multi-agent queries."""
        query = "What's the best move and is it legal?"
        result = detector.requires_multi_agent(query)
        # Should detect both Decisore (best move) and Arbitro (legal)
        assert AgentType.DECISORE in result
        assert AgentType.ARBITRO in result

    def test_single_agent_not_multi(self, detector):
        """Test single-agent query returns empty list."""
        query = "What's the best move?"
        result = detector.requires_multi_agent(query)
        assert result == []


class TestOrchestratorSwitching:
    """Integration tests for agent switching in orchestrator."""

    @pytest.mark.asyncio
    async def test_route_entry_detects_switch(self):
        """Test _route_entry detects and honors switch requests."""
        from src.application.orchestrator import GameOrchestrator
        from unittest.mock import MagicMock

        orchestrator = GameOrchestrator()

        state = GameAgentState(
            game_id=uuid4(),
            session_id=uuid4(),
            user_query="Switch to Decisore for strategy",
            current_agent=AgentType.TUTOR,
        )

        route = orchestrator._route_entry(state)

        # Should route to Decisore due to switch detection
        assert route == "decisore"
        assert state.next_agent == AgentType.DECISORE

    @pytest.mark.asyncio
    async def test_no_switch_without_trigger(self):
        """Test routing continues normally without switch trigger."""
        from src.application.orchestrator import GameOrchestrator

        orchestrator = GameOrchestrator()

        state = GameAgentState(
            game_id=uuid4(),
            session_id=uuid4(),
            user_query="Tell me about arbitro",  # No switch trigger
            current_agent=AgentType.TUTOR,
        )

        route = orchestrator._route_entry(state)

        # Should go to classify, not switch
        assert route == "classify"
        assert state.next_agent is None
