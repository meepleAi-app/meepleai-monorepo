/**
 * Tabs Story
 * Demonstrates the Tabs navigation component with configurable tab count.
 */

'use client';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';

import type { ShowcaseStory } from '../types';

type TabsShowcaseProps = {
  tabCount: number;
  variant: string;
};

const TAB_SETS: Record<string, { value: string; label: string; content: string }[]> = {
  game: [
    { value: 'overview', label: 'Overview', content: 'General game information and stats.' },
    { value: 'rules', label: 'Rules', content: 'Rulebook and how-to-play guide.' },
    { value: 'sessions', label: 'Sessions', content: 'Your play history and session notes.' },
    { value: 'comments', label: 'Comments', content: 'Community reviews and feedback.' },
  ],
  admin: [
    { value: 'users', label: 'Users', content: 'User management and roles.' },
    { value: 'content', label: 'Content', content: 'Games and KB content.' },
    { value: 'system', label: 'System', content: 'Configuration and health.' },
  ],
};

export const tabsStory: ShowcaseStory<TabsShowcaseProps> = {
  id: 'tabs',
  title: 'Tabs',
  category: 'Navigation',
  description: 'Horizontal tab navigation for switching between related content sections.',

  component: function TabsStory({ tabCount, variant }: TabsShowcaseProps) {
    const setKey = variant === 'admin' ? 'admin' : 'game';
    const tabs = (TAB_SETS[setKey] ?? TAB_SETS.game).slice(0, tabCount);
    const defaultTab = tabs[0]?.value ?? 'overview';

    return (
      <div className="w-96 p-4">
        <Tabs defaultValue={defaultTab}>
          <TabsList>
            {tabs.map(tab => (
              <TabsTrigger key={tab.value} value={tab.value}>
                {tab.label}
              </TabsTrigger>
            ))}
          </TabsList>
          {tabs.map(tab => (
            <TabsContent key={tab.value} value={tab.value}>
              <p className="text-sm text-muted-foreground py-2">{tab.content}</p>
            </TabsContent>
          ))}
        </Tabs>
      </div>
    );
  },

  defaultProps: {
    tabCount: 3,
    variant: 'game',
  },

  controls: {
    tabCount: { type: 'range', label: 'tabCount', min: 2, max: 4, step: 1, default: 3 },
    variant: {
      type: 'select',
      label: 'variant',
      options: ['game', 'admin'],
      default: 'game',
    },
  },

  presets: {
    game: { label: 'Game Tabs', props: { tabCount: 4, variant: 'game' } },
    admin: { label: 'Admin Tabs', props: { tabCount: 3, variant: 'admin' } },
    minimal: { label: 'Minimal', props: { tabCount: 2, variant: 'game' } },
  },
};
