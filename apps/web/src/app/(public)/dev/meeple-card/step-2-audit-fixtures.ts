/**
 * Fixed realistic fixtures for Step 2 migration audit (Gate 2).
 * Each row corresponds to one of the 17 production call-sites listed in
 * spec §3.1. Fixtures are intentionally minimal but realistic — enough to
 * trigger the same nav-channel rendering path as production.
 */

export interface AuditRow {
  /** Component name as it appears in the source tree (filename minus .tsx) */
  componentName: string;
  /** Bounded context label */
  bc: string;
  /** Path relative to apps/web/src */
  path: string;
}

export const STEP_2_AUDIT_ROWS: AuditRow[] = [
  {
    componentName: 'MeepleChatCard',
    bc: 'KnowledgeBase',
    path: 'components/chat-unified/MeepleChatCard.tsx',
  },
  {
    componentName: 'MeepleKbCard',
    bc: 'KnowledgeBase',
    path: 'components/documents/MeepleKbCard.tsx',
  },
  {
    componentName: 'MeepleGameCatalogCard',
    bc: 'GameManagement',
    path: 'components/catalog/MeepleGameCatalogCard.tsx',
  },
  {
    componentName: 'MeepleLibraryGameCard',
    bc: 'GameManagement',
    path: 'components/library/MeepleLibraryGameCard.tsx',
  },
  {
    componentName: 'CollectionGameGrid',
    bc: 'GameManagement',
    path: 'components/collection/CollectionGameGrid.tsx',
  },
  {
    componentName: 'MeeplePlaylistCard',
    bc: 'GameManagement',
    path: 'components/playlists/MeeplePlaylistCard.tsx',
  },
  {
    componentName: 'MeepleSessionCard',
    bc: 'SessionTracking',
    path: 'components/session/MeepleSessionCard.tsx',
  },
  {
    componentName: 'MeepleResumeSessionCard',
    bc: 'SessionTracking',
    path: 'components/session/MeepleResumeSessionCard.tsx',
  },
  {
    componentName: 'MeepleParticipantCard',
    bc: 'SessionTracking',
    path: 'components/session/MeepleParticipantCard.tsx',
  },
  {
    componentName: 'MeeplePausedSessionCard',
    bc: 'SessionTracking',
    path: 'components/library/private-game-detail/MeeplePausedSessionCard.tsx',
  },
  {
    componentName: 'PlayHistory',
    bc: 'SessionTracking',
    path: 'components/play-records/PlayHistory.tsx',
  },
  {
    componentName: 'MeepleAgentCard',
    bc: 'AgentMemory',
    path: 'components/agent/MeepleAgentCard.tsx',
  },
  {
    componentName: 'MeepleEventCard',
    bc: 'GameNight',
    path: 'components/game-night/MeepleEventCard.tsx',
  },
  {
    componentName: 'MeepleGameNightCard',
    bc: 'GameNight',
    path: 'components/game-night/planning/MeepleGameNightCard.tsx',
  },
  {
    componentName: 'ToolboxKitCard',
    bc: 'GameToolbox',
    path: 'components/toolbox/ToolboxKitCard.tsx',
  },
  {
    componentName: 'MeepleContributorCard',
    bc: 'SharedGameCatalog',
    path: 'components/shared-games/MeepleContributorCard.tsx',
  },
  {
    componentName: 'MeepleUserLibraryCard',
    bc: 'UserLibrary',
    path: 'components/library/MeepleUserLibraryCard.tsx',
  },
];

if (STEP_2_AUDIT_ROWS.length !== 17) {
  // This is a guard — spec §3.1 says exactly 17 call-sites.
  throw new Error(
    `STEP_2_AUDIT_ROWS must have 17 entries (one per spec §3.1 call-site), got ${STEP_2_AUDIT_ROWS.length}`
  );
}
