"""e5 instruction prefix asymmetry (issue #3737).

Until #3737 the /embeddings endpoint prepended "passage: " to every text, while a
comment three lines above described the query/passage distinction the code then
ignored. The same endpoint serves BOTH the indexing path (passages) and every
search query, so one hard-coded side was necessarily wrong for the other.

These tests pin the contract without loading the model: the prefix comes from the
caller, `passage` remains the default so un-updated clients are unaffected, and an
unknown purpose is rejected rather than silently coerced.
"""
import pytest
from pydantic import ValidationError

import main


def test_query_purpose_uses_query_prefix():
    assert main.apply_instruction_prefix(["how do I set up Catan?"], "query") == [
        "query: how do I set up Catan?"
    ]


def test_passage_purpose_uses_passage_prefix():
    assert main.apply_instruction_prefix(["Setup: place the hexes."], "passage") == [
        "passage: Setup: place the hexes."
    ]


def test_prefix_is_applied_to_every_text_in_the_batch():
    texts = ["first", "second", "third"]
    assert main.apply_instruction_prefix(texts, "query") == [
        "query: first",
        "query: second",
        "query: third",
    ]


def test_the_two_purposes_produce_different_inputs():
    # The whole point of #3737: the encoder must see a different string per side.
    text = ["identical text"]
    assert main.apply_instruction_prefix(text, "query") != main.apply_instruction_prefix(
        text, "passage"
    )


def test_unknown_purpose_is_rejected():
    with pytest.raises(ValueError, match="Unsupported purpose"):
        main.apply_instruction_prefix(["x"], "document")


def test_request_defaults_to_passage_when_purpose_is_omitted():
    # DoD #3737: a client that does not send `purpose` keeps the pre-fix behaviour,
    # so no already-indexed chunk is invalidated by this change.
    request = main.EmbeddingRequest(texts=["chunk"], language="en")
    assert request.purpose == "passage"
    assert main.apply_instruction_prefix(request.texts, request.purpose) == [
        "passage: chunk"
    ]


def test_request_accepts_query_purpose():
    request = main.EmbeddingRequest(texts=["q"], language="it", purpose="query")
    assert request.purpose == "query"


def test_request_rejects_unknown_purpose_at_the_schema_boundary():
    # Rejected by the pattern before it can reach the encoder — a typo must not
    # degrade silently into a wrong-prefix embedding.
    with pytest.raises(ValidationError):
        main.EmbeddingRequest(texts=["q"], language="en", purpose="passages")


def test_default_purpose_constant_matches_the_schema_default():
    assert main.DEFAULT_EMBEDDING_PURPOSE == "passage"
    assert set(main.EMBEDDING_PURPOSES) == {"passage", "query"}
