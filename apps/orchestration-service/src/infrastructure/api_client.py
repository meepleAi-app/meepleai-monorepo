"""
ISSUE-3502: API Client for .NET Backend Services
HTTP client for calling HybridSearchEngine and other backend APIs.
"""

import logging
from typing import Optional
from uuid import UUID

import httpx

from ..config import settings

logger = logging.getLogger(__name__)


class MeepleAIApiClient:
    """
    HTTP client for MeepleAI .NET backend API.

    Integrates with:
    - HybridSearchEngine (Issue #3492)
    - Context Engineering framework (Issue #3491)
    """

    def __init__(self, base_url: str = "http://api:8080"):
        """
        Initialize API client.

        Args:
            base_url: .NET API base URL (default: Docker service name)
        """
        self.base_url = base_url.rstrip("/")
        self.client = httpx.AsyncClient(timeout=5.0)

    async def hybrid_search(
        self,
        game_id: UUID,
        query: str,
        intent_type: Optional[str] = None,
        top_k: int = 5,
    ) -> dict:
        """
        Execute hybrid search using .NET HybridSearchEngine.

        Args:
            game_id: Game identifier
            query: Search query
            intent_type: Optional intent for strategy selection
            top_k: Number of results to return

        Returns:
            Search results with chunks and scores

        Raises:
            httpx.HTTPError: If API call fails
        """
        try:
            logger.debug(f"Hybrid search: query='{query[:50]}...', game_id={game_id}")

            # Map intent to retrieval strategy
            strategy = self._intent_to_strategy(intent_type)

            response = await self.client.post(
                f"{self.base_url}/api/v1/knowledge/search",
                json={
                    "gameId": str(game_id),
                    "query": query,
                    "strategy": strategy,
                    "topK": top_k,
                    "includeReranking": True,  # Always use reranking for quality
                },
                timeout=3.0,  # Fast timeout for <1s P95 target
            )

            response.raise_for_status()
            data = response.json()

            logger.info(f"Search returned {len(data.get('results', []))} results")
            return data

        except httpx.HTTPError as e:
            logger.error(f"Hybrid search API call failed: {e}")
            raise

    def _intent_to_strategy(self, intent_type: Optional[str]) -> str:
        """
        Map intent type to retrieval strategy.

        Args:
            intent_type: Intent classification (setup/rules/general)

        Returns:
            Strategy name for HybridSearchEngine
        """
        strategy_map = {
            "setup": "temporal_scoring",  # Recent setup docs more relevant
            "rules": "hybrid_search",  # Rules need keyword + vector
            "general": "capability_matching",  # General queries use capability match
        }

        return strategy_map.get(intent_type or "general", "hybrid_search")

    async def close(self) -> None:
        """Close HTTP client."""
        await self.client.aclose()

    async def tutor_query(
        self,
        game_id: UUID,
        session_id: UUID,
        query: str,
        conversation_history: Optional[list] = None,
    ) -> dict:
        """
        Query Tutor agent for setup and rules questions.

        Args:
            game_id: Game identifier
            session_id: Session identifier
            query: User question
            conversation_history: Optional conversation context

        Returns:
            Tutor response with citations and confidence

        Raises:
            httpx.HTTPError: If API call fails
        """
        try:
            logger.debug(f"Tutor query: session={session_id}, query='{query[:50]}...'")

            response = await self.client.post(
                f"{self.base_url}/api/v1/kb/agents/tutor/query",
                json={
                    "gameId": str(game_id),
                    "sessionId": str(session_id),
                    "query": query,
                },
                timeout=3.0,
            )

            response.raise_for_status()
            data = response.json()

            logger.info(
                f"Tutor response: confidence={data.get('confidence', 0):.2f}, "
                f"time={data.get('executionTimeMs', 0):.1f}ms"
            )
            return data

        except httpx.HTTPError as e:
            logger.error(f"Tutor query API call failed: {e}")
            raise

    async def decisore_analyze(
        self,
        session_id: UUID,
        player_name: str,
        analysis_depth: str = "standard",
        max_suggestions: int = 3,
    ) -> dict:
        """
        Request strategic move analysis from Decisore agent.

        Args:
            session_id: Game session identifier
            player_name: Player requesting analysis (e.g., "White", "Black")
            analysis_depth: Analysis depth (standard|deep)
            max_suggestions: Number of move suggestions to return

        Returns:
            Strategic analysis with move suggestions and reasoning

        Raises:
            httpx.HTTPError: If API call fails
        """
        try:
            logger.debug(
                f"Decisore analyze: session={session_id}, player={player_name}, depth={analysis_depth}"
            )

            response = await self.client.post(
                f"{self.base_url}/api/v1/agents/decisore/analyze",
                json={
                    "gameSessionId": str(session_id),
                    "playerName": player_name,
                    "analysisDepth": analysis_depth,
                    "maxSuggestions": max_suggestions,
                },
                timeout=5.0,  # Longer timeout for strategic analysis
            )

            response.raise_for_status()
            data = response.json()

            logger.info(
                f"Decisore analysis: suggestions={len(data.get('suggestions', []))}, "
                f"time={data.get('executionTimeMs', 0):.1f}ms"
            )
            return data

        except httpx.HTTPError as e:
            logger.error(f"Decisore analyze API call failed: {e}")
            raise