/**
 * @mockup admin-mockups/design_files/sp7-notifications-hub.html
 *
 * Repointed 2026-07-15 (#2063 ratchet): the canonical /notifications mockup is
 * sp7-notifications-hub.html; notifications.html is the superseded SP1 legacy
 * archive (#2028, now design_intent=forward-refactor-obsolete). This story is
 * wired as sp7-notifications-hub's fidelity.story_path.
 *
 * Notifications argTypes matrix story — DS-17 Phase C-1 (sub-issue #2160).
 *
 * Multi-route mockup covering 2 (authenticated) routes:
 *   /notifications, /notifications/preferences
 *
 * Stage axis (notifications.jsx FILTER + GROUPS, 5 PhoneShell frames Desktop;
 * Mobile DEFERRED Phase 4 viewport sweep):
 *   screen: 'feed' | 'detail-drawer' | 'empty' | 'filtered' | 'preferences'
 *   filter: 'all' | 'sessions' | 'agents' | 'events' | 'system'
 *   state: 'default' | 'empty' | 'loading' | 'error'
 *
 * CANONICAL COMPONENT PICK: `NotificationsPage` default export (production
 * feed component with filter pills + day grouping + Drawer detail).
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { mswForNotificationsState } from '@/__tests__/fixtures/mockup-pilots/auth/notifications';

import NotificationsPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof NotificationsPage> = {
  title: 'Pages/Auth/Notifications',
  component: NotificationsPage,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di notifications.jsx stage frames 01-05 (Desktop only Phase C-1; Mobile deferred Phase 4). 5 PhoneShell frames mappati 1:1 ai canonical screen del feed di notifiche cross-entity. State + filter axis drives MSW handler scenario via mswForNotificationsState().',
      },
    },
  },
  args: {},
};
export default meta;

type Story = StoryObj<typeof NotificationsPage>;

export const Frame01_FeedDefault: Story = {
  name: '01 · Feed default — all filters + day groups',
  parameters: { msw: { handlers: mswForNotificationsState('default') } },
};

export const Frame02_DetailDrawer: Story = {
  name: '02 · Detail drawer aperto su notifica session',
  parameters: {
    msw: { handlers: mswForNotificationsState('default') },
    docs: {
      description: {
        story:
          'Drawer opens via NotificationCard click → setDetail(n) (page.tsx). Frame documents the open state; interactive demo via Storybook play function (Phase 2 iteration).',
      },
    },
  },
};

export const Frame03_EmptyState: Story = {
  name: '03 · Empty state — no notifiche',
  parameters: { msw: { handlers: mswForNotificationsState('empty') } },
};

export const Frame04_FilteredByEntity: Story = {
  name: '04 · Filtro Agenti attivo (subset notifications)',
  parameters: {
    msw: { handlers: mswForNotificationsState('default') },
    docs: {
      description: {
        story:
          'Filter pill "Agenti" selected → only agent_ready + rule_spec_generated types shown. Filter axis owned by useState(filter). Storybook play() can click pill to assert filtered state.',
      },
    },
  },
};

export const Frame05_PreferencesQuickLink: Story = {
  name: '05 · Quick-link impostazioni notifiche',
  parameters: {
    msw: { handlers: mswForNotificationsState('default') },
    docs: {
      description: {
        story:
          'Mockup shows quick-link to /settings?section=notifications. Real route /notifications has no inline link — settings panel reached via main nav. Designer review whether to add inline CTA.',
      },
    },
  },
};

export const Loading: Story = {
  name: 'State · Loading skeleton',
  parameters: { msw: { handlers: mswForNotificationsState('loading') } },
};

export const ErrorState: Story = {
  name: 'State · Error (server 500)',
  parameters: { msw: { handlers: mswForNotificationsState('error') } },
};
