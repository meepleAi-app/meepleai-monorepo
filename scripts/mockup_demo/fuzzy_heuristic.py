"""Scores button text against mockup filename catalog using difflib.SequenceMatcher.

Returns list of (filename, score) sorted descending. Threshold for usable
match is 0.7 (caller decides).
"""
from __future__ import annotations
from difflib import SequenceMatcher
import re


def _normalize_filename(name: str) -> str:
    # Drop prefix tokens (sp3-, sp4-, sp6-, sp7-, librogame-, nanolith-)
    base = re.sub(r"^(sp[3467]-|librogame-|nanolith-)", "", name)
    base = base.replace(".html", "").replace("-", " ")
    return base.strip().lower()


def _score(query: str, candidate: str) -> float:
    return SequenceMatcher(None, query.lower().strip(), _normalize_filename(candidate)).ratio()


def score_candidates(query: str, catalog: list[str]) -> list[tuple[str, float]]:
    scored = [(c, _score(query, c)) for c in catalog]
    return sorted(scored, key=lambda t: t[1], reverse=True)
