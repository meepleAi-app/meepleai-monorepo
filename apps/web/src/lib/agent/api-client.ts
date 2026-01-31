/**
 * Agent API Client
 * Issue #3237 (FRONT-001)
 *
 * Centralized HTTP client for agent-related endpoints
 */

import type { AgentTypology, AgentSession, AgentConfig } from './types';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || '';

export class AgentApiClient {
  /**
   * Get all approved typologies
   */
  async getTypologies(): Promise<AgentTypology[]> {
    const response = await fetch(`${API_BASE}/api/v1/agent-typologies?status=Approved`, {
      credentials: 'include',
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch typologies: ${response.statusText}`);
    }

    const data = await response.json();
    return data.typologies || [];
  }

  /**
   * Launch agent session
   */
  async launchSession(params: {
    gameSessionId: string;
    typologyId: string;
    agentId: string;
    gameId: string;
    initialGameStateJson: string;
  }): Promise<{ agentSessionId: string }> {
    const response = await fetch(
      `${API_BASE}/api/v1/game-sessions/${params.gameSessionId}/agent/launch`,
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify(params),
      }
    );

    if (!response.ok) {
      throw new Error(`Failed to launch agent: ${response.statusText}`);
    }

    return response.json();
  }

  /**
   * Update agent typology during session
   * Issue #3252 (BACK-AGT-001)
   */
  async updateTypology(gameSessionId: string, agentSessionId: string, newTypologyId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE}/api/v1/game-sessions/${gameSessionId}/agent/typology`,
      {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ agentSessionId, newTypologyId }),
      }
    );

    if (!response.ok) {
      throw new Error(`Failed to update typology: ${response.statusText}`);
    }
  }

  /**
   * Update agent runtime config
   * Issue #3253 (BACK-AGT-002)
   */
  async updateConfig(gameSessionId: string, agentSessionId: string, config: AgentConfig): Promise<void> {
    const response = await fetch(
      `${API_BASE}/api/v1/game-sessions/${gameSessionId}/agent/config`,
      {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ agentSessionId, ...config }),
      }
    );

    if (!response.ok) {
      throw new Error(`Failed to update config: ${response.statusText}`);
    }
  }

  /**
   * End agent session
   */
  async endSession(gameSessionId: string, agentSessionId: string): Promise<void> {
    const response = await fetch(
      `${API_BASE}/api/v1/game-sessions/${gameSessionId}/agent`,
      {
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ agentSessionId }),
      }
    );

    if (!response.ok) {
      throw new Error(`Failed to end session: ${response.statusText}`);
    }
  }
}

export const agentApi = new AgentApiClient();
