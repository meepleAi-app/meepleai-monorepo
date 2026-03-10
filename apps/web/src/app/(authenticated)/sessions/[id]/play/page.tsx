/**
 * Live Game Session Play Page — /sessions/[id]/play
 *
 * Server component wrapper that loads the LiveSessionView client component.
 * Session data is loaded in the [id]/layout.tsx via useSessionStore.
 *
 * Issue #5587 — Live Game Session UI
 */

'use client';

import { use } from 'react';

import { LiveSessionView } from '@/components/game-night';

interface PlayPageProps {
  params: Promise<{ id: string }>;
}

export default function LiveSessionPlayPage({ params }: PlayPageProps) {
  const { id } = use(params);

  return <LiveSessionView sessionId={id} />;
}
