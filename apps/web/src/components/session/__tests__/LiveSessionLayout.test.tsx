import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, beforeEach, vi } from 'vitest';

import { useSessionStore } from '@/store/session';
import { LiveSessionLayout } from '../LiveSessionLayout';

vi.mock('next/navigation', () => ({ usePathname: () => '/sessions/s1' }));

const defaultProps = {
  leftPanel: <div>Left</div>,
  centerContent: <div>Center</div>,
  rightPanel: <div>Right</div>,
};

describe('LiveSessionLayout', () => {
  beforeEach(() => {
    useSessionStore.getState().reset();
    localStorage.clear();
  });

  it('renders three columns on desktop', () => {
    render(<LiveSessionLayout {...defaultProps} />);
    expect(screen.getByText('Left')).toBeInTheDocument();
    expect(screen.getByText('Center')).toBeInTheDocument();
    expect(screen.getByText('Right')).toBeInTheDocument();
  });

  it('renders with data-testid', () => {
    render(<LiveSessionLayout {...defaultProps} />);
    expect(screen.getByTestId('live-session-layout')).toBeInTheDocument();
  });

  it('toggles left panel on Ctrl+[', () => {
    render(<LiveSessionLayout {...defaultProps} />);
    fireEvent.keyDown(document, { key: '[', ctrlKey: true });
    expect(localStorage.getItem('session-left-collapsed')).toBe('true');
  });

  it('toggles right panel on Ctrl+]', () => {
    render(<LiveSessionLayout {...defaultProps} />);
    fireEvent.keyDown(document, { key: ']', ctrlKey: true });
    expect(localStorage.getItem('session-right-collapsed')).toBe('true');
  });

  it('ignores shortcuts when typing in an input', () => {
    render(
      <LiveSessionLayout
        leftPanel={<div>Left</div>}
        centerContent={<input data-testid="input" />}
        rightPanel={<div>Right</div>}
      />
    );
    const input = screen.getByTestId('input');
    fireEvent.keyDown(input, { key: '[', ctrlKey: true });
    expect(localStorage.getItem('session-left-collapsed')).toBe('false');
  });
});
