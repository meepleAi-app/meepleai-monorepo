import type { Meta, StoryObj } from '@storybook/react';
import { EmptyState } from './EmptyState';
import { Upload, FolderOpen, Gamepad2, MessageSquare, Users } from 'lucide-react';

/**
 * EmptyState - Reusable empty state component
 *
 * ## Features
 * - Multiple visual variants (default, noData, noResults, noAccess, error)
 * - Custom icon support via Lucide React
 * - Optional action button with primary/secondary variants
 * - Reduced motion support
 * - Full accessibility support
 *
 * ## Accessibility
 * - ✅ role="status" for screen readers
 * - ✅ aria-live="polite" for dynamic updates
 * - ✅ Icon with aria-hidden="true"
 * - ✅ Screen reader text with full context
 * - ✅ Respects prefers-reduced-motion
 *
 * ## Usage
 * Use EmptyState when a data list, search result, or content area has no items to display.
 * Always provide helpful context through the description and consider adding an action
 * button to help users resolve the empty state.
 */
const meta = {
  title: 'EmptyState/EmptyState',
  component: EmptyState,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Reusable empty state component for consistent UI when there is no data to display. Supports multiple variants, custom icons, and action buttons.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    title: {
      control: 'text',
      description: 'Main title text (required)',
      table: {
        type: { summary: 'string' },
      },
    },
    description: {
      control: 'text',
      description: 'Optional description providing additional context',
      table: {
        type: { summary: 'string' },
      },
    },
    variant: {
      control: 'select',
      options: ['default', 'noData', 'noResults', 'noAccess', 'error'],
      description: 'Visual variant with default icon and styling',
      table: {
        type: { summary: "'default' | 'noData' | 'noResults' | 'noAccess' | 'error'" },
        defaultValue: { summary: 'default' },
      },
    },
    icon: {
      control: false,
      description: 'Custom Lucide icon (overrides variant default)',
      table: {
        type: { summary: 'LucideIcon' },
      },
    },
    action: {
      control: 'object',
      description: 'Optional action button with label and onClick',
      table: {
        type: {
          summary: '{ label: string; onClick: () => void; variant?: "primary" | "secondary" }',
        },
      },
    },
    className: {
      control: 'text',
      description: 'Additional CSS classes',
      table: {
        type: { summary: 'string' },
      },
    },
  },
} satisfies Meta<typeof EmptyState>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default empty state.
 * Simple message with inbox icon.
 */
export const Default: Story = {
  args: {
    title: 'No items yet',
    description: 'Add your first item to get started.',
  },
};

/**
 * No data variant.
 * For empty lists or collections.
 */
export const NoData: Story = {
  args: {
    title: 'No games in your collection',
    description: 'Add games to your collection to see them here.',
    variant: 'noData',
    action: {
      label: 'Add Game',
      onClick: () => console.log('Add game clicked'),
    },
  },
};

/**
 * No results variant.
 * For search with no matching results.
 */
export const NoResults: Story = {
  args: {
    title: 'No results found',
    description: 'Try adjusting your search or filter criteria.',
    variant: 'noResults',
    action: {
      label: 'Clear Filters',
      onClick: () => console.log('Clear filters clicked'),
      variant: 'secondary',
    },
  },
};

/**
 * No access variant.
 * For permission-restricted content.
 */
export const NoAccess: Story = {
  args: {
    title: 'Access denied',
    description: 'You do not have permission to view this content.',
    variant: 'noAccess',
    action: {
      label: 'Request Access',
      onClick: () => console.log('Request access clicked'),
    },
  },
};

/**
 * Error variant.
 * For error states with recovery action.
 */
export const Error: Story = {
  args: {
    title: 'Something went wrong',
    description: 'We encountered an error while loading this content. Please try again.',
    variant: 'error',
    action: {
      label: 'Retry',
      onClick: () => console.log('Retry clicked'),
    },
  },
};

/**
 * With custom icon.
 * Override variant icon with custom Lucide icon.
 */
export const WithCustomIcon: Story = {
  args: {
    title: 'No files uploaded',
    description: 'Drag and drop files here or click to browse.',
    icon: Upload,
    action: {
      label: 'Upload Files',
      onClick: () => console.log('Upload clicked'),
    },
  },
};

/**
 * MeepleAI specific - No games.
 * Empty state for game collection.
 */
export const NoGames: Story = {
  args: {
    title: 'Nessun gioco nella collezione',
    description: 'Aggiungi il tuo primo gioco da tavolo per iniziare.',
    icon: Gamepad2,
    action: {
      label: 'Aggiungi Gioco',
      onClick: () => console.log('Add game clicked'),
    },
  },
};

/**
 * MeepleAI specific - No chat history.
 * Empty state for chat sidebar.
 */
export const NoChatHistory: Story = {
  args: {
    title: 'Nessuna conversazione',
    description: 'Inizia una nuova conversazione per ricevere aiuto sulle regole.',
    icon: MessageSquare,
    action: {
      label: 'Nuova Chat',
      onClick: () => console.log('New chat clicked'),
    },
  },
};

