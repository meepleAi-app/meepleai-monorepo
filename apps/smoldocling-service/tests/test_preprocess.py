"""
Tests for the /api/v1/preprocess endpoint.

Spike adaptation notes (2026-05-04):
  - Module path is `src.main`, not bare `main` — consistent with test_metrics.py.
  - SmolDocling does NOT expose native token-level confidence. Confidence is a
    heuristic (0.7 base, max 1.0) computed by SmolDoclingAdapter._estimate_confidence.
  - pdf_service.vlm_adapter is NOT initialised in unit tests (no GPU/model).
    _extract_text_with_confidence() returns (0.85, "") when uniniialised — no patching needed.
  - test_preprocess_clear_page_returns_high_confidence requires a real manual fixture
    (clear-page.jpg) which is blocked by BLOCKERS.md B-1 (no physical manuals yet).
"""
import io
import types
import pytest
from fastapi.testclient import TestClient
from PIL import Image

import src.main as main

client = TestClient(main.app)


@pytest.fixture(autouse=True)
def reset_pdf_service(monkeypatch):
    """
    Ensure pdf_service exists but has an uninitialised VLM adapter so tests
    do not attempt GPU inference.  Mirrors the pattern in test_metrics.py.
    """
    from src.application import PdfExtractionService

    svc = PdfExtractionService()
    # vlm_adapter._is_initialized defaults to False — no GPU calls will be made.
    monkeypatch.setattr(main, "pdf_service", svc)
    yield


def _make_jpeg(color: str = "white", size: tuple[int, int] = (800, 1000)) -> bytes:
    """Return raw JPEG bytes for a solid-colour synthetic image."""
    img = Image.new("RGB", size, color=color)
    buf = io.BytesIO()
    img.save(buf, format="JPEG")
    return buf.getvalue()


# ---------------------------------------------------------------------------
# Blank-page detection
# ---------------------------------------------------------------------------

def test_preprocess_blank_page_detected():
    """
    A plain white image should be identified as a blank page.
    Endpoint must return 200 with is_blank=True.
    """
    response = client.post(
        "/api/v1/preprocess",
        files={"image": ("blank.jpg", _make_jpeg("white"), "image/jpeg")},
        data={"preprocessing_mode": "photo-camera"},
    )
    assert response.status_code == 200, response.text
    payload = response.json()
    assert payload["is_blank"] is True
    assert "Page appears blank" in payload["warnings"]


# ---------------------------------------------------------------------------
# Default / pass-through mode
# ---------------------------------------------------------------------------

def test_preprocess_default_mode_passes_through():
    """
    In 'default' mode the endpoint returns the original image base64-encoded
    with confidence 0.95 and is_blank=False, regardless of content.
    """
    img_bytes = _make_jpeg("gray", size=(600, 800))
    response = client.post(
        "/api/v1/preprocess",
        files={"image": ("page.jpg", img_bytes, "image/jpeg")},
        data={"preprocessing_mode": "default"},
    )
    assert response.status_code == 200, response.text
    payload = response.json()
    assert "extracted_text" in payload
    assert payload["confidence"] == 0.95
    assert payload["is_blank"] is False
    assert isinstance(payload["warnings"], list)


# ---------------------------------------------------------------------------
# Response shape contract
# ---------------------------------------------------------------------------

def test_preprocess_response_has_required_fields():
    """
    The response must contain all required fields regardless of mode.
    """
    response = client.post(
        "/api/v1/preprocess",
        files={"image": ("page.jpg", _make_jpeg(), "image/jpeg")},
        data={"preprocessing_mode": "photo-camera"},
    )
    assert response.status_code == 200, response.text
    payload = response.json()
    for field in ("processed_image_base64", "extracted_text", "confidence",
                  "orientation", "is_blank", "warnings"):
        assert field in payload, f"Missing field: {field}"
    assert 0.0 <= payload["confidence"] <= 1.0
    assert payload["orientation"] in ("portrait", "landscape", "rotated", "unknown")


# ---------------------------------------------------------------------------
# Empty image → 400
# ---------------------------------------------------------------------------

def test_preprocess_empty_image_returns_400():
    """Uploading an empty file should return 400 Bad Request."""
    response = client.post(
        "/api/v1/preprocess",
        files={"image": ("empty.jpg", b"", "image/jpeg")},
        data={"preprocessing_mode": "photo-camera"},
    )
    assert response.status_code == 400


# ---------------------------------------------------------------------------
# Real-fixture test (blocked B-1 — no physical manuals available yet)
# ---------------------------------------------------------------------------

@pytest.mark.skip(reason="Requires real rulebook scan fixture — blocked by BLOCKERS.md B-1")
def test_preprocess_clear_page_returns_high_confidence():
    """
    A sharp, well-lit rulebook page scan should yield confidence >= 0.7.
    Requires 'tests/fixtures/clear-page.jpg' which is not available until
    physical manuals are acquired (Sprint 0 BLOCKERS.md B-1).
    """
    fixture_path = "tests/fixtures/clear-page.jpg"
    with open(fixture_path, "rb") as f:
        img_bytes = f.read()

    response = client.post(
        "/api/v1/preprocess",
        files={"image": ("clear-page.jpg", img_bytes, "image/jpeg")},
        data={"preprocessing_mode": "photo-camera"},
    )
    assert response.status_code == 200
    payload = response.json()
    assert payload["confidence"] >= 0.7
    assert not payload["is_blank"]
