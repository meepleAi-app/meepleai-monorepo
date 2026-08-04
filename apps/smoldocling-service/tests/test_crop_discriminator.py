"""
Unit tests for the crop-discriminator (issue #3435 SP3, Option B).

No model is loaded — a fake adapter stands in for the VLM so the colorfulness pre-filter and
the orchestration are tested in isolation. The design contract under test (memory "De-risk
Opzione B" / spec §5quinquies):
  - a colorful illustration is rejected by the pre-filter WITHOUT invoking the VLM;
  - a B/W table-like crop reaches the VLM, whose <otsl> gate (encoded in extract_crop) decides;
  - the pre-filter is overridable per-request and is fail-open (the gate is authoritative).
"""
import numpy as np
import pytest
from PIL import Image

from src.application.crop_discriminator import CropDiscriminator, colorfulness
from src.domain.models import CropExtractionResult
from src.config import settings


class _FakeAdapter:
    """Records whether extract_crop was called and returns a canned VLM-side result."""

    def __init__(self, result: CropExtractionResult):
        self._result = result
        self.calls = 0
        self._is_initialized = True

    def extract_crop(self, page_image) -> CropExtractionResult:
        self.calls += 1
        return self._result


def _table_result() -> CropExtractionResult:
    return CropExtractionResult(
        is_table=True, reason="table-otsl", markdown="| a | b |\n|---|---|\n| 1 | 2 |",
        bbox=(0.1, 0.2, 0.9, 0.6), doctags="<otsl>...", confidence=0.9,
        prefiltered=False, degenerated=False, colorfulness=0.0, duration_ms=3000,
    )


def _discard_result(reason: str = "no-otsl") -> CropExtractionResult:
    return CropExtractionResult(
        is_table=False, reason=reason, markdown="", bbox=None, doctags="<picture>",
        confidence=0.7, prefiltered=False, degenerated=False, colorfulness=0.0, duration_ms=1000,
    )


def _grayscale(size=(400, 300)) -> Image.Image:
    """A near-B/W table-like image: very low colorfulness."""
    arr = np.random.default_rng(0).integers(200, 256, size=(size[1], size[0]), dtype=np.uint8)
    return Image.fromarray(arr, mode="L").convert("RGB")


def _colorful(size=(400, 300)) -> Image.Image:
    """A fully-saturated hue sweep: high colorfulness (illustration-like)."""
    w, h = size
    hue = np.tile(np.linspace(0, 255, w, dtype=np.uint8), (h, 1))
    sat = np.full((h, w), 255, np.uint8)
    val = np.full((h, w), 255, np.uint8)
    hsv = np.dstack([hue, sat, val])
    return Image.fromarray(hsv, mode="HSV").convert("RGB")


# ------------------------------------------------------------------ colorfulness metric

def test_colorfulness_low_for_grayscale():
    assert colorfulness(_grayscale()) < 15.0


def test_colorfulness_high_for_saturated_image():
    assert colorfulness(_colorful()) > settings.crop_prefilter_colorfulness_threshold


# ------------------------------------------------------------------ pre-filter gating

def test_prefilter_rejects_colorful_without_invoking_vlm():
    adapter = _FakeAdapter(_table_result())
    result = CropDiscriminator(adapter).discriminate(_colorful())
    assert result.is_table is False
    assert result.reason == "prefilter-colorful"
    assert result.prefiltered is True
    assert result.markdown == ""
    assert result.bbox is None
    assert adapter.calls == 0  # the whole point: no VLM on obvious illustrations
    assert result.colorfulness > settings.crop_prefilter_colorfulness_threshold


def test_prefilter_can_be_disabled_per_request():
    adapter = _FakeAdapter(_table_result())
    result = CropDiscriminator(adapter).discriminate(_colorful(), prefilter=False)
    assert adapter.calls == 1  # VLM ran despite the colour
    assert result.is_table is True


def test_table_like_crop_passes_prefilter_and_returns_markdown_and_bbox():
    adapter = _FakeAdapter(_table_result())
    result = CropDiscriminator(adapter).discriminate(_grayscale())
    assert adapter.calls == 1
    assert result.is_table is True
    assert result.reason == "table-otsl"
    assert result.markdown != ""
    assert result.bbox == (0.1, 0.2, 0.9, 0.6)
    assert result.colorfulness < 15.0  # set by the discriminator, not the fake adapter


def test_illustration_reaching_vlm_is_discarded_by_gate():
    # Low-colour crop passes the pre-filter but the VLM emits no <otsl> -> discarded.
    adapter = _FakeAdapter(_discard_result("no-otsl"))
    result = CropDiscriminator(adapter).discriminate(_grayscale())
    assert adapter.calls == 1
    assert result.is_table is False
    assert result.reason == "no-otsl"
    assert result.colorfulness < 15.0


def test_prefilter_disabled_via_config(monkeypatch):
    monkeypatch.setattr(settings, "crop_prefilter_enabled", False)
    adapter = _FakeAdapter(_table_result())
    result = CropDiscriminator(adapter).discriminate(_colorful())
    assert adapter.calls == 1  # config default off -> VLM runs even on colourful crops
    assert result.is_table is True
