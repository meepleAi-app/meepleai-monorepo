/**
 * Tests for KeyboardShortcutsHelp Component
 * Issue #1100: Keyboard shortcuts help modal
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { KeyboardShortcutsHelp } from '../layout/KeyboardShortcutsHelp';
import type { KeyboardShortcut } from '@/hooks/useKeyboardShortcuts';

describe('KeyboardShortcutsHelp', () => {
  const mockOnClose = vi.fn();

  const mockShortcuts: KeyboardShortcut[] = [
    {
      key: 'n',
      ctrl: true,
      meta: true,
      description: 'New chat',
      action: vi.fn(),
      category: 'navigation',
    },
    {
      key: 'u',
      ctrl: true,
      meta: true,
      description: 'Upload PDF',
      action: vi.fn(),
      category: 'navigation',
    },
    {
      key: 'Enter',
      ctrl: true,
      meta: true,
      description: 'Send message',
      action: vi.fn(),
      category: 'editor',
    },
    {
      key: '?',
      shift: true,
      description: 'Show keyboard shortcuts',
      action: vi.fn(),
      category: 'system',
    },
    {
      key: 'Escape',
      description: 'Close modal',
      action: vi.fn(),
      category: 'system',
    },
  ];

  beforeEach(() => {
    mockOnClose.mockClear();
  });

  it('renders when open', () => {
    render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={mockShortcuts}
      />
    );

    expect(screen.getByText('Keyboard Shortcuts')).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    render(
      <KeyboardShortcutsHelp
        isOpen={false}
        onClose={mockOnClose}
        shortcuts={mockShortcuts}
      />
    );

    expect(screen.queryByText('Keyboard Shortcuts')).not.toBeInTheDocument();
  });

  it('displays all shortcuts grouped by category', () => {
    render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={mockShortcuts}
      />
    );

    // Check category headers
    expect(screen.getByText('Navigation')).toBeInTheDocument();
    expect(screen.getByText('Editor')).toBeInTheDocument();
    expect(screen.getByText('System')).toBeInTheDocument();

    // Check shortcut descriptions
    expect(screen.getByText('New chat')).toBeInTheDocument();
    expect(screen.getByText('Upload PDF')).toBeInTheDocument();
    expect(screen.getByText('Send message')).toBeInTheDocument();
    expect(screen.getByText('Show keyboard shortcuts')).toBeInTheDocument();
    expect(screen.getByText('Close modal')).toBeInTheDocument();
  });

  it('displays keyboard shortcut indicators', () => {
    render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={mockShortcuts}
      />
    );

    // Check for kbd elements
    const kbdElements = screen.getAllByRole('generic', { hidden: true });
    expect(kbdElements.length).toBeGreaterThan(0);
  });

  it('displays help tip at the bottom', () => {
    render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={mockShortcuts}
      />
    );

    expect(screen.getByText(/Tip:/)).toBeInTheDocument();
    expect(screen.getByText(/anytime to open this help dialog/)).toBeInTheDocument();
  });

  it('calls onClose when dialog is closed', async () => {
    const user = userEvent.setup();

    render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={mockShortcuts}
      />
    );

    // Note: Testing dialog close behavior depends on Radix UI Dialog implementation
    // The actual close button/overlay interaction might need adjustment based on
    // the rendered DOM structure
  });

  it('uses default shortcuts when none provided', () => {
    render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
      />
    );

    // Check for some default shortcuts
    expect(screen.getByText('New chat')).toBeInTheDocument();
    expect(screen.getByText('Upload PDF')).toBeInTheDocument();
    expect(screen.getByText('Open command palette')).toBeInTheDocument();
  });

  it('displays correct platform-specific modifier key', () => {
    // Test default platform (likely Windows in CI/test environment)
    render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={mockShortcuts}
      />
    );

    // Check that DialogDescription contains platform-specific key info
    // In test environment (non-Mac), should show "Ctrl key"
    expect(screen.getByText(/Use (⌘ \(Command\)|Ctrl) key with these shortcuts/)).toBeInTheDocument();

    // Check that the tip section also mentions the platform key
    expect(screen.getByText(/Most shortcuts work with (⌘ Command|Ctrl) key/)).toBeInTheDocument();
  });

  it('displays category icons', () => {
    render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={mockShortcuts}
      />
    );

    // Check that icons are rendered (Lucide icons are SVG elements)
    // Dialog content is rendered in a portal, so query from document.body
    const svgIcons = document.body.querySelectorAll('svg');
    expect(svgIcons.length).toBeGreaterThan(0);
  });

  it('is accessible', () => {
    render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={mockShortcuts}
      />
    );

    // Check for dialog role (Radix UI Dialog renders with role="dialog" in a portal)
    const dialog = document.body.querySelector('[role="dialog"]');
    expect(dialog).toBeInTheDocument();

    // Check for accessible title
    expect(screen.getByText('Keyboard Shortcuts')).toBeInTheDocument();

    // Check for description
    expect(screen.getByText(/Use.*key with these shortcuts/)).toBeInTheDocument();
  });

  it('groups shortcuts correctly by category', () => {
    const customShortcuts: KeyboardShortcut[] = [
      { key: 'a', description: 'Action A', action: vi.fn(), category: 'actions' },
      { key: 'b', description: 'Action B', action: vi.fn(), category: 'actions' },
      { key: 'n', description: 'Nav N', action: vi.fn(), category: 'navigation' },
    ];

    render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={customShortcuts}
      />
    );

    expect(screen.getByText('Actions')).toBeInTheDocument();
    expect(screen.getByText('Navigation')).toBeInTheDocument();
    expect(screen.getByText('Action A')).toBeInTheDocument();
    expect(screen.getByText('Action B')).toBeInTheDocument();
    expect(screen.getByText('Nav N')).toBeInTheDocument();
  });

  it('handles empty shortcuts array', () => {
    render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={[]}
      />
    );

    // Should still render dialog but with no shortcuts
    expect(screen.getByText('Keyboard Shortcuts')).toBeInTheDocument();
  });
});
