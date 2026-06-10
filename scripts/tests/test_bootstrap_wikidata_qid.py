"""
Tests for ``bootstrap_wikidata_qid.py`` (issue #2123 QID bootstrap script).

The script talks to PostgreSQL and to the Wikimedia SPARQL endpoint; both
dependencies are dependency-injected via ``run()`` so the tests can exercise
the full flow without real network or DB access.

Run with::

    python -m pytest scripts/tests/test_bootstrap_wikidata_qid.py -v
"""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import MagicMock

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import bootstrap_wikidata_qid as boot  # noqa: E402


# ---------------------------------------------------------------------------
# Pure helpers
# ---------------------------------------------------------------------------


def test_build_sparql_query_includes_all_ids():
    q = boot.build_sparql_query([13, 266192, 163412])
    assert '"13"' in q
    assert '"266192"' in q
    assert '"163412"' in q
    assert "wdt:P2339" in q


def test_build_sparql_query_rejects_non_positive_ids():
    with pytest.raises(ValueError):
        boot.build_sparql_query([0, 13])
    with pytest.raises(ValueError):
        boot.build_sparql_query([-1])
    with pytest.raises(ValueError):
        boot.build_sparql_query(["13"])  # type: ignore[list-item]


def test_parse_sparql_response_extracts_valid_bindings():
    payload = {
        "results": {
            "bindings": [
                {
                    "bggId": {"value": "13"},
                    "item": {"value": "http://www.wikidata.org/entity/Q47533"},
                },
                {
                    "bggId": {"value": "266192"},
                    "item": {"value": "http://www.wikidata.org/entity/Q66338944"},
                },
            ]
        }
    }
    result = boot.parse_sparql_response(payload)
    assert result == {13: "Q47533", 266192: "Q66338944"}


def test_parse_sparql_response_skips_invalid_qid_format():
    payload = {
        "results": {
            "bindings": [
                {"bggId": {"value": "13"}, "item": {"value": "http://example.org/notaqid"}},
                {"bggId": {"value": "14"}, "item": {"value": "http://www.wikidata.org/entity/X1"}},
                {"bggId": {"value": "15"}, "item": {"value": "http://www.wikidata.org/entity/Q42"}},
            ]
        }
    }
    result = boot.parse_sparql_response(payload)
    assert result == {15: "Q42"}


def test_parse_sparql_response_handles_missing_fields():
    payload = {"results": {"bindings": [{}, {"bggId": {"value": "not-a-number"}}]}}
    assert boot.parse_sparql_response(payload) == {}


def test_parse_sparql_response_first_qid_wins_on_duplicates():
    payload = {
        "results": {
            "bindings": [
                {"bggId": {"value": "13"}, "item": {"value": "http://www.wikidata.org/entity/Q1"}},
                {"bggId": {"value": "13"}, "item": {"value": "http://www.wikidata.org/entity/Q2"}},
            ]
        }
    }
    result = boot.parse_sparql_response(payload)
    assert result == {13: "Q1"}


def test_chunked_yields_correct_sized_batches():
    items = [boot.BgGameRow(id=f"r{i}", bgg_id=i) for i in range(7)]
    batches = list(boot.chunked(items, size=3))
    assert len(batches) == 3
    assert [len(b) for b in batches] == [3, 3, 1]


# ---------------------------------------------------------------------------
# run() integration with injected fakes
# ---------------------------------------------------------------------------


class FakeCursor:
    """psycopg2-compatible cursor stub."""

    def __init__(self, candidates: list[tuple[str, int]]):
        self._candidates = candidates
        self.executed: list[tuple[str, tuple]] = []
        self.rowcount = 0

    def execute(self, sql, params=None):
        self.executed.append((sql, params or ()))
        if "UPDATE" in sql:
            self.rowcount = 1
        else:
            self.rowcount = len(self._candidates)

    def fetchall(self):
        return self._candidates

    def __enter__(self):
        return self

    def __exit__(self, *_args):
        return False


class FakeConn:
    def __init__(self, candidates: list[tuple[str, int]]):
        self._candidates = candidates
        self.cursors: list[FakeCursor] = []
        self.committed = False
        self.closed = False

    def cursor(self):
        cur = FakeCursor(self._candidates)
        self.cursors.append(cur)
        return cur

    def commit(self):
        self.committed = True

    def close(self):
        self.closed = True


class FakePsycopg2:
    def __init__(self, conn: FakeConn):
        self._conn = conn
        self.connect_calls: list[str] = []

    def connect(self, cs: str):
        self.connect_calls.append(cs)
        return self._conn


class FakeResponse:
    def __init__(self, payload: dict):
        self._payload = payload

    def raise_for_status(self):
        return None

    def json(self):
        return self._payload


