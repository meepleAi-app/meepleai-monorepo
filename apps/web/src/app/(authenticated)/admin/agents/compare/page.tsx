/**
 * Strategy Comparison Page - Server Component
 * Issue #3380
 *
 * Admin-only page for side-by-side strategy comparison testing.
 */

import { Metadata } from 'next';

import { StrategyComparisonClient } from './client';

export const metadata: Metadata = {
  title: 'Strategy Comparison | Admin',
  description: 'Compare RAG strategies side-by-side',
};

export default function StrategyComparisonPage() {
  return <StrategyComparisonClient />;
}
