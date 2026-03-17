// This file is lazy-loaded by ui-library pages via next/dynamic.
// It statically imports all components for runtime rendering.
// Generated from COMPONENT_REGISTRY in apps/web/src/config/component-registry.ts

import type { ComponentType } from 'react';

// ─── Data Display ─────────────────────────────────────────────────────────────

import { ActivityFeed } from '@/components/admin/ActivityFeed';
import { ActivityTimeline } from '@/components/admin/ActivityTimeline';
import { AgentBuilderHeader } from '@/components/admin/agent-builder/AgentBuilderHeader';
import { AgentBuilderSteps } from '@/components/admin/agent-builder/AgentBuilderSteps';
import { AgentPreviewPanel } from '@/components/admin/agent-builder/AgentPreviewPanel';
import { BasicInfoStep } from '@/components/admin/agent-builder/BasicInfoStep';
import { PromptEditorStep } from '@/components/admin/agent-builder/PromptEditorStep';
import { ReviewStep } from '@/components/admin/agent-builder/ReviewStep';
import { ToolsStrategyStep } from '@/components/admin/agent-builder/ToolsStrategyStep';
import { AgentBuilderForm } from '@/components/admin/agent-definitions/AgentBuilderForm';
import { BuilderFilters } from '@/components/admin/agent-definitions/BuilderFilters';
import { BuilderTable } from '@/components/admin/agent-definitions/BuilderTable';
import { PhaseModelConfiguration } from '@/components/admin/agent-typologies/PhaseModelConfiguration';
import { TypologyForm } from '@/components/admin/agent-typologies/TypologyForm';
import { TypologyPromptEditor } from '@/components/admin/agent-typologies/TypologyPromptEditor';
import { AgentConfigPanel } from '@/components/admin/agents/AgentConfigPanel';
import { AgentModeSelector } from '@/components/admin/agents/AgentModeSelector';
import { ComparisonPanel } from '@/components/admin/agents/ComparisonPanel';
import { CostBreakdownChart } from '@/components/admin/agents/CostBreakdownChart';
import { MetricsKpiCards } from '@/components/admin/agents/MetricsKpiCards';
import { TestChatInterface } from '@/components/admin/agents/TestChatInterface';
import { TestMetricsDisplay } from '@/components/admin/agents/TestMetricsDisplay';
import { TopAgentsTable } from '@/components/admin/agents/TopAgentsTable';
import { UsageChart } from '@/components/admin/agents/UsageChart';
import { AiModelsTable } from '@/components/admin/AiModelsTable';
import { AlertRuleForm } from '@/components/admin/alert-rules/AlertRuleForm';
import { AlertRuleList } from '@/components/admin/alert-rules/AlertRuleList';
import { AlertTemplateGallery } from '@/components/admin/alert-rules/AlertTemplateGallery';
import { AlertsBanner } from '@/components/admin/AlertsBanner';
import { BudgetAlertBanner } from '@/components/admin/BudgetAlertBanner';
import { BulkActionsToolbar } from '@/components/admin/bulk-actions-toolbar';
import { BulkActionBar } from '@/components/admin/BulkActionBar';
import { AIUsageDonut } from '@/components/admin/charts/AIUsageDonut';
import { APIRequestsChart } from '@/components/admin/charts/APIRequestsChart';
import { ChartsSection } from '@/components/admin/charts/ChartsSection';
import { CommandCenterDashboard } from '@/components/admin/command-center/CommandCenterDashboard';
import { DebugCostBadge } from '@/components/admin/debug-chat/DebugCostBadge';
import { DebugEventCard } from '@/components/admin/debug-chat/DebugEventCard';
import { DebugTimeline } from '@/components/admin/debug-chat/DebugTimeline';
import { StrategySelectorBar } from '@/components/admin/debug-chat/StrategySelectorBar';
import { DuplicateWarningDialog } from '@/components/admin/games/import/DuplicateWarningDialog';
import { ProcessingMonitor } from '@/components/admin/games/processing/ProcessingMonitor';
import { AdminGameWizard } from '@/components/admin/games/wizard/AdminGameWizard';
import { BulkInviteDialog } from '@/components/admin/invitations/BulkInviteDialog';
import { InvitationRow } from '@/components/admin/invitations/InvitationRow';
import { InvitationStatusBadge } from '@/components/admin/invitations/InvitationStatusBadge';
import { InviteUserDialog } from '@/components/admin/invitations/InviteUserDialog';
import { KBSettings } from '@/components/admin/knowledge-base/kb-settings';
import { ProcessingMetrics } from '@/components/admin/knowledge-base/processing-metrics';
import { ProcessingQueue } from '@/components/admin/knowledge-base/processing-queue';
import { RAGPipelineFlow } from '@/components/admin/knowledge-base/rag-pipeline-flow';
import { UploadSettings } from '@/components/admin/knowledge-base/upload-settings';
import { UploadZone } from '@/components/admin/knowledge-base/upload-zone';
import { VectorCollectionCard } from '@/components/admin/knowledge-base/vector-collection-card';
import { KPICard } from '@/components/admin/KPICard';
import { KPICardsGrid } from '@/components/admin/KPICardsGrid';
import { AdminErrorBoundary } from '@/components/admin/layout/AdminErrorBoundary';
import { AdminHubEmptyState } from '@/components/admin/layout/AdminHubEmptyState';
import { AdminHubQuickLink } from '@/components/admin/layout/AdminHubQuickLink';
import { AdminHubTabBar } from '@/components/admin/layout/AdminHubTabBar';
import { EmergencyBanner } from '@/components/admin/layout/EmergencyBanner';
import { ChannelSelector } from '@/components/admin/notifications/ChannelSelector';
import { NotificationPreview } from '@/components/admin/notifications/NotificationPreview';
import { RecipientSelector } from '@/components/admin/notifications/RecipientSelector';
import { BudgetDebugPanel } from '@/components/admin/playground/budget-debug-panel';
import { ConfidenceBadge as RagConfidenceBadge } from '@/components/admin/rag/ConfidenceBadge';
import { PipelineDiagram } from '@/components/admin/rag/PipelineDiagram';
import { StrategyBadge } from '@/components/admin/rag/StrategyBadge';
import { WaterfallChart } from '@/components/admin/rag/WaterfallChart';
import { AdminConfirmationDialog } from '@/components/ui/admin/admin-confirmation-dialog';
import { Accordion } from '@/components/ui/data-display/accordion';
import { DataTable } from '@/components/ui/data-display/data-table';
import { EntityListView } from '@/components/ui/data-display/entity-list-view/entity-list-view';
import { GameExtraMeepleCard } from '@/components/ui/data-display/extra-meeple-card/EntityExtraMeepleCard'; // EntityExtraMeepleCard — uses GameExtraMeepleCard as representative
import { ExtraMeepleCard } from '@/components/ui/data-display/extra-meeple-card/ExtraMeepleCard';
import { CartaEstesa } from '@/components/ui/data-display/meeple-card/CartaEstesa';
import { MeepleCard } from '@/components/ui/data-display/meeple-card/MeepleCard';
import { MeepleCardCompact } from '@/components/ui/data-display/meeple-card/variants/MeepleCardCompact';
import { MeepleCardExpanded } from '@/components/ui/data-display/meeple-card/variants/MeepleCardExpanded';
import { MeepleCardFeatured } from '@/components/ui/data-display/meeple-card/variants/MeepleCardFeatured';
import { MeepleCardGrid } from '@/components/ui/data-display/meeple-card/variants/MeepleCardGrid';
import { MeepleCardHero } from '@/components/ui/data-display/meeple-card/variants/MeepleCardHero';
import { MeepleCardList } from '@/components/ui/data-display/meeple-card/variants/MeepleCardList';
import { MeepleCards } from '@/components/ui/data-display/meeple-card-compound'; // compound
import { MeepleCardBrowser } from '@/components/ui/data-display/meeple-card-browser/MeepleCardBrowser';
import { ExtraMeepleCardDrawer } from '@/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer';
import { GameCarousel } from '@/components/ui/data-display/game-carousel';
import { AgentModelInfo } from '@/components/ui/data-display/meeple-card-features/AgentModelInfo';
import { BulkSelectCheckbox } from '@/components/ui/data-display/meeple-card-features/BulkSelectCheckbox';
import { CardAgentAction } from '@/components/ui/data-display/meeple-card-features/CardAgentAction';
import { CardNavigationFooter } from '@/components/ui/data-display/meeple-card-features/CardNavigationFooter';
import { MeepleInfoCard } from '@/components/ui/data-display/meeple-info-card';
import { Avatar } from '@/components/ui/data-display/avatar';
import { Badge } from '@/components/ui/data-display/badge';
import { Card } from '@/components/ui/data-display/card';
import { CardGridSkeletons } from '@/components/ui/data-display/CardGridSkeletons';
import { CitationLink } from '@/components/ui/data-display/citation-link';
import { Collapsible } from '@/components/ui/data-display/collapsible';
import { CollectionLimitIndicator as CollectionLimitIndicatorDataDisplay } from '@/components/ui/data-display/CollectionLimitIndicator';
import { ConfidenceBadge } from '@/components/ui/data-display/confidence-badge';
import { EntityLinkBadge } from '@/components/ui/data-display/entity-link/entity-link-badge';
import { EntityLinkCard } from '@/components/ui/data-display/entity-link/entity-link-card';
import { EntityLinkChip } from '@/components/ui/data-display/entity-link/entity-link-chip';
import { EntityLinkPreviewRow } from '@/components/ui/data-display/entity-link/entity-link-preview-row';
import { EntityRelationshipGraph } from '@/components/ui/data-display/entity-link/entity-relationship-graph';
import { RelatedEntitiesSection } from '@/components/ui/data-display/entity-link/related-entities-section';
import { AddEntityLinkModal } from '@/components/ui/data-display/entity-link/add-entity-link-modal';
import { CompactIconBar } from '@/components/ui/data-display/entity-link/compact-icon-bar';
import { ListPageHeader } from '@/components/ui/data-display/ListPageHeader';
import { RatingStars } from '@/components/ui/data-display/rating-stars';
import { StatCard } from '@/components/ui/data-display/stat-card';
import { StatusCard } from '@/components/ui/data-display/status-card';
import { Table } from '@/components/ui/data-display/table';
import { Tooltip as TooltipDataDisplay } from '@/components/ui/data-display/tooltip';
import { UserRoleBadge } from '@/components/ui/data-display/user-role-badge';
import { UserStatusIndicator } from '@/components/ui/data-display/user-status-indicator';
import { ActivityList } from '@/components/ui/data-display/activity-list/activity-list';

