/**
 * Admin RAG Execution History Page - Server Component Wrapper
 * Issue #4458: RAG Execution History
 */

import { RagExecutionHistoryClient } from './client';

export const metadata = {
  title: 'RAG Execution History | Admin | MeepleAI',
  description: 'Monitor RAG execution history, performance metrics, and pipeline traces',
};

export default function AdminRagExecutionsPage() {
  return <RagExecutionHistoryClient />;
}
