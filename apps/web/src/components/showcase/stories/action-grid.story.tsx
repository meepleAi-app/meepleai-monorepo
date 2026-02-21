/**
 * ActionGrid Story
 */

import { LayoutDashboard, Users, BookOpen, Gamepad2, Brain, FileText } from 'lucide-react';

import { ActionGrid, type ActionItem } from '@/components/ui/navigation/action-grid';

import type { ShowcaseStory } from '../types';

const SAMPLE_ACTIONS: ActionItem[] = [
  { id: '1', label: 'Overview', description: 'Dashboard overview', href: '#', icon: LayoutDashboard, gradient: 'blue-indigo' },
  { id: '2', label: 'Users', description: 'Manage users', href: '#', icon: Users, gradient: 'purple-violet' },
  { id: '3', label: 'Knowledge Base', description: 'RAG documents', href: '#', icon: BookOpen, gradient: 'green-emerald', badge: 3 },
  { id: '4', label: 'Games', description: 'Game catalog', href: '#', icon: Gamepad2, gradient: 'amber-orange' },
  { id: '5', label: 'Agents', description: 'AI agents', href: '#', icon: Brain, gradient: 'stone-stone' },
  { id: '6', label: 'Documents', description: 'PDF uploads', href: '#', icon: FileText, variant: 'default' },
];

type ActionGridShowcaseProps = {
  columns: number;
  showCard: boolean;
  loading: boolean;
  title: string;
};

export const actionGridStory: ShowcaseStory<ActionGridShowcaseProps> = {
  id: 'action-grid',
  title: 'ActionGrid',
  category: 'Navigation',
  description: 'Responsive grid of navigation actions with gradient icons.',

  component: function ActionGridStory({ columns, showCard, loading, title }: ActionGridShowcaseProps) {
    return (
      <div className="w-[600px]">
        <ActionGrid
          actions={SAMPLE_ACTIONS}
          columns={Number(columns) as 2 | 3 | 4}
          showCard={showCard}
          loading={loading}
          title={title || undefined}
        />
      </div>
    );
  },

  defaultProps: {
    columns: 3,
    showCard: true,
    loading: false,
    title: 'Quick Actions',
  },

  controls: {
    columns: {
      type: 'select',
      label: 'columns',
      options: ['2', '3', '4'],
      default: '3',
    },
    title: { type: 'text', label: 'title', default: 'Quick Actions' },
    showCard: { type: 'boolean', label: 'showCard', default: true },
    loading: { type: 'boolean', label: 'loading', default: false },
  },

  presets: {
    default: { label: '3 Columns', props: { columns: 3, showCard: true, title: 'Quick Actions' } },
    twoCol: { label: '2 Columns', props: { columns: 2, showCard: true, title: 'Admin Actions' } },
    noCard: { label: 'No Card', props: { showCard: false, title: '' } },
    loading: { label: 'Loading', props: { loading: true } },
  },
};