// ─── MeepleCard Features ──────────────────────────────────────────────────────

import { DocumentStatusBadge } from '@/components/ui/data-display/meeple-card-features/DocumentStatusBadge';
import { DragHandle } from '@/components/ui/data-display/meeple-card-features/DragHandle';
import { FlipCard } from '@/components/ui/data-display/meeple-card-features/FlipCard';
import { GameBackContent } from '@/components/ui/data-display/meeple-card-features/GameBackContent';
import { HoverPreview } from '@/components/ui/data-display/meeple-card-features/HoverPreview';
import { QuickActionsMenu } from '@/components/ui/data-display/meeple-card-features/QuickActionsMenu';
import { SessionActionButtons } from '@/components/ui/data-display/meeple-card-features/SessionActionButtons';
import { SessionBackContent } from '@/components/ui/data-display/meeple-card-features/SessionBackContent';
import { SessionPlayerPopup } from '@/components/ui/data-display/meeple-card-features/SessionPlayerPopup';
import { SessionScoreModal } from '@/components/ui/data-display/meeple-card-features/SessionScoreModal';
import { SessionScoreTable } from '@/components/ui/data-display/meeple-card-features/SessionScoreTable';
import { SessionStatusBadge } from '@/components/ui/data-display/meeple-card-features/SessionStatusBadge';
import { SessionTurnSequence } from '@/components/ui/data-display/meeple-card-features/SessionTurnSequence';
import { SnapshotHistorySlider } from '@/components/ui/data-display/meeple-card-features/SnapshotHistorySlider';
import { StatusBadge } from '@/components/ui/data-display/meeple-card-features/StatusBadge';
import { TimeTravelOverlay } from '@/components/ui/data-display/meeple-card-features/TimeTravelOverlay';
import { WishlistButton } from '@/components/ui/data-display/meeple-card-features/WishlistButton';
import { ChatAgentInfo } from '@/components/ui/data-display/meeple-card-features/ChatAgentInfo';
import { ChatGameContext } from '@/components/ui/data-display/meeple-card-features/ChatGameContext';
import { ChatStatsDisplay } from '@/components/ui/data-display/meeple-card-features/ChatStatsDisplay';
import { ChatStatusBadge } from '@/components/ui/data-display/meeple-card-features/ChatStatusBadge';
import { ChatUnreadBadge } from '@/components/ui/data-display/meeple-card-features/ChatUnreadBadge';
import { MeepleCardInfoButton } from '@/components/ui/data-display/meeple-card-info-button';
import { MeepleCardQuickActions } from '@/components/ui/data-display/meeple-card-quick-actions';
import { MobileTagDisplay } from '@/components/ui/data-display/meeple-card-mobile-tags'; // registry name: MeepleCardMobileTags

