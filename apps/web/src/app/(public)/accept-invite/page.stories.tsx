/**
 * sp3-accept-invite — DS-17-10 #2208 sub-issue.
 *
 * Multi-route mockup with /accept-invite + /invites/[token] variants.
 * Canonical story target = /accept-invite base (static fallback if no token).
 *
 * Mockup parity: `admin-mockups/design_files/sp3-accept-invite.{html,jsx}`.
 */

import AcceptInvitePage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof AcceptInvitePage> = {
  title: 'Public / sp3-accept-invite',
  component: AcceptInvitePage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: {
        component:
          '#2208 DS-17-10. Canonical story for sp3-accept-invite.html mockup base route (token-bearing /invites/[token] variant lives separately).',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof AcceptInvitePage>;

export const Default: Story = {};
