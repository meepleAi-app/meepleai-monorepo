"""Shared pytest fixtures for v2_audit tests."""
import sys
from pathlib import Path

# Make scripts.v2_audit.* importable when running pytest from repo root
sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

import pytest

FIXTURES = Path(__file__).parent / "fixtures"


@pytest.fixture
def sample_matrix_row_path():
    return FIXTURES / "sample_matrix_row.md"


@pytest.fixture
def sample_component_path():
    return FIXTURES / "sample_component.tsx"


@pytest.fixture
def sample_component_props_path():
    return FIXTURES / "sample_component_props.tsx"
