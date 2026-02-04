"""
ISSUE-3496: Intent Classification Service
LLM-based classification with few-shot prompting and confidence scoring.
"""

import logging
from typing import Optional, Tuple

from langchain_core.prompts import ChatPromptTemplate
from langchain_openai import ChatOpenAI

from ..config import settings
from ..domain import IntentType

logger = logging.getLogger(__name__)


class IntentClassifier:
    """
    Classifies user intent using few-shot LLM prompting.

    Intent Types:
    - SETUP: How to set up the game, initial configuration
    - RULES: Game rules, move legality, rule clarifications
    - GENERAL: General questions, game strategy, other topics

    Performance: <500ms P95, accuracy >85%
    """

    # Few-shot examples for classification
    FEW_SHOT_EXAMPLES = """
Examples:

User: "How do I set up Catan for 4 players?"
Intent: SETUP
Confidence: 0.95
Reasoning: Explicit setup question

User: "Can I move my knight backwards in chess?"
Intent: RULES
Confidence: 0.92
Reasoning: Legal move question (rules)

User: "What's the best strategy for the opening phase?"
Intent: GENERAL
Confidence: 0.88
Reasoning: Strategy question, not about rules or setup

User: "How many cards does each player start with?"
Intent: SETUP
Confidence: 0.90
Reasoning: Initial game state question

User: "Is it legal to build a settlement here?"
Intent: RULES
Confidence: 0.93
Reasoning: Legality validation (rules)

User: "Tell me about the history of this game"
Intent: GENERAL
Confidence: 0.85
Reasoning: General information, not gameplay-specific
"""

    CLASSIFICATION_PROMPT = ChatPromptTemplate.from_messages([
        ("system", """You are an intent classifier for a board game AI assistant.

Classify user queries into one of these intents:
- SETUP: Questions about game setup, initial configuration, starting conditions
- RULES: Questions about game rules, move legality, rule clarifications, violations
- GENERAL: General questions, strategy, game history, other topics

{few_shot_examples}

Respond ONLY in this exact format:
Intent: <SETUP|RULES|GENERAL>
Confidence: <0.0-1.0>
Reasoning: <brief explanation>

Be decisive. If unsure between SETUP and RULES, prefer RULES."""),
        ("user", "{query}")
    ])

    def __init__(self, redis_cache: Optional[any] = None):
        """
        Initialize intent classifier.

        Args:
            redis_cache: Optional Redis cache for frequent patterns
        """
        self.llm = ChatOpenAI(
            model="gpt-4o-mini",  # Fast, cheap model for classification
            temperature=0.0,  # Deterministic classification
            api_key=settings.openrouter_api_key,
            base_url=settings.openrouter_base_url,
            default_headers={"HTTP-Referer": "https://meepleai.app", "X-Title": "MeepleAI"},
            timeout=5.0,  # Fast timeout for low latency
        )
        self.cache = redis_cache
        self.prompt = self.CLASSIFICATION_PROMPT.partial(few_shot_examples=self.FEW_SHOT_EXAMPLES)

    async def classify(self, query: str, game_context: Optional[dict] = None) -> Tuple[IntentType, float]:
        """
        Classify user intent with confidence scoring.

        Args:
            query: User query text
            game_context: Optional game context for disambiguation

        Returns:
            Tuple of (IntentType, confidence_score)

        Raises:
            ValueError: If classification fails
        """
        if not query or len(query.strip()) == 0:
            raise ValueError("Query cannot be empty")

        # Check cache first
        if self.cache:
            cached = await self._get_from_cache(query)
            if cached:
                logger.debug(f"Cache HIT for query: {query[:50]}...")
                return cached

        try:
            # Invoke LLM for classification
            logger.debug(f"Classifying query: {query[:100]}...")

            chain = self.prompt | self.llm
            response = await chain.ainvoke({"query": query})

            # Parse response
            intent, confidence = self._parse_response(response.content)

            # Cache result
            if self.cache:
                await self._save_to_cache(query, intent, confidence)

            logger.info(f"Classified intent: {intent.value}, confidence: {confidence:.2f}")
            return intent, confidence

        except Exception as e:
            logger.error(f"Intent classification failed: {e}", exc_info=True)
            # Fallback to simple keyword matching
            return self._fallback_classification(query)

    def _parse_response(self, response: str) -> Tuple[IntentType, float]:
        """
        Parse LLM response into intent and confidence.

        Expected format:
        Intent: SETUP
        Confidence: 0.95
        Reasoning: ...
        """
        lines = response.strip().split('\n')
        intent_str = None
        confidence = 0.5  # Default

        for line in lines:
            if line.startswith("Intent:"):
                intent_str = line.split(":", 1)[1].strip().upper()
            elif line.startswith("Confidence:"):
                try:
                    confidence = float(line.split(":", 1)[1].strip())
                except ValueError:
                    confidence = 0.5

        # Map to IntentType
        intent_map = {
            "SETUP": IntentType.SETUP_QUESTION,
            "RULES": IntentType.RULES_QUESTION,
            "GENERAL": IntentType.GENERAL,
        }

        if intent_str not in intent_map:
            logger.warning(f"Invalid intent in response: {intent_str}, defaulting to GENERAL")
            return IntentType.GENERAL, 0.5

        return intent_map[intent_str], confidence

    def _fallback_classification(self, query: str) -> Tuple[IntentType, float]:
        """Simple keyword-based fallback classification."""
        query_lower = query.lower()

        if any(word in query_lower for word in ["setup", "how to start", "initial", "begin"]):
            return IntentType.SETUP_QUESTION, 0.6
        elif any(word in query_lower for word in ["rule", "legal", "allowed", "can i", "valid"]):
            return IntentType.RULES_QUESTION, 0.6
        else:
            return IntentType.GENERAL, 0.5

    async def _get_from_cache(self, query: str) -> Optional[Tuple[IntentType, float]]:
        """Get classification from Redis cache."""
        if not self.cache:
            return None
        return await self.cache.get(query)

    async def _save_to_cache(self, query: str, intent: IntentType, confidence: float) -> None:
        """Save classification to Redis cache."""
        if not self.cache:
            return
        await self.cache.set(query, intent, confidence)
