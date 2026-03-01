import { type Metadata } from 'next';

import { BuilderClient } from './BuilderClient';

/**
 * Admin — Visual Pipeline Builder page
 * Issue #5110 — Integrate visual pipeline builder into /admin/agents/builder
 */

export const metadata: Metadata = {
  title: 'Agent Builder',
  description: 'Visual drag-and-drop pipeline editor for RAG strategies',
};

export default function AgentBuilderPage() {
  return <BuilderClient />;
}
