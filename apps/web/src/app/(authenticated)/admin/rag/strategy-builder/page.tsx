/**
 * RAG Strategy Builder Admin Page
 *
 * Visual drag-and-drop builder for creating custom RAG strategies.
 * Admin-only access for pipeline configuration and testing.
 *
 * Features:
 * - Drag blocks from palette (23 RAG building blocks)
 * - Connect blocks with edges
 * - Configure parameters per block
 * - Real-time validation
 * - Live testing with SSE streaming
 * - Save/load custom strategies
 *
 * @see Epic #3453 - Visual RAG Strategy Builder
 * @see #3468 - E2E tests for visual strategy builder
 */

import type { Metadata } from 'next';

import { StrategyBuilderClient } from './client';

export const metadata: Metadata = {
  title: 'RAG Strategy Builder | Admin | MeepleAI',
  description:
    'Visual drag-and-drop builder for creating custom RAG strategies with 23 building blocks',
};

export default function StrategyBuilderPage() {
  return <StrategyBuilderClient />;
}
