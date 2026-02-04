"""
ISSUE-3496: Unit tests for Intent Classification
Tests accuracy, confidence scoring, and caching behavior.
"""

import pytest
from unittest.mock import AsyncMock, MagicMock, patch

from src.application.intent_classifier import IntentClassifier
from src.domain import IntentType


class TestIntentClassifier:
    """Test suite for IntentClassifier."""

    @pytest.fixture
    def mock_llm_response(self):
        """Mock LLM response."""
        mock = AsyncMock()
        mock.content = "Intent: SETUP\nConfidence: 0.92\nReasoning: Setup question"
        return mock

    @pytest.fixture
    def classifier_no_cache(self):
        """Create classifier without cache."""
        return IntentClassifier(redis_cache=None)

    @pytest.mark.asyncio
    async def test_classify_setup_question(self, classifier_no_cache, mock_llm_response):
        """Test classification of setup questions."""
        with patch.object(classifier_no_cache.llm, 'ainvoke', return_value=mock_llm_response):
            intent, confidence = await classifier_no_cache.classify("How do I set up the game?")

            assert intent == IntentType.SETUP_QUESTION
            assert confidence == 0.92

    @pytest.mark.asyncio
    async def test_classify_rules_question(self, classifier_no_cache):
        """Test classification of rules questions."""
        mock_response = AsyncMock()
        mock_response.content = "Intent: RULES\nConfidence: 0.89\nReasoning: Rule legality"

        with patch.object(classifier_no_cache.llm, 'ainvoke', return_value=mock_response):
            intent, confidence = await classifier_no_cache.classify("Can I castle after moving my king?")

            assert intent == IntentType.RULES_QUESTION
            assert 0.85 <= confidence <= 1.0

    @pytest.mark.asyncio
    async def test_classify_general_question(self, classifier_no_cache):
        """Test classification of general questions."""
        mock_response = AsyncMock()
        mock_response.content = "Intent: GENERAL\nConfidence: 0.87\nReasoning: General question"

        with patch.object(classifier_no_cache.llm, 'ainvoke', return_value=mock_response):
            intent, confidence = await classifier_no_cache.classify("What's the history of chess?")

            assert intent == IntentType.GENERAL
            assert confidence > 0.0

    @pytest.mark.asyncio
    async def test_empty_query_raises_error(self, classifier_no_cache):
        """Test that empty query raises ValueError."""
        with pytest.raises(ValueError, match="Query cannot be empty"):
            await classifier_no_cache.classify("")

    @pytest.mark.asyncio
    async def test_fallback_on_llm_failure(self, classifier_no_cache):
        """Test fallback to keyword matching when LLM fails."""
        with patch.object(classifier_no_cache.llm, 'ainvoke', side_effect=Exception("LLM error")):
            intent, confidence = await classifier_no_cache.classify("How do I setup the board?")

            # Should fall back to keyword matching
            assert intent == IntentType.SETUP_QUESTION
            assert confidence > 0.0  # Fallback still provides confidence

    @pytest.mark.asyncio
    async def test_confidence_score_in_valid_range(self, classifier_no_cache, mock_llm_response):
        """Test confidence score is between 0 and 1."""
        with patch.object(classifier_no_cache.llm, 'ainvoke', return_value=mock_llm_response):
            _, confidence = await classifier_no_cache.classify("Test query")

            assert 0.0 <= confidence <= 1.0

    @pytest.mark.asyncio
    async def test_caching_stores_result(self):
        """Test that classifications are cached."""
        mock_cache = AsyncMock()
        mock_cache.get.return_value = None  # Cache miss

        classifier = IntentClassifier(redis_cache=mock_cache)

        mock_response = AsyncMock()
        mock_response.content = "Intent: SETUP\nConfidence: 0.90\nReasoning: Setup"

        with patch.object(classifier.llm, 'ainvoke', return_value=mock_response):
            await classifier.classify("How do I start?")

            # Verify cache.set was called
            mock_cache.set.assert_called_once()
            args = mock_cache.set.call_args[0]
            assert args[0] == "How do I start?"
            assert args[1] == IntentType.SETUP_QUESTION
            assert args[2] == 0.90

    @pytest.mark.asyncio
    async def test_cache_hit_skips_llm(self):
        """Test that cache hits skip LLM invocation."""
        mock_cache = AsyncMock()
        mock_cache.get.return_value = (IntentType.RULES_QUESTION, 0.95)  # Cache hit

        classifier = IntentClassifier(redis_cache=mock_cache)

        with patch.object(classifier.llm, 'ainvoke') as mock_llm:
            intent, confidence = await classifier.classify("Cached query")

            # LLM should NOT be called
            mock_llm.assert_not_called()

            # Should return cached result
            assert intent == IntentType.RULES_QUESTION
            assert confidence == 0.95

    @pytest.mark.asyncio
    async def test_parse_malformed_response_defaults_to_general(self, classifier_no_cache):
        """Test handling of malformed LLM responses."""
        mock_response = AsyncMock()
        mock_response.content = "Invalid response format"

        with patch.object(classifier_no_cache.llm, 'ainvoke', return_value=mock_response):
            intent, confidence = await classifier_no_cache.classify("Test")

            assert intent == IntentType.GENERAL
            assert confidence == 0.5  # Default confidence

    def test_fallback_classification_setup_keywords(self, classifier_no_cache):
        """Test fallback classifies setup keywords correctly."""
        intent, confidence = classifier_no_cache._fallback_classification("how to start the game")

        assert intent == IntentType.SETUP_QUESTION
        assert confidence > 0.0

    def test_fallback_classification_rules_keywords(self, classifier_no_cache):
        """Test fallback classifies rules keywords correctly."""
        intent, confidence = classifier_no_cache._fallback_classification("is this move legal?")

        assert intent == IntentType.RULES_QUESTION
        assert confidence > 0.0
