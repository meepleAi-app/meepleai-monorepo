"""Unit tests for Settings.is_llm_configured guard.

Issue #1047 (Blocker 1): orchestration-service must fail fast with an
actionable error when OPENROUTER_API_KEY is missing, instead of letting
langchain emit a cryptic OpenAIError("Missing credentials") deeper in the
lifespan stack.
"""

from src.config.settings import Settings


class TestIsLlmConfigured:
    """Settings.is_llm_configured reflects OpenRouter API key presence."""

    def test_returns_true_when_key_present(self):
        settings = Settings(openrouter_api_key="non-empty-stub")
        assert settings.is_llm_configured is True

    def test_returns_false_when_key_empty(self):
        settings = Settings(openrouter_api_key="")
        assert settings.is_llm_configured is False

    def test_returns_false_when_key_whitespace_only(self):
        settings = Settings(openrouter_api_key="   \t\n  ")
        assert settings.is_llm_configured is False

    def test_returns_true_for_ci_stub_key(self):
        """CI workflows set OPENROUTER_API_KEY=test-key — guard must accept it."""
        settings = Settings(openrouter_api_key="test-key")
        assert settings.is_llm_configured is True
