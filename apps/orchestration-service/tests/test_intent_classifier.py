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
    def classifier_no_cache(self):
        """Create classifier without cache."""
        return IntentClassifier(redis_cache=None)

    @pytest.fixture
    def mock_chain(self):
        """Create a mock LangChain chain."""
        chain = AsyncMock()
        return chain

    @pytest.mark.asyncio
    async def test_classify_setup_question(self, classifier_no_cache):
        """Test classification of setup questions."""
        mock_response = AsyncMock()
        mock_response.content = "Intent: SETUP\nConfidence: 0.92\nReasoning: Setup question"

        with patch.object(classifier_no_cache, 'prompt') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.return_value = mock_response
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

            intent, confidence = await classifier_no_cache.classify("How do I set up the game?")

            assert intent == IntentType.SETUP_QUESTION
            assert confidence == 0.92
            mock_chain.ainvoke.assert_called_once()

    @pytest.mark.asyncio
    async def test_classify_rules_question(self, classifier_no_cache):
        """Test classification of rules questions."""
        mock_response = AsyncMock()
        mock_response.content = "Intent: RULES\nConfidence: 0.89\nReasoning: Rule legality"

        with patch.object(classifier_no_cache, 'prompt') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.return_value = mock_response
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

            intent, confidence = await classifier_no_cache.classify("Can I castle after moving my king?")

            assert intent == IntentType.RULES_QUESTION
            assert 0.85 <= confidence <= 1.0

    @pytest.mark.asyncio
    async def test_classify_general_question(self, classifier_no_cache):
        """Test classification of general questions."""
        mock_response = AsyncMock()
        mock_response.content = "Intent: GENERAL\nConfidence: 0.87\nReasoning: General question"

        with patch.object(classifier_no_cache, 'prompt') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.return_value = mock_response
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

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
        with patch.object(classifier_no_cache, 'prompt') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.side_effect = Exception("LLM error")
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

            intent, confidence = await classifier_no_cache.classify("How do I setup the board?")

            # Should fall back to keyword matching
            assert intent == IntentType.SETUP_QUESTION
            assert confidence > 0.0  # Fallback still provides confidence

    @pytest.mark.asyncio
    async def test_confidence_score_in_valid_range(self, classifier_no_cache):
        """Test confidence score is between 0 and 1."""
        mock_response = AsyncMock()
        mock_response.content = "Intent: SETUP\nConfidence: 0.90\nReasoning: Setup"

        with patch.object(classifier_no_cache, 'prompt') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.return_value = mock_response
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

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

        with patch.object(classifier, 'prompt') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.return_value = mock_response
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

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

        with patch.object(classifier, 'prompt') as mock_prompt:
            mock_chain = AsyncMock()
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

            intent, confidence = await classifier.classify("Cached query")

            # LLM should NOT be called
            mock_chain.ainvoke.assert_not_called()

            # Should return cached result
            assert intent == IntentType.RULES_QUESTION
            assert confidence == 0.95

    @pytest.mark.asyncio
    async def test_parse_malformed_response_defaults_to_general(self, classifier_no_cache):
        """Test handling of malformed LLM responses."""
        mock_response = AsyncMock()
        mock_response.content = "Invalid response format"

        with patch.object(classifier_no_cache, 'prompt') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.return_value = mock_response
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

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

    def test_fallback_classification_general_default(self, classifier_no_cache):
        """Test fallback defaults to GENERAL for ambiguous queries."""
        intent, confidence = classifier_no_cache._fallback_classification("tell me something")

        assert intent == IntentType.GENERAL
        assert confidence == 0.5

    def test_parse_response_with_intent_only(self, classifier_no_cache):
        """Test parsing response with intent but no confidence."""
        intent, confidence = classifier_no_cache._parse_response("Intent: SETUP")

        assert intent == IntentType.SETUP_QUESTION
        assert confidence == 0.5  # Default when missing

    def test_parse_response_with_confidence_only(self, classifier_no_cache):
        """Test parsing response with confidence but no intent."""
        intent, confidence = classifier_no_cache._parse_response("Confidence: 0.85")

        assert intent == IntentType.GENERAL  # Default when missing
        assert confidence == 0.5  # Defaults to 0.5 when intent is invalid

    def test_parse_response_with_invalid_confidence(self, classifier_no_cache):
        """Test parsing response with invalid confidence value."""
        intent, confidence = classifier_no_cache._parse_response("Intent: RULES\nConfidence: invalid")

        assert intent == IntentType.RULES_QUESTION
        assert confidence == 0.5  # Default on parse error

    @pytest.mark.asyncio
    async def test_cache_methods_called(self):
        """Test that cache get/set methods are called correctly."""
        mock_cache = AsyncMock()
        mock_cache.get.return_value = None

        classifier = IntentClassifier(redis_cache=mock_cache)

        mock_response = AsyncMock()
        mock_response.content = "Intent: RULES\nConfidence: 0.88\nReasoning: Rules"

        with patch.object(classifier, 'prompt') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.return_value = mock_response
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

            await classifier.classify("Test query")

            # Verify cache operations
            mock_cache.get.assert_called_once_with("Test query")
            mock_cache.set.assert_called_once()

    @pytest.mark.asyncio
    async def test_whitespace_only_query_raises_error(self, classifier_no_cache):
        """Test that whitespace-only query raises ValueError."""
        with pytest.raises(ValueError, match="Query cannot be empty"):
            await classifier_no_cache.classify("   ")

    @pytest.mark.asyncio
    async def test_classify_with_game_context(self, classifier_no_cache):
        """Test classification with optional game context (future enhancement)."""
        mock_response = AsyncMock()
        mock_response.content = "Intent: RULES\nConfidence: 0.91\nReasoning: Context-aware"

        with patch.object(classifier_no_cache, 'prompt') as mock_prompt:
            mock_chain = AsyncMock()
            mock_chain.ainvoke.return_value = mock_response
            mock_prompt.__or__ = MagicMock(return_value=mock_chain)

            intent, confidence = await classifier_no_cache.classify(
                "Can I do this?",
                game_context={"game_id": "chess", "phase": "middle"}
            )

            assert intent == IntentType.RULES_QUESTION
            assert confidence == 0.91
