/**
 * Board Game AI Ask Page (Server Component)
 *
 * This page allows users to ask questions about board game rules
 * and get AI-powered answers with streaming responses.
 *
 * Issue #1006: Backend API Integration (/api/v1/board-game-ai/ask)
 *
 * Benefits:
 * - Server Component wrapper for metadata and SEO
 * - Client component for interactive chat functionality
 * - Streaming SSE responses for real-time user feedback
 *
 * @see https://github.com/DegrassiAaron/meepleai-monorepo/issues/1006
 */

// Force dynamic rendering
export const dynamic = 'force-dynamic';

import { Metadata } from 'next';
import BoardGameAskClient from './BoardGameAskClient';

export const metadata: Metadata = {
  title: 'Ask - Board Game AI - MeepleAI',
  description: 'Ask questions about board game rules and get instant AI-powered answers with citations from official rulebooks.',
  keywords: ['board game', 'AI', 'rules', 'question answering', 'game assistant'],
  openGraph: {
    title: 'Ask Board Game AI - MeepleAI',
    description: 'Get instant answers to your board game rule questions with AI-powered assistance.',
    type: 'website',
    locale: 'it_IT',
  },
  robots: {
    index: true,
    follow: true,
  },
};

/**
 * Board Game AI Ask Page - Server Component
 *
 * This Server Component wrapper provides metadata and renders the
 * interactive client-side ask interface.
 */
export default function BoardGameAskPage() {
  return <BoardGameAskClient />;
}