// ─── Feedback ─────────────────────────────────────────────────────────────────

import { BulkCollectionWarning } from '@/components/ui/dialogs/bulk-collection-warning';
import { CollectionRemovalWarning } from '@/components/ui/dialogs/collection-removal-warning';
import { Alert } from '@/components/ui/feedback/alert';
import { AlertDialog } from '@/components/ui/feedback/alert-dialog';
import { CollectionLimitIndicator } from '@/components/ui/feedback/collection-limit-indicator';
import { CollectionProgressBar } from '@/components/ui/feedback/collection-progress-bar';
import { ConfidenceBadge as FeedbackConfidenceBadge } from '@/components/ui/feedback/ConfidenceBadge';
import { ConfirmDialog } from '@/components/ui/feedback/confirm-dialog';
import { ImpersonationBanner } from '@/components/ui/feedback/impersonation-banner';
import { OfflineBanner } from '@/components/ui/feedback/offline-banner';
import { OfflineFaqBadge } from '@/components/ui/feedback/offline-faq-badge';
import { Progress } from '@/components/ui/feedback/progress';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Toaster } from '@/components/ui/feedback/sonner';
import { TierBadge } from '@/components/ui/feedback/tier-badge';
import { UpgradePrompt } from '@/components/ui/feedback/upgrade-prompt';

