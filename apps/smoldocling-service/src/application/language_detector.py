"""Language detection module using langdetect with majority voting across text chunks."""
from dataclasses import dataclass
from langdetect import detect_langs, DetectorFactory

DetectorFactory.seed = 0  # deterministic results


@dataclass
class LanguageResult:
    """Result of language detection."""

    language: str  # ISO 639-1 code ("it", "en", "de", etc.)
    confidence: float  # 0.0 - 1.0


def detect_document_language(
    text_chunks: list[str], min_chunk_length: int = 50
) -> LanguageResult:
    """Detect document language via majority voting on text chunks.

    Uses first 10 chunks, skips chunks shorter than min_chunk_length.
    Deterministic results via seed=0.
    Falls back to English with confidence 0.0 if detection fails.

    Args:
        text_chunks: List of text strings to analyze.
        min_chunk_length: Minimum character length for a chunk to be considered.

    Returns:
        LanguageResult with detected language code and confidence score.
    """
    votes: dict[str, float] = {}

    for chunk in text_chunks[:10]:
        if len(chunk) < min_chunk_length:
            continue
        try:
            langs = detect_langs(chunk)
            top = langs[0]
            votes[top.lang] = votes.get(top.lang, 0) + top.prob
        except Exception:
            continue

    if not votes:
        return LanguageResult(language="en", confidence=0.0)

    best = max(votes, key=votes.get)
    total = sum(votes.values())
    confidence = votes[best] / total if total > 0 else 0.0
    return LanguageResult(language=best, confidence=round(confidence, 2))
