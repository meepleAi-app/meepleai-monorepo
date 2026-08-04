"""
Tests for the POST /api/v1/extract-image endpoint (issue #3435 SP3, Option B).

The crop-discriminator itself is unit-tested in test_crop_discriminator.py; here we assert the
HTTP contract only, so _discriminate_crop is monkeypatched to canned results (mirroring the
pdf_service.extract monkeypatch pattern in test_metrics.py). No model / GPU is involved.
"""
import io

import pytest
from fastapi.testclient import TestClient
from PIL import Image

import src.main as main
from src.application import PdfExtractionService
from src.domain.models import CropExtractionResult

client = TestClient(main.app)


@pytest.fixture(autouse=True)
def reset(monkeypatch):
    for key in list(main.metrics):
        main.metrics[key] = type(main.metrics[key])(0)
    # pdf_service exists but its adapter is uninitialised (no GPU) — most tests monkeypatch
    # _discriminate_crop anyway, so the model is never touched.
    monkeypatch.setattr(main, "pdf_service", PdfExtractionService())
    yield


def _jpeg(color: str = "white", size=(300, 200)) -> bytes:
    buf = io.BytesIO()
    Image.new("RGB", size, color).save(buf, format="JPEG")
    return buf.getvalue()


def _table_result() -> CropExtractionResult:
    return CropExtractionResult(
        is_table=True, reason="table-otsl", markdown="| a |\n|---|\n| 1 |",
        bbox=(0.1, 0.2, 0.9, 0.6), doctags="<otsl>...", confidence=0.9,
        prefiltered=False, degenerated=False, colorfulness=5.0, duration_ms=3000,
    )


# ------------------------------------------------------------------ input validation

def test_extract_image_empty_file_returns_400():
    r = client.post("/api/v1/extract-image", files={"image": ("x.jpg", b"", "image/jpeg")})
    assert r.status_code == 400
    assert main.metrics["extract_image_failures_total"] == 1


def test_extract_image_invalid_bytes_returns_400():
    r = client.post(
        "/api/v1/extract-image",
        files={"image": ("x.jpg", b"not-an-image", "image/jpeg")},
    )
    assert r.status_code == 400
    assert main.metrics["extract_image_failures_total"] == 1


# ------------------------------------------------------------------ response contract

def test_extract_image_table_response_shape(monkeypatch):
    monkeypatch.setattr(main, "_discriminate_crop", lambda img, prefilter: _table_result())

    r = client.post("/api/v1/extract-image", files={"image": ("t.jpg", _jpeg(), "image/jpeg")})
    assert r.status_code == 200, r.text
    payload = r.json()
    for field in (
        "is_table", "reason", "markdown", "bbox", "doctags", "confidence",
        "prefiltered", "degenerated", "colorfulness", "duration_ms",
    ):
        assert field in payload, f"Missing field: {field}"
    assert payload["is_table"] is True
    assert payload["bbox"] == [0.1, 0.2, 0.9, 0.6]
    assert payload["markdown"] != ""
    assert main.metrics["extract_image_requests_total"] == 1
    assert main.metrics["extract_image_tables_total"] == 1
    assert main.metrics["extract_image_prefiltered_total"] == 0
    assert main.metrics["extract_image_duration_ms_sum"] == 3000
    assert main.metrics["extract_image_degenerated_total"] == 0


def test_extract_image_prefiltered_illustration_discarded(monkeypatch):
    discard = CropExtractionResult(
        is_table=False, reason="prefilter-colorful", markdown="", bbox=None, doctags="",
        confidence=0.0, prefiltered=True, degenerated=False, colorfulness=88.0, duration_ms=1,
    )
    monkeypatch.setattr(main, "_discriminate_crop", lambda img, prefilter: discard)

    r = client.post("/api/v1/extract-image", files={"image": ("i.jpg", _jpeg("red"), "image/jpeg")})
    assert r.status_code == 200
    payload = r.json()
    assert payload["is_table"] is False
    assert payload["bbox"] is None
    assert payload["markdown"] == ""
    assert payload["prefiltered"] is True
    assert main.metrics["extract_image_prefiltered_total"] == 1
    assert main.metrics["extract_image_tables_total"] == 0


def test_extract_image_forwards_prefilter_form_field(monkeypatch):
    seen = {}

    def fake(img, prefilter):
        seen["prefilter"] = prefilter
        return CropExtractionResult(
            is_table=False, reason="no-otsl", markdown="", bbox=None, doctags="",
            confidence=0.0, prefiltered=False, degenerated=False, colorfulness=1.0, duration_ms=1,
        )

    monkeypatch.setattr(main, "_discriminate_crop", fake)
    r = client.post(
        "/api/v1/extract-image",
        files={"image": ("i.jpg", _jpeg(), "image/jpeg")},
        data={"prefilter": "false"},
    )
    assert r.status_code == 200
    assert seen["prefilter"] is False


def test_extract_image_service_unavailable_answers_200_non_table(monkeypatch):
    # No monkeypatch of _discriminate_crop: with pdf_service present but adapter uninitialised,
    # a real call would try to init the model. Simulate the "no service" branch instead.
    monkeypatch.setattr(main, "pdf_service", None)
    r = client.post("/api/v1/extract-image", files={"image": ("t.jpg", _jpeg(), "image/jpeg")})
    assert r.status_code == 200
    payload = r.json()
    assert payload["is_table"] is False
    assert payload["reason"] == "service-unavailable"
