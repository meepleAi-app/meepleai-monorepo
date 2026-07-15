/**
 * @mockup admin-mockups/design_files/sp7-notifications-preferences.html
 *
 * sp7-notifications-preferences — DS-17 residual MIGRATE (#2971, umbrella #2063).
 *
 * Route /notifications/preferences → NotificationPreferences (raw useState/useEffect
 * + httpClient GET /api/v1/notifications/preferences on mount; no React Query/SSE).
 * DEC-A5 states: default (loaded prefs) / loading (spinner) / error (500 → error wall).
 * `empty` N/A (preferences is always a complete object) and `sse` N/A (no realtime).
 *
 * The Default GET fixture MUST be NotificationPreferencesSchema-valid (uuid userId +
 * all booleans) or the Zod parse throws and the story falls into the error wall.
 */
import { http, HttpResponse } from 'msw';

import { NotificationPreferences } from '@/components/notifications/NotificationPreferences';

import type { Meta, StoryObj } from '@storybook/react';

const PREFS = '*/api/v1/notifications/preferences';

/** NotificationPreferencesSchema-valid fixture (all required booleans + valid uuid). */
const VALID_PREFS = {
  userId: '11111111-1111-4111-8111-111111111111',
  emailOnDocumentReady: true,
  emailOnDocumentFailed: true,
  emailOnRetryAvailable: false,
  pushOnDocumentReady: false,
  pushOnDocumentFailed: true,
  pushOnRetryAvailable: false,
  inAppOnDocumentReady: true,
  inAppOnDocumentFailed: true,
  inAppOnRetryAvailable: true,
  hasPushSubscription: false,
  inAppOnGameNightInvitation: true,
  emailOnGameNightInvitation: true,
  pushOnGameNightInvitation: false,
  emailOnGameNightReminder: true,
  pushOnGameNightReminder: false,
  emailOnCardSuppressed: false,
};

const meta: Meta<typeof NotificationPreferences> = {
  title: 'Authenticated / sp7-notifications-preferences',
  component: NotificationPreferences,
  parameters: {
    layout: 'fullscreen',
    // DS-17 #2063: httpClient GET (no quoted state literal) → declare states explicitly.
    canonicalStates: ['default', 'loading', 'error'],
    nextjs: { appDirectory: true, navigation: { pathname: '/notifications/preferences' } },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2971 DS-17 MIGRATE. Notification preferences (document + game-night categories). ' +
          'Default = loaded prefs; Loading = spinner; Error = 500 error wall.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof NotificationPreferences>;

/** Default: loaded preferences — 6 category cards of toggles + save button. */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(PREFS, () => HttpResponse.json(VALID_PREFS)),
        // Permissive PUTs so a Save interaction doesn't emit unhandled-request warnings.
        http.put(PREFS, () => new HttpResponse(null, { status: 204 })),
        http.put(`${PREFS}/card-suppression`, () => new HttpResponse(null, { status: 204 })),
      ],
    },
  },
};

/** Loading: GET never resolves — shows the initial spinner. */
export const Loading: Story = {
  parameters: {
    msw: { handlers: [http.get(PREFS, () => new Promise(() => {}))] },
  },
};

/** Error: GET returns 500 → Zod/httpClient throws → error wall with Riprova. */
export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(PREFS, () => HttpResponse.json({ error: 'server_error' }, { status: 500 })),
      ],
    },
  },
};
