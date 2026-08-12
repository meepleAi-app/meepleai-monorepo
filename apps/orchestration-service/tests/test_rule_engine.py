"""Tests for RuleEngine._match_rules — issue #3668.

The Constraint branch had no tests at all, which is how it kept a body that reported
every rule as satisfied without checking anything. These tests pin the two behaviours
that matter: a rule that cannot be evaluated must not be reported as passed, and the
absence of an evaluation must be visible.
"""

import logging

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
        with caplog.at_level(logging.WARNING):
            engine._match_rules("e4", [CONSTRAINT_RULE], game_state=None)

        assert any(
            "No Piece Obstruction" in record.getMessage()
            and "NOT evaluated" in record.getMessage()
            for record in caplog.records
        ), "skipping a constraint must be logged, not silent"

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
