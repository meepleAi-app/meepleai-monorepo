"""
ISSUE-3497: Tutor Agent with Multi-Turn Dialogue
LangGraph-based stateful conversation with context retention across 10+ turns.
"""

import logging
from typing import Any

from langgraph.graph import StateGraph, END
from langchain_core.prompts import ChatPromptTemplate
from langchain_openai import ChatOpenAI

from ..config import settings
from ..domain import TutorState, IntentType

logger = logging.getLogger(__name__)


class TutorAgent:
    """
    Tutor agent for handling setup and rules questions.

    Features:
    - Multi-turn dialogue with context retention
    - Automatic summarization for conversations >10 turns
    - Integration with hybrid search (placeholder)
    - Conversation memory persistence (placeholder)
    """

    SUMMARIZATION_PROMPT = ChatPromptTemplate.from_messages([
        ("system", """You are a conversation summarizer for a board game tutor AI.

Summarize the conversation history into a concise context paragraph that:
1. Captures the main topics discussed
2. Notes any unresolved questions
3. Highlights key information provided
4. Maintains game-specific context

Keep the summary under 200 words."""),
        ("user", """Summarize this conversation:

{conversation_history}

Provide a concise summary that preserves key context for future turns.""")
    ])

    TUTOR_PROMPT = ChatPromptTemplate.from_messages([
        ("system", """You are a helpful board game tutor assistant.

Your role:
- Answer setup questions (how to start, initial configuration)
- Explain game rules clearly and accurately
- Provide helpful examples and clarifications
- Maintain friendly, patient tone

Use this context from the conversation:
{context}

Retrieved knowledge:
{retrieved_context}

Answer the user's question helpfully and concisely."""),
        ("user", "{query}")
    ])

    def __init__(self):
        """Initialize Tutor agent with LangGraph workflow."""
        self.llm = ChatOpenAI(
            model="gpt-4o-mini",
            temperature=0.7,
            api_key=settings.openrouter_api_key,
            base_url=settings.openrouter_base_url,
            default_headers={"HTTP-Referer": "https://meepleai.app", "X-Title": "MeepleAI"},
            timeout=10.0,
        )
        self.graph = self._build_workflow()

    def _build_workflow(self) -> StateGraph:
        """
        Build the Tutor agent LangGraph workflow.

        Nodes:
        - check_context: Determine if summarization needed
        - summarize: Condense conversation history
        - search_knowledge: Retrieve relevant chunks (placeholder)
        - generate_response: Create tutor response
        - save_memory: Persist conversation state (placeholder)
        """
        graph = StateGraph(TutorState)

        # Add nodes
        graph.add_node("check_context", self._check_context_node)
        graph.add_node("summarize", self._summarize_node)
        graph.add_node("search_knowledge", self._search_knowledge_node)
        graph.add_node("generate_response", self._generate_response_node)
        graph.add_node("save_memory", self._save_memory_node)

        # Set entry point
        graph.set_entry_point("check_context")

        # Conditional routing from check_context
        graph.add_conditional_edges(
            "check_context",
            self._should_summarize,
            {
                "summarize": "summarize",
                "search": "search_knowledge",
            }
        )

        # Linear flow after summarization or direct to search
        graph.add_edge("summarize", "search_knowledge")
        graph.add_edge("search_knowledge", "generate_response")
        graph.add_edge("generate_response", "save_memory")
        graph.add_edge("save_memory", END)

        return graph.compile()

    def _should_summarize(self, state: TutorState) -> str:
        """Determine if conversation needs summarization."""
        if state.should_summarize():
            logger.info(f"Conversation needs summarization (turns: {state.turn_count})")
            return "summarize"
        return "search"

    def _check_context_node(self, state: TutorState) -> dict[str, Any]:
        """Check conversation context and prepare for processing."""
        logger.info(f"Processing turn {state.turn_count + 1} for session {state.session_id}")

        return {
            "updated_at": datetime.utcnow(),
        }

    async def _summarize_node(self, state: TutorState) -> dict[str, Any]:
        """
        Summarize conversation history to maintain context efficiency.

        Triggered when conversation exceeds max_turns_before_summary.
        """
        logger.info("Summarizing conversation history")

        try:
            # Format conversation history
            history_text = "\n".join([
                f"{msg.role.upper()}: {msg.content}"
                for msg in state.conversation_history
            ])

            # Generate summary
            chain = self.SUMMARIZATION_PROMPT | self.llm
            response = await chain.ainvoke({"conversation_history": history_text})

            summary = response.content

            logger.info(f"Generated summary: {summary[:100]}...")

            return {
                "conversation_summary": summary,
                "needs_summarization": False,
            }

        except Exception as e:
            logger.error(f"Summarization failed: {e}", exc_info=True)
            # Continue without summary
            return {"needs_summarization": False}

    async def _search_knowledge_node(self, state: TutorState) -> dict[str, Any]:
        """
        Search knowledge base for relevant information.

        PLACEHOLDER: Real implementation in Issue #3502 (Hybrid Search Integration).
        """
        logger.info("Searching knowledge base")

        # Placeholder: Would integrate with HybridSearchEngine
        # from context engineering framework (Issue #3492)

        return {
            "retrieved_chunks": [],
            "search_query": state.user_query,
        }

    async def _generate_response_node(self, state: TutorState) -> dict[str, Any]:
        """Generate tutor response using conversation context and retrieved knowledge."""
        logger.info("Generating tutor response")

        try:
            # Build context from summary or recent history
            if state.conversation_summary:
                context = f"Conversation summary:\n{state.conversation_summary}\n\nRecent turns:\n"
                context += "\n".join([
                    f"{msg.role}: {msg.content}"
                    for msg in state.get_recent_context(num_turns=2)
                ])
            else:
                context = "\n".join([
                    f"{msg.role}: {msg.content}"
                    for msg in state.get_recent_context(num_turns=5)
                ])

            # Retrieved context (placeholder - will be from hybrid search)
            retrieved_context = "No additional knowledge retrieved (hybrid search not yet integrated)."

            # Generate response
            chain = self.TUTOR_PROMPT | self.llm
            response = await chain.ainvoke({
                "context": context,
                "retrieved_context": retrieved_context,
                "query": state.user_query or "",
            })

            return {
                "agent_response": response.content,
                "confidence_score": 0.85,  # Placeholder - real confidence scoring in future
            }

        except Exception as e:
            logger.error(f"Response generation failed: {e}", exc_info=True)
            return {
                "agent_response": "I apologize, but I encountered an error. Could you rephrase your question?",
                "confidence_score": 0.0,
                "error": str(e),
            }

    async def _save_memory_node(self, state: TutorState) -> dict[str, Any]:
        """
        Save conversation state to PostgreSQL.

        PLACEHOLDER: Real implementation in Issue #3498 (Conversation Memory).
        """
        logger.info("Saving conversation memory")

        # Placeholder: Would persist to ConversationMemoryRepository
        # using PostgreSQL schema from Issue #3493

        return {}

    async def execute(self, state: TutorState) -> TutorState:
        """
        Execute the tutor workflow.

        Args:
            state: Current tutor state

        Returns:
            Updated state after workflow execution
        """
        try:
            logger.info(f"Starting tutor workflow for session {state.session_id}")

            # Run the compiled graph
            result = await self.graph.ainvoke(state)

            # Add the turn to history
            if state.user_query and result.get("agent_response"):
                result.add_turn(state.user_query, result.agent_response)

            logger.info(f"Tutor workflow completed (turn {result.turn_count})")
            return result

        except Exception as e:
            logger.error(f"Tutor workflow failed: {e}", exc_info=True)
            state.error = str(e)
            state.agent_response = "I encountered an error. Please try again."
            return state
