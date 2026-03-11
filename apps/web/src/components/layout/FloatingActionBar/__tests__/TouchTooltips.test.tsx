/**
 * FloatingActionBar Touch-Friendly Tooltips Tests
 * Issue #7 from mobile-first-ux-epic.md
 */

import React from 'react';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';

// ─── Mocks ───────────────────────────────────────────────────────────────────

let mockIsMobile = false;

vi.mock('@/hooks/useResponsive', () => ({
  useResponsive: () => ({
    isMobile: mockIsMobile,
    isTablet: false,
    isDesktop: !mockIsMobile,
    deviceType: mockIsMobile ? 'mobile' : 'desktop',
  }),
  usePrefersReducedMotion: () => false,
}));

vi.mock('@/hooks/useScrollDirection', () => ({
  useScrollDirection: () => 'up',
}));

vi.mock('@/hooks/useVirtualKeyboard', () => ({
  useVirtualKeyboard: () => ({ isKeyboardOpen: false }),
}));

const mockUseNavigation = vi.fn();
vi.mock('@/context/NavigationContext', () => ({
  useNavigation: () => mockUseNavigation(),
}));

import { FloatingActionBar } from '../FloatingActionBar';

// ─── Helpers ─────────────────────────────────────────────────────────────────

const DummyIcon = () => <span data-testid="icon">icon</span>;

function setMobile() {
  mockIsMobile = true;
}

function setDesktop() {
  mockIsMobile = false;
}

function withCompactActions() {
  mockUseNavigation.mockReturnValue({
    actionBarActions: [
      { id: 'edit', label: 'Modifica', icon: DummyIcon, variant: 'ghost', onClick: vi.fn() },
      { id: 'delete', label: 'Elimina', icon: DummyIcon, variant: 'destructive', onClick: vi.fn() },
      { id: 'save', label: 'Salva', icon: DummyIcon, variant: 'primary', onClick: vi.fn() },
    ],
    miniNavTabs: [],
  });
}

function withDisabledAction() {
  mockUseNavigation.mockReturnValue({
    actionBarActions: [
      {
        id: 'delete',
        label: 'Elimina',
        icon: DummyIcon,
        variant: 'destructive',
        onClick: vi.fn(),
        disabled: true,
        disabledTooltip: 'Seleziona almeno un elemento',
      },
    ],
    miniNavTabs: [],
  });
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('FloatingActionBar — Touch-Friendly Tooltips', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    mockIsMobile = false;
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('Mobile: inline labels', () => {
    beforeEach(() => setMobile());

    it('shows text labels below compact icons on mobile', () => {
      withCompactActions();
      render(<FloatingActionBar />);
      // Compact (non-primary) actions should show inline label
      expect(screen.getByTestId('action-label-edit')).toBeInTheDocument();
      expect(screen.getByTestId('action-label-edit')).toHaveTextContent('Modifica');
    });

    it('shows inline label for destructive compact action', () => {
      withCompactActions();
      render(<FloatingActionBar />);
      expect(screen.getByTestId('action-label-delete')).toBeInTheDocument();
      expect(screen.getByTestId('action-label-delete')).toHaveTextContent('Elimina');
    });

    it('uses vertical layout for compact mobile actions', () => {
      withCompactActions();
      render(<FloatingActionBar />);
      const editButton = screen.getByLabelText('Modifica');
      expect(editButton.className).toContain('flex-col');
    });

    it('does not show hover tooltip on mobile', () => {
      withCompactActions();
      render(<FloatingActionBar />);
      const editWrapper = screen.getByLabelText('Modifica').parentElement!;
      fireEvent.mouseEnter(editWrapper);
      act(() => {
        vi.advanceTimersByTime(500);
      });
      expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
    });
  });

  describe('Desktop: hover tooltips', () => {
    beforeEach(() => setDesktop());

    it('does NOT show inline labels on desktop', () => {
      withCompactActions();
      render(<FloatingActionBar />);
      expect(screen.queryByTestId('action-label-edit')).not.toBeInTheDocument();
    });

    it('shows tooltip on hover for compact desktop actions', () => {
      withCompactActions();
      render(<FloatingActionBar />);
      const editWrapper = screen.getByLabelText('Modifica').parentElement!;
      fireEvent.mouseEnter(editWrapper);
      act(() => {
        vi.advanceTimersByTime(500);
      });
      expect(screen.getByRole('tooltip')).toHaveTextContent('Modifica');
    });

    it('hides tooltip on mouse leave', () => {
      withCompactActions();
      render(<FloatingActionBar />);
      const editWrapper = screen.getByLabelText('Modifica').parentElement!;
      fireEvent.mouseEnter(editWrapper);
      act(() => {
        vi.advanceTimersByTime(500);
      });
      expect(screen.getByRole('tooltip')).toBeInTheDocument();
      fireEvent.mouseLeave(editWrapper);
      expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
    });
  });

  describe('Disabled action tooltip', () => {
    it('shows disabledTooltip on long-press (mobile)', () => {
      setMobile();
      withDisabledAction();
      render(<FloatingActionBar />);
      const wrapper = screen.getByLabelText('Elimina').parentElement!;
      fireEvent.touchStart(wrapper);
      act(() => {
        vi.advanceTimersByTime(600);
      });
      expect(screen.getByRole('tooltip')).toHaveTextContent('Seleziona almeno un elemento');
    });

    it('shows disabledTooltip on hover (desktop)', () => {
      setDesktop();
      withDisabledAction();
      render(<FloatingActionBar />);
      const wrapper = screen.getByLabelText('Elimina').parentElement!;
      fireEvent.mouseEnter(wrapper);
      act(() => {
        vi.advanceTimersByTime(500);
      });
      expect(screen.getByRole('tooltip')).toHaveTextContent('Seleziona almeno un elemento');
    });
  });

  describe('Primary actions', () => {
    it('primary actions always show label (mobile and desktop)', () => {
      setMobile();
      withCompactActions();
      render(<FloatingActionBar />);
      // Primary action "Salva" should have a regular label, not inline
      const saveButton = screen.getByLabelText('Salva');
      expect(saveButton).toHaveTextContent('Salva');
      // Should NOT have the inline test id (it's not compact)
      expect(screen.queryByTestId('action-label-save')).not.toBeInTheDocument();
    });
  });
});
