/**
 * Showcase Story Metadata — Server-safe
 *
 * Static metadata only (no component imports, no hooks).
 * Used by the showcase homepage (Server Component) to render the grid.
 * The full story registry (with actual components) lives in `index.ts`
 * and is only imported by the `[component]/page.tsx` Client Component.
 */

import type { ShowcaseCategory } from '../types';

export interface StoryMeta {
  id: string;
  title: string;
  category: ShowcaseCategory;
  description?: string;
  controlCount: number;
  presetCount: number;
}

export const STORY_METADATA: StoryMeta[] = [
  // ─── Data Display ──────────────────────────────────────────────────────────
  {
    id: 'meeple-card',
    title: 'MeepleCard',
    category: 'Data Display',
    description: 'Universal card component for games, players, agents, sessions, and more.',
    controlCount: 9,
    presetCount: 6,
  },
  {
    id: 'entity-list-view',
    title: 'EntityListView',
    category: 'Data Display',
    description: 'Generic multi-view list with grid, list, carousel, and table modes.',
    controlCount: 4,
    presetCount: 4,
  },
  {
    id: 'game-carousel',
    title: 'GameCarousel',
    category: 'Data Display',
    description: 'Card-based carousel for game discovery with optional sorting and flip.',
    controlCount: 5,
    presetCount: 4,
  },
  {
    id: 'badge',
    title: 'Badge',
    category: 'Tags',
    description: 'Compact label chip for status, category, and metadata tagging.',
    controlCount: 2,
    presetCount: 4,
  },
  {
    id: 'rating-stars',
    title: 'RatingStars',
    category: 'Data Display',
    description: 'Star rating display with BGG 10-point scale conversion. Supports half-stars.',
    controlCount: 5,
    presetCount: 4,
  },
  {
    id: 'stat-card',
    title: 'StatCard',
    category: 'Data Display',
    description: 'Metric card with icon, value, trend indicator, and semantic color variants.',
    controlCount: 6,
    presetCount: 4,
  },

  // ─── Navigation ────────────────────────────────────────────────────────────
  {
    id: 'action-grid',
    title: 'ActionGrid',
    category: 'Navigation',
    description: 'Responsive grid of navigation actions with gradient icons.',
    controlCount: 4,
    presetCount: 4,
  },
  {
    id: 'tabs',
    title: 'Tabs',
    category: 'Navigation',
    description: 'Horizontal tab navigation for switching between related content sections.',
    controlCount: 2,
    presetCount: 3,
  },
  {
    id: 'dropdown-menu',
    title: 'DropdownMenu',
    category: 'Navigation',
    description: 'Context menu with grouped items, separators, and keyboard navigation.',
    controlCount: 2,
    presetCount: 2,
  },

  // ─── Feedback ──────────────────────────────────────────────────────────────
  {
    id: 'confidence-badge',
    title: 'ConfidenceBadge',
    category: 'Feedback',
    description: 'Displays AI confidence score with color-coded visual feedback.',
    controlCount: 2,
    presetCount: 4,
  },
  {
    id: 'tier-badge',
    title: 'TierBadge',
    category: 'Feedback',
    description: 'Displays user subscription tier with color-coded styling.',
    controlCount: 2,
    presetCount: 5,
  },
  {
    id: 'rule-source-card',
    title: 'RuleSourceCard',
    category: 'Feedback',
    description: 'Citation card per risposte RAG con fonti dal regolamento.',
    controlCount: 4,
    presetCount: 4,
  },
  {
    id: 'alert',
    title: 'Alert',
    category: 'Feedback',
    description: 'Contextual feedback banner with optional icon, title, and description.',
    controlCount: 4,
    presetCount: 3,
  },
  {
    id: 'progress',
    title: 'Progress',
    category: 'Feedback',
    description: 'Linear progress bar for upload, loading, and completion states.',
    controlCount: 3,
    presetCount: 4,
  },
  {
    id: 'skeleton',
    title: 'Skeleton',
    category: 'Feedback',
    description:
      'Pulsing placeholder for loading states. Compose into card, list, and form patterns.',
    controlCount: 1,
    presetCount: 4,
  },

  // ─── Tags ──────────────────────────────────────────────────────────────────
  {
    id: 'tag-strip',
    title: 'TagStrip',
    category: 'Tags',
    description: 'Horizontal strip of colored tag badges with overflow indicator.',
    controlCount: 3,
    presetCount: 4,
  },

  // ─── Animations ────────────────────────────────────────────────────────────
  {
    id: 'page-transition',
    title: 'PageTransition',
    category: 'Animations',
    description: 'Smooth page transition animations using Framer Motion.',
    controlCount: 1,
    presetCount: 3,
  },

  // ─── Forms ─────────────────────────────────────────────────────────────────
  {
    id: 'button',
    title: 'Button',
    category: 'Forms',
    description: 'Primary interactive element with multiple variants and sizes.',
    controlCount: 4,
    presetCount: 6,
  },
  {
    id: 'input',
    title: 'Input',
    category: 'Forms',
    description: 'Text input field with glassmorphic styling and focus states.',
    controlCount: 3,
    presetCount: 4,
  },
  {
    id: 'switch',
    title: 'Switch',
    category: 'Forms',
    description: 'Toggle switch for boolean settings with accessible label.',
    controlCount: 3,
    presetCount: 3,
  },
  {
    id: 'checkbox',
    title: 'Checkbox',
    category: 'Forms',
    description: 'Checkbox input for multi-select and agreement flows.',
    controlCount: 3,
    presetCount: 3,
  },
  {
    id: 'textarea',
    title: 'Textarea',
    category: 'Forms',
    description: 'Multi-line text input with resizable rows and glassmorphic styling.',
    controlCount: 3,
    presetCount: 4,
  },
  {
    id: 'select',
    title: 'Select',
    category: 'Forms',
    description: 'Dropdown select with glassmorphic popover and keyboard navigation.',
    controlCount: 3,
    presetCount: 4,
  },
  {
    id: 'radio-group',
    title: 'RadioGroup',
    category: 'Forms',
    description: 'Radio button group for exclusive single-value selection.',
    controlCount: 2,
    presetCount: 4,
  },
  {
    id: 'form-showcase',
    title: 'Form Showcase',
    category: 'Forms',
    description:
      'Composed game session form showing Input, Textarea, Checkbox, and Switch together.',
    controlCount: 1,
    presetCount: 2,
  },

  // ─── Overlays ──────────────────────────────────────────────────────────────
  {
    id: 'dialog',
    title: 'Dialog',
    category: 'Overlays',
    description: 'Modal dialog with glassmorphic panel, animations, and accessible close button.',
    controlCount: 3,
    presetCount: 3,
  },
  {
    id: 'sheet',
    title: 'Sheet',
    category: 'Overlays',
    description: 'Slide-in panel from any screen edge. Used for nav drawers and detail panels.',
    controlCount: 2,
    presetCount: 3,
  },
  {
    id: 'tooltip',
    title: 'Tooltip',
    category: 'Overlays',
    description: 'Contextual hint on hover with configurable position and primary-color styling.',
    controlCount: 2,
    presetCount: 4,
  },
  {
    id: 'popover',
    title: 'Popover',
    category: 'Overlays',
    description: 'Floating content panel attached to a trigger element with blur backdrop.',
    controlCount: 3,
    presetCount: 3,
  },

  // ─── Layout ────────────────────────────────────────────────────────────────
  {
    id: 'separator',
    title: 'Separator',
    category: 'Layout',
    description: 'Horizontal or vertical divider for grouping content sections.',
    controlCount: 2,
    presetCount: 2,
  },

  // ─── Meeple ────────────────────────────────────────────────────────────────
  {
    id: 'meeple-avatar',
    title: 'MeepleAvatar',
    category: 'Meeple',
    description: 'Animated meeple character representing the AI assistant with 5 activity states.',
    controlCount: 2,
    presetCount: 5,
  },
  {
    id: 'meeple-logo',
    title: 'MeepleLogo',
    category: 'Meeple',
    description: 'Brand logo with full, icon, and wordmark variants. Supports light/dark themes.',
    controlCount: 3,
    presetCount: 5,
  },
  {
    id: 'chat-message',
    title: 'ChatMessage',
    category: 'Meeple',
    description:
      'Chat message bubble with role-based layout, AI confidence badge, and feedback buttons.',
    controlCount: 5,
    presetCount: 4,
  },

  // ─── Agent ─────────────────────────────────────────────────────────────────
  {
    id: 'agent-status-badge',
    title: 'AgentStatusBadge',
    category: 'Agent',
    description: 'Color-coded badge indicating agent operational status with optional label.',
    controlCount: 2,
    presetCount: 4,
  },
  {
    id: 'agent-stats-display',
    title: 'AgentStatsDisplay',
    category: 'Agent',
    description:
      'Agent metadata panel with status badge, invocation count, response time, and capabilities.',
    controlCount: 4,
    presetCount: 4,
  },
  {
    id: 'strategy-badge',
    title: 'StrategyBadge',
    category: 'Agent',
    description: 'Color-coded badge for RAG strategy types used in the admin pipeline view.',
    controlCount: 1,
    presetCount: 3,
  },

  // ─── Charts ────────────────────────────────────────────────────────────────
  {
    id: 'waterfall-chart',
    title: 'WaterfallChart',
    category: 'Charts',
    description: 'Chrome DevTools-style waterfall for visualizing RAG pipeline timing.',
    controlCount: 1,
    presetCount: 3,
  },
  {
    id: 'kpi-cards',
    title: 'KpiCards',
    category: 'Charts',
    description: 'Admin dashboard KPI grid showing spend, requests, RPM utilization, and balance.',
    controlCount: 4,
    presetCount: 3,
  },

  // ─── Gates ─────────────────────────────────────────────────────────────────
  {
    id: 'upgrade-prompt',
    title: 'UpgradePrompt',
    category: 'Gates',
    description: 'Upgrade CTA shown when a user tries to access a tier-locked feature.',
    controlCount: 3,
    presetCount: 3,
  },
  {
    id: 'collection-progress',
    title: 'CollectionProgress',
    category: 'Gates',
    description:
      'Quota progress bar with color-coded warnings: green (<75%), amber (75-90%), red (>90%).',
    controlCount: 4,
    presetCount: 4,
  },
];
