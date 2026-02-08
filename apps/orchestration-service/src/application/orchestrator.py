"""
ISSUE-3495: LangGraph Orchestrator
Multi-agent workflow coordination for Tutor, Arbitro, Decisore.
"""

import logging
from datetime import datetime
from typing import Any, Literal

from langgraph.graph import StateGraph, END

from ..domain import GameAgentState, AgentType, IntentType, ArbitroState
from .arbitro_agent import ArbitroAgent

logger = logging.getLogger(__name__)


class GameOrchestrator:
    """
    LangGraph-based orchestrator for multi-agent game assistance.

    Workflow:
    1. Classify user intent
    2. Route to appropriate agent (Tutor/Arbitro/Decisore)
    3. Execute agent logic
    4. Return response
    """

    def __init__(self):
        """Initialize the LangGraph workflow."""
        # Initialize agents
        self.arbitro = ArbitroAgent()
        self.graph = self._build_graph()

    def _build_graph(self) -> StateGraph:
        """
        Build the LangGraph state machine.

        Nodes:
        - classify_intent: Determine user intent from query
        - tutor_agent: Tutorial and setup questions
        - arbitro_agent: Rules arbitration and move validation
        - decisore_agent: Strategic move suggestions
        - format_response: Prepare final response
        """
        # Create graph with GameAgentState
        graph = StateGraph(GameAgentState)

        # Add nodes
        graph.add_node("classify_intent", self._classify_intent_node)
        graph.add_node("tutor_agent", self._tutor_node)
        graph.add_node("arbitro_agent", self._arbitro_node)
        graph.add_node("decisore_agent", self._decisore_node)
        graph.add_node("format_response", self._format_response_node)

        # Set entry point with conditional routing
        graph.set_conditional_entry_point(
            self._route_entry,
            {
                "classify": "classify_intent",
                "tutor": "tutor_agent",
                "arbitro": "arbitro_agent",
                "decisore": "decisore_agent",
            }
        )

        # Add conditional edges from classify_intent
        graph.add_conditional_edges(
            "classify_intent",
            self._route_by_intent,
            {
                "tutor": "tutor_agent",
                "arbitro": "arbitro_agent",
                "decisore": "decisore_agent",
                "error": END,
            }
        )

        # All agents route to format_response
        graph.add_edge("tutor_agent", "format_response")
        graph.add_edge("arbitro_agent", "format_response")
        graph.add_edge("decisore_agent", "format_response")
        graph.add_edge("format_response", END)

        return graph.compile()

    def _route_entry(self, state: GameAgentState) -> Literal["classify", "tutor", "arbitro", "decisore"]:
        """
        Determine entry point based on existing state.

        - If intent already classified → route directly to agent
        - If pending move → arbitro
        - Otherwise → classify intent first
        """
        if state.pending_move:
            return "arbitro"

        if state.intent == IntentType.SETUP_QUESTION or state.intent == IntentType.RULES_QUESTION:
            return "tutor"
        elif state.intent == IntentType.MOVE_VALIDATION:
            return "arbitro"
        elif state.intent == IntentType.MOVE_SUGGESTION:
            return "decisore"

        # Default: classify intent first
        return "classify"

    def _route_by_intent(self, state: GameAgentState) -> Literal["tutor", "arbitro", "decisore", "error"]:
        """Route to appropriate agent based on classified intent."""
        if state.error:
            return "error"

        intent_to_agent = {
            IntentType.SETUP_QUESTION: "tutor",
            IntentType.RULES_QUESTION: "tutor",
            IntentType.GENERAL: "tutor",
            IntentType.MOVE_VALIDATION: "arbitro",
            IntentType.MOVE_SUGGESTION: "decisore",
        }

        agent = intent_to_agent.get(state.intent, "tutor")
        logger.info(f"Routing intent {state.intent} to {agent} agent")
        return agent  # type: ignore

    def _classify_intent_node(self, state: GameAgentState) -> dict[str, Any]:
        """
        Classify user intent using simple keyword matching.

        In production, this would use an LLM for classification.
        ISSUE-3496 will implement proper intent classification.
        """
        query = (state.user_query or "").lower()

        # Simple keyword-based classification (placeholder)
        if any(word in query for word in ["setup", "how to start", "tutorial", "learn"]):
            intent = IntentType.SETUP_QUESTION
        elif any(word in query for word in ["rule", "legal", "allowed", "can i"]):
            intent = IntentType.RULES_QUESTION
        elif any(word in query for word in ["validate", "check move", "is this valid"]):
            intent = IntentType.MOVE_VALIDATION
        elif any(word in query for word in ["suggest", "recommend", "what should i", "best move"]):
            intent = IntentType.MOVE_SUGGESTION
        else:
            intent = IntentType.GENERAL

        logger.info(f"Classified query intent: {intent}")

        return {
            "intent": intent,
            "current_agent": AgentType.TUTOR if intent in [IntentType.SETUP_QUESTION, IntentType.RULES_QUESTION] else None,
        }

    def _tutor_node(self, state: GameAgentState) -> dict[str, Any]:
        """
        Tutor agent: Handles setup and rules questions.

        In production, this will:
        1. Retrieve relevant context from PostgreSQL (conversation memory)
        2. Search knowledge base (Qdrant hybrid search)
        3. Generate response with LLM
        4. Store interaction in memory

        For now: Returns placeholder response.
        """
        logger.info(f"Tutor agent processing query: {state.user_query}")

        # Placeholder response
        response = f"[TUTOR AGENT] I can help you with that question. (Query: {state.user_query})"

        return {
            "agent_response": response,
            "current_agent": AgentType.TUTOR,
            "confidence_score": 0.85,
            "citations": [],
        }

    async def _arbitro_node(self, state: GameAgentState) -> dict[str, Any]:
        """
        Arbitro agent: Validates moves and arbitrates rules disputes.

        ISSUE-3759: Real-time move validation with rule arbitration.
        Target: <100ms P95 latency with Redis tier-1 cache.
        """
        logger.info(f"Arbitro agent processing: {state.pending_move or state.user_query}")

        try:
            # Create ArbitroState from GameAgentState
            arbitro_state = ArbitroState(
                game_id=state.game_id,
                session_id=state.session_id,
                user_query=state.user_query,
                intent=state.intent,
                board_state=state.board_state,
                pending_move=state.pending_move,
                current_agent=AgentType.ARBITRO,
                conversation_history=state.conversation_history,
                move_notation=state.pending_move.move_notation if state.pending_move else state.user_query,
                game_state=None,  # TODO: Extract from board_state
            )

            # Execute Arbitro workflow
            result = await self.arbitro.execute(arbitro_state)

            return {
                "agent_response": result.agent_response,
                "current_agent": AgentType.ARBITRO,
                "confidence_score": result.confidence_score or 0.90,
                "citations": result.citations,
            }

        except Exception as e:
            logger.error(f"Arbitro node failed: {e}", exc_info=True)
            return {
                "agent_response": "I encountered an error validating the move.",
                "current_agent": AgentType.ARBITRO,
                "confidence_score": 0.0,
                "citations": [],
                "error": str(e),
            }

    def _decisore_node(self, state: GameAgentState) -> dict[str, Any]:
        """
        Decisore agent: Provides strategic move suggestions.

        In production, this will:
        1. Analyze board state
        2. Generate candidate moves
        3. Evaluate moves with strategic reasoning
        4. Rank and return top suggestions

        For now: Returns placeholder response.
        """
        logger.info(f"Decisore agent processing board state for game {state.game_id}")

        response = "[DECISORE AGENT] Strategic analysis placeholder. Real implementation in Phase 4."

        return {
            "agent_response": response,
            "current_agent": AgentType.DECISORE,
            "confidence_score": 0.75,
            "citations": [],
        }

    def _format_response_node(self, state: GameAgentState) -> dict[str, Any]:
        """Format final response with metadata."""
        logger.info(f"Formatting response from {state.current_agent} agent")

        return {
            "updated_at": datetime.utcnow(),
        }

    async def execute(self, state: GameAgentState) -> GameAgentState:
        """
        Execute the orchestration workflow.

        Args:
            state: Initial game agent state

        Returns:
            Updated state after workflow execution
        """
        try:
            logger.info(f"Starting workflow for session {state.session_id}")

            # Run the compiled graph
            result = await self.graph.ainvoke(state)

            logger.info(f"Workflow completed successfully for session {state.session_id}")
            return result

        except Exception as e:
            logger.error(f"Workflow execution failed: {e}", exc_info=True)
            state.error = str(e)
            state.agent_response = "I encountered an error processing your request. Please try again."
            return state
