"""Coordinate normalization for PDF elements (SP-B #3406, epic #3403).

unstructured `partition_pdf` attaches `element.metadata.coordinates` — a
CoordinatesMetadata carrying `.points` (list of (x, y)) and `.system`
(CoordinateSystem with `.width`/`.height`). PixelSpace uses a top-left origin
(y grows downward), so no Y flip is needed. We normalize to [0,1] fractions of the
page layout so the value is independent of strategy (fast vs hi_res), DPI, and the
viewer zoom — the C# side receives an already-normalized box and the FE overlay is
zero-math.
"""
from typing import Any, Optional

from .schemas import BBoxSchema


def _clamp_unit(value: float) -> float:
    return max(0.0, min(1.0, value))


def normalized_bbox(element: Any) -> Optional[BBoxSchema]:
    """Return the element's bounding box normalized to [0,1] (top-left origin),
    or None when the element carries no usable coordinates (e.g. PageBreak, or an
    OCR element without a layout box)."""
    coordinates = getattr(getattr(element, "metadata", None), "coordinates", None)
    if coordinates is None:
        return None

    points = getattr(coordinates, "points", None)
    system = getattr(coordinates, "system", None)
    if not points or system is None:
        return None

    width = getattr(system, "width", None)
    height = getattr(system, "height", None)
    if not width or not height:
        return None

    xs = [point[0] for point in points]
    ys = [point[1] for point in points]
    x_min, x_max = min(xs), max(xs)
    y_min, y_max = min(ys), max(ys)

    return BBoxSchema(
        x=_clamp_unit(x_min / width),
        y=_clamp_unit(y_min / height),
        width=_clamp_unit((x_max - x_min) / width),
        height=_clamp_unit((y_max - y_min) / height),
    )
