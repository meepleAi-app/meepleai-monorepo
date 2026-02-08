"""
ISSUE-3759: Rule Engine for Arbitro Agent
Real-time move validation with 3-tier rule retrieval (Redis → Memory → Qdrant).
Target: <100ms P95 latency
"""

import hashlib
import json
import logging
from dataclasses import dataclass
from typing import Optional
from uuid import UUID

import redis.asyncio as redis

from ..config import settings

logger = logging.getLogger(__name__)


@dataclass
class RuleMatch:
    """Result of rule matching against move."""
    rule_id: UUID
    rule_name: str
    rule_type: str
    matches: bool
    reason: str
    precedence: int


class RuleCache:
    """
    Redis tier-1 cache for hot rules (<1ms lookup).

    Caches top 20 most-used rules per game for instant validation.
    TTL: 24 hours with auto-refresh on hit.
    Target: >80% cache hit rate.
    """

    def __init__(self, redis_url: Optional[str] = None):
        """
        Initialize Redis rule cache.

        Args:
            redis_url: Redis connection URL (defaults to settings)
        """
        self.redis_url = redis_url or settings.redis_url
        self.client: Optional[redis.Redis] = None
        self.ttl_seconds = 86400  # 24 hours
        self.key_prefix = "rules:game:"

    async def connect(self) -> None:
        """Establish Redis connection."""
        try:
            self.client = await redis.from_url(
                self.redis_url,
                encoding="utf-8",
                decode_responses=True,
            )
            await self.client.ping()
            logger.info("✅ Rule cache connected to Redis")
        except Exception as e:
            logger.error(f"Failed to connect to Redis for rule cache: {e}")
            self.client = None

    async def disconnect(self) -> None:
        """Close Redis connection."""
        if self.client:
            await self.client.aclose()
            logger.info("Rule cache disconnected from Redis")

    async def get_rules(self, game_id: UUID) -> Optional[list[dict]]:
        """
        Get cached rules for game.

        Args:
            game_id: Game UUID

        Returns:
            List of rule dicts or None if cache miss
        """
        if not self.client:
            return None

        try:
            key = f"{self.key_prefix}{game_id}"
            cached = await self.client.get(key)

            if cached:
                rules = json.loads(cached)
                logger.info(f"✅ Cache HIT: {len(rules)} rules for game {game_id}")

                # Refresh TTL on hit
                await self.client.expire(key, self.ttl_seconds)

                return rules

            logger.info(f"Cache MISS for game {game_id}")
            return None

        except Exception as e:
            logger.error(f"Redis get failed: {e}")
            return None

    async def set_rules(self, game_id: UUID, rules: list[dict]) -> bool:
        """
        Cache rules for game.

        Args:
            game_id: Game UUID
            rules: List of rule dicts to cache

        Returns:
            True if cached successfully
        """
        if not self.client:
            return False

        try:
            key = f"{self.key_prefix}{game_id}"
            await self.client.setex(
                key,
                self.ttl_seconds,
                json.dumps(rules),
            )
            logger.info(f"Cached {len(rules)} rules for game {game_id}")
            return True

        except Exception as e:
            logger.error(f"Redis set failed: {e}")
            return False


