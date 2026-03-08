/**
 * ISSUE-3777: Orchestrator API Client
 * Client for Python orchestration-service (LangGraph multi-agent coordinator)
 */

// ========== Types ==========

export type AgentType = 'tutor' | 'arbitro' | 'stratega' | 'narratore';

export interface ExecuteWorkflowRequest {
  game_id: string;
  session_id: string;
  query: string;
  board_state?: {
    current_player: string;
    board_data: Record<string, unknown>;
    legal_moves?: string[];
  };
  pending_move?: {
    move_notation: string;
    player: string;
  };
}

export interface ExecuteWorkflowResponse {
  agent_type: AgentType;
  response: string;
  confidence: number;
  citations: string[];
  execution_time_ms: number;
  session_id: string;
  error: string | null;
}

// ========== API Client ==========

const ORCHESTRATOR_BASE_URL = process.env.NEXT_PUBLIC_ORCHESTRATOR_URL || 'http://localhost:8004';

/**
 * Execute multi-agent workflow via Python orchestration-service
 */
export async function executeWorkflow(
  request: ExecuteWorkflowRequest
): Promise<ExecuteWorkflowResponse> {
  const response = await fetch(`${ORCHESTRATOR_BASE_URL}/execute`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({
      error: `HTTP ${response.status}: Orchestrator request failed`,
    }));
    throw new Error(error.error || 'Orchestration failed');
  }

  return response.json();
}

/**
 * Request agent switch by sending trigger query
 */
export async function requestAgentSwitch(
  sessionId: string,
  gameId: string,
  targetAgent: AgentType
): Promise<ExecuteWorkflowResponse> {
  const switchQueries: Record<AgentType, string> = {
    tutor: 'Switch to Tutor to explain the rules',
    arbitro: 'Switch to Arbitro to validate moves',
    stratega: 'Switch to Stratega for strategic analysis',
    narratore: 'Switch to Narratore for lore and atmosphere',
  };

  return executeWorkflow({
    game_id: gameId,
    session_id: sessionId,
    query: switchQueries[targetAgent],
  });
}

/**
 * Check orchestrator service health
 */
export async function checkOrchestratorHealth(): Promise<{
  status: 'healthy' | 'unhealthy';
  version: string;
  dependencies: Record<string, string>;
}> {
  try {
    const response = await fetch(`${ORCHESTRATOR_BASE_URL}/health`, {
      method: 'GET',
    });

    if (!response.ok) {
      return { status: 'unhealthy', version: 'unknown', dependencies: {} };
    }

    return response.json();
  } catch {
    return { status: 'unhealthy', version: 'unknown', dependencies: {} };
  }
}