// ─── Forms ────────────────────────────────────────────────────────────────────

import { Form } from '@/components/ui/forms/form';
import { Switch } from '@/components/ui/forms/switch';
import { FeatureGate } from '@/components/ui/gates/FeatureGate';
import { DateRangePicker } from '@/components/ui/inputs/date-range-picker';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { RadioGroup } from '@/components/ui/primitives/radio-group';
import { ResizablePanelGroup as Resizable } from '@/components/ui/primitives/resizable';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { Slider } from '@/components/ui/primitives/slider';
import { Textarea } from '@/components/ui/primitives/textarea';
import { Toggle } from '@/components/ui/primitives/toggle';
import { ToggleGroup } from '@/components/ui/primitives/toggle-group';

// ─── Navigation ───────────────────────────────────────────────────────────────

import { ActionGrid } from '@/components/ui/navigation/action-grid';
import { Command } from '@/components/ui/navigation/command';
import { DropdownMenu } from '@/components/ui/navigation/dropdown-menu';
import { FocusedCardArea } from '@/components/ui/navigation/focused-card-area';

import { Separator } from '@/components/ui/navigation/separator';
import { Sheet } from '@/components/ui/navigation/sheet';
import { Tabs } from '@/components/ui/navigation/tabs';
import { ThemeToggle } from '@/components/ui/navigation/ThemeToggle';

// ─── Overlays ─────────────────────────────────────────────────────────────────

import { AlertDialog as AlertDialogPrimitivesComponent } from '@/components/ui/overlays/alert-dialog-primitives';
import { ConfirmationDialog } from '@/components/ui/overlays/confirmation-dialog';
import { Dialog } from '@/components/ui/overlays/dialog';
import { HoverCard } from '@/components/ui/overlays/hover-card';
import { Popover } from '@/components/ui/overlays/popover';
import { Select } from '@/components/ui/overlays/select';
import { SmartTooltip } from '@/components/ui/overlays/smart-tooltip';
import { Tooltip } from '@/components/ui/overlays/tooltip';

// ─── Animations ───────────────────────────────────────────────────────────────

import { FadeIn } from '@/components/ui/animations/FadeIn';
// ModalAnimations exports animation variant objects, not a React component — skipped
import { PageTransition } from '@/components/ui/animations/PageTransition';
import { StaggerChildren } from '@/components/ui/animations/StaggerChildren';

// ─── Tags ─────────────────────────────────────────────────────────────────────

import { TagBadge } from '@/components/ui/tags/TagBadge';
import { TagOverflow } from '@/components/ui/tags/TagOverflow';
import { TagStrip } from '@/components/ui/tags/TagStrip';

// ─── Gates ────────────────────────────────────────────────────────────────────

import { PermissionGate } from '@/components/ui/gates/permission-gate';
import { RoleGate } from '@/components/ui/gates/role-gate';
import { TierGate } from '@/components/ui/gates/tier-gate';

// ─── Meeple ───────────────────────────────────────────────────────────────────

