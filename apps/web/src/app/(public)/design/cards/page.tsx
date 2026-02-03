'use client';

/**
 * Game Card Design Showcase Page (Public)
 * Access at: /design/cards (no auth required)
 */

import dynamic from 'next/dynamic';

// Dynamic import to avoid SSR issues with framer-motion
const GameCardShowcase = dynamic(
  () => import('@/components/cards/mocks').then((mod) => mod.GameCardShowcase),
  {
    ssr: false,
    loading: () => (
      <div className="min-h-screen bg-slate-900 flex items-center justify-center">
        <div className="text-white text-xl">Loading Card Designs...</div>
      </div>
    )
  }
);

export default function CardDesignPage() {
  return <GameCardShowcase />;
}
