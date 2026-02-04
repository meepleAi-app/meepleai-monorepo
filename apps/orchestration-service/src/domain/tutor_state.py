"""
ISSUE-3497: TutorState for Multi-Turn Dialogue
Extended state for maintaining conversation context across 10+ turns.
"""

from dataclasses import dataclass, field
from datetime import datetime
from typing import Optional
from uuid import UUID

from .state import GameAgentState, Message


@dataclass
class TutorState(GameAgentState):
    """
    Extended state for Tutor agent multi-turn conversations.

    Maintains:
    - Full conversation history
    - Turn count tracking
    - Conversation summary (for >10 turns)
    - Context from previous interactions
    """

    # Turn tracking
    turn_count: int = 0
    max_turns_before_summary: int = 10

    # Conversation summarization
    conversation_summary: Optional[str] = None
    needs_summarization: bool = False

    # Context from previous turns
    previous_topics: list[str] = field(default_factory=list)
    unresolved_questions: list[str] = field(default_factory=list)

    # Retrieved context for current turn
    retrieved_chunks: list[dict] = field(default_factory=list)
    search_query: Optional[str] = None

    # Generation parameters
    temperature: float = 0.7
    max_response_tokens: int = 500

    def add_turn(self, user_message: str, assistant_message: str) -> None:
        """
        Add a conversation turn and update tracking.

        Args:
            user_message: User's message
            assistant_message: Assistant's response
        """
        self.conversation_history.append(
            Message(role="user", content=user_message, timestamp=datetime.utcnow())
        )
        self.conversation_history.append(
            Message(role="assistant", content=assistant_message, timestamp=datetime.utcnow())
        )
        self.turn_count += 1

        # Check if summarization needed
        if self.turn_count >= self.max_turns_before_summary and not self.conversation_summary:
            self.needs_summarization = True

    def get_recent_context(self, num_turns: int = 3) -> list[Message]:
        """
        Get recent conversation history for context window.

        Args:
            num_turns: Number of recent turns to retrieve

        Returns:
            List of recent messages (user + assistant pairs)
        """
        # Get last N*2 messages (N turns = N user + N assistant messages)
        return self.conversation_history[-(num_turns * 2):]

    def should_summarize(self) -> bool:
        """Check if conversation should be summarized."""
        return self.needs_summarization or self.turn_count >= self.max_turns_before_summary
