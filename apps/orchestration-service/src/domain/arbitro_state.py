"""
ISSUE-3759: ArbitroState for Move Validation
Extended state for rules arbitration and move validation with real-time response.
"""

from dataclasses import dataclass, field
from typing import Optional
from uuid import UUID

from .state import GameAgentState


@dataclass
class ArbitroState(GameAgentState):
    """
    Extended state for Arbitro agent move validation.

    Maintains:
    - Move being validated
    - Game state snapshot
    - Applied rules and conflicts
    - Validation result with explanation
    """

    # Move validation context
    move_notation: Optional[str] = None
    game_state: Optional[str] = None  # Serialized game state (JSON/FEN)

    # Retrieved rules
    retrieved_rules: list[dict] = field(default_factory=list)
    applied_rule_ids: list[UUID] = field(default_factory=list)

    # Validation result
    is_valid: bool = False
    validation_reason: str = ""
    rule_conflicts: list[str] = field(default_factory=list)

    # Performance tracking
    redis_cache_hit: bool = False
    rule_retrieval_time_ms: float = 0.0

    def mark_valid(self, reason: str, applied_rules: list[UUID]) -> None:
        """Mark move as valid with explanation."""
        self.is_valid = True
        self.validation_reason = reason
        self.applied_rule_ids = applied_rules

    def mark_invalid(self, reason: str, violated_rules: list[UUID]) -> None:
        """Mark move as invalid with explanation."""
        self.is_valid = False
        self.validation_reason = reason
        self.applied_rule_ids = violated_rules

    def has_conflicts(self) -> bool:
        """Check if rule conflicts were detected."""
        return len(self.rule_conflicts) > 0