import { ChatMessage } from '@/components/ui/meeple/chat-message';
import { FeedbackButtons } from '@/components/ui/meeple/feedback-buttons';
import { MeepleAvatar } from '@/components/ui/meeple/meeple-avatar';
import { MeepleLogo } from '@/components/ui/meeple/meeple-logo';
import { MotionButton } from '@/components/ui/meeple/motion-button';

// ─── Agent ────────────────────────────────────────────────────────────────────

import { AgentStatsDisplay } from '@/components/ui/agent/AgentStatsDisplay';
import { AgentStatusBadge } from '@/components/ui/agent/AgentStatusBadge';

// ─── Icons & Background ───────────────────────────────────────────────────────

import { DiceIcon3D } from '@/components/ui/icons/dice-icon-3d';
import { BackgroundTexture } from '@/components/ui/BackgroundTexture';

// ─── Admin — Layout ───────────────────────────────────────────────────────────

// ─── Admin — Charts ───────────────────────────────────────────────────────────

import { TimelineStep } from '@/components/admin/rag/TimelineStep';
import { KpiCards as UsageKpiCards } from '@/components/admin/usage/KpiCards';
import { RequestTimelineChart } from '@/components/admin/usage/RequestTimelineChart';
import { CostBreakdownPanel } from '@/components/admin/usage/CostBreakdownPanel';
import { FreeQuotaIndicator } from '@/components/admin/usage/FreeQuotaIndicator';
import { RateLimitGauge } from '@/components/admin/usage/RateLimitGauge';
import { RecentRequestsTable } from '@/components/admin/usage/RecentRequestsTable';

// ─── Admin — Agent Builder ────────────────────────────────────────────────────

// ─── Admin — Sandbox ──────────────────────────────────────────────────────────

import { SandboxChat } from '@/components/admin/sandbox/SandboxChat';
import { AgentConfigPanel as SandboxAgentConfigPanelImpl } from '@/components/admin/sandbox/AgentConfigPanel';
import { PipelinePanel } from '@/components/admin/sandbox/PipelinePanel';
import { PipelineStepCard } from '@/components/admin/sandbox/PipelineStepCard';
import { PipelineTraceTree } from '@/components/admin/sandbox/PipelineTraceTree';
import { RetrievedChunkCard } from '@/components/admin/sandbox/RetrievedChunkCard';
import { PipelineDeepMetrics } from '@/components/admin/sandbox/PipelineDeepMetrics';
import { DebugSidePanel } from '@/components/admin/sandbox/DebugSidePanel';
import { AutoTestRunner } from '@/components/admin/sandbox/AutoTestRunner';
import { AutoTestSummary } from '@/components/admin/sandbox/AutoTestSummary';

// ─── Admin — Debug Chat ───────────────────────────────────────────────────────

// ─── Admin — Knowledge Base ───────────────────────────────────────────────────

// ─── Admin — Shared Games ─────────────────────────────────────────────────────

import { AdminSharedGameCardContainer } from '@/components/admin/shared-games/AdminSharedGameCardContainer';
import { AgentBuilderModal } from '@/components/admin/shared-games/AgentBuilderModal';
import { BggSearchPanel } from '@/components/admin/shared-games/BggSearchPanel';
import { GameCatalogGrid } from '@/components/admin/shared-games/game-catalog-grid';
import { GameForm } from '@/components/admin/shared-games/GameForm';
import { GameStatusBadge } from '@/components/admin/shared-games/GameStatusBadge';
import { PdfDocumentList } from '@/components/admin/shared-games/PdfDocumentList';
import { PdfIndexingStatus } from '@/components/admin/shared-games/PdfIndexingStatus';
import { PdfUploadSection } from '@/components/admin/shared-games/PdfUploadSection';
import { RagReadinessIndicator } from '@/components/admin/shared-games/rag-setup/RagReadinessIndicator';
import { InlineChatPanel } from '@/components/admin/shared-games/rag-setup/InlineChatPanel';
import { AgentSetupPanel } from '@/components/admin/shared-games/rag-setup/AgentSetupPanel';

// ─── Admin — Users ────────────────────────────────────────────────────────────

import { PermissionsMatrix } from '@/components/admin/users/permissions-matrix';
import { InlineRoleSelect } from '@/components/admin/users/InlineRoleSelect';
import { RoleCard } from '@/components/admin/users/role-card';
import { ActivityTable } from '@/components/admin/users/activity-table';
import { ActivityFilters } from '@/components/admin/users/activity-filters';

// ─── Admin — Alert Rules ──────────────────────────────────────────────────────

