/**
 * Agent Detail Page — Wave C.2 brownfield migration (Issue #581).
 *
 * CONVERTED: server component → client thin shell (mirror Wave C.1 pattern).
 *
 * BEFORE (legacy v1 server component):
 *   export default async function AgentPage({ params }) {
 *     let agent;
 *     try { agent = await api.agents.getById(params.id); } catch { notFound(); }
 *     if (!agent) notFound();
 *     return <AgentCharacterSheet data={agentDetailData} />;
 *   }
 *
 * AFTER (Wave C.2 client thin shell):
 *   - `useParams` for agentId (pre-hydration safe normalization)
 *   - agentId normalized to string|null (NEVER undefined or 'undefined' string)
 *   - delegates entirely to AgentDetailView orchestrator
 *   - No server-side data fetching, no notFound(), no generateMetadata
 *
 * Phase 0.5 contract sez. 2.1 — agentId normalization:
 *   params?.id may be undefined during Next.js 16 app router pre-hydration.
 *   `typeof rawId === 'string' && rawId.length > 0` coerces to string|null.
 *
 * AgentCharacterSheet legacy component is no longer used here (kept as
 * deprecated for backward compat in components/agent/AgentCharacterSheet.tsx).
 *
 * Refs #581 (Wave C umbrella — /agents/[id] brownfield).
 */

'use client';

import { useParams } from 'next/navigation';

import { AgentDetailView } from './_components/AgentDetailView';

export default function AgentPage() {
  const params = useParams<{ id: string }>();

  // Phase 0.5 contract sez. 2.1: normalize to string|null.
  // params?.id can be undefined during pre-hydration in Next.js 16 app router.
  // typeof check + length guard ensures we NEVER pass 'undefined' or '' as agentId.
  const rawId = params?.id;
  const agentId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;

  return <AgentDetailView agentId={agentId} />;
}
