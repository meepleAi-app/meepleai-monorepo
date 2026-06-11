/**
 * @mockup admin-mockups/design_files/notifications.html
 *
 * Notifications argTypes matrix story — DS-17 Phase C-1 (sub-issue #2160).
 *
 * Multi-route mockup covering 2 (auth) routes:
 *   /notifications, /notifications/preferences
 *
 * Stage axis del mockup (5 PhoneShell frames, Desktop only Phase C-1; Mobile
 * DEFERRED a Phase 4):
 *   screen: 'feed' | 'detail-drawer' | 'empty' | 'filtered' | 'preferences'
 *   filter: 'all' | 'sessions' | 'agents' | 'events' | 'system'
 *   state: 'default' | 'empty' | 'loading' | 'error'
 *
 * CANONICAL COMPONENT PICK (multi-route consolidation, DS-17 Phase C-1 spec § 6):
 *   We render `NotificationsPage` (default export from
 *   apps/web/src/app/(authenticated)/notifications/page.tsx) — the production
 *   feed component with filter pills + day grouping + Drawer detail.
 *
 * NOTE: The page is a default export of a 'use client' component. It depends
 * on `useNotificationStore` (Zustand) which initializes via
 * `fetchNotifications({})` (useEffect). Storybook MSW handlers must respond
 * to `GET /api/v1/notifications` for the store to populate.
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import NotificationsPage from '@/app/(authenticated)/notifications/page';

import { mswForState } from '@/__tests__/fixtures/mockup-pilots/auth/notifications';

import type { Meta, StoryObj } from '@storybook/react';

type NotificationsScreen = 'feed' | 'detail-drawer' | 'empty' | 'filtered' | 'preferences';
type NotificationsFilter = 'all' | 'sessions' | 'agents' | 'events' | 'system';
type NotificationsState = 'default' | 'empty' | 'loading' | 'error';

const meta: Meta<typeof NotificationsPage> = {
  title: 'Pages/Auth/Notifications',
  component: NotificationsPage,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di notifications.jsx stage frames 01-05 (Desktop only Phase C-1; Mobile deferred Phase 4). 5 PhoneShell frames mappati 1:1 ai canonical screen del feed di notifiche cross-entity. State + filter axis drives MSW handler scenario via mswForState().',
      },
    },
  },
  argTypes: {
    // NotificationsPage takes no props (URL/store-driven). argTypes are
    // documentation only for designer review.
  },
  args: {},
};
export default meta;

type Story = StoryObj<typeof NotificationsPage>;

// ── Stage frame canonicals (mapped 1:1 ai 5 PhoneShell frames Desktop) ────
// Mobile frames DEFERRED to Phase 4 (viewport sweep, Mobile opt-in).

export const Frame01_FeedDefault: Story = {
  name: '01 · Feed default — all filters + day groups',
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame02_DetailDrawer: Story = {
  name: '02 · Detail drawer aperto su notifica session',
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Drawer opens via NotificationCard click → `setDetail(n)` (page.tsx:224-230). Frame documents the open state; interactive demo via Storybook play function (Phase 2 iteration).',
      },
    },
  },
};

export const Frame03_EmptyState: Story = {
  name: '03 · Empty state — no notifiche',
  parameters: { msw: { handlers: mswForState('empty') } },
};

export const Frame04_FilteredByEntity: Story = {
  name: '04 · Filtro Agenti attivo (subset notifications)',
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Filter pill "Agenti" selected → only `agent_ready` + `rule_spec_generated` types shown. Filter axis owned by useState(filter) (page.tsx:160). Storybook play() can click pill to assert filtered state.',
      },
    },
  },
};

export const Frame05_PreferencesQuickLink: Story = {
  name: '05 · Quick-link impostazioni notifiche',
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Mockup shows quick-link to /settings?section=notifications. Real route /notifications has no inline link to preferences — settings panel reached via main nav. Designer review whether to add inline CTA.',
      },
    },
  },
};

// ── State variants ─────────────────────────────────────────────────────────

export const Loading: Story = {
  name: 'State · Loading skeleton',
  parameters: { msw: { handlers: mswForState('loading') } },
};

export const Error: Story = {
  name: 'State · Error (server 500)',
  parameters: { msw: { handlers: mswForState('error') } },
};
