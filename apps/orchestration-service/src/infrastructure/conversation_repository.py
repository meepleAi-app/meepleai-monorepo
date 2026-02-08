"""
ISSUE-3497: Conversation State Persistence
PostgreSQL repository for storing dialogue state across sessions.
"""

import json
import logging
from datetime import datetime
from typing import Any, Optional
from uuid import UUID

import asyncpg

from ..config import settings
from ..domain import TutorState, Message

logger = logging.getLogger(__name__)


class ConversationRepository:
    """
    Repository for persisting conversation state to PostgreSQL.

    Uses ConversationMemory table from Issue #3493 schema.
    """

    def __init__(self, database_url: Optional[str] = None):
        """
        Initialize repository.

        Args:
            database_url: PostgreSQL connection URL
        """
        self.database_url = database_url or settings.database_url
        self.pool: Optional[asyncpg.Pool] = None

    async def connect(self) -> None:
        """Establish database connection pool."""
        try:
            self.pool = await asyncpg.create_pool(
                self.database_url,
                min_size=2,
                max_size=10,
                command_timeout=5.0,
            )
            logger.info("✅ PostgreSQL connection pool created")
        except Exception as e:
            logger.error(f"Failed to connect to PostgreSQL: {e}")
            self.pool = None

    async def disconnect(self) -> None:
        """Close database connection pool."""
        if self.pool:
            await self.pool.close()
            logger.info("PostgreSQL connection pool closed")

    async def save_state(self, state: TutorState) -> None:
        """
        Save conversation state to database.

        Args:
            state: TutorState to persist
        """
        if not self.pool:
            logger.warning("Database pool not available, skipping state save")
            return

        try:
            # Serialize conversation history and metadata
            history_json = json.dumps([
                {
                    "role": msg.role,
                    "content": msg.content,
                    "timestamp": msg.timestamp.isoformat(),
                }
                for msg in state.conversation_history
            ])

            metadata_json = json.dumps({
                "turn_count": state.turn_count,
                "conversation_summary": state.conversation_summary,
                "previous_topics": state.previous_topics,
                "intent": state.intent.value if state.intent else None,
            })

            async with self.pool.acquire() as conn:
                # Upsert conversation memory
                await conn.execute("""
                    INSERT INTO "ConversationMemory" (
                        "Id", "GameId", "SessionId", "UserQuery", "AssistantResponse",
                        "ConversationHistory", "Metadata", "CreatedAt", "UpdatedAt"
                    ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
                    ON CONFLICT ("Id") DO UPDATE SET
                        "ConversationHistory" = EXCLUDED."ConversationHistory",
                        "Metadata" = EXCLUDED."Metadata",
                        "UpdatedAt" = EXCLUDED."UpdatedAt"
                """,
                    state.session_id,
                    state.game_id,
                    state.session_id,
                    state.user_query or "",
                    state.agent_response or "",
                    history_json,
                    metadata_json,
                    datetime.utcnow(),
                    datetime.utcnow(),
                )

            logger.info(f"Saved conversation state for session {state.session_id}")

        except Exception as e:
            logger.error(f"Failed to save conversation state: {e}", exc_info=True)

    async def load_state(self, session_id: UUID) -> Optional[TutorState]:
        """
        Load conversation state from database.

        Args:
            session_id: Session identifier

        Returns:
            TutorState if found, None otherwise
        """
        if not self.pool:
            logger.warning("Database pool not available, cannot load state")
            return None

        try:
            async with self.pool.acquire() as conn:
                row = await conn.fetchrow("""
                    SELECT "GameId", "SessionId", "ConversationHistory", "Metadata", "CreatedAt"
                    FROM "ConversationMemory"
                    WHERE "SessionId" = $1
                    ORDER BY "UpdatedAt" DESC
                    LIMIT 1
                """, session_id)

                if not row:
                    return None

                # Deserialize conversation history
                history_data = json.loads(row["ConversationHistory"])
                conversation_history = [
                    Message(
                        role=msg["role"],
                        content=msg["content"],
                        timestamp=datetime.fromisoformat(msg["timestamp"]),
                    )
                    for msg in history_data
                ]

                # Deserialize metadata
                metadata = json.loads(row["Metadata"])

                # Reconstruct state
                state = TutorState(
                    game_id=row["GameId"],
                    session_id=row["SessionId"],
                    conversation_history=conversation_history,
                    turn_count=metadata.get("turn_count", 0),
                    conversation_summary=metadata.get("conversation_summary"),
                    previous_topics=metadata.get("previous_topics", []),
                    created_at=row["CreatedAt"],
                )

                logger.info(f"Loaded conversation state for session {session_id} ({state.turn_count} turns)")
                return state

        except Exception as e:
            logger.error(f"Failed to load conversation state: {e}", exc_info=True)
            return None

    def _check_context_node(self, state: TutorState) -> dict[str, Any]:
        """Check if context management needed."""
        logger.debug(f"Checking context (turn {state.turn_count})")
        return {}

    async def _summarize_node(self, state: TutorState) -> dict[str, Any]:
        """Summarize conversation history for context efficiency."""
        logger.info(f"Summarizing {len(state.conversation_history)} messages")

        try:
            history_text = "\n".join([
                f"{msg.role.upper()}: {msg.content}"
                for msg in state.conversation_history
            ])

            chain = self.SUMMARIZATION_PROMPT | self.llm
            response = await chain.ainvoke({"conversation_history": history_text})

            return {
                "conversation_summary": response.content,
                "needs_summarization": False,
            }

        except Exception as e:
            logger.error(f"Summarization failed: {e}")
            return {"needs_summarization": False}

    async def _search_knowledge_node(self, state: TutorState) -> dict[str, Any]:
        """Search knowledge base (placeholder for Issue #3502)."""
        logger.debug("Searching knowledge base (placeholder)")
        return {"retrieved_chunks": []}

    async def _generate_response_node(self, state: TutorState) -> dict[str, Any]:
        """Generate tutor response with conversation context."""
        logger.info("Generating response")

        try:
            # Build context
            if state.conversation_summary:
                context = f"{state.conversation_summary}\n\nRecent:\n"
                context += "\n".join([f"{m.role}: {m.content}" for m in state.get_recent_context(2)])
            else:
                context = "\n".join([f"{m.role}: {m.content}" for m in state.get_recent_context(5)])

            # Generate
            chain = self.TUTOR_PROMPT | self.llm
            response = await chain.ainvoke({
                "context": context,
                "retrieved_context": "Hybrid search integration pending (Issue #3502)",
                "query": state.user_query or "",
            })

            return {
                "agent_response": response.content,
                "confidence_score": 0.85,
            }

        except Exception as e:
            logger.error(f"Response generation failed: {e}")
            return {
                "agent_response": "I encountered an error. Please try again.",
                "confidence_score": 0.0,
                "error": str(e),
            }

    async def _save_memory_node(self, state: TutorState) -> dict[str, Any]:
        """Save conversation state (handled by repository in main.py)."""
        logger.debug("Memory save triggered")
        return {}

    async def execute(self, state: TutorState) -> TutorState:
        """
        Execute tutor workflow.

        Args:
            state: Current tutor state

        Returns:
            Updated state after workflow
        """
        try:
            result = await self.graph.ainvoke(state)

            # Add turn to history
            if state.user_query and result.agent_response:
                result.add_turn(state.user_query, result.agent_response)

            return result

        except Exception as e:
            logger.error(f"Tutor workflow failed: {e}", exc_info=True)
            state.error = str(e)
            return state
