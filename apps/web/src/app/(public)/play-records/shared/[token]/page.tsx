/**
 * Public Shared Play Record Page (#2437-2)
 *
 * Public route for viewing a shared play record via share-link token.
 * No authentication required — access controlled by the share token in the URL.
 *
 * Route: /play-records/shared/{token}
 *
 * Rendering is delegated to PlayRecordPublicView, which fetches the record
 * and renders it in read-only (spectator) mode via PlayRecordDetailBody.
 */
'use client';

import { useParams } from 'next/navigation';

import { PlayRecordPublicView } from '@/components/play-records/PlayRecordPublicView';

export default function SharedPlayRecordPage() {
  const params = useParams();
  const token = typeof params?.token === 'string' ? params.token : '';

  return <PlayRecordPublicView token={token} />;
}
