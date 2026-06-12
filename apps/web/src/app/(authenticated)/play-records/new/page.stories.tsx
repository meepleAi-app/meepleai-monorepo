/**
 * sp4-play-records-new — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-play-records-new.{html,jsx}`.
 */

import PlayRecordNewPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof PlayRecordNewPage> = {
  title: 'Authenticated / sp4-play-records-new',
  component: PlayRecordNewPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Play record new form.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof PlayRecordNewPage>;

export const Default: Story = {};
