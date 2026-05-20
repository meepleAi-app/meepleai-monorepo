"""Shared pytest fixtures for mockup-demo tests."""
from pathlib import Path
import pytest

FIXTURES = Path(__file__).parent / "fixtures"

@pytest.fixture
def sample_html_path():
    return FIXTURES / "sample_static.html"

@pytest.fixture
def sample_jsx_path():
    return FIXTURES / "sample_component.jsx"
