/**
 * QuickActionsPanel Component Tests (Issue #2861)
 *
 * Test Coverage:
 * - Re-export verification (QuickActions as QuickActionsPanel)
 * - Type exports (QuickAction, QuickActionsProps, GradientKey)
 * - Default actions export
 * - Component rendering via re-export
 *
 * Note: Comprehensive QuickActions tests exist in QuickActions.test.tsx
 * These tests verify the re-export wrapper works correctly.
 *
 * Target: >=85% coverage
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import {
  QuickActionsPanel,
  defaultQuickActions,
  type QuickAction,
  type QuickActionsProps,
  type GradientKey,
} from '../QuickActionsPanel';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

// ============================================================================
// Test Suite
// ============================================================================

describe('QuickActionsPanel', () => {
  // ============================================================================
  // Re-export Verification Tests
  // ============================================================================

  describe('Re-export Verification', () => {
    it('exports QuickActionsPanel component', () => {
      expect(QuickActionsPanel).toBeDefined();
      expect(typeof QuickActionsPanel).toBe('function');
    });

    it('exports defaultQuickActions array', () => {
      expect(defaultQuickActions).toBeDefined();
      expect(Array.isArray(defaultQuickActions)).toBe(true);
      expect(defaultQuickActions.length).toBeGreaterThan(0);
    });

    it('defaultQuickActions has expected structure', () => {
      const action = defaultQuickActions[0];
      expect(action).toHaveProperty('id');
      expect(action).toHaveProperty('label');
      expect(action).toHaveProperty('href');
      expect(action).toHaveProperty('icon');
    });

    it('defaultQuickActions includes gradient styling', () => {
      const actionsWithGradient = defaultQuickActions.filter(a => a.gradient);
      expect(actionsWithGradient.length).toBeGreaterThan(0);
    });
  });

  // ============================================================================
  // Component Rendering Tests
  // ============================================================================

  describe('Component Rendering', () => {
    it('renders with default actions', () => {
      render(<QuickActionsPanel />);

      expect(screen.getByTestId('quick-actions-title')).toBeInTheDocument();
      expect(screen.getByTestId('quick-actions-grid')).toBeInTheDocument();
    });

    it('renders default title "Quick Actions"', () => {
      render(<QuickActionsPanel />);

      expect(screen.getByText('Quick Actions')).toBeInTheDocument();
    });

    it('renders custom title', () => {
      render(<QuickActionsPanel title="Azioni Rapide" />);

      expect(screen.getByText('Azioni Rapide')).toBeInTheDocument();
    });

    it('renders loading state', () => {
      render(<QuickActionsPanel loading={true} />);

      expect(screen.getByTestId('quick-actions-skeleton')).toBeInTheDocument();
    });

    it('renders custom actions', () => {
      const customActions: QuickAction[] = [
        {
          id: 'custom-1',
          label: 'Custom Action',
          href: '/custom',
          icon: () => <span>Icon</span>,
          gradient: 'blue-indigo' as GradientKey,
        },
      ];

      render(<QuickActionsPanel actions={customActions} />);

      expect(screen.getByText('Custom Action')).toBeInTheDocument();
    });

    it('applies custom className', () => {
      const { container } = render(<QuickActionsPanel className="custom-class" />);

      const card = container.querySelector('.custom-class');
      expect(card).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Badge Support Tests
  // ============================================================================

  describe('Badge Support', () => {
    it('renders badges from badges prop', () => {
      render(<QuickActionsPanel badges={{ 'approve-games': 5 }} />);

      expect(screen.getByText('5')).toBeInTheDocument();
    });

    it('renders 99+ for large badge counts', () => {
      render(<QuickActionsPanel badges={{ 'approve-games': 150 }} />);

      expect(screen.getByText('99+')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Type Export Tests
  // ============================================================================

  describe('Type Exports', () => {
    it('QuickAction type can be used', () => {
      const action: QuickAction = {
        id: 'test',
        label: 'Test',
        href: '/test',
        icon: () => null,
      };

      expect(action.id).toBe('test');
    });

    it('QuickActionsProps type can be used', () => {
      const props: QuickActionsProps = {
        actions: [],
        loading: false,
        className: 'test-class',
        title: 'Test Title',
        badges: {},
      };

      expect(props.title).toBe('Test Title');
    });

    it('GradientKey type accepts valid values', () => {
      const gradients: GradientKey[] = [
        'green-emerald',
        'blue-indigo',
        'amber-orange',
        'red-rose',
        'purple-violet',
        'stone-stone',
      ];

      expect(gradients.length).toBe(6);
    });
  });

  // ============================================================================
  // Default Actions Content Tests
  // ============================================================================

  describe('Default Actions Content', () => {
    it('includes Approva Giochi action', () => {
      const action = defaultQuickActions.find(a => a.id === 'approve-games');
      expect(action).toBeDefined();
      expect(action?.label).toBe('Approva Giochi');
      expect(action?.href).toBe('/admin/games/pending');
    });

    it('includes Gestisci Utenti action', () => {
      const action = defaultQuickActions.find(a => a.id === 'manage-users');
      expect(action).toBeDefined();
      expect(action?.label).toBe('Gestisci Utenti');
      expect(action?.href).toBe('/admin/users');
    });

    it('includes Vedi Alert action', () => {
      const action = defaultQuickActions.find(a => a.id === 'view-alerts');
      expect(action).toBeDefined();
      expect(action?.label).toBe('Vedi Alert');
    });

    it('includes Svuota Cache action', () => {
      const action = defaultQuickActions.find(a => a.id === 'clear-cache');
      expect(action).toBeDefined();
      expect(action?.label).toBe('Svuota Cache');
    });

    it('includes Esporta Dati action', () => {
      const action = defaultQuickActions.find(a => a.id === 'export-data');
      expect(action).toBeDefined();
      expect(action?.label).toBe('Esporta Dati');
    });

    it('includes Configurazione action', () => {
      const action = defaultQuickActions.find(a => a.id === 'configuration');
      expect(action).toBeDefined();
      expect(action?.label).toBe('Configurazione');
    });

    it('all default actions have gradient styling', () => {
      defaultQuickActions.forEach(action => {
        expect(action.gradient).toBeDefined();
      });
    });
  });

  // ============================================================================
  // Accessibility Tests
  // ============================================================================

  describe('Accessibility', () => {
    it('renders action links with href', () => {
      render(<QuickActionsPanel />);

      const links = screen.getAllByRole('link');
      expect(links.length).toBe(defaultQuickActions.length);
    });

    it('icons are hidden from screen readers', () => {
      const { container } = render(<QuickActionsPanel />);

      const iconContainers = container.querySelectorAll('[aria-hidden="true"]');
      expect(iconContainers.length).toBeGreaterThan(0);
    });
  });
});
