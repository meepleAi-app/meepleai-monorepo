/**
 * Breadcrumb Tests
 * Issue #3292 - Phase 6: Breadcrumb & Polish
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { type ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { LayoutProvider } from '../../LayoutProvider';
import { Breadcrumb, getContextConfig } from '../Breadcrumb';

// Mock mobile viewport
vi.mock('@/hooks/useResponsive', () => ({
  useResponsive: () => ({
    deviceType: 'mobile',
    isMobile: true,
    isTablet: false,
    isDesktop: false,
    viewportWidth: 375,
  }),
}));

// Create query client for tests
const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

// Helper wrapper
function createWrapper(context: 'default' | 'library' | 'game_detail' | 'session_active' | 'chat') {
  return function Wrapper({ children }: { children: ReactNode }) {
    const queryClient = createTestQueryClient();
    return (
      <QueryClientProvider client={queryClient}>
        <LayoutProvider initialContext={context}>{children}</LayoutProvider>
      </QueryClientProvider>
    );
  };
}

describe('Breadcrumb', () => {
  it('should render in library context on mobile', () => {
    render(<Breadcrumb />, { wrapper: createWrapper('library') });

    expect(screen.getByText('La mia libreria')).toBeInTheDocument();
  });

  it('should show correct label for game_detail context', () => {
    render(<Breadcrumb />, { wrapper: createWrapper('game_detail') });

    expect(screen.getByText('Dettagli gioco')).toBeInTheDocument();
  });

  it('should show correct label for session_active context', () => {
    render(<Breadcrumb />, { wrapper: createWrapper('session_active') });

    expect(screen.getByText('Sessione attiva')).toBeInTheDocument();
  });

  it('should show correct label for chat context', () => {
    render(<Breadcrumb />, { wrapper: createWrapper('chat') });

    expect(screen.getByText('Chat AI')).toBeInTheDocument();
  });

  it('should have navigation role', () => {
    render(<Breadcrumb />, { wrapper: createWrapper('library') });

    const nav = screen.getByRole('navigation');
    expect(nav).toBeInTheDocument();
  });

  it('should have proper aria-label', () => {
    render(<Breadcrumb />, { wrapper: createWrapper('library') });

    const nav = screen.getByRole('navigation');
    expect(nav).toHaveAttribute('aria-label', 'Contesto corrente');
  });

  it('should use custom label when provided', () => {
    render(<Breadcrumb customLabel="Gioco personalizzato" />, {
      wrapper: createWrapper('game_detail'),
    });

    expect(screen.getByText('Gioco personalizzato')).toBeInTheDocument();
  });

  it('should have screen reader announcement', () => {
    render(<Breadcrumb />, { wrapper: createWrapper('library') });

    expect(screen.getByText('Sei in: La mia libreria')).toBeInTheDocument();
  });

  it('should render with emoji when useEmoji is true', () => {
    render(<Breadcrumb useEmoji={true} />, { wrapper: createWrapper('library') });

    expect(screen.getByText('📚')).toBeInTheDocument();
  });

  it('should apply custom className', () => {
    render(<Breadcrumb className="test-class" />, {
      wrapper: createWrapper('library'),
    });

    const nav = screen.getByRole('navigation');
    expect(nav).toHaveClass('test-class');
  });
});

describe('getContextConfig', () => {
  it('should return correct config for library', () => {
    const config = getContextConfig('library');

    expect(config.label).toBe('La mia libreria');
    expect(config.emoji).toBe('📚');
  });

  it('should return correct config for game_detail', () => {
    const config = getContextConfig('game_detail');

    expect(config.label).toBe('Dettagli gioco');
    expect(config.emoji).toBe('🎲');
  });

  it('should return correct config for session_active', () => {
    const config = getContextConfig('session_active');

    expect(config.label).toBe('Sessione attiva');
    expect(config.emoji).toBe('▶️');
  });

  it('should return correct config for chat', () => {
    const config = getContextConfig('chat');

    expect(config.label).toBe('Chat AI');
    expect(config.emoji).toBe('🤖');
  });
});
