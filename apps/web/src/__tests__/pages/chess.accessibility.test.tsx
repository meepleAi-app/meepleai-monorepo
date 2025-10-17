/**
 * Accessibility tests for Chess Page (chess.tsx)
 * Tests WCAG 2.1 AA compliance using jest-axe
 */

import React from 'react';
import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import ChessPage from '../../pages/chess';

// Extend Jest matchers
expect.extend(toHaveNoViolations);

// Mock Next.js router
jest.mock('next/router', () => ({
  useRouter: () => ({
    push: jest.fn(),
    pathname: '/chess',
    query: {},
    asPath: '/chess',
  }),
}));

// Mock Next.js Link
jest.mock('next/link', () => {
  const MockLink = ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  );
  MockLink.displayName = 'MockLink';
  return MockLink;
});

// Mock API client
jest.mock('../../lib/api', () => ({
  api: {
    post: jest.fn(),
    get: jest.fn().mockResolvedValue(null), // Not authenticated by default
  },
}));

// Mock react-chessboard
jest.mock('react-chessboard', () => ({
  Chessboard: () => <div data-testid="chessboard">Chess Board</div>,
}));

// Mock chess.js
jest.mock('chess.js', () => {
  return {
    Chess: jest.fn().mockImplementation(() => ({
      fen: jest.fn().mockReturnValue('rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1'),
      turn: jest.fn().mockReturnValue('w'),
      isCheckmate: jest.fn().mockReturnValue(false),
      isCheck: jest.fn().mockReturnValue(false),
      isDraw: jest.fn().mockReturnValue(false),
      isStalemate: jest.fn().mockReturnValue(false),
      move: jest.fn(),
    })),
  };
});

describe('Chess Page Accessibility (UI-05)', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Unauthenticated View (Login Required)', () => {
    it('should not have any accessibility violations (WCAG 2.1 AA)', async () => {
      const { container } = render(<ChessPage />);
      const results = await axe(container, {
        rules: {
          'color-contrast': { enabled: true },
          'html-has-lang': { enabled: true },
          'document-title': { enabled: true },
          'aria-prohibited-attr': { enabled: true },
          'valid-lang': { enabled: true },
        },
      });

      expect(results).toHaveNoViolations();
    });

    it('should have sufficient color contrast on login required view', async () => {
      const { container } = render(<ChessPage />);
      const results = await axe(container, {
        rules: {
          'color-contrast': { enabled: true },
        },
      });

      expect(results).toHaveNoViolations();
    });

    it('should have accessible "Torna alla Home" link', async () => {
      const { container } = render(<ChessPage />);
      const results = await axe(container, {
        rules: {
          'link-name': { enabled: true },
          'color-contrast': { enabled: true },
        },
      });

      expect(results).toHaveNoViolations();
    });

    it('should pass WCAG 2.1 Level AA rules', async () => {
      const { container } = render(<ChessPage />);
      const results = await axe(container, {
        runOnly: {
          type: 'tag',
          values: ['wcag2aa', 'wcag21aa'],
        },
      });

      expect(results).toHaveNoViolations();
    });
  });

  describe('Authenticated View (Chess Interface)', () => {
    beforeEach(() => {
      // Mock authenticated user
      const { api } = require('../../lib/api');
      api.get.mockResolvedValue({
        user: {
          id: 'test-user-id',
          email: 'test@meepleai.dev',
          displayName: 'Test User',
          role: 'user',
        },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      });
    });

    it('should not have any accessibility violations when authenticated', async () => {
      const { container } = render(<ChessPage />);

      // Wait for authentication check
      await new Promise(resolve => setTimeout(resolve, 100));

      const results = await axe(container, {
        rules: {
          'color-contrast': { enabled: true },
          'aria-prohibited-attr': { enabled: true },
          'button-name': { enabled: true },
          'label': { enabled: true },
        },
      });

      expect(results).toHaveNoViolations();
    });

    it('should have accessible form controls', async () => {
      const { container } = render(<ChessPage />);

      await new Promise(resolve => setTimeout(resolve, 100));

      const results = await axe(container, {
        rules: {
          'label': { enabled: true },
          'button-name': { enabled: true },
          'input-button-name': { enabled: true },
        },
      });

      expect(results).toHaveNoViolations();
    });

    it('should have keyboard-accessible controls', async () => {
      const { container } = render(<ChessPage />);

      await new Promise(resolve => setTimeout(resolve, 100));

      const results = await axe(container, {
        rules: {
          'tabindex': { enabled: true },
          'heading-order': { enabled: false },
        },
      });

      expect(results).toHaveNoViolations();
    });
  });

  describe('Specific Accessibility Features', () => {
    it('should have proper ARIA attributes', async () => {
      const { container } = render(<ChessPage />);
      const results = await axe(container, {
        rules: {
          'aria-allowed-attr': { enabled: true },
          'aria-prohibited-attr': { enabled: true },
          'aria-valid-attr': { enabled: true },
          'aria-valid-attr-value': { enabled: true },
        },
      });

      expect(results).toHaveNoViolations();
    });

    it('should have accessible landmark regions', async () => {
      const { container } = render(<ChessPage />);
      const results = await axe(container, {
        rules: {
          'landmark-one-main': { enabled: true },
          'region': { enabled: true },
        },
      });

      expect(results).toHaveNoViolations();
    });

    it('should pass WCAG 2.1 Level A rules', async () => {
      const { container } = render(<ChessPage />);
      const results = await axe(container, {
        runOnly: {
          type: 'tag',
          values: ['wcag2a', 'wcag21a'],
        },
      });

      expect(results).toHaveNoViolations();
    });

    it('should pass WCAG 2.1 Level AA rules', async () => {
      const { container } = render(<ChessPage />);
      const results = await axe(container, {
        runOnly: {
          type: 'tag',
          values: ['wcag2aa', 'wcag21aa'],
        },
      });

      expect(results).toHaveNoViolations();
    });
  });
});
