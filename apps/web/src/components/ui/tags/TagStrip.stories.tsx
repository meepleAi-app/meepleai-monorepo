import { Sparkles, Tag as TagIcon, Check, Heart } from 'lucide-react';

import { TagStrip } from './TagStrip';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'UI/Tags/TagStrip',
  component: TagStrip,
  parameters: { layout: 'centered' },
  tags: ['autodocs']
} satisfies Meta<typeof TagStrip>;

export default meta;
type Story = StoryObj<typeof meta>;

const gameTags = [
  { id: 'new', label: 'New', icon: Sparkles, bgColor: 'hsl(142 76% 36%)', color: 'hsl(0 0% 100%)' },
  { id: 'sale', label: 'Sale', icon: TagIcon, bgColor: 'hsl(0 84% 60%)', color: 'hsl(0 0% 100%)' },
  { id: 'owned', label: 'Owned', icon: Check, bgColor: 'hsl(221 83% 53%)', color: 'hsl(0 0% 100%)' },
  { id: 'wishlisted', label: 'Wishlist', icon: Heart, bgColor: 'hsl(350 89% 60%)', color: 'hsl(0 0% 100%)' }
];

export const Default: Story = {
  args: { tags: gameTags.slice(0, 3), maxVisible: 3, variant: 'desktop' },
  decorators: [(Story) => <div className="relative w-64 h-96 bg-slate-200 rounded-2xl"><Story /></div>]
};

export const WithOverflow: Story = {
  args: { tags: gameTags, maxVisible: 3, variant: 'desktop' },
  decorators: Default.decorators
};

export const Mobile: Story = {
  args: { tags: gameTags, maxVisible: 3, variant: 'mobile' },
  decorators: [(Story) => <div className="relative w-48 h-64 bg-slate-200 rounded-2xl"><Story /></div>]
};
