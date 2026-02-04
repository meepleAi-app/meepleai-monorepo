"""
ISSUE-3495: LangGraph Orchestration Service
FastAPI application for multi-agent game orchestration.
"""

import logging
import time
import uuid
from contextlib import asynccontextmanager
from datetime import datetime, UTC
from typing import AsyncIterator

import httpx
from fastapi import FastAPI, HTTPException, status
from fastapi.responses import PlainTextResponse

from src.api import (
    ExecuteWorkflowRequest,
    ExecuteWorkflowResponse,
    HealthResponse,
    ErrorResponse,
)
from src.application import GameOrchestrator, IntentClassifier
from src.config import settings
from src.domain import GameAgentState, Message
from src.infrastructure import IntentCache

# Configure logging
logging.basicConfig(
    level=getattr(logging, settings.log_level.upper()),
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)

# Global instances
orchestrator: GameOrchestrator = None  # type: ignore
intent_cache: IntentCache = None  # type: ignore


# Metrics (simple in-memory counters for Prometheus)
metrics = {
    "workflow_executions_total": 0,
    "workflow_failures_total": 0,
    "workflow_duration_ms_sum": 0.0,
    "workflow_duration_ms_count": 0,
    "health_checks_total": 0,
}


@asynccontextmanager
async def lifespan(app: FastAPI) -> AsyncIterator[None]:
    """Application lifespan manager."""
    global orchestrator, intent_cache

    logger.info("🚀 Starting MeepleAI Orchestration Service")
    logger.info(f"Config: LangGraph timeout={settings.langgraph_timeout}s, max_depth={settings.max_workflow_depth}")

    try:
        # Initialize Redis cache for intent classification (ISSUE-3496)
        intent_cache = IntentCache()
        await intent_cache.connect()

        # Initialize intent classifier with Redis cache
        intent_classifier = IntentClassifier(redis_cache=intent_cache)
        logger.info("✅ Intent classifier initialized with Redis cache")

        # Initialize LangGraph orchestrator with intent classifier
        orchestrator = GameOrchestrator(intent_classifier=intent_classifier)
        logger.info("✅ LangGraph orchestrator initialized successfully")

        yield

    finally:
        logger.info("🛑 Shutting down MeepleAI Orchestration Service")
        if intent_cache:
            await intent_cache.disconnect()


# Create FastAPI app
app = FastAPI(
    title="MeepleAI Orchestration Service",
    description="LangGraph-based multi-agent orchestration for game AI (Issue #3495)",
    version="0.1.0",
    lifespan=lifespan,
)


@app.get("/", tags=["Info"])
async def root():
    """Service information endpoint."""
    return {
        "service": "MeepleAI Orchestration Service",
        "version": "0.1.0",
        "issue": "3495",
        "description": "LangGraph-based multi-agent orchestration",
        "agents": ["tutor", "arbitro", "decisore"],
        "endpoints": {
            "health": "/health",
            "execute": "/execute",
            "metrics": "/metrics",
        },
    }


@app.get("/health", response_model=HealthResponse, tags=["Health"])
async def health_check():
    """
    Health check endpoint with dependency validation.

    Checks:
    - Service is running
    - Dependent services are reachable
    - LangGraph orchestrator is initialized
    """
    global orchestrator
    metrics["health_checks_total"] += 1

    dependencies = {}

    # Check dependent services
    async with httpx.AsyncClient(timeout=2.0) as client:
        services_to_check = {
            "embedding": settings.embedding_service_url,
            "reranker": settings.reranker_service_url,
        }

        for service_name, service_url in services_to_check.items():
            try:
                response = await client.get(f"{service_url}/health")
                dependencies[service_name] = "healthy" if response.status_code == 200 else "unhealthy"
            except Exception as e:
                logger.warning(f"Health check failed for {service_name}: {e}")
                dependencies[service_name] = "unhealthy"

    # Check orchestrator initialization
    if orchestrator is None:
        dependencies["orchestrator"] = "uninitialized"
        overall_status = "unhealthy"
    else:
        dependencies["orchestrator"] = "healthy"
        overall_status = "healthy" if all(s == "healthy" for s in dependencies.values()) else "degraded"

    return HealthResponse(
        status=overall_status,
        version="0.1.0",
        dependencies=dependencies,
        timestamp=datetime.now(UTC),
    )


