/**
 * Admin Agent Typologies Page (Issue #3179)
 *
 * Server component for admin agent typology management.
 * Delegates to client component for interactivity.
 * Part of Epic #3174 (AI Agent System).
 */

import { Metadata } from 'next';

import { TypologiesClient } from './client';

export const metadata: Metadata = {
  title: 'Agent Typologies | Admin | MeepleAI',
  description: 'Manage AI agent typologies: view, approve, and configure agent types.',
};

export default function AdminTypologiesPage() {
  return <TypologiesClient />;
}
