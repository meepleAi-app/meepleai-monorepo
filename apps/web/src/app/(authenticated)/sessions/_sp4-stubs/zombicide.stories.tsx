/**
 * sp4-session-zombicide stub — DS-17-15 #2231 Phase C-2 skeleton-first.
 * Full implementation deferred Phase C-3.
 *
 * Mockup refs:
 * - admin-mockups/design_files/sp4-session-zombicide-live.html
 * - admin-mockups/design_files/sp4-session-zombicide-summary.html
 */

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta = {
  title: 'Authenticated / sp4-session-zombicide',
  parameters: {
    layout: 'fullscreen',
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2231 DS-17-15. Zombicide session stub. Live + Summary variants. Full implementation deferred Phase C-3.',
      },
    },
  },
};

export default meta;

type Story = StoryObj;

export const Live: Story = {
  render: () => (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 p-8 text-center text-muted-foreground">
      <h2 className="font-quicksand text-3xl font-bold text-foreground">
        sp4-session-zombicide-live (Stub)
      </h2>
      <p>Per-game Zombicide flavor components deferred Phase C-3 follow-up.</p>
      <p className="text-xs">Mockup: admin-mockups/design_files/sp4-session-zombicide-live.html</p>
    </div>
  ),
};

export const Summary: Story = {
  render: () => (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 p-8 text-center text-muted-foreground">
      <h2 className="font-quicksand text-3xl font-bold text-foreground">
        sp4-session-zombicide-summary (Stub)
      </h2>
      <p>Per-game Zombicide summary components deferred Phase C-3 follow-up.</p>
      <p className="text-xs">
        Mockup: admin-mockups/design_files/sp4-session-zombicide-summary.html
      </p>
    </div>
  ),
};
