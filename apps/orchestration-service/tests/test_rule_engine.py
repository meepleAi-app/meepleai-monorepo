"""Tests for RuleEngine._match_rules — issue #3668.

The Constraint branch had no tests at all, which is how it kept a body that reported
every rule as satisfied without checking anything. These tests pin the two behaviours
that matter: a rule that cannot be evaluated must not be reported as passed, and the
absence of an evaluation must be visible.
"""

import logging
from uuid import uuid4

import pytest

from src.application.rule_engine import RuleEngine, RuleMatch

CONSTRAINT_RULE = {
    "id": "00000000-0000-0000-0000-000000000003",
    "name": "No Piece Obstruction",
    "type": "Constraint",
    "precedence": 40,
    "pattern": None,  # state-based: no pattern to match on
    "description": "Pieces cannot move through other pieces (except Knight)",
}

MOVEMENT_RULE = {
    "id": "00000000-0000-0000-0000-000000000002",
    "name": "Pawn Forward Movement",
    "type": "Movement",
    "precedence": 10,
    "pattern": r"^[a-h][2-7]$",
    "description": "Pawns move forward one square",
}


@pytest.fixture
def engine():
    return RuleEngine()


class TestConstraintRuleIsNeverFalselyApproved:
    """#3668: the placeholder appended matches=True — "for MVP, assume constraint passes"."""

    def test_constraint_without_game_state_is_not_reported_as_matching(self, engine):
        """Today's real path: the orchestrator passes game_state=None.

        The rule is not evaluated, and that must NOT look like a passed check. Before
        #3668 this branch was simply skipped, which was already the safer of the two
        wrong behaviours — this test pins it so a future "just extract game_state"
        cannot silently turn it into an unconditional approval.
        """
        matches = engine._match_rules("e4", [CONSTRAINT_RULE], game_state=None)

        assert matches == [], (
            "an unevaluated Constraint must produce no RuleMatch at all; "
            "emitting one with matches=True would report success without checking"
        )

    def test_constraint_without_game_state_logs_that_it_was_skipped(self, engine, caplog):
        """A gap nobody can see is the reason this survived until #3668."""
        RuleEngine._warned_constraints.clear()

        with caplog.at_level(logging.WARNING):
            engine._match_rules("e4", [CONSTRAINT_RULE], game_state=None)

        # Assert on level + rule name, not on prose: rewording the message must not break
        # a correct behaviour.
        assert any(
            record.levelno == logging.WARNING and "No Piece Obstruction" in record.getMessage()
            for record in caplog.records
        ), "skipping a constraint must be logged, not silent"

    def test_constraint_warning_is_not_repeated_every_validation(self, engine, caplog):
        """The condition is constant; a warning per move would train operators to ignore it."""
        RuleEngine._warned_constraints.clear()

        with caplog.at_level(logging.WARNING):
            for _ in range(5):
                engine._match_rules("e4", [CONSTRAINT_RULE], game_state=None)

        warnings = [
            r for r in caplog.records
            if r.levelno == logging.WARNING and "No Piece Obstruction" in r.getMessage()
        ]
        assert len(warnings) == 1, f"expected one warning per process, got {len(warnings)}"

    def test_constraint_carrying_a_pattern_is_still_not_approved(self, engine):
        """The hole the first version left open.

        The branch used to key off a missing `pattern`, so a Constraint that *carries* one
        fell into the pattern arm and was appended matches=True — silently, without even
        the warning. Not reachable through CHESS_RULES, but `_match_rules` also consumes
        rules from the Redis cache, which is arbitrary JSON per game.
        """
        constraint_with_pattern = {**CONSTRAINT_RULE, "pattern": r"^e[0-9]$"}

        matches = engine._match_rules("e4", [constraint_with_pattern], game_state="FEN")

        assert matches == [], (
            "a Constraint must never be approved on the strength of a regex: "
            "the rule type decides, not the presence of a pattern"
        )

    def test_constraint_with_game_state_is_still_not_approved(self, engine):
        """The regression guard.

        `validate_move(game_id, move, game_state)` is public, so this branch is reachable
        even though the orchestrator passes None. Before #3668 that path appended
        matches=True — the rule was reported as satisfied without a single check, which is
        worse than not evaluating it because it is invisible in the result.
        """
        matches = engine._match_rules(
            "e4", [CONSTRAINT_RULE], game_state="rnbqkbnr/pppppppp"
        )

        assert matches == [], (
            "supplying a game state must not conjure a passing constraint: "
            "there is still no board model to check obstruction against"
        )

    def test_constraint_never_yields_a_passing_match(self, engine):
        """Belt and braces: no game_state value produces matches=True."""
        for state in (None, "", "rnbqkbnr/pppppppp", "any-state"):
            produced: list[RuleMatch] = engine._match_rules(
                "e4", [CONSTRAINT_RULE], game_state=state
            )
            assert not any(m.matches for m in produced), (
                f"constraint reported as matching with game_state={state!r}"
            )


class TestValidateMoveVerdict:
    """The user-visible layer. The tests above pin `_match_rules`; this is what callers see."""

    @pytest.mark.asyncio
    async def test_unmatched_move_with_game_state_is_invalid(self, engine):
        """The regression that mattered most, and it was invisible from `_match_rules`.

        Before #3668, supplying ANY game state made ANY string a legal move: the Constraint
        rubber-stamp was the only match, `_determine_validity` found no violated constraint
        and no movement rule, and fell through to "Move notation is valid". Verified against
        the previous revision — `"total garbage!!"` came back valid.
        """
        is_valid, reason, _, _ = await engine.validate_move(
            uuid4(), "total garbage!!", game_state="rnbqkbnr/pppppppp"
        )

        assert is_valid is False, (
            f"a move no rule can evaluate must not be declared valid (reason: {reason})"
        )

    @pytest.mark.asyncio
    async def test_pattern_matched_move_stays_valid_with_game_state(self, engine):
        """The other side of the same coin: the fix must not invalidate real moves."""
        is_valid, _, applied, _ = await engine.validate_move(
            uuid4(), "e4", game_state="rnbqkbnr/pppppppp"
        )

        assert is_valid is True
        assert applied, "a pattern-matched move must still report the rules it applied"


class TestPatternRulesAreUnaffected:
    """The Constraint change must not disturb the pattern-matching path."""

    def test_pattern_rule_matches_normally(self, engine):
        matches = engine._match_rules("e4", [MOVEMENT_RULE], game_state=None)

        assert len(matches) == 1
        assert matches[0].rule_name == "Pawn Forward Movement"
        assert matches[0].matches is True

    def test_pattern_rule_not_matching_yields_nothing(self, engine):
        assert engine._match_rules("Qh5", [MOVEMENT_RULE], game_state=None) == []

    def test_mixed_ruleset_still_evaluates_the_pattern_rule(self, engine):
        """A skipped Constraint must not suppress the rules that CAN be evaluated."""
        matches = engine._match_rules(
            "e4", [MOVEMENT_RULE, CONSTRAINT_RULE], game_state=None
        )

        assert [m.rule_name for m in matches] == ["Pawn Forward Movement"]
