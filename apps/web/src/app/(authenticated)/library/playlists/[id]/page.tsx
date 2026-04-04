'use client';

/**
 * Playlist detail page — shows a single playlist with its game list.
 */

import { use } from 'react';

import { PlaylistDetail } from '@/components/playlists/PlaylistDetail';

// ============================================================================
// Page
// ============================================================================

export default function PlaylistDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);

  return (
    <div className="p-4 sm:p-6">
      <PlaylistDetail playlistId={id} />
    </div>
  );
}
