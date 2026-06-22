"""Shared pytest fixtures and environment defaults for orchestration-service tests.

The orchestration service refuses to start without OPENROUTER_API_KEY (see
`main.lifespan` and `Settings.is_llm_configured`). CI workflows export a stub
value (`OPENROUTER_API_KEY=test-key`); this conftest mirrors that contract for
local `pytest` invocations so unit tests don't need to know about the guard.
"""

import os


def _default_env_for_tests() -> None:
    """Ensure a non-empty OPENROUTER_API_KEY is present before settings load."""
    os.environ.setdefault("OPENROUTER_API_KEY", "test-key")


_default_env_for_tests()
