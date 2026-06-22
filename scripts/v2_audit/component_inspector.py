"""Inspects a TSX file: Link hrefs, JSX landmarks, Tailwind tokens, props interface.

Regex-based; ~85% coverage of common patterns. Complex cases (computed hrefs,
spread props, dynamic className) silently miss — caller marks LOW confidence.
"""
from __future__ import annotations
from dataclasses import dataclass, field
from pathlib import Path
import re


# Matches <Link href="..."> or href={`...`} (template literals partially handled)
_LINK_HREF_RE = re.compile(
    r'<Link\b[^>]*?\bhref\s*=\s*[{"]([^"}`]+)["}`]',
    re.IGNORECASE,
)
# router.push("..."), router.replace("...")
_ROUTER_PUSH_RE = re.compile(
    r"router\.(?:push|replace|prefetch)\s*\(\s*['\"`]([^'\"`]+)['\"`]"
)
# All className="..." string values (concatenate into single token list)
_CLASSNAME_RE = re.compile(r'className\s*=\s*"([^"]+)"')
# Tailwind tokens we care about (semantic + entity + hardcoded color)
_TOKEN_SPLIT_RE = re.compile(r"\s+")
# Semantic landmarks and headings
_LANDMARK_RE = re.compile(
    r"<(header|nav|main|section|article|aside|footer)\b",
    re.IGNORECASE,
)
_HEADING_RE = re.compile(r"<(h[1-6])\b", re.IGNORECASE)
# TypeScript interface for props
_INTERFACE_RE = re.compile(
    r"interface\s+(\w+Props)\s*\{([^}]+)\}",
    re.DOTALL,
)
_FIELD_RE = re.compile(r"(\w+)\s*\??\s*:\s*([^;,\n]+)")


@dataclass
class ComponentSnapshot:
    path: Path
    link_hrefs: set[str] = field(default_factory=set)
    router_calls: set[str] = field(default_factory=set)
    tailwind_tokens: set[str] = field(default_factory=set)
    landmarks: set[str] = field(default_factory=set)
    headings: set[str] = field(default_factory=set)
    props_interfaces: dict[str, dict[str, str]] = field(default_factory=dict)
    raw_text_length: int = 0


def inspect_component(path: Path) -> ComponentSnapshot:
    text = path.read_text(encoding="utf-8")
    snap = ComponentSnapshot(path=path, raw_text_length=len(text))

    for m in _LINK_HREF_RE.finditer(text):
        snap.link_hrefs.add(m.group(1))
    for m in _ROUTER_PUSH_RE.finditer(text):
        snap.router_calls.add(m.group(1))

    for m in _CLASSNAME_RE.finditer(text):
        for tok in _TOKEN_SPLIT_RE.split(m.group(1).strip()):
            if tok:
                snap.tailwind_tokens.add(tok)

    for m in _LANDMARK_RE.finditer(text):
        snap.landmarks.add(m.group(1).lower())
    for m in _HEADING_RE.finditer(text):
        snap.headings.add(m.group(1).lower())

    for m in _INTERFACE_RE.finditer(text):
        name = m.group(1)
        body = m.group(2)
        fields = {}
        for fm in _FIELD_RE.finditer(body):
            fname = fm.group(1).strip()
            ftype = fm.group(2).strip().rstrip(";,")
            fields[fname] = ftype
        snap.props_interfaces[name] = fields

    return snap
