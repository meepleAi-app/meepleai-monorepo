/**
 * sp4-session-power-grid stub — DS-17-15 #2231 Phase C-2 skeleton-first.
 * Full implementation deferred Phase C-3.
 *
 * Mockup refs:
 * - admin-mockups/design_files/sp4-session-power-grid-live.html
 * - admin-mockups/design_files/sp4-session-power-grid-summary.html
 */

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta = {
  title: 'Authenticated / sp4-session-power-grid',
  parameters: {
    layout: 'fullscreen',
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2231 DS-17-15. Power Grid session stub. Live + Summary variants. Full implementation deferred Phase C-3.',
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
        sp4-session-power-grid-live (Stub)
      </h2>
      <p>Per-game Power Grid flavor components deferred Phase C-3 follow-up.</p>
      <p className="text-xs">Mockup: admin-mockups/design_files/sp4-session-power-grid-live.html</p>
    </div>
  ),
};

export const Summary: Story = {
  render: () => (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 p-8 text-center text-muted-foreground">
      <h2 className="font-quicksand text-3xl font-bold text-foreground">
        sp4-session-power-grid-summary (Stub)
      </h2>
      <p>Per-game Power Grid summary components deferred Phase C-3 follow-up.</p>
      <p className="text-xs">
        Mockup: admin-mockups/design_files/sp4-session-power-grid-summary.html
      </p>
    </div>
  ),
};
