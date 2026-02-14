"""
ISSUE-3776: Agent Switching Detection
Detect mid-conversation requests to switch between agents.
"""

import logging
import re
from typing import Optional

from ..domain import AgentType

logger = logging.getLogger(__name__)


class SwitchAgentDetector:
    """
    Detects user requests to switch agents mid-conversation.

    Examples:
    - "Actually, let me ask the Decisore instead"
    - "Switch to Arbitro to validate this move"
    - "Can Tutor explain the rules?"
    """

    # Agent-specific keywords
    AGENT_PATTERNS = {
        AgentType.TUTOR: [
            r"\btutor\b",
            r"\bteach\b",
            r"\bexplain\b",
            r"\brules?\b",
            r"\bsetup\b",
            r"\bhow to\b",
        ],
        AgentType.ARBITRO: [
            r"\barbitro\b",
            r"\bvalidate\b",
            r"\bcheck move\b",
            r"\blegal\b",
            r"\ballowed\b",
            r"\bcan i\b",
        ],
        AgentType.DECISORE: [
            r"\bdecisore\b",
            r"\bstrategy\b",
            r"\bsuggest\b",
            r"\brecommend\b",
            r"\bbest move\b",
            r"\bwhat should i\b",
        ],
    }

    # Switching trigger phrases
    SWITCH_TRIGGERS = [
        r"\bswitch to\b",
        r"\bask.*instead\b",
        r"\blet.*answer\b",
        r"\bcan\b.*(tutor|arbitro|decisore)",  # "Can Decisore suggest..."
        r"\bactually\b.*\b(tutor|arbitro|decisore)\b",
        r"\binstead\b",  # "Ask Decisore instead"
        r"\bwait\b.*\blet\b",  # "Wait, let Tutor..."
    ]

    def detect_switch(self, query: str, current_agent: Optional[AgentType]) -> Optional[AgentType]:
        """
        Detect if user wants to switch to a different agent.

        Args:
            query: User query text
            current_agent: Currently active agent (if any)

        Returns:
            AgentType to switch to, or None if no switch detected
        """
        if not query:
            return None

        query_lower = query.lower()

        # Check if query contains switch trigger
        has_switch_trigger = any(
            re.search(pattern, query_lower)
            for pattern in self.SWITCH_TRIGGERS
        )

        if not has_switch_trigger:
            # No explicit switch request
            return None

        # Detect which agent is requested
        for agent_type, patterns in self.AGENT_PATTERNS.items():
            if any(re.search(pattern, query_lower) for pattern in patterns):
                if agent_type != current_agent:
                    logger.info(f"Switch detected: {current_agent} → {agent_type}")
                    return agent_type

        # Switch trigger found but no specific agent mentioned
        logger.debug("Switch trigger detected but no target agent identified")
        return None

    def requires_multi_agent(self, query: str) -> list[AgentType]:
        """
        Detect if query requires multiple agents (experimental feature).

        Example: "What's the best move and why is it legal?"
        → [DECISORE, ARBITRO]

        Args:
            query: User query text

        Returns:
            List of agent types needed (empty if single-agent query)
        """
        query_lower = query.lower()
        agents_needed = []

        for agent_type, patterns in self.AGENT_PATTERNS.items():
            if any(re.search(pattern, query_lower) for pattern in patterns):
                agents_needed.append(agent_type)

        # Return only if multiple agents detected
        if len(agents_needed) > 1:
            logger.info(f"Multi-agent query detected: {agents_needed}")
            return agents_needed

        return []
