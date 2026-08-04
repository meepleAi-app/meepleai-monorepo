"""Crop-table discriminator (issue #3435 SP3 — Option B).

Decides whether an hi_res image-region crop is a *table* worth extracting or a decorative
*illustration* to discard, and — for tables — returns the rebuilt markdown plus the table
bbox in [0,1] top-left.

Design (de-risked empirically on GPU; spec §5quinquies / memory "De-risk Opzione B"):
  1. Colorfulness pre-filter (CPU, ms): reject obviously-colorful illustrations BEFORE the
     VLM to contain cost. This is the ONE place colour alone decides (the VLM never runs for a
     rejected crop), so the threshold is deliberately conservative to keep false-rejects of
     real tables rare, and it is overridable per-request (``prefilter=False``). It is a cost
     optimization, NOT a hard guarantee — a sufficiently colourful real table can be rejected
     here.
  2. VLM plain mode + repetition early-stop (adapter.extract_crop): NO ``no_repeat_ngram_size``
     (it fabricates spurious tables from illustrations — a proven false positive).
  3. ``<otsl>`` gate: for every crop that REACHES the VLM, only an OTSL table in the DocTags is
     authoritative. Illustrations do not emit ``<otsl>``.

The heavy VLM/docling work lives in ``SmolDoclingAdapter``; this application service owns only
the cheap pre-filter and the orchestration, so it stays trivially unit-testable with a fake
adapter (no model).
"""
import logging
from typing import Optional

import numpy as np
from PIL import Image

from ..config import settings
from ..domain.models import CropExtractionResult, PageImage

logger = logging.getLogger(__name__)


def colorfulness(image: Image.Image) -> float:
    """Hasler-Süsstrunk (2003) colorfulness metric.

    ~0 for a grayscale / black-and-white image (tables), high for saturated multi-colour
    images (illustrations). Computed as ``sqrt(std_rg^2 + std_yb^2) + 0.3 * sqrt(mean_rg^2 +
    mean_yb^2)`` where ``rg = R - G`` and ``yb = 0.5*(R+G) - B``.
    """
    rgb = np.asarray(image.convert("RGB"), dtype=np.float32)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    rg = r - g
    yb = 0.5 * (r + g) - b
    std_root = np.sqrt(float(rg.std()) ** 2 + float(yb.std()) ** 2)
    mean_root = np.sqrt(float(rg.mean()) ** 2 + float(yb.mean()) ** 2)
    return std_root + 0.3 * mean_root


class CropDiscriminator:
    """Orchestrates pre-filter → VLM (adapter.extract_crop) → ``<otsl>`` gate → markdown+bbox."""

    def __init__(self, adapter, config=settings):
        self.adapter = adapter
        self.settings = config

    def discriminate(self, image: Image.Image, prefilter: Optional[bool] = None) -> CropExtractionResult:
        """Classify a crop and, if it is a table, return its markdown + bbox.

        Args:
            image: the crop (a PIL image of an hi_res Image/FigureCaption region).
            prefilter: override the colorfulness pre-filter; ``None`` uses the config default.
        """
        use_prefilter = self.settings.crop_prefilter_enabled if prefilter is None else prefilter
        crop_colorfulness = colorfulness(image)

        # 1. Cheap colorfulness reject: colour alone decides here (the VLM is skipped). A
        #    conservative threshold + the per-request override keep false-rejects of real
        #    tables rare; the <otsl> gate is authoritative only for crops that reach the VLM.
        if use_prefilter and crop_colorfulness > self.settings.crop_prefilter_colorfulness_threshold:
            logger.info(
                "Crop rejected by colorfulness pre-filter (%.1f > %.1f) — VLM skipped",
                crop_colorfulness,
                self.settings.crop_prefilter_colorfulness_threshold,
            )
            return CropExtractionResult(
                is_table=False, reason="prefilter-colorful", markdown="", bbox=None,
                doctags="", confidence=0.0, prefiltered=True, degenerated=False,
                colorfulness=crop_colorfulness, duration_ms=0,
            )

        # 2/3. VLM plain mode + early-stop + <otsl> gate + markdown/bbox (all in the adapter).
        page = PageImage.from_pil_image(1, image.convert("RGB"), dpi=self.settings.image_dpi)
        result = self.adapter.extract_crop(page)
        result.colorfulness = crop_colorfulness
        return result
