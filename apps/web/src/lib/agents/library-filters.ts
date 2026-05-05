/**
 * Pure helpers for the /agents v2 surface (Issue #634, Wave B.2).
 *
 * Mirrors the lib/games/library-filters.ts pattern from Wave B.1 — no React,
 * no API client, exclusively transforms over `AgentDto` so it stays
 * unit-testable in isolation.
 */

import type { AgentDto } from '@/lib/api/schemas/agents.schemas';

import { deriveStatus } from './derive-status';

export type AgentsStatusKey = 'all' | 'attivo' | 'in-setup' | 'archiviato';
export type AgentsSortKey = 'recent' | 'alpha' | 'used';

export interface AgentsLibraryStats {
  attivo: number;
  inSetup: number;
  archiviato: number;
  totalInvocations: number;
}

export function filterByStatus(agents: readonly AgentDto[], status: AgentsStatusKey): AgentDto[] {
  if (status === 'all') return agents.slice();
  return agents.filter(a => deriveStatus(a) === status);
}

export function matchQuery(agent: AgentDto, query: string): boolean {
  const trimmed = query.trim().toLowerCase();
  if (trimmed.length === 0) return true;
  if (agent.name.toLowerCase().includes(trimmed)) return true;
  if (agent.type.toLowerCase().includes(trimmed)) return true;
  if (agent.strategyName.toLowerCase().includes(trimmed)) return true;
  return false;
}

export function sortAgents(agents: readonly AgentDto[], sortKey: AgentsSortKey): AgentDto[] {
  const copy = agents.slice();
  switch (sortKey) {
    case 'alpha':
      return copy.sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
    case 'used':
      return copy.sort((a, b) => b.invocationCount - a.invocationCount);
    case 'recent':
    default:
      return copy.sort((a, b) => {
        const aTime = a.lastInvokedAt ? Date.parse(a.lastInvokedAt) : Date.parse(a.createdAt);
        const bTime = b.lastInvokedAt ? Date.parse(b.lastInvokedAt) : Date.parse(b.createdAt);
        return bTime - aTime;
      });
  }
}

export function deriveStats(agents: readonly AgentDto[]): AgentsLibraryStats {
  let attivo = 0;
  let inSetup = 0;
  let archiviato = 0;
  let totalInvocations = 0;
  for (const agent of agents) {
    const status = deriveStatus(agent);
    if (status === 'attivo') attivo += 1;
    else if (status === 'in-setup') inSetup += 1;
    else archiviato += 1;
    totalInvocations += agent.invocationCount;
  }
  return { attivo, inSetup, archiviato, totalInvocations };
}
