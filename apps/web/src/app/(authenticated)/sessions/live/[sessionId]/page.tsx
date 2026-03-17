/**
 * Live Session Main Page — /sessions/live/[sessionId]
 *
 * Game Night Improvvisata — Task 15
 *
 * Renders the SessionCardParent component as the primary session overview.
 */

'use client';

import { use } from 'react';

import { SessionCardParent } from '@/components/sessions/live/SessionCardParent';

interface LiveSessionPageProps {
  params: Promise<{ sessionId: string }>;
}

export default function LiveSessionPage({ params }: LiveSessionPageProps) {
  const { sessionId } = use(params);
  return <SessionCardParent sessionId={sessionId} />;
}