class FakeRequests:
    def __init__(self, response_payload: dict):
        self._payload = response_payload
        self.calls: list[dict] = []

    def get(self, url, params=None, headers=None, timeout=None):
        self.calls.append({"url": url, "params": params, "headers": headers, "timeout": timeout})
        return FakeResponse(self._payload)


def test_run_dry_run_does_not_call_update(monkeypatch):
    candidates = [("uuid-1", 13), ("uuid-2", 266192)]
    fake_conn = FakeConn(candidates)
    fake_psycopg2 = FakePsycopg2(fake_conn)
    fake_requests = FakeRequests(
        {
            "results": {
                "bindings": [
                    {"bggId": {"value": "13"}, "item": {"value": "http://www.wikidata.org/entity/Q47533"}},
                    {"bggId": {"value": "266192"}, "item": {"value": "http://www.wikidata.org/entity/Q66338944"}},
                ]
            }
        }
    )

    result = boot.run(
        connection_string="ignored",
        batch_size=50,
        sparql_sleep_seconds=0.0,
        sparql_timeout_seconds=1.0,
        dry_run=True,
        psycopg2_module=fake_psycopg2,
        requests_module=fake_requests,
    )

    assert result.candidates == 2
    assert result.resolved == 2
    assert result.updated == 0
    assert not fake_conn.committed
    # Only the SELECT — no UPDATE.
    assert all("UPDATE" not in sql for sql, _ in fake_conn.cursors[0].executed)


def test_run_live_run_applies_updates_and_commits(monkeypatch):
    candidates = [("uuid-1", 13)]
    fake_conn = FakeConn(candidates)
    fake_psycopg2 = FakePsycopg2(fake_conn)
    fake_requests = FakeRequests(
        {
            "results": {
                "bindings": [
                    {"bggId": {"value": "13"}, "item": {"value": "http://www.wikidata.org/entity/Q47533"}},
                ]
            }
        }
    )

    result = boot.run(
        connection_string="x",
        batch_size=50,
        sparql_sleep_seconds=0.0,
        sparql_timeout_seconds=1.0,
        dry_run=False,
        psycopg2_module=fake_psycopg2,
        requests_module=fake_requests,
    )

    assert result.candidates == 1
    assert result.resolved == 1
    assert result.updated == 1
    assert fake_conn.committed
    update_sqls = [sql for cur in fake_conn.cursors for sql, _ in cur.executed if "UPDATE" in sql]
    assert len(update_sqls) == 1


def test_run_handles_zero_candidates_without_sparql_call():
    fake_conn = FakeConn(candidates=[])
    fake_psycopg2 = FakePsycopg2(fake_conn)
    fake_requests = FakeRequests({"results": {"bindings": []}})

    result = boot.run(
        connection_string="x",
        batch_size=50,
        sparql_sleep_seconds=0.0,
        sparql_timeout_seconds=1.0,
        dry_run=False,
        psycopg2_module=fake_psycopg2,
        requests_module=fake_requests,
    )

    assert result.candidates == 0
    assert result.resolved == 0
    assert result.updated == 0
    assert fake_requests.calls == []  # no SPARQL traffic when nothing to resolve


def test_run_continues_on_sparql_failure(monkeypatch):
    candidates = [("uuid-1", 13), ("uuid-2", 14)]
    fake_conn = FakeConn(candidates)
    fake_psycopg2 = FakePsycopg2(fake_conn)

    class BrokenRequests:
        def get(self, *args, **kwargs):
            raise RuntimeError("Wikimedia 503")

    result = boot.run(
        connection_string="x",
        batch_size=50,
        sparql_sleep_seconds=0.0,
        sparql_timeout_seconds=1.0,
        dry_run=True,
        psycopg2_module=fake_psycopg2,
        requests_module=BrokenRequests(),
    )

    assert result.candidates == 2
    assert result.resolved == 0  # batch failed, nothing resolved
    assert result.updated == 0


def test_apply_qid_updates_rejects_invalid_qid_defense_in_depth():
    """Even if a malformed QID slips into the mapping, apply_qid_updates discards it."""
    cur = FakeCursor(candidates=[])
    bgg_id_to_row_id = {13: "uuid-1"}
    bad_mapping = {13: "X-not-a-qid"}
    updated = boot.apply_qid_updates(cur, bad_mapping, bgg_id_to_row_id)
    assert updated == 0
    assert cur.executed == []  # no SQL emitted


def test_user_agent_header_includes_contact():
    """ADR-059 §4 requires abuse@meepleai.app on outbound Wikimedia traffic."""
    assert "abuse@meepleai.app" in boot.USER_AGENT
    assert "MeepleAI" in boot.USER_AGENT
