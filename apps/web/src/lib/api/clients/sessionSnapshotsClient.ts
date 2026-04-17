/**
 * Session Snapshots API Client
 *
 * Session Vision AI: Frontend client for snapshot CRUD and game state retrieval.
 *
 * Uses native fetch (not httpClient.post) because multipart/form-data
 * requires the browser to set the Content-Type boundary automatically.
 */

export interface SessionSnapshotDto {
  id: string;
  sessionId: string;
  turnNumber: number;
  caption: string | null;
  hasGameState: boolean;
  createdAt: string;
  images: SnapshotImageDto[];
}

export interface SnapshotImageDto {
  id: string;
  downloadUrl: string | null;
  mediaType: string;
  width: number;
  height: number;
  orderIndex: number;
}

export interface GameStateResult {
  snapshotId: string;
  gameStateJson: string | null;
  isExtracted: boolean;
  snapshotCreatedAt: string;
}

export interface CreateSnapshotResult {
  snapshotId: string;
  imageCount: number;
}

export function createSessionSnapshotsClient() {
  return {
    async getSnapshots(sessionId: string): Promise<SessionSnapshotDto[]> {
      const res = await fetch(
        `/api/v1/live-sessions/${encodeURIComponent(sessionId)}/vision-snapshots`,
        {
          credentials: 'include',
        }
      );
      if (!res.ok) throw new Error(`Failed to fetch snapshots: ${res.status}`);
      return res.json();
    },

    async getGameState(sessionId: string): Promise<GameStateResult | null> {
      const res = await fetch(
        `/api/v1/live-sessions/${encodeURIComponent(sessionId)}/vision-snapshots/game-state`,
        { credentials: 'include' }
      );
      if (res.status === 204) return null;
      if (!res.ok) throw new Error(`Failed to fetch game state: ${res.status}`);
      return res.json();
    },

    async createSnapshot(
      sessionId: string,
      userId: string,
      turnNumber: number,
      images: File[],
      caption?: string
    ): Promise<CreateSnapshotResult> {
      const fd = new FormData();
      fd.append('userId', userId);
      fd.append('turnNumber', turnNumber.toString());
      if (caption) fd.append('caption', caption);
      images.forEach(img => fd.append('images', img));

      const res = await fetch(
        `/api/v1/live-sessions/${encodeURIComponent(sessionId)}/vision-snapshots`,
        {
          method: 'POST',
          credentials: 'include',
          body: fd,
        }
      );
      if (!res.ok) throw new Error(`Failed to create snapshot: ${res.status}`);
      return res.json();
    },
  };
}
