"""Unit tests for coordinate normalization (SP-B #3406, epic #3403).

The unstructured `partition_pdf` output carries `element.metadata.coordinates`
(points + system with width/height, PixelSpace = top-left origin). We normalize
each element's bounding box to [0,1] fractions of the page layout so the value is
independent of strategy (fast vs hi_res), DPI, and the viewer zoom.
"""
from types import SimpleNamespace

import pytest

from src.api.coordinates import normalized_bbox


def _element(points, width, height):
    """Build a mock unstructured element carrying metadata.coordinates."""
    system = SimpleNamespace(width=width, height=height)
    coordinates = SimpleNamespace(points=points, system=system)
    return SimpleNamespace(metadata=SimpleNamespace(coordinates=coordinates))


class TestNormalizedBbox:
    def test_pixelspace_points_are_normalized_top_left(self):
        # 4 points TL->BL->BR->TR in a 1000x2000 PixelSpace layout
        el = _element([(100, 200), (100, 400), (500, 400), (500, 200)], 1000, 2000)
        bbox = normalized_bbox(el)
        assert bbox is not None
        assert bbox.x == pytest.approx(0.1)  # 100/1000
        assert bbox.y == pytest.approx(0.1)  # 200/2000, top-left origin (no Y flip)
        assert bbox.width == pytest.approx(0.4)  # (500-100)/1000
        assert bbox.height == pytest.approx(0.1)  # (400-200)/2000

    def test_returns_none_when_coordinates_absent(self):
        el = SimpleNamespace(metadata=SimpleNamespace(coordinates=None))
        assert normalized_bbox(el) is None

    def test_returns_none_when_metadata_absent(self):
        el = SimpleNamespace()
        assert normalized_bbox(el) is None

    def test_returns_none_when_points_empty(self):
        el = _element([], 1000, 2000)
        assert normalized_bbox(el) is None

    def test_returns_none_when_layout_dims_missing(self):
        el = _element([(100, 200), (500, 400)], 0, 0)
        assert normalized_bbox(el) is None

    def test_layout_dimension_independent(self):
        # Same relative box at two different layout scales → identical normalized result.
        # min/max makes it robust to point ordering and count (2 or 4 points).
        el_small = _element([(100, 200), (500, 400)], 1000, 2000)
        el_large = _element([(200, 400), (1000, 800)], 2000, 4000)
        b1 = normalized_bbox(el_small)
        b2 = normalized_bbox(el_large)
        assert b1.x == pytest.approx(b2.x)
        assert b1.y == pytest.approx(b2.y)
        assert b1.width == pytest.approx(b2.width)
        assert b1.height == pytest.approx(b2.height)

    def test_clamps_components_to_unit_interval(self):
        # Points beyond layout bounds get clamped into [0,1].
        el = _element([(-10, -20), (1100, 2100)], 1000, 2000)
        bbox = normalized_bbox(el)
        for value in (bbox.x, bbox.y, bbox.width, bbox.height):
            assert 0.0 <= value <= 1.0
