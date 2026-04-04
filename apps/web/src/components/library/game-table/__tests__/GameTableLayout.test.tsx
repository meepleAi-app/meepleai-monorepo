/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { GameTableLayout } from '../GameTableLayout';

// Mock useMediaQuery to control mobile/desktop rendering
const mockUseMediaQuery = vi.fn(() => false);
vi.mock('@/hooks/useMediaQuery', () => ({
  useMediaQuery: (...args: unknown[]) => mockUseMediaQuery(...args),
}));

// Mock framer-motion to avoid animation complexity in tests
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => {
      // Filter out framer-motion props to avoid React warnings
      const {
        initial: _i,
        animate: _a,
        exit: _e,
        variants: _v,
        transition: _t,
        layout: _l,
        ...rest
      } = props;
      return <div {...rest}>{children}</div>;
    },
    span: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => {
      const { initial: _i, animate: _a, exit: _e, variants: _v, transition: _t, ...rest } = props;
      return <span {...rest}>{children}</span>;
    },
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => <>{children}</>,
}));

describe('GameTableLayout', () => {
  const defaultProps = {
    card: <div data-testid="card">Card</div>,
    toolsZone: <div data-testid="tools">Tools</div>,
    knowledgeZone: <div data-testid="knowledge">Knowledge</div>,
    sessionsZone: <div data-testid="sessions">Sessions</div>,
  };

  beforeEach(() => {
    mockUseMediaQuery.mockReturnValue(false); // desktop by default
  });

  it('renders card and all zones', () => {
    render(<GameTableLayout {...defaultProps} />);
    expect(screen.getByTestId('card')).toBeInTheDocument();
    expect(screen.getByTestId('tools')).toBeInTheDocument();
    expect(screen.getByTestId('knowledge')).toBeInTheDocument();
    expect(screen.getByTestId('sessions')).toBeInTheDocument();
  });

  it('renders drawer when drawerOpen is true', () => {
    render(
      <GameTableLayout
        {...defaultProps}
        drawer={<div data-testid="drawer-content">Drawer</div>}
        drawerOpen={true}
        onDrawerClose={vi.fn()}
      />
    );
    expect(screen.getByTestId('drawer-content')).toBeInTheDocument();
  });

  it('does not render drawer when drawerOpen is false', () => {
    render(
      <GameTableLayout
        {...defaultProps}
        drawer={<div data-testid="drawer-content">Drawer</div>}
        drawerOpen={false}
        onDrawerClose={vi.fn()}
      />
    );
    expect(screen.queryByTestId('drawer-content')).not.toBeInTheDocument();
  });

  it('calls onDrawerClose when backdrop is clicked', () => {
    const onClose = vi.fn();
    render(
      <GameTableLayout
        {...defaultProps}
        drawer={<div>Drawer</div>}
        drawerOpen={true}
        onDrawerClose={onClose}
      />
    );
    const backdrop = screen.getByTestId('drawer-backdrop');
    fireEvent.click(backdrop);
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('calls onDrawerClose on Escape key', () => {
    const onClose = vi.fn();
    render(
      <GameTableLayout
        {...defaultProps}
        drawer={<div>Drawer</div>}
        drawerOpen={true}
        onDrawerClose={onClose}
      />
    );
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('renders zone headers', () => {
    render(<GameTableLayout {...defaultProps} />);
    expect(screen.getByText('Strumenti')).toBeInTheDocument();
    expect(screen.getByText('Conoscenza')).toBeInTheDocument();
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
  });

  describe('mobile layout', () => {
    beforeEach(() => {
      mockUseMediaQuery.mockReturnValue(true); // mobile
    });

    it('renders card and zone headers in mobile mode', () => {
      render(<GameTableLayout {...defaultProps} />);
      expect(screen.getByTestId('card')).toBeInTheDocument();
      expect(screen.getByText('Strumenti')).toBeInTheDocument();
      expect(screen.getByText('Conoscenza')).toBeInTheDocument();
      expect(screen.getByText('Sessioni')).toBeInTheDocument();
    });

    it('shows first zone expanded by default', () => {
      render(<GameTableLayout {...defaultProps} />);
      // Tools is open by default
      expect(screen.getByTestId('tools')).toBeInTheDocument();
    });

    it('renders drawer overlay in mobile mode', () => {
      render(
        <GameTableLayout
          {...defaultProps}
          drawer={<div data-testid="drawer-content">Drawer</div>}
          drawerOpen={true}
          onDrawerClose={vi.fn()}
        />
      );
      expect(screen.getByTestId('drawer-content')).toBeInTheDocument();
    });
  });

  it('drawer has dialog role and aria-modal', () => {
    render(
      <GameTableLayout
        {...defaultProps}
        drawer={<div>Drawer</div>}
        drawerOpen={true}
        onDrawerClose={vi.fn()}
      />
    );
    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAttribute('aria-modal', 'true');
  });
});
