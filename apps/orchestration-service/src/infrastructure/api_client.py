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