@app.post("/execute", response_model=ExecuteWorkflowResponse, tags=["Orchestration"])
async def execute_workflow(request: ExecuteWorkflowRequest):
    """
    Execute multi-agent workflow.

    Routes the request through LangGraph orchestration:
    1. Classify user intent
    2. Route to appropriate agent (Tutor/Arbitro/Decisore)
    3. Execute agent logic
    4. Return response with citations
    """
    request_id = str(uuid.uuid4())
    start_time = time.perf_counter()

    try:
        logger.info(f"[{request_id}] Executing workflow for session {request.session_id}")

        # Build initial state
        state = GameAgentState(
            game_id=request.game_id,
            session_id=request.session_id,
            user_query=request.query,
            conversation_history=[
                Message(role="user", content=request.query)
            ],
        )

        # Execute workflow
        result_state = await orchestrator.execute(state)

        # Calculate execution time
        execution_time_ms = (time.perf_counter() - start_time) * 1000

        # Update metrics
        metrics["workflow_executions_total"] += 1
        metrics["workflow_duration_ms_sum"] += execution_time_ms
        metrics["workflow_duration_ms_count"] += 1

        # Handle workflow errors
        if result_state.error:
            metrics["workflow_failures_total"] += 1
            logger.error(f"[{request_id}] Workflow failed: {result_state.error}")

            return ExecuteWorkflowResponse(
                agent_type=result_state.current_agent.value if result_state.current_agent else "unknown",
                response=result_state.agent_response or "An error occurred",
                confidence=0.0,
                citations=[],
                execution_time_ms=execution_time_ms,
                session_id=request.session_id,
                error=result_state.error,
            )

        # Success response
        logger.info(
            f"[{request_id}] Workflow completed: agent={result_state.current_agent}, "
            f"confidence={result_state.confidence_score}, time={execution_time_ms:.1f}ms"
        )

        return ExecuteWorkflowResponse(
            agent_type=result_state.current_agent.value if result_state.current_agent else "unknown",
            response=result_state.agent_response or "",
            confidence=result_state.confidence_score or 0.0,
            citations=result_state.citations,
            execution_time_ms=execution_time_ms,
            session_id=request.session_id,
            error=None,
        )

    except Exception as e:
        execution_time_ms = (time.perf_counter() - start_time) * 1000
        metrics["workflow_failures_total"] += 1

        logger.error(f"[{request_id}] Unhandled exception: {e}", exc_info=True)

        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail={
                "code": "WORKFLOW_ERROR",
                "message": str(e),
                "request_id": request_id,
            },
        )


@app.get("/metrics", response_class=PlainTextResponse, tags=["Monitoring"])
async def prometheus_metrics():
    """
    Prometheus metrics endpoint.

    Exports workflow execution metrics in Prometheus text format.
    """
    if not settings.metrics_enabled:
        return PlainTextResponse("# Metrics disabled\n")

    # Calculate average duration
    avg_duration = (
        metrics["workflow_duration_ms_sum"] / metrics["workflow_duration_ms_count"]
        if metrics["workflow_duration_ms_count"] > 0
        else 0.0
    )

    lines = [
        "# HELP workflow_executions_total Total workflow executions",
        "# TYPE workflow_executions_total counter",
        f"workflow_executions_total {metrics['workflow_executions_total']}",
        "",
        "# HELP workflow_failures_total Total workflow failures",
        "# TYPE workflow_failures_total counter",
        f"workflow_failures_total {metrics['workflow_failures_total']}",
        "",
        "# HELP workflow_duration_ms_avg Average workflow duration in milliseconds",
        "# TYPE workflow_duration_ms_avg gauge",
        f"workflow_duration_ms_avg {avg_duration:.2f}",
        "",
        "# HELP health_checks_total Total health check requests",
        "# TYPE health_checks_total counter",
        f"health_checks_total {metrics['health_checks_total']}",
        "",
    ]

    return PlainTextResponse("\n".join(lines) + "\n")


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(
        "main:app",
        host="0.0.0.0",
        port=settings.port,
        log_level=settings.log_level.lower(),
        reload=False,
    )
