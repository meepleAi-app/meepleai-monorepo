"""Finding dataclass and enum types for audit results."""
from __future__ import annotations
from dataclasses import dataclass, field
from enum import Enum
from typing import Any


class Dimension(str, Enum):
    NAV = "nav"
    STRUCTURAL = "structural"
    TOKENS = "tokens"
    PROPS = "props"


class Severity(str, Enum):
    CRITICAL = "critical"
    IMPORTANT = "important"
    MINOR = "minor"

    @property
    def weight(self) -> int:
        return {"critical": 3, "important": 2, "minor": 1}[self.value]


class Confidence(str, Enum):
    HIGH = "high"
    MEDIUM = "medium"
    LOW = "low"


@dataclass
class Finding:
    dimension: Dimension
    severity: Severity
    confidence: Confidence
    component: str
    route: str
    description: str
    evidence: dict[str, Any] = field(default_factory=dict)

    def __post_init__(self):
        # Allow passing strings; normalize to enum values for stable serialization
        if isinstance(self.dimension, Dimension):
            self.dimension = self.dimension.value
        if isinstance(self.severity, Severity):
            self.severity = self.severity.value
        if isinstance(self.confidence, Confidence):
            self.confidence = self.confidence.value
