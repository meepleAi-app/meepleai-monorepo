"""
ISSUE-3495: API Request/Response Schemas
Pydantic models for REST API validation.
"""

from datetime import datetime, UTC
from typing import Optional
from uuid import UUID

from pydantic import BaseModel, Field


class ExecuteWorkflowRequest(BaseModel):
    """Request to execute an agent workflow."""

    game_id: UUID = Field(description="Game identifier")
    session_id: UUID = Field(description="Session identifier")
    query: str = Field(min_length=1, max_length=2000, description="User query")
    board_state: Optional[dict] = Field(default=None, description="Optional game board state")
    pending_move: Optional[str] = Field(default=None, description="Optional move to validate")

    model_config = {"json_schema_extra": {
        "example": {
            "game_id": "123e4567-e89b-12d3-a456-426614174000",
            "session_id": "123e4567-e89b-12d3-a456-426614174001",
            "query": "How do I set up the game?",
            "board_state": None,
            "pending_move": None,
        }
    }}


class ExecuteWorkflowResponse(BaseModel):
    """Response from workflow execution."""

    agent_type: str = Field(description="Agent that handled the request")
    response: str = Field(description="Agent response text")
    confidence: float = Field(ge=0.0, le=1.0, description="Response confidence score")
    citations: list[str] = Field(default_factory=list, description="Source citations")
    execution_time_ms: float = Field(description="Workflow execution time")
    session_id: UUID = Field(description="Session identifier")
    error: Optional[str] = Field(default=None, description="Error message if failed")

    model_config = {"json_schema_extra": {
        "example": {
            "agent_type": "tutor",
            "response": "To set up the game, first place the board...",
            "confidence": 0.92,
            "citations": ["rulebook.pdf:p5", "setup_guide.pdf:p2"],
            "execution_time_ms": 245.3,
            "session_id": "123e4567-e89b-12d3-a456-426614174001",
            "error": None,
        }
    }}


class HealthResponse(BaseModel):
    """Health check response."""

    status: str = Field(description="Service status (healthy/unhealthy)")
    version: str = Field(description="Service version")
    dependencies: dict[str, str] = Field(description="Dependent service statuses")
    timestamp: datetime = Field(default_factory=lambda: datetime.now(UTC))

    model_config = {"json_schema_extra": {
        "example": {
            "status": "healthy",
            "version": "0.1.0",
            "dependencies": {
                "embedding": "healthy",
                "reranker": "healthy",
                "database": "healthy",
            },
            "timestamp": "2026-02-04T10:30:00Z",
        }
    }}


class ErrorResponse(BaseModel):
    """Error response."""

    error: dict = Field(description="Error details")

    model_config = {"json_schema_extra": {
        "example": {
            "error": {
                "code": "WORKFLOW_ERROR",
                "message": "Failed to execute workflow",
                "request_id": "abc123",
            }
        }
    }}
