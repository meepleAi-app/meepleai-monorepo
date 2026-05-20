"""Shared pytest fixtures for mockup-demo tests."""
import sys
from pathlib import Path

# Make `scripts.mockup_demo` importable when pytest is run from repo root
sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

import pytest

FIXTURES = Path(__file__).parent / "fixtures"

@pytest.fixture
def sample_html_path():
    return FIXTURES / "sample_static.html"

@pytest.fixture
def sample_jsx_path():
    return FIXTURES / "sample_component.jsx"
