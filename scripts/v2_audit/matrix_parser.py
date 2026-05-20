"""Parses v2-migration-matrix.md into structured rows."""
from __future__ import annotations
from dataclasses import dataclass
from pathlib import Path
from typing import Iterator
import re


@dataclass
class MatrixRow:
    mockup: str
    component: str
    path: str
    route: str
    status: str
    pr: str


# Match a 6-or-more-column row where first cell is a backtick'd mockup filename
# (excludes header rows like "| Mockup | Component | ...").
_ROW_RE = re.compile(
    r"^\|\s*`(?P<mockup>[^`]+\.(?:html|jsx|tsx))`"
    r"\s*\|\s*`(?P<component>[^`]+)`"
    r"\s*\|\s*`(?P<path>[^`]+\.tsx)`"
    r"\s*\|\s*`(?P<route>[^`]+)`"
    r"\s*\|\s*(?P<status>done|pending|in-progress|stub)"
    r"\s*\|\s*(?P<pr>[^|]+)"
    r"\s*\|"
)


def parse_matrix(path: Path, status_filter: str | None = "done") -> Iterator[MatrixRow]:
    """Yield matrix rows. If status_filter is set, only rows with that status."""
    text = path.read_text(encoding="utf-8")
    for line in text.splitlines():
        m = _ROW_RE.match(line.strip())
        if not m:
            continue
        row = MatrixRow(
            mockup=m.group("mockup").strip(),
            component=m.group("component").strip(),
            path=m.group("path").strip(),
            route=m.group("route").strip(),
            status=m.group("status").strip(),
            pr=m.group("pr").strip(),
        )
        if status_filter is None or row.status == status_filter:
            yield row
