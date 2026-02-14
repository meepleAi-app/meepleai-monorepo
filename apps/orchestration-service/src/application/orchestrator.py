"""
ISSUE-3495: LangGraph Orchestrator
Multi-agent workflow coordination for Tutor, Arbitro, Decisore.
"""

import logging
from datetime import datetime
from typing import Any, Literal, Optional

from langgraph.graph import StateGraph, END

from ..domain import GameAgentState, AgentType, IntentType, ArbitroState, Message
from .arbitro_agent import ArbitroAgent
from .switch_detector import SwitchAgentDetector
from .fallback_strategy import FallbackStrategy
from ..infrastructure.api_client import MeepleAIApiClient

logger = logging.getLogger(__name__)


class GameOrchestrator:
    """
    LangGraph-based orchestrator for multi-agent game assistance.

    Workflow:
    1. Classify user intent
    2. Route to appropriate agent (Tutor/Arbitro/Decisore)
    3. Execute agent logic
    4. Return response
    
    ISSUE-3776: Enhanced with agent switching, context preservation, and fallback strategies.
    """

    def __init__(self, api_client: Optional[MeepleAIApiClient] = None):
        """
        Initialize the LangGraph workflow.
        
        Args:
            api_client: Optional API client for .NET backend (auto-created if None)
        """
        # Initialize agents and utilities
        self.arbitro = ArbitroAgent()
        self.api_client = api_client or MeepleAIApiClient()
        self.switch_detector = SwitchAgentDetector()
        self.fallback = FallbackStrategy()
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
        
        ISSUE-3776: Enhanced with agent switching detection.

        - If user requests agent switch → route to requested agent
        - If intent already classified → route directly to agent
        - If pending move → arbitro
        - Otherwise → classify intent first
        """
        # Check for agent switching request
        if state.user_query:
            switch_target = self.switch_detector.detect_switch(
                state.user_query,
                state.current_agent
            )
            if switch_target:
                logger.info(f"Agent switch requested: {state.current_agent} → {switch_target}")
                state.next_agent = switch_target
                return switch_target.value  # type: ignore

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

    async def _tutor_node(self, state: GameAgentState) -> dict[str, Any]:
        """
        Tutor agent: Handles setup and rules questions.
        
        ISSUE-3776: Integration with C# Tutor API endpoint.
        Calls POST /api/v1/kb/agents/tutor/query with conversation context.
        """
        logger.info(f"Tutor agent processing query: {state.user_query}")
        
        try:
            # Call C# Tutor API with retry fallback
            result = await self.fallback.execute_with_retry(
                self.api_client.tutor_query,
                game_id=state.game_id,
                session_id=state.session_id,
                query=state.user_query or "",
                conversation_history=state.conversation_history,
            )
            
            # Update conversation history with agent response
            updated_history = state.conversation_history + [
                Message(role="user", content=state.user_query or ""),
                Message(role="assistant", content=result.get("response", ""), metadata={"agent": "tutor"}),
            ]
            
            return {
                "agent_response": result.get("response", ""),
                "current_agent": AgentType.TUTOR,
                "confidence_score": result.get("confidence", 0.85),
                "citations": result.get("citations", []),
                "conversation_history": updated_history,
            }
            
        except Exception as e:
            logger.error(f"Tutor node failed after retries: {e}", exc_info=True)
            return self.fallback.get_timeout_fallback(AgentType.TUTOR)

    async def _arbitro_node(self, state: GameAgentState) -> dict[str, Any]:
        """
        Arbitro agent: Validates moves and arbitrates rules disputes.

        ISSUE-3759: Real-time move validation with rule arbitration.
        Target: <100ms P95 latency with Redis tier-1 cache.
        ISSUE-3776: Enhanced with context preservation.
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

            # Execute Arbitro workflow with retry fallback
            result = await self.fallback.execute_with_retry(
                self.arbitro.execute,
                arbitro_state
            )
            
            # Update conversation history
            move_text = state.pending_move.move_notation if state.pending_move else state.user_query
            updated_history = state.conversation_history + [
                Message(role="user", content=f"Validate move: {move_text}"),
                Message(role="assistant", content=result.agent_response, metadata={"agent": "arbitro"}),
            ]

            return {
                "agent_response": result.agent_response,
                "current_agent": AgentType.ARBITRO,
                "confidence_score": result.confidence_score or 0.90,
                "citations": result.citations,
                "conversation_history": updated_history,
            }

        except Exception as e:
            logger.error(f"Arbitro node failed: {e}", exc_info=True)
            return self.fallback.get_timeout_fallback(AgentType.ARBITRO)

    async def _decisore_node(self, state: GameAgentState) -> dict[str, Any]:
        """
        Decisore agent: Provides strategic move suggestions.
        
        ISSUE-3776: Integration with C# Decisore API endpoint.
        Calls POST /api/v1/agents/decisore/analyze for strategic analysis.
        """
        logger.info(f"Decisore agent processing board state for game {state.game_id}")
        
        try:
            # Extract player from board_state or default to "White"
            player_name = state.board_state.current_player if state.board_state else "White"
            
            # Call C# Decisore API with retry fallback
            result = await self.fallback.execute_with_retry(
                self.api_client.decisore_analyze,
                session_id=state.session_id,
                player_name=player_name,
                analysis_depth="standard",
                max_suggestions=3,
            )
            
            # Format suggestions into response text
            suggestions = result.get("suggestions", [])
            response_text = self._format_decisore_response(suggestions, result)
            
            # Update conversation history
            updated_history = state.conversation_history + [
                Message(role="assistant", content=response_text, metadata={"agent": "decisore"}),
            ]
            
            return {
                "agent_response": response_text,
                "current_agent": AgentType.DECISORE,
                "confidence_score": result.get("overallConfidence", 0.75),
                "citations": [],  # Decisore uses strategic reasoning, not citations
                "conversation_history": updated_history,
            }
            
        except Exception as e:
            logger.error(f"Decisore node failed after retries: {e}", exc_info=True)
            return self.fallback.get_timeout_fallback(AgentType.DECISORE)

    def _format_decisore_response(self, suggestions: list, result: dict) -> str:
        """Format Decisore suggestions into readable response."""
        if not suggestions:
            return "No strategic suggestions available for this position."
        
        lines = ["**Strategic Analysis:**\n"]
        for i, sug in enumerate(suggestions[:3], 1):
            move = sug.get("move", {})
            reasoning = sug.get("reasoning", "")
            lines.append(f"{i}. **{move.get('notation', 'N/A')}** - {reasoning}")
        
        return "\n".join(lines)

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