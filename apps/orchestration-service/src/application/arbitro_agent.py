"""
ISSUE-3759: Arbitro Agent for Rules Arbitration
LangGraph-based move validation with real-time rule arbitration.
Target: <100ms P95 latency
"""

import logging
from typing import Any
from uuid import UUID

from langgraph.graph import StateGraph, END
from langchain_core.prompts import ChatPromptTemplate
from langchain_openai import ChatOpenAI

from ..config import settings
from ..domain import ArbitroState
from .rule_engine import RuleEngine, RuleCache

logger = logging.getLogger(__name__)


class ArbitroAgent:
    """
    Arbitro agent for real-time move validation and rules arbitration.

    Features:
    - 3-tier rule retrieval (Redis → Memory → Qdrant)
    - Natural language explanations via LLM
    - Conflict detection and precedence resolution
    - <100ms P95 latency target
    """

    EXPLANATION_PROMPT = ChatPromptTemplate.from_messages([
        ("system", """You are a board game rules arbitrator.

Your role:
- Explain why moves are valid or invalid
- Use natural, clear language
- Reference specific rules when explaining
- Be concise (2-3 sentences max)

Validation result:
Move: {move}
Valid: {is_valid}
Applied Rules: {applied_rules}

Provide a natural language explanation."""),
        ("user", "Explain this validation result clearly.")
    ])

    def __init__(self, api_client=None):
        """
        Initialize Arbitro agent with LangGraph workflow.

        Args:
            api_client: Optional MeepleAIApiClient for hybrid search
        """
        self.llm = ChatOpenAI(
            model="gpt-4o-mini",
            temperature=0.3,  # Lower temp for deterministic rule validation
            api_key=settings.openrouter_api_key,
            base_url=settings.openrouter_base_url,
            default_headers={"HTTP-Referer": "https://meepleai.app", "X-Title": "MeepleAI"},
            timeout=5.0,  # Tight timeout for real-time performance
        )
        self.api_client = api_client

        # Initialize rule engine with Redis cache
        self.rule_cache = RuleCache()
        self.rule_engine = RuleEngine(cache=self.rule_cache)

        self.graph = self._build_workflow()

    def _build_workflow(self) -> StateGraph:
        """
        Build Arbitro agent LangGraph workflow.

        Nodes:
        - retrieve_rules: 3-tier rule retrieval
        - validate_move: Apply rules to move
        - generate_explanation: Natural language reason
        """
        graph = StateGraph(ArbitroState)

        # Add nodes
        graph.add_node("retrieve_rules", self._retrieve_rules_node)
        graph.add_node("validate_move", self._validate_move_node)
        graph.add_node("generate_explanation", self._generate_explanation_node)

        # Set entry point
        graph.set_entry_point("retrieve_rules")

        # Linear flow: retrieve → validate → explain
        graph.add_edge("retrieve_rules", "validate_move")
        graph.add_edge("validate_move", "generate_explanation")
        graph.add_edge("generate_explanation", END)

        return graph.compile()

    async def _retrieve_rules_node(self, state: ArbitroState) -> dict[str, Any]:
        """
        Retrieve rules using 3-tier strategy.

        Tier 1: Redis cache (<1ms)
        Tier 2: In-memory hardcoded (MVP)
        Tier 3: Qdrant semantic search (future)
        """
        import time
        start_time = time.time()

        logger.info(f"Retrieving rules for game {state.game_id}")

        # Connect Redis if not connected
        if not self.rule_cache.client:
            await self.rule_cache.connect()

        # Attempt cache lookup
        cached_rules = await self.rule_cache.get_rules(state.game_id)

        retrieval_time_ms = (time.time() - start_time) * 1000

        if cached_rules:
            logger.info(f"Redis tier-1 hit: {len(cached_rules)} rules ({retrieval_time_ms:.2f}ms)")
            return {
                "retrieved_rules": cached_rules,
                "redis_cache_hit": True,
                "rule_retrieval_time_ms": retrieval_time_ms,
            }

        # Tier 2: Fallback to in-memory (MVP)
        logger.info(f"Tier-2 fallback: using hardcoded rules ({retrieval_time_ms:.2f}ms)")

        # Cache for future requests
        await self.rule_cache.set_rules(state.game_id, self.rule_engine.CHESS_RULES)

        return {
            "retrieved_rules": self.rule_engine.CHESS_RULES,
            "redis_cache_hit": False,
            "rule_retrieval_time_ms": retrieval_time_ms,
        }

    async def _validate_move_node(self, state: ArbitroState) -> dict[str, Any]:
        """Validate move using rule engine."""
        logger.info(f"Validating move: {state.move_notation}")

        try:
            is_valid, reason, applied_ids, latency_ms = await self.rule_engine.validate_move(
                game_id=state.game_id,
                move=state.move_notation or "",
                game_state=state.game_state,
            )

            return {
                "is_valid": is_valid,
                "validation_reason": reason,
                "applied_rule_ids": applied_ids,
            }

        except Exception as e:
            logger.error(f"Move validation failed: {e}", exc_info=True)
            return {
                "is_valid": False,
                "validation_reason": f"Validation error: {str(e)}",
                "applied_rule_ids": [],
                "error": str(e),
            }

    async def _generate_explanation_node(self, state: ArbitroState) -> dict[str, Any]:
        """Generate natural language explanation of validation result."""
        logger.info("Generating natural language explanation")

        try:
            # Format applied rules for prompt
            applied_rules_text = "\n".join([
                f"- {rule.get('name', 'Unknown')}: {rule.get('description', '')}"
                for rule in state.retrieved_rules
                if UUID(rule["id"]) in state.applied_rule_ids
            ]) if state.applied_rule_ids else "None"

            # Generate explanation
            chain = self.EXPLANATION_PROMPT | self.llm
            response = await chain.ainvoke({
                "move": state.move_notation,
                "is_valid": state.is_valid,
                "applied_rules": applied_rules_text,
            })

            explanation = response.content.strip()

            return {
                "agent_response": explanation,
                "confidence_score": 0.95 if state.is_valid else 0.90,
            }

        except Exception as e:
            logger.error(f"Explanation generation failed: {e}", exc_info=True)
            # Fallback to rule engine reason
            return {
                "agent_response": state.validation_reason or "Move validation completed",
                "confidence_score": 0.75,
            }

    async def execute(self, state: ArbitroState) -> ArbitroState:
        """
        Execute Arbitro workflow.

        Args:
            state: Current arbitro state with move to validate

        Returns:
            Updated state with validation result
        """
        try:
            logger.info(f"Starting Arbitro workflow for move '{state.move_notation}'")

            # Run the compiled graph
            result = await self.graph.ainvoke(state)

            logger.info(
                f"Arbitro workflow completed: "
                f"valid={result.get('is_valid')}, "
                f"cache_hit={result.get('redis_cache_hit')}, "
                f"latency={result.get('rule_retrieval_time_ms', 0):.2f}ms"
            )

            return result

        except Exception as e:
            logger.error(f"Arbitro workflow failed: {e}", exc_info=True)
            state.error = str(e)
            state.agent_response = "I encountered an error validating your move."
            state.is_valid = False
            return state
