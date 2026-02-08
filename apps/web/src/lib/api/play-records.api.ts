// Play Records API Client
// Issue #3892: Frontend integration for play records

export interface CreatePlayRecordRequest {
  gameId?: string;
  gameName: string;
  sessionDate: string;
  visibility: 'Private' | 'Group';
  groupId?: string;
  scoringDimensions?: string[];
  dimensionUnits?: Record<string, string>;
}

export interface PlayRecordDto {
  id: string;
  gameId?: string;
  gameName: string;
  sessionDate: string;
  duration?: string;
  status: 'Planned' | 'InProgress' | 'Completed' | 'Archived';
  players: PlayerDto[];
  createdByUserId: string;
}

export interface PlayerDto {
  id: string;
  userId?: string;
  displayName: string;
  scores: ScoreDto[];
}

export interface ScoreDto {
  dimension: string;
  value: number;
  unit?: string;
}

export const playRecordsApi = {
  async createRecord(data: CreatePlayRecordRequest): Promise<string> {
    const res = await fetch('/api/v1/game-management/play-records', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(data),
    });
    if (!res.ok) throw new Error('Failed to create record');
    return res.json();
  },

  async getRecord(id: string): Promise<PlayRecordDto> {
    const res = await fetch(`/api/v1/game-management/play-records/${id}`);
    if (!res.ok) throw new Error('Failed to get record');
    return res.json();
  },

  async getUserHistory(page = 1, pageSize = 20): Promise<any> {
    const res = await fetch(
      `/api/v1/game-management/play-records/history?page=${page}&pageSize=${pageSize}`
    );
    if (!res.ok) throw new Error('Failed to get history');
    return res.json();
  },
};