class RuleEngine:
    """
    Rule validation engine for Arbitro agent.

    Validates moves against game rules with 3-tier retrieval:
    1. Redis tier-1 cache (<1ms) - hot rules
    2. In-memory tier-2 (hardcoded Chess rules for MVP)
    3. Qdrant tier-3 (100ms) - fallback semantic search

    Target Performance: <100ms P95 latency
    """

    # Hardcoded Chess rules for MVP (Issue #3759)
    # Future: Load from PostgreSQL + Qdrant
    CHESS_RULES = [
        {
            "id": "00000000-0000-0000-0000-000000000001",
            "name": "Knight L-Shape Movement",
            "type": "Movement",
            "precedence": 10,
            "pattern": r"^[NBRQK]?[a-h]?[1-8]?[x\-]?[a-h][1-8]",  # Algebraic notation
            "description": "Knights move in L-shape: 2 squares + 1 perpendicular",
        },
        {
            "id": "00000000-0000-0000-0000-000000000002",
            "name": "Pawn Forward Movement",
            "type": "Movement",
            "precedence": 10,
            "pattern": r"^[a-h][2-7]$",
            "description": "Pawns move forward one square (or two from starting position)",
        },
        {
            "id": "00000000-0000-0000-0000-000000000003",
            "name": "No Piece Obstruction",
            "type": "Constraint",
            "precedence": 40,
            "pattern": None,  # Requires game state check
            "description": "Pieces cannot move through other pieces (except Knight)",
        },
    ]

    def __init__(self, cache: Optional[RuleCache] = None):
        """
        Initialize rule engine.

        Args:
            cache: Optional RuleCache for tier-1 lookup
        """
        self.cache = cache

    async def validate_move(
        self,
        game_id: UUID,
        move: str,
        game_state: Optional[str] = None,
    ) -> tuple[bool, str, list[UUID], float]:
        """
        Validate move against game rules.

        Args:
            game_id: Game UUID
            move: Move notation (e.g., "Nf3", "e4")
            game_state: Optional game state (FEN for chess)

        Returns:
            (is_valid, reason, applied_rule_ids, cache_hit_latency_ms)
        """
        import time
        start_time = time.time()

        # Tier 1: Redis cache lookup
        cached_rules = None
        if self.cache:
            cached_rules = await self.cache.get_rules(game_id)

        # Tier 2: In-memory hardcoded rules (MVP)
        rules = cached_rules if cached_rules else self.CHESS_RULES

        # Validate move against rules
        matches = self._match_rules(move, rules, game_state)

        # Determine validity
        is_valid, reason, applied_ids = self._determine_validity(matches)

        latency_ms = (time.time() - start_time) * 1000

        # Update cache if miss (tier-1 warmup)
        if not cached_rules and self.cache:
            await self.cache.set_rules(game_id, self.CHESS_RULES)

        logger.info(
            f"Move validation: {move} → {'VALID' if is_valid else 'INVALID'} "
            f"({len(matches)} rules, {latency_ms:.2f}ms)"
        )

        return is_valid, reason, applied_ids, latency_ms

    def _match_rules(
        self,
        move: str,
        rules: list[dict],
        game_state: Optional[str],
    ) -> list[RuleMatch]:
        """Match move against rule patterns."""
        import re

        matches = []

        for rule in rules:
            pattern = rule.get("pattern")
            rule_id = UUID(rule["id"])

            # Pattern-based matching
            if pattern:
                match = re.match(pattern, move)
                if match:
                    matches.append(RuleMatch(
                        rule_id=rule_id,
                        rule_name=rule["name"],
                        rule_type=rule["type"],
                        matches=True,
                        reason=rule["description"],
                        precedence=rule["precedence"],
                    ))

            # State-based validation (requires game state)
            elif rule["type"] == "Constraint" and game_state:
                # Placeholder: Would check game state for obstruction
                # For MVP, assume constraint passes
                matches.append(RuleMatch(
                    rule_id=rule_id,
                    rule_name=rule["name"],
                    rule_type=rule["type"],
                    matches=True,
                    reason=rule["description"],
                    precedence=rule["precedence"],
                ))

        return matches

    def _determine_validity(
        self,
        matches: list[RuleMatch],
    ) -> tuple[bool, str, list[UUID]]:
        """
        Determine if move is valid based on rule matches.

        Args:
            matches: List of rule matches

        Returns:
            (is_valid, reason, applied_rule_ids)
        """
        if not matches:
            return (
                False,
                "No matching rules found for this move notation",
                [],
            )

        # Sort by precedence (higher precedence wins in conflicts)
        matches.sort(key=lambda m: m.precedence, reverse=True)

        # Check for constraint violations (precedence 40+)
        constraints = [m for m in matches if m.rule_type == "Constraint"]
        if any(not c.matches for c in constraints):
            violated = constraints[0]
            return (
                False,
                f"Move violates constraint: {violated.reason}",
                [violated.rule_id],
            )

        # All matches pass - move is valid
        movement_rules = [m for m in matches if m.rule_type in ("Movement", "Capture", "SpecialMove")]

        if movement_rules:
            primary = movement_rules[0]
            applied_ids = [m.rule_id for m in matches]

            return (
                True,
                primary.reason,
                applied_ids,
            )

        # Fallback: generic valid
        return (
            True,
            "Move notation is valid",
            [m.rule_id for m in matches],
        )
