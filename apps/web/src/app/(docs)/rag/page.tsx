/**
 * RAG Strategy Dashboard Page
 *
 * Interactive documentation for MeepleAI's TOMAC-RAG system.
 * Features dual-audience view (Technical/Business) with:
 * - Query Simulator
 * - Token Flow Visualizer
 * - Cost Calculator
 * - Architecture Explorer
 *
 * @see docs/03-api/rag/ for detailed documentation
 */

import { Suspense } from 'react';

import { RagDashboard } from '@/components/rag-dashboard/RagDashboard';

import type { Metadata } from 'next';


export const metadata: Metadata = {
  title: 'RAG Strategy Dashboard | MeepleAI',
  description: 'Token-Optimized Modular Adaptive Corrective RAG (TOMAC-RAG) - Interactive documentation for MeepleAI\'s intelligent retrieval system.',
  keywords: [
    'RAG',
    'TOMAC-RAG',
    'retrieval augmented generation',
    'AI',
    'board games',
    'documentation',
    'token optimization',
  ],
  openGraph: {
    title: 'MeepleAI RAG Strategy Dashboard',
    description: 'Explore our Token-Optimized Modular Adaptive Corrective RAG system with interactive visualizations.',
    type: 'website',
  },
};

export default function RagDashboardPage() {
  return (
    <Suspense fallback={<div className="min-h-screen" />}>
      <RagDashboard />
    </Suspense>
  );
}
