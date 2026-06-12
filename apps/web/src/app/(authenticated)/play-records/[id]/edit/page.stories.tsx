/**
 * sp4-play-records-edit — DS-17-13 #2220.
 * Mockup parity: `admin-mockups/design_files/sp4-play-records-edit.{html,jsx}`.
 */

import PlayRecordEditPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof PlayRecordEditPage> = {
  title: 'Authenticated / sp4-play-records-edit',
  component: PlayRecordEditPage,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: { pathname: '/play-records/sp4-fixture-id/edit' },
    },
    viewport: { defaultViewport: 'desktop' },
    docs: {
      description: { component: '#2220 DS-17-13. Play record edit form.' },
    },
  },
};

export default meta;

type Story = StoryObj<typeof PlayRecordEditPage>;

export const Default: Story = {};