// ─── Admin — Notifications ────────────────────────────────────────────────────

// ─── Admin — Invitations ──────────────────────────────────────────────────────

// ─── Admin — Strategies & Overview ───────────────────────────────────────────

import { StrategyEditor } from '@/components/admin/strategies/StrategyEditor';

// ─── Admin — Games ────────────────────────────────────────────────────────────

// ─── Admin — Playground ───────────────────────────────────────────────────────

// ─── Admin — Misc ─────────────────────────────────────────────────────────────

import { StatCard as AdminStatCard } from '@/components/admin/StatCard';
import { ServiceHealthMatrix } from '@/components/admin/ServiceHealthMatrix';

// ─── Component Map ────────────────────────────────────────────────────────────

export const COMPONENT_MAP: Record<string, ComponentType<any>> = {
  // Data Display
  'meeple-card': MeepleCard,
  'meeple-card-compact': MeepleCardCompact,
  'meeple-card-expanded': MeepleCardExpanded,
  'meeple-card-featured': MeepleCardFeatured,
  'meeple-card-grid': MeepleCardGrid,
  'meeple-card-hero': MeepleCardHero,
  'meeple-card-list': MeepleCardList,
  'carta-estesa': CartaEstesa,
  'meeple-card-compound': MeepleCards as unknown as ComponentType<any>,
  'meeple-card-browser': MeepleCardBrowser,
  'extra-meeple-card': ExtraMeepleCard,
  'extra-meeple-card-drawer': ExtraMeepleCardDrawer,
  'entity-extra-meeple-card': GameExtraMeepleCard, // representative variant
  'entity-list-view': EntityListView as ComponentType<any>,
  'game-carousel': GameCarousel,
  'meeple-info-card': MeepleInfoCard,
  'data-table': DataTable as ComponentType<any>,
  accordion: Accordion,
  avatar: Avatar,
  badge: Badge,
  card: Card,
  'card-grid-skeletons': CardGridSkeletons,
  'citation-link': CitationLink,
  collapsible: Collapsible,
  'collection-limit-indicator': CollectionLimitIndicatorDataDisplay,
  'confidence-badge': ConfidenceBadge,
  'entity-link-badge': EntityLinkBadge,
  'entity-link-card': EntityLinkCard,
  'entity-link-chip': EntityLinkChip,
  'entity-link-preview-row': EntityLinkPreviewRow,
  'entity-relationship-graph': EntityRelationshipGraph,
  'related-entities-section': RelatedEntitiesSection,
  'add-entity-link-modal': AddEntityLinkModal,
  'compact-icon-bar': CompactIconBar,
  'list-page-header': ListPageHeader,
  'rating-stars': RatingStars,
  'stat-card': StatCard,
  'status-card': StatusCard,
  table: Table,
  'tooltip-data-display': TooltipDataDisplay,
  'user-role-badge': UserRoleBadge,
  'user-status-indicator': UserStatusIndicator,
  'activity-list': ActivityList,

  // MeepleCard Features
  'agent-model-info': AgentModelInfo,
  'bulk-select-checkbox': BulkSelectCheckbox,
  'card-agent-action': CardAgentAction,
  'card-navigation-footer': CardNavigationFooter,
  'document-status-badge': DocumentStatusBadge,
  'drag-handle': DragHandle,
  'flip-card': FlipCard,
  'game-back-content': GameBackContent,
  'hover-preview': HoverPreview,
  'quick-actions-menu': QuickActionsMenu,
  'session-action-buttons': SessionActionButtons,
  'session-back-content': SessionBackContent,
  'session-player-popup': SessionPlayerPopup,
  'session-score-modal': SessionScoreModal,
  'session-score-table': SessionScoreTable,
  'session-status-badge': SessionStatusBadge,
  'session-turn-sequence': SessionTurnSequence,
  'snapshot-history-slider': SnapshotHistorySlider,
  'status-badge': StatusBadge,
  'time-travel-overlay': TimeTravelOverlay,
  'wishlist-button': WishlistButton,
  'chat-agent-info': ChatAgentInfo,
  'chat-game-context': ChatGameContext,
  'chat-stats-display': ChatStatsDisplay,
  'chat-status-badge': ChatStatusBadge,
  'chat-unread-badge': ChatUnreadBadge,
  'meeple-card-info-button': MeepleCardInfoButton,
  'meeple-card-quick-actions': MeepleCardQuickActions,
  'meeple-card-mobile-tags': MobileTagDisplay,

  // Feedback
  alert: Alert,
  'alert-dialog': AlertDialog,
  'collection-limit-indicator-feedback': CollectionLimitIndicator,
  'collection-progress-bar': CollectionProgressBar,
  'feedback-confidence-badge': FeedbackConfidenceBadge,
  'confirm-dialog': ConfirmDialog,
  'impersonation-banner': ImpersonationBanner,
  'offline-banner': OfflineBanner,
  'offline-faq-badge': OfflineFaqBadge,
  progress: Progress,
  skeleton: Skeleton,
  sonner: Toaster,
  'tier-badge': TierBadge,
  'upgrade-prompt': UpgradePrompt,

  // Forms
  button: Button,
  checkbox: Checkbox,
  input: Input,
  label: Label,
  'radio-group': RadioGroup,
  resizable: Resizable,
  'scroll-area': ScrollArea,
  slider: Slider,
  textarea: Textarea,
  toggle: Toggle,
  'toggle-group': ToggleGroup,
  switch: Switch,
  form: Form,
  'date-range-picker': DateRangePicker,

  // Navigation
  'action-grid': ActionGrid,
  command: Command,
  'dropdown-menu': DropdownMenu,
  'focused-card-area': FocusedCardArea,
  // 'hand-stack': removed — component no longer exists
  separator: Separator,
  sheet: Sheet,
  tabs: Tabs,
  'theme-toggle': ThemeToggle,

  // Overlays
  'alert-dialog-primitives': AlertDialogPrimitivesComponent,
  'confirmation-dialog': ConfirmationDialog,
  dialog: Dialog,
  'hover-card': HoverCard,
  popover: Popover,
  select: Select,
  'smart-tooltip': SmartTooltip,
  tooltip: Tooltip,
  'bulk-collection-warning': BulkCollectionWarning,
  'collection-removal-warning': CollectionRemovalWarning,
  'admin-confirmation-dialog': AdminConfirmationDialog,

  // Animations
  'fade-in': FadeIn,
  // 'modal-animations': skipped — exports animation variant objects, not a React component
  'page-transition': PageTransition,
  'stagger-children': StaggerChildren,

  // Tags
  'tag-badge': TagBadge,
  'tag-overflow': TagOverflow,
  'tag-strip': TagStrip,

  // Gates
  'feature-gate': FeatureGate,
  'permission-gate': PermissionGate,
  'role-gate': RoleGate,
  'tier-gate': TierGate,

  // Meeple
  'chat-message': ChatMessage,
  'feedback-buttons': FeedbackButtons,
  'meeple-avatar': MeepleAvatar,
  'meeple-logo': MeepleLogo,
  'motion-button': MotionButton,

  // Agent
  'agent-stats-display': AgentStatsDisplay,
  'agent-status-badge': AgentStatusBadge,

  // Icons & Background
  'dice-icon-3d': DiceIcon3D,
  'background-texture': BackgroundTexture,

  // Admin — Layout
  // 'admin-top-nav': removed — component no longer exists
  // 'admin-mobile-nav': removed — component no longer exists

  'admin-hub-tab-bar': AdminHubTabBar,
  'admin-hub-quick-link': AdminHubQuickLink,
  'admin-hub-empty-state': AdminHubEmptyState,
  'admin-error-boundary': AdminErrorBoundary as unknown as ComponentType<any>,
  'emergency-banner': EmergencyBanner,

  // Admin — Charts & RAG
  'pipeline-diagram': PipelineDiagram,
  'waterfall-chart': WaterfallChart,
  'strategy-badge': StrategyBadge,
  'timeline-step': TimelineStep,
  'rag-confidence-badge': RagConfidenceBadge,
  'ai-usage-donut': AIUsageDonut,
  'api-requests-chart': APIRequestsChart,
  'charts-section': ChartsSection,
  'usage-kpi-cards': UsageKpiCards,
  'request-timeline-chart': RequestTimelineChart,
  'cost-breakdown-panel': CostBreakdownPanel,
  'free-quota-indicator': FreeQuotaIndicator,
  'rate-limit-gauge': RateLimitGauge,
  'recent-requests-table': RecentRequestsTable,
  'metrics-kpi-cards': MetricsKpiCards,
  'usage-chart': UsageChart,
  'cost-breakdown-chart': CostBreakdownChart,

  // Admin — Agent Builder
  'agent-builder-header': AgentBuilderHeader,
  'agent-builder-steps': AgentBuilderSteps,
  'agent-preview-panel': AgentPreviewPanel,
  'basic-info-step': BasicInfoStep,
  'prompt-editor-step': PromptEditorStep,
  'review-step': ReviewStep,
  'tools-strategy-step': ToolsStrategyStep,
  'agent-builder-form': AgentBuilderForm,
  'builder-filters': BuilderFilters,
  'builder-table': BuilderTable,
  'agent-config-panel': AgentConfigPanel,
  'agent-mode-selector': AgentModeSelector,
  'test-chat-interface': TestChatInterface,
  'test-metrics-display': TestMetricsDisplay,
  'top-agents-table': TopAgentsTable,
  'comparison-panel': ComparisonPanel,
  'phase-model-configuration': PhaseModelConfiguration,
  'typology-form': TypologyForm,
  'typology-prompt-editor': TypologyPromptEditor,

  // Admin — Sandbox
  'sandbox-chat': SandboxChat,
  'sandbox-agent-config-panel': SandboxAgentConfigPanelImpl,
  'pipeline-panel': PipelinePanel,
  'pipeline-step-card': PipelineStepCard,
  'pipeline-trace-tree': PipelineTraceTree,
  'retrieved-chunk-card': RetrievedChunkCard,
  'pipeline-deep-metrics': PipelineDeepMetrics,
  'debug-side-panel': DebugSidePanel,
  'auto-test-runner': AutoTestRunner,
  'auto-test-summary': AutoTestSummary,

  // Admin — Debug Chat
  'debug-cost-badge': DebugCostBadge,
  'debug-event-card': DebugEventCard,
  'debug-timeline': DebugTimeline,
  'strategy-selector-bar': StrategySelectorBar,

  // Admin — Knowledge Base
  'kb-settings': KBSettings,
  'processing-metrics': ProcessingMetrics,
  'processing-queue': ProcessingQueue,
  'rag-pipeline-flow': RAGPipelineFlow,
  'upload-settings': UploadSettings,
  'upload-zone': UploadZone,
  'vector-collection-card': VectorCollectionCard,

  // Admin — Shared Games
  'admin-shared-game-card-container': AdminSharedGameCardContainer,
  'agent-builder-modal': AgentBuilderModal,
  'bgg-search-panel': BggSearchPanel,
  'game-catalog-grid': GameCatalogGrid,
  'game-form': GameForm,
  'game-status-badge': GameStatusBadge,
  'pdf-document-list': PdfDocumentList,
  'pdf-indexing-status': PdfIndexingStatus,
  'pdf-upload-section': PdfUploadSection,
  'rag-readiness-indicator': RagReadinessIndicator,
  'inline-chat-panel': InlineChatPanel,
  'agent-setup-panel': AgentSetupPanel,

  // Admin — Users
  'permissions-matrix': PermissionsMatrix,
  'inline-role-select': InlineRoleSelect,
  'role-card': RoleCard,
  'user-activity-table': ActivityTable,
  'user-activity-filters': ActivityFilters,

  // Admin — Alert Rules
  'alert-rule-form': AlertRuleForm,
  'alert-rule-list': AlertRuleList,
  'alert-template-gallery': AlertTemplateGallery,

  // Admin — Notifications
  'channel-selector': ChannelSelector,
  'notification-preview': NotificationPreview,
  'recipient-selector': RecipientSelector,

  // Admin — Invitations
  'bulk-invite-dialog': BulkInviteDialog,
  'invitation-row': InvitationRow,
  'invitation-status-badge': InvitationStatusBadge,
  'invite-user-dialog': InviteUserDialog,

  // Admin — Strategies & Overview
  'strategy-editor': StrategyEditor,
  'command-center-dashboard': CommandCenterDashboard,

  // Admin — Games
  'admin-game-wizard': AdminGameWizard,
  'processing-monitor': ProcessingMonitor,
  'duplicate-warning-dialog': DuplicateWarningDialog,

  // Admin — Playground
  'budget-debug-panel': BudgetDebugPanel,

  // Admin — Misc
  'kpi-card': KPICard,
  'kpi-cards-grid': KPICardsGrid,
  'admin-stat-card': AdminStatCard,
  'activity-feed': ActivityFeed,
  'activity-timeline': ActivityTimeline,
  'alerts-banner': AlertsBanner,
  'budget-alert-banner': BudgetAlertBanner,
  'bulk-action-bar': BulkActionBar,
  'bulk-actions-toolbar': BulkActionsToolbar,
  'service-health-matrix': ServiceHealthMatrix,
  'ai-models-table': AiModelsTable,
};
