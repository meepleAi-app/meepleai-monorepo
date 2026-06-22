/**
 * agent-detail feature components barrel — Wave C.2 (Issue #581)
 *
 * All 7 pure presentation components for /agents/[id] route.
 * Orchestrator (AgentDetailView) imports from here.
 */

// AgentHero — variant matrix (active/draft/archived) CTA + banners
export {
  AgentHero,
  type AgentHeroProps,
  type AgentHeroVariant,
  type AgentHeroLabels,
  type AgentHeroMeta,
} from '@/components/features/agent-detail/AgentHero';

// AgentTabs — 5-tab animated tablist (identity/knowledge/performance/history/settings)
export {
  AgentTabs,
  tabIdFor,
  panelIdFor,
  type AgentTabsProps,
  type AgentTabKey,
  type AgentTabConfig,
} from '@/components/features/agent-detail/AgentTabs';

// PersonaCard — pure identity display
export {
  PersonaCard,
  type PersonaCardProps,
  type PersonaCardLabels,
} from '@/components/features/agent-detail/PersonaCard';

// SystemPromptViewer — system prompt display
export {
  SystemPromptViewer,
  type SystemPromptViewerProps,
  type SystemPromptViewerLabels,
} from '@/components/features/agent-detail/SystemPromptViewer';

// KbDocList — 5-state discriminated union (loading/error/empty/standalone/success)
// CRITICAL: standalone (Cell 10) vs empty (Cell 8) are distinct states
export {
  KbDocList,
  type KbDocListProps,
  type KbDocListLabels,
  type KbDocsState,
  type KbDocEntry,
  type KbDocStatus,
} from '@/components/features/agent-detail/KbDocList';

// ChatHistoryTimeline — 4-state discriminated union (loading/error/empty/success)
export {
  ChatHistoryTimeline,
  type ChatHistoryTimelineProps,
  type ChatHistoryTimelineLabels,
  type ChatHistoryState,
  type ChatThreadEntry,
} from '@/components/features/agent-detail/ChatHistoryTimeline';

// AgentSettingsForm — variant-aware (editable for active, read-only for archived)
export {
  AgentSettingsForm,
  type AgentSettingsFormProps,
  type AgentSettingsFormLabels,
  type SettingsState,
  type AgentConfig,
} from '@/components/features/agent-detail/AgentSettingsForm';

// AgentDangerZone — only renders when variant === 'active', returns null otherwise
export {
  AgentDangerZone,
  type AgentDangerZoneProps,
  type AgentDangerZoneLabels,
  type AgentDangerZoneVariant,
} from '@/components/features/agent-detail/AgentDangerZone';
