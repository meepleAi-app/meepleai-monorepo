/**
 * Editor Agent Proposals Page (Issue #3182)
 *
 * Server component for editor agent typology proposals.
 * Delegates to client component for interactivity.
 * Part of Epic #3174 (AI Agent System).
 */

import { Metadata } from 'next';

import { ProposalsClient } from './client';

export const metadata: Metadata = {
  title: 'My Proposals | Editor | MeepleAI',
  description: 'Manage your AI agent typology proposals: draft, test, and submit for approval.',
};

export default function EditorProposalsPage() {
  return <ProposalsClient />;
}
