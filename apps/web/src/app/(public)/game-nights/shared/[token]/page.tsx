/**
 * Public Shared Game Night Summary Page (#2702).
 *
 * Public route for viewing a shared game-night summary via its share-link token.
 * No authentication required — access is controlled by the token in the URL.
 *
 * Route: /game-nights/shared/{token}
 */
'use client';

import { useParams } from 'next/navigation';

import { SharedGameNightSummaryView } from '@/components/features/game-nights/summary/SharedGameNightSummaryView';

export default function SharedGameNightSummaryPage() {
  const params = useParams();
  const token = typeof params?.token === 'string' ? params.token : '';

  return <SharedGameNightSummaryView token={token} />;
}
