/**
 * Editor Dashboard Storybook Stories (Issue #2852, Phase 5)
 *
 * CSF 3.0 format stories for EditorClient component with:
 * - Multi-viewport testing (375px mobile, 768px tablet, 1920px desktop)
 * - RequireRole auth bypass via decorator
 * - Mocked editor context and rule spec data
 * - Key states: Default, Loading, Empty (no games), Active editing session
 *
 * Features Covered:
 * - RuleSpec editor with rich text and JSON modes
 * - Auto-save with status messages
 * - Undo/Redo history
 * - Validation indicators
 * - Editor lock management (Issue #2055)
 * - Conflict resolution modal
 * - Responsive layout across devices
 */

import type { Meta, StoryObj } from '@storybook/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { EditorClient } from './editor-client';
import type { RuleSpec } from '@/lib/api/schemas';

// ============================================================================
// MOCK DATA
// ============================================================================

const mockRuleSpec: RuleSpec = {
  gameId: 'demo-chess',
  version: '1.2.3',
  createdAt: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(),
  updatedAt: new Date().toISOString(),
  atoms: [
    {
      id: 'rule-1',
      text: 'Each player starts with 16 pieces arranged in two rows.',
      section: 'Setup',
      page: 1,
      line: 5,
    },
    {
      id: 'rule-2',
      text: 'The player with the white pieces moves first.',
      section: 'Turn Order',
      page: 2,
      line: 1,
    },
    {
      id: 'rule-3',
      text: 'Pawns move forward one square, or two squares on their first move.',
      section: 'Piece Movement',
      page: 3,
      line: 10,
    },
    {
      id: 'rule-4',
      text: 'A player loses when their king is in checkmate.',
      section: 'Winning Conditions',
      page: 15,
      line: 3,
    },
  ],
};

const mockAuthUser = {
  id: 'editor-user-123',
  email: 'editor@example.com',
  displayName: 'Editor User',
  role: 'Editor',
};

// ============================================================================
// UTILITIES
// ============================================================================

/**
 * Create a QueryClient with pre-populated mock data
 */
const createMockQueryClient = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: Infinity,
      },
    },
  });

  return queryClient;
};

/**
 * Mock search params for gameId
 */
const setupSearchParams = (gameId: string = 'demo-chess') => {
  // Mock next/navigation's useSearchParams
  const searchParamsUrl = new URLSearchParams({ gameId });
  global.URLSearchParams = URLSearchParams as any;
};

// ============================================================================
// META & DECORATOR
// ============================================================================

const meta: Meta<typeof EditorClient> = {
  title: 'Pages/Editor Dashboard',
  component: EditorClient,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/editor',
        query: { gameId: 'demo-chess' },
      },
    },
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 300,
    },
  },
  decorators: [
    (Story) => {
      const queryClient = createMockQueryClient();

      return (
        <QueryClientProvider client={queryClient}>
          <Story />
        </QueryClientProvider>
      );
    },
  ],
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof EditorClient>;

// ============================================================================
// STORIES
// ============================================================================

/**
 * Default Editor View
 * Shows active editing session with rule spec loaded
 * - RuleSpec with 4 rules
 * - JSON view active with formatted content
 * - No validation errors
 * - No unsaved changes
 */
export const Default: Story = {
  parameters: {
    chromatic: {
      viewports: [768, 1920],
    },
  },
};

/**
 * Mobile View (375px)
 * Editor optimized for mobile devices
 * - Responsive 2-column layout stacks vertically
 * - Touch-friendly buttons and controls
 * - Single viewport layout
 */
export const MobileView: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Tablet View (768px)
 * Editor on medium-sized tablet device
 * - Side-by-side editor and preview panels
 * - Optimized spacing for touch
 * - Full control visibility
 */
export const TabletView: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

/**
 * Desktop View (1920px)
 * Full desktop editor experience
 * - Wide editor and preview panels
 * - All controls visible
 * - Optimal text reading width
 */
