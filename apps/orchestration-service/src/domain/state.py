"""
ISSUE-3495: LangGraph State Definitions
Domain models for multi-agent orchestration state.
"""

from dataclasses import dataclass, field
from datetime import datetime, UTC
from enum import Enum
from typing import Optional
from uuid import UUID


class AgentType(str, Enum):
    """Agent types in the multi-agent system."""
    TUTOR = "tutor"
    ARBITRO = "arbitro"
    DECISORE = "decisore"


class IntentType(str, Enum):
    """User intent classification for routing."""
    SETUP_QUESTION = "setup"
    RULES_QUESTION = "rules"
    MOVE_VALIDATION = "move_validation"
    MOVE_SUGGESTION = "move_suggestion"
    GENERAL = "general"


@dataclass
class Message:
    """Conversation message."""
    role: str  # "user" or "assistant"
    content: str
    timestamp: datetime = field(default_factory=lambda: datetime.now(UTC))
    metadata: dict = field(default_factory=dict)


@dataclass
class BoardState:
    """Game board state representation."""
    game_id: UUID
    current_player: str
    board_data: dict  # Game-specific board representation
    legal_moves: list[str] = field(default_factory=list)
    last_move: Optional[str] = None


@dataclass
class Move:
    """Pending move for validation."""
    move_notation: str
    player: str
    timestamp: datetime = field(default_factory=lambda: datetime.now(UTC))


@dataclass
class GameAgentState:
    """
    ISSUE-3495: LangGraph state for multi-agent game orchestration.

    This state is passed through the LangGraph workflow and maintains
    context across agent transitions (Tutor → Arbitro → Decisore).
    """
    # Session identifiers
    game_id: UUID
    session_id: UUID

    # User interaction
    user_query: Optional[str] = None
    intent: Optional[IntentType] = None

    # Game state
    board_state: Optional[BoardState] = None
    pending_move: Optional[Move] = None

    # Agent routing
    current_agent: Optional[AgentType] = None
    next_agent: Optional[AgentType] = None

    # Conversation context
    conversation_history: list[Message] = field(default_factory=list)

    # Agent outputs
    agent_response: Optional[str] = None
    confidence_score: Optional[float] = None
    citations: list[str] = field(default_factory=list)

    # Error handling
    error: Optional[str] = None
    retry_count: int = 0

    # Metadata
    created_at: datetime = field(default_factory=lambda: datetime.now(UTC))
    updated_at: datetime = field(default_factory=lambda: datetime.now(UTC))


@dataclass
class WorkflowResult:
    """Result from workflow execution."""
    agent_type: AgentType
    response: str
    confidence: float
    citations: list[str]
    state: GameAgentState
    execution_time_ms: float
    error: Optional[str] = None
