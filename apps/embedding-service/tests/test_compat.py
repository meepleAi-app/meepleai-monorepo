"""Regression guard for the transformers 5.x / sentence-transformers 5.x bump (issue #1455).

The embedding service is inference-only — it uses SentenceTransformer.encode() and
get_sentence_embedding_dimension() (see main.py). These tests pin the major versions
that resolve Dependabot alert GHSA-69w3-r845-3855 and confirm the encode interface the
service relies on survives the 3.x -> 5.x upgrade, WITHOUT downloading the model.
"""
import sentence_transformers
import transformers
from sentence_transformers import SentenceTransformer


def _major(version: str) -> int:
    return int(version.split(".")[0])


def test_transformers_major_is_5_plus():
    # >=5 (GA) is the condition that closes alert #1455's transformers half.
    assert _major(transformers.__version__) >= 5, (
        f"transformers must be >=5.x to resolve alert #1455; got {transformers.__version__}"
    )


def test_sentence_transformers_major_is_5_plus():
    # sentence-transformers 3.x pinned transformers<5; >=5 is required for the bump.
    assert _major(sentence_transformers.__version__) >= 5, (
        f"sentence-transformers must be >=5.x for transformers 5.x compat; "
        f"got {sentence_transformers.__version__}"
    )


def test_inference_interface_intact():
    # The only two SentenceTransformer methods the service calls (main.py:94-100,193).
    # Confirm they still exist after the upgrade without instantiating (no model download).
    assert hasattr(SentenceTransformer, "encode")
    assert hasattr(SentenceTransformer, "get_sentence_embedding_dimension")


def test_main_imports_under_upgraded_stack():
    # Importing main must not break with the upgraded deps; the model is loaded lazily
    # in the FastAPI lifespan, so this does not trigger a download.
    import main  # noqa: F401

    assert hasattr(main, "app")