/**
 * MeepleAI specific - No players.
 * Empty state for player list.
 */
export const NoPlayers: Story = {
  args: {
    title: 'Nessun giocatore',
    description: 'Aggiungi giocatori alla sessione di gioco.',
    icon: Users,
    action: {
      label: 'Aggiungi Giocatore',
      onClick: () => console.log('Add player clicked'),
      variant: 'secondary',
    },
  },
};

/**
 * Title only.
 * Minimal empty state without description.
 */
export const TitleOnly: Story = {
  args: {
    title: 'No items',
  },
};

/**
 * With secondary action button.
 * Less prominent call-to-action.
 */
export const SecondaryAction: Story = {
  args: {
    title: 'No recent activity',
    description: 'Your recent activity will appear here.',
    action: {
      label: 'View All Activity',
      onClick: () => console.log('View all clicked'),
      variant: 'secondary',
    },
  },
};

/**
 * All variants gallery.
 * Side-by-side comparison of all variants.
 */
export const AllVariants: Story = {
  render: () => (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-6 max-w-4xl">
      <div className="border border-slate-200 dark:border-slate-700 rounded-lg">
        <EmptyState
          title="Default"
          description="Default variant with inbox icon"
          variant="default"
        />
      </div>
      <div className="border border-slate-200 dark:border-slate-700 rounded-lg">
        <EmptyState title="No Data" description="For empty collections" variant="noData" />
      </div>
      <div className="border border-slate-200 dark:border-slate-700 rounded-lg">
        <EmptyState
          title="No Results"
          description="For search with no matches"
          variant="noResults"
        />
      </div>
      <div className="border border-slate-200 dark:border-slate-700 rounded-lg">
        <EmptyState
          title="No Access"
          description="For permission-restricted content"
          variant="noAccess"
        />
      </div>
      <div className="border border-slate-200 dark:border-slate-700 rounded-lg md:col-span-2">
        <EmptyState
          title="Error"
          description="For error states"
          variant="error"
          action={{
            label: 'Retry',
            onClick: () => console.log('Retry clicked'),
          }}
        />
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'All available variants displayed side-by-side for comparison.',
      },
    },
  },
};

/**
 * Custom icons gallery.
 * Examples with different Lucide icons.
 */
export const CustomIconsGallery: Story = {
  render: () => (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-6 max-w-4xl">
      <div className="border border-slate-200 dark:border-slate-700 rounded-lg">
        <EmptyState title="No Files" description="Upload files to continue" icon={Upload} />
      </div>
      <div className="border border-slate-200 dark:border-slate-700 rounded-lg">
        <EmptyState title="Empty Folder" description="No files in this folder" icon={FolderOpen} />
      </div>
      <div className="border border-slate-200 dark:border-slate-700 rounded-lg">
        <EmptyState title="No Games" description="Add games to your collection" icon={Gamepad2} />
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Examples with different custom Lucide icons.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Empty state on dark background.
 */
export const DarkTheme: Story = {
  args: {
    title: 'No items found',
    description: 'Try adjusting your search criteria.',
    variant: 'noResults',
    action: {
      label: 'Clear Filters',
      onClick: () => console.log('Clear clicked'),
    },
  },
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    Story => (
      <div className="dark p-8 bg-background min-w-[400px]">
        <Story />
      </div>
    ),
  ],
};

/**
 * With custom styling.
 * Custom background and border via className.
 */
export const WithCustomStyling: Story = {
  args: {
    title: 'Welcome to MeepleAI',
    description: 'Start by adding your first board game to the collection.',
    icon: Gamepad2,
    className: 'bg-blue-50 dark:bg-blue-950 border border-blue-200 dark:border-blue-800',
    action: {
      label: 'Add Your First Game',
      onClick: () => console.log('Add game clicked'),
    },
  },
};

/**
 * Responsive test.
 * Tests responsive behavior at different widths.
 */
export const Responsive: Story = {
  render: () => (
    <div className="space-y-8">
      <div className="w-64 border border-slate-200 dark:border-slate-700 rounded-lg">
        <EmptyState
          title="Small width"
          description="Testing at 256px width"
          action={{
            label: 'Action',
            onClick: () => {},
          }}
        />
      </div>
      <div className="w-96 border border-slate-200 dark:border-slate-700 rounded-lg">
        <EmptyState
          title="Medium width"
          description="Testing at 384px width with longer description text that may wrap."
          action={{
            label: 'Action',
            onClick: () => {},
          }}
        />
      </div>
      <div className="w-full max-w-2xl border border-slate-200 dark:border-slate-700 rounded-lg">
        <EmptyState
          title="Full width"
          description="Testing at full width with even longer description text that demonstrates how the component handles extended content gracefully."
          action={{
            label: 'Take Action',
            onClick: () => {},
          }}
        />
      </div>
    </div>
  ),
  parameters: {
    docs: {
      description: {
        story: 'Demonstrates responsive behavior at different container widths.',
      },
    },
  },
};
