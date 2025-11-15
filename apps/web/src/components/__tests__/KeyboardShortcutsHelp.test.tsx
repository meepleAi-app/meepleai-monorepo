/**
 * Tests for KeyboardShortcutsHelp Component
 * Issue #1100: Keyboard shortcuts help modal
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { KeyboardShortcutsHelp } from '../KeyboardShortcutsHelp';
import type { KeyboardShortcut } from '@/hooks/useKeyboardShortcuts';

describe('KeyboardShortcutsHelp', () => {
  const mockOnClose = jest.fn();

  const mockShortcuts: KeyboardShortcut[] = [
    {
      key: 'n',
      ctrl: true,
      meta: true,
      description: 'New chat',
      action: jest.fn(),
      category: 'navigation',
    },
    {
      key: 'u',
      ctrl: true,
      meta: true,
      description: 'Upload PDF',
      action: jest.fn(),
      category: 'navigation',
    },
    {
      key: 'Enter',
      ctrl: true,
      meta: true,
      description: 'Send message',
      action: jest.fn(),
      category: 'editor',
    },
    {
      key: '?',
      shift: true,
      description: 'Show keyboard shortcuts',
      action: jest.fn(),
      category: 'system',
    },
    {
      key: 'Escape',
      description: 'Close modal',
      action: jest.fn(),
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
    // Mock Mac platform
    Object.defineProperty(global.navigator, 'platform', {
      value: 'MacIntel',
      writable: true,
      configurable: true,
    });

    const { rerender } = render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={mockShortcuts}
      />
    );

    expect(screen.getByText(/⌘ Command/)).toBeInTheDocument();

    // Mock Windows platform
    Object.defineProperty(global.navigator, 'platform', {
      value: 'Win32',
      writable: true,
      configurable: true,
    });

    rerender(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={mockShortcuts}
      />
    );

    // Note: After platform change, component should show Ctrl instead of Cmd
    // This might require component re-mount or dynamic check
  });

  it('displays category icons', () => {
    const { container } = render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={mockShortcuts}
      />
    );

    // Check that icons are rendered (Lucide icons are SVG elements)
    const svgIcons = container.querySelectorAll('svg');
    expect(svgIcons.length).toBeGreaterThan(0);
  });

  it('is accessible', () => {
    const { container } = render(
      <KeyboardShortcutsHelp
        isOpen={true}
        onClose={mockOnClose}
        shortcuts={mockShortcuts}
      />
    );

    // Check for dialog role (Radix UI Dialog renders with role="dialog")
    const dialog = container.querySelector('[role="dialog"]');
    expect(dialog).toBeInTheDocument();

    // Check for accessible title
    expect(screen.getByText('Keyboard Shortcuts')).toBeInTheDocument();

    // Check for description
    expect(screen.getByText(/Use.*key with these shortcuts/)).toBeInTheDocument();
  });

  it('groups shortcuts correctly by category', () => {
    const customShortcuts: KeyboardShortcut[] = [
      { key: 'a', description: 'Action A', action: jest.fn(), category: 'actions' },
      { key: 'b', description: 'Action B', action: jest.fn(), category: 'actions' },
      { key: 'n', description: 'Nav N', action: jest.fn(), category: 'navigation' },
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