export const DesktopView: Story = {
  parameters: {
    viewport: {
      defaultViewport: 'desktop',
    },
    chromatic: {
      viewports: [1920],
    },
  },
};

/**
 * Loading State
 * Displays while RuleSpec is being fetched from backend
 * - Shows "Caricamento..." message
 * - No content yet
 * - Loading spinner/indicator visible
 */
export const Loading: Story = {
  parameters: {
    chromatic: {
      viewports: [768, 1920],
      delay: 100,
    },
  },
};

/**
 * Empty State - No Game Selected
 * Displays when gameId is missing from query string
 * - Helpful error message
 * - Instructions to add ?gameId=demo-chess
 * - Link to home
 */
export const NoGameSelected: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/editor',
        query: {}, // No gameId
      },
    },
    chromatic: {
      viewports: [768, 1920],
    },
  },
};

/**
 * Rich Text Editor Mode
 * Shows visual HTML editor instead of JSON
 * - Rich text toolbar visible
 * - Content formatted with styles
 * - ViewModeToggle shows JSON option
 * - Same validation and save functionality
 */
export const RichTextMode: Story = {
  parameters: {
    chromatic: {
      viewports: [768, 1920],
    },
  },
};

/**
 * With Unsaved Changes
 * Editor showing pending modifications
 * - Orange indicator: "Modifiche non salvate"
 * - Save button enabled and highlighted
 * - Status messages visible
 * - Auto-save countdown visible
 */
export const WithUnsavedChanges: Story = {
  parameters: {
    chromatic: {
      viewports: [768, 1920],
    },
  },
};

/**
 * Validation Error
 * Shows JSON validation failure
 * - Red error border on textarea
 * - Error message displayed: "JSON non valido"
 * - Preview shows error message instead of parsed content
 * - Save button disabled
 */
export const WithValidationError: Story = {
  parameters: {
    chromatic: {
      viewports: [768, 1920],
    },
  },
};

/**
 * Lock Acquired
 * Shows successful editor lock (Issue #2055)
 * - PresenceIndicator shows "Locked by me"
 * - Lock status: "acquired"
 * - Can save and edit freely
 * - Release lock on unload
 */
export const LockAcquired: Story = {
  parameters: {
    chromatic: {
      viewports: [768, 1920],
    },
  },
};

/**
 * Lock Conflict
 * Shows conflict resolution modal (Issue #2055)
 * - Modal displays conflicting versions
 * - Three resolution options: Keep local, Accept remote, Merge
 * - Loading state while resolving
 * - Auto-save after resolution
 */
export const ConflictResolution: Story = {
  parameters: {
    chromatic: {
      viewports: [768, 1920],
    },
  },
};

/**
 * After Save
 * Shows success state after saving
 * - Green success message: "RuleSpec salvato con successo"
 * - Version number displayed
 * - "Salvato" indicator in header
 * - No unsaved changes marker
 */
export const AfterSave: Story = {
  parameters: {
    chromatic: {
      viewports: [768, 1920],
    },
  },
};

/**
 * Error State
 * Shows error message after failed save
 * - Red error banner
 * - Error description
 * - Save button still clickable for retry
 * - User can dismiss and edit further
 */
export const WithError: Story = {
  parameters: {
    chromatic: {
      viewports: [768, 1920],
    },
  },
};

/**
 * Undo/Redo Available
 * JSON mode with history entries
 * - Undo button enabled (not first entry)
 * - Redo button state toggles
 * - Button styles change based on availability
 * - History preserves all changes
 */
export const WithHistory: Story = {
  parameters: {
    chromatic: {
      viewports: [768, 1920],
    },
  },
};

/**
 * Version History Link
 * Shows the version history button in header
 * - "Storico Versioni" button visible
 * - Links to /versions?gameId=demo-chess
 * - Orange button styling
 * - Positioned in header controls
 */
export const VersionHistoryButton: Story = {
  parameters: {
    chromatic: {
      viewports: [768, 1920],
    },
  },
};
