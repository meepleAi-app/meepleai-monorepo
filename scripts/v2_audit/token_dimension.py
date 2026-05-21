"""Token dimension audit: hardcoded color utilities + eslint-disable + paired text-white."""
from __future__ import annotations
from pathlib import Path
from typing import Iterator
import re

from scripts.v2_audit.component_inspector import ComponentSnapshot
from scripts.v2_audit.finding import Finding, Severity, Confidence, Dimension


# Hardcoded color utility patterns (from CLAUDE.md DS-15 ESLint rule)
_HARDCODED_PATTERNS = re.compile(
    r"^(?:bg|text|border|ring|divide|outline|placeholder|caret|accent|decoration)-"
    r"(?:white|black|slate|gray|zinc|neutral|stone|red|orange|amber|yellow|lime|green|emerald|teal|cyan|sky|blue|indigo|violet|purple|fuchsia|pink|rose)-?\d*$"
)
# White exemption: text-white/border-white/ring-white when paired with colored bg
_WHITE_TOKEN = re.compile(r"^(text|border|ring)-white$")
_ESLINT_DISABLE_RE = re.compile(
    r"/\*\s*eslint-disable\b[^*]*local/no-hardcoded-color-utility",
    re.IGNORECASE,
)


def _has_paired_colored_bg(file_text: str, white_token: str) -> bool:
    """Check if the same element with white_token also has an entity/gradient/colored bg."""
    for m in re.finditer(r"""className\s*=\s*["']([^"']+)["']""", file_text):
        classes = m.group(1)
        if white_token in classes.split():
            for tok in classes.split():
                if (
                    tok.startswith("bg-entity-")
                    or tok.startswith("bg-gradient")
                    or tok.startswith("bg-[")
                    or tok in ("bg-primary", "bg-secondary", "bg-accent", "bg-destructive")
                ):
                    return True
    return False


def audit_tokens(
    comp: ComponentSnapshot,
    file_text: str,
    route: str,
) -> Iterator[Finding]:
    # Critical: file-level eslint-disable
    if _ESLINT_DISABLE_RE.search(file_text):
        yield Finding(
            dimension=Dimension.TOKENS,
            severity=Severity.CRITICAL,
            confidence=Confidence.HIGH,
            component=comp.path.name,
            route=route,
            description="File-level eslint-disable for hardcoded-color-utility",
            evidence={"file": str(comp.path)},
        )

    # Important / Minor: hardcoded color utilities
    for token in sorted(comp.tailwind_tokens):
        if _HARDCODED_PATTERNS.match(token):
            if _WHITE_TOKEN.match(token):
                if _has_paired_colored_bg(file_text, token):
                    continue  # exempt
                yield Finding(
                    dimension=Dimension.TOKENS,
                    severity=Severity.MINOR,
                    confidence=Confidence.MEDIUM,
                    component=comp.path.name,
                    route=route,
                    description=f"Unpaired {token} (no colored bg on same element)",
                    evidence={"token": token},
                )
                continue
            yield Finding(
                dimension=Dimension.TOKENS,
                severity=Severity.IMPORTANT,
                confidence=Confidence.HIGH,
                component=comp.path.name,
                route=route,
                description=f"Hardcoded color utility: {token}",
                evidence={"token": token},
            )
