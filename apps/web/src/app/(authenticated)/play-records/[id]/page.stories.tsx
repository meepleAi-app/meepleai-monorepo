/**
 * sp4-play-records-detail — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-play-records-detail.{html,jsx}`.
 */

import PlayRecordDetailPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof PlayRecordDetailPage> = {
  title: 'Authenticated / sp4-play-records-detail',
  component: PlayRecordDetailPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: { pathname: '/play-records/sp4-fixture-id' },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Play record detail view.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof PlayRecordDetailPage>;

export const Default: Story = {};
