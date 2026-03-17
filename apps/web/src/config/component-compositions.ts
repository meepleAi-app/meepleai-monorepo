import type React from 'react';

import type { AppArea } from './component-registry';

export interface ComponentComposition {
  id: string;
  name: string;
  description: string;
  area: AppArea;
  componentIds: string[];
  render: () => Promise<{ default: React.ComponentType }>;
}

export const COMPOSITIONS: ComponentComposition[] = [
  {
    id: 'entity-cards',
    name: 'Entity Cards',
    description: 'MeepleCard in all entity types with link badges',
    area: 'shared',
    componentIds: ['meeple-card', 'entity-link-badge'],
    render: () =>
      import('@/components/admin/ui-library/scenes/EntityCardsScene') as Promise<{
        default: React.ComponentType;
      }>,
  },
  {
    id: 'rag-pipeline',
    name: 'RAG Pipeline',
    description: 'Pipeline explorer with waterfall, timeline, and confidence badges',
    area: 'admin',
    componentIds: [
      'pipeline-diagram',
      'timeline-step',
      'waterfall-chart',
      'confidence-badge',
      'strategy-badge',
    ],
    render: () =>
      import('@/components/admin/ui-library/scenes/RagPipelineScene') as Promise<{
        default: React.ComponentType;
      }>,
  },
  {
    id: 'game-management',
    name: 'Game Management',
    description: 'GameForm, PdfUploadSection, CategoriesTable, GameStatusBadge',
    area: 'admin',
    componentIds: ['game-form', 'pdf-upload-section', 'game-status-badge'],
    render: () =>
      import('@/components/admin/ui-library/scenes/GameManagementScene') as Promise<{
        default: React.ComponentType;
      }>,
  },
  {
    id: 'agent-builder',
    name: 'Agent Builder',
    description: 'AgentBuilderModal, LlmProviderSelector, DocumentSelector, AgentConfigPanel',
    area: 'admin',
    componentIds: ['agent-builder-modal', 'agent-config-panel'],
    render: () =>
      import('@/components/admin/ui-library/scenes/AgentBuilderScene') as Promise<{
        default: React.ComponentType;
      }>,
  },
  {
    id: 'user-admin',
    name: 'User Administration',
    description: 'PermissionsMatrix, RoleCard, ActivityTable, InviteUserDialog',
    area: 'admin',
    componentIds: ['permissions-matrix', 'role-card', 'user-activity-table', 'invite-user-dialog'],
    render: () =>
      import('@/components/admin/ui-library/scenes/UserAdminScene') as Promise<{
        default: React.ComponentType;
      }>,
  },
  {
    id: 'kb-cards',
    name: 'Knowledge Base',
    description: 'UploadZone, VectorCollectionCard, ProcessingQueue',
    area: 'admin',
    componentIds: ['upload-zone', 'vector-collection-card', 'processing-queue'],
    render: () =>
      import('@/components/admin/ui-library/scenes/KbCardsScene') as Promise<{
        default: React.ComponentType;
      }>,
  },
  {
    id: 'sandbox-debug',
    name: 'Sandbox & Debug',
    description: 'SandboxChat, PipelineTraceTree, RetrievedChunkCard',
    area: 'admin',
    componentIds: ['sandbox-chat', 'pipeline-trace-tree', 'retrieved-chunk-card'],
    render: () =>
      import('@/components/admin/ui-library/scenes/SandboxDebugScene') as Promise<{
        default: React.ComponentType;
      }>,
  },
  {
    id: 'usage-monitoring',
    name: 'Usage Monitoring',
    description: 'KpiCards, UsageChart, CostBreakdownChart',
    area: 'admin',
    componentIds: ['usage-kpi-cards', 'usage-chart', 'cost-breakdown-chart'],
    render: () =>
      import('@/components/admin/ui-library/scenes/UsageMonitoringScene') as Promise<{
        default: React.ComponentType;
      }>,
  },
  {
    id: 'alert-system',
    name: 'Alert System',
    description: 'AlertRuleForm, AlertTemplateGallery, BudgetAlertBanner',
    area: 'admin',
    componentIds: ['alert-rule-form', 'alert-template-gallery', 'budget-alert-banner'],
    render: () =>
      import('@/components/admin/ui-library/scenes/AlertSystemScene') as Promise<{
        default: React.ComponentType;
      }>,
  },
  {
    id: 'navigation-system',
    name: 'Navigation System',
    description: 'ActionGrid showing quick-action navigation patterns',
    area: 'shared',
    componentIds: ['action-grid'],
    render: () =>
      import('@/components/admin/ui-library/scenes/NavigationSystemScene') as Promise<{
        default: React.ComponentType;
      }>,
  },
  {
    id: 'gate-system',
    name: 'Gate System',
    description: 'FeatureGate, RoleGate, TierGate, PermissionGate, TierBadge',
    area: 'shared',
    componentIds: ['feature-gate', 'role-gate', 'tier-gate', 'permission-gate', 'tier-badge'],
    render: () =>
      import('@/components/admin/ui-library/scenes/GateSystemScene') as Promise<{
        default: React.ComponentType;
      }>,
  },
  {
    id: 'feedback-ui',
    name: 'Feedback UI',
    description: 'Alert, Progress, Skeleton, OfflineBanner',
    area: 'shared',
    componentIds: ['alert', 'progress', 'skeleton', 'offline-banner'],
    render: () =>
      import('@/components/admin/ui-library/scenes/FeedbackUiScene') as Promise<{
        default: React.ComponentType;
      }>,
  },
];

export function getComposition(id: string): ComponentComposition | undefined {
  return COMPOSITIONS.find(c => c.id === id);
}
