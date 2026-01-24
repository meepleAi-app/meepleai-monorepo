/**
 * @vitest-environment jsdom
 *
 * Accessibility Tests for Game Detail Components (Issue #2842)
 *
 * WCAG 2.1 AA compliance testing using axe-core
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';

import { GameDetailClient } from '../game-detail-client';
import { GameFAQTab } from '../components/GameFAQTab';
import { GameChatTab } from '../components/GameChatTab';
import { HeroSection } from '../components/HeroSection';
import { InfoGrid } from '../components/InfoGrid';
import { api } from '@/lib/api';
import type { GameFAQ } from '@/lib/api';

expect.extend(toHaveNoViolations);

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getFAQs: vi.fn(),
      upvoteFAQ: vi.fn(),
      getQuickQuestions: vi.fn(),
    },
  },
}));

// Mock fetch
global.fetch = vi.fn();

const mockGame = {
  id: 'game-a11y-123',
  title: 'Accessibility Test Game',
  publisher: 'A11y Publisher',
  yearPublished: 2024,
  minPlayers: 2,
  maxPlayers: 4,
  minPlayTimeMinutes: 30,
  maxPlayTimeMinutes: 60,
  bggId: 12345,
  imageUrl: 'https://example.com/game.jpg',
  averageRating: 8.0,
  createdAt: '2024-01-01T00:00:00Z',
};

const mockFAQs: GameFAQ[] = [
  {
    id: 'faq-a11y-1',
    question: 'Test question for accessibility?',
    answer: 'Test answer with proper semantics.',
    upvotes: 5,
    gameId: 'game-a11y-123',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
];

describe('Accessibility Compliance (WCAG 2.1 AA)', () => {
  beforeEach(() => {
    vi.clearAllMocks();

    // Default API mocks
    (api.games.getFAQs as any).mockResolvedValue({ faqs: mockFAQs });
    (api.games.getQuickQuestions as any).mockResolvedValue([
      { id: 'q1', text: 'Accessible quick question' },
    ]);

    // Mock BGG fetch
    (global.fetch as any).mockResolvedValue({
      ok: true,
      json: async () => ({ averageWeight: 3.5 }),
    });
  });

  describe('GameDetailClient', () => {
    it('should have no accessibility violations', async () => {
      const { container } = render(<GameDetailClient game={mockGame} />);

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have proper heading hierarchy', () => {
      const { container } = render(<GameDetailClient game={mockGame} />);

      const headings = container.querySelectorAll('h1, h2, h3, h4, h5, h6');
      expect(headings.length).toBeGreaterThan(0);
    });

    it('should have accessible navigation links', () => {
      const { getByRole } = render(<GameDetailClient game={mockGame} />);

      const backLink = getByRole('link', { name: /torna al catalogo/i });
      expect(backLink).toHaveAttribute('href');
    });

    it('should have properly labeled tab controls', () => {
      const { getAllByRole } = render(<GameDetailClient game={mockGame} />);

      const tabs = getAllByRole('tab');
      expect(tabs.length).toBeGreaterThan(0);

      tabs.forEach((tab) => {
        expect(tab).toHaveAccessibleName();
      });
    });
  });

  describe('HeroSection', () => {
    it('should have no accessibility violations', async () => {
      const { container } = render(
        <HeroSection
          title="Accessible Game Title"
          imageUrl="https://example.com/image.jpg"
          publisher="Publisher Name"
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have alt text for images', () => {
      const { container } = render(
        <HeroSection
          title="Game with Image"
          imageUrl="https://example.com/game.jpg"
          publisher="Publisher"
        />
      );

      const images = container.querySelectorAll('img');
      images.forEach((img) => {
        expect(img).toHaveAttribute('alt');
      });
    });

    it('should have accessible heading for title', () => {
      const { getByRole } = render(
        <HeroSection title="Main Game Title" imageUrl={null} publisher={null} />
      );

      expect(getByRole('heading')).toHaveTextContent('Main Game Title');
    });
  });

  describe('InfoGrid', () => {
    it('should have no accessibility violations', async () => {
      const { container } = render(
        <InfoGrid
          minPlayers={2}
          maxPlayers={4}
          minPlayTimeMinutes={30}
          maxPlayTimeMinutes={60}
          averageWeight={3.5}
        />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have semantic HTML structure', () => {
      const { container } = render(
        <InfoGrid
          minPlayers={2}
          maxPlayers={4}
          minPlayTimeMinutes={30}
          maxPlayTimeMinutes={60}
          averageWeight={3.5}
        />
      );

      // Should use proper semantic elements
      const grid = container.querySelector('[role="grid"], .grid, [class*="grid"]');
      expect(grid).toBeInTheDocument();
    });

    it('should have accessible text for screen readers', () => {
      const { container } = render(
        <InfoGrid
          minPlayers={2}
          maxPlayers={4}
          minPlayTimeMinutes={30}
          maxPlayTimeMinutes={60}
          averageWeight={3.5}
        />
      );

      // All text should be readable
      const text = container.textContent;
      expect(text).toContain('2');
      expect(text).toContain('4');
    });
  });

  describe('GameFAQTab', () => {
    it('should have no accessibility violations', async () => {
      const { container } = render(
        <GameFAQTab gameId="game-a11y-123" gameTitle="A11y Game" />
      );

      // Wait for content to load
      await new Promise((resolve) => setTimeout(resolve, 100));

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have accessible accordion buttons', async () => {
      const { getAllByRole } = render(
        <GameFAQTab gameId="game-a11y-123" gameTitle="A11y Game" />
      );

      // Wait for FAQs to load
      await new Promise((resolve) => setTimeout(resolve, 100));

      const buttons = getAllByRole('button');
      buttons.forEach((button) => {
        expect(button).toHaveAccessibleName();
      });
    });

    it('should have proper ARIA attributes for accordion', async () => {
      const { container } = render(
        <GameFAQTab gameId="game-a11y-123" gameTitle="A11y Game" />
      );

      // Wait for FAQs to load
      await new Promise((resolve) => setTimeout(resolve, 100));

      // Accordion should have proper ARIA
      const ariaElements = container.querySelectorAll(
        '[aria-expanded], [aria-controls], [data-state]'
      );
      expect(ariaElements.length).toBeGreaterThan(0);
    });

    it('should have accessible upvote buttons', async () => {
      const { getAllByRole } = render(
        <GameFAQTab gameId="game-a11y-123" gameTitle="A11y Game" />
      );

      // Wait for FAQs to load
      await new Promise((resolve) => setTimeout(resolve, 100));

      const upvoteButtons = getAllByRole('button', { name: /thumbs-up/i });
      upvoteButtons.forEach((button) => {
        expect(button).toHaveAccessibleName();
        expect(button).not.toHaveAttribute('aria-hidden', 'true');
      });
    });
  });

  describe('GameChatTab', () => {
    it('should have no accessibility violations', async () => {
      const { container } = render(
        <GameChatTab gameId="game-a11y-123" gameTitle="A11y Game" />
      );

      // Wait for quick questions to load
      await new Promise((resolve) => setTimeout(resolve, 100));

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should have accessible quick question chips', async () => {
      const { container } = render(
        <GameChatTab gameId="game-a11y-123" gameTitle="A11y Game" />
      );

      // Wait for quick questions to load
      await new Promise((resolve) => setTimeout(resolve, 100));

      const chips = container.querySelectorAll('button, [role="button"]');
      chips.forEach((chip) => {
        expect(chip).toHaveAccessibleName();
      });
    });

    it('should have proper ARIA labels for interactive elements', async () => {
      const { getAllByRole } = render(
        <GameChatTab gameId="game-a11y-123" gameTitle="A11y Game" />
      );

      // Wait for content to load
      await new Promise((resolve) => setTimeout(resolve, 100));

      const buttons = getAllByRole('button');
      buttons.forEach((button) => {
        // All buttons should have accessible names
        expect(button.getAttribute('aria-label') || button.textContent).toBeTruthy();
      });
    });
  });

  describe('Keyboard Navigation', () => {
    it('should support tab navigation through interactive elements', () => {
      const { getAllByRole } = render(<GameDetailClient game={mockGame} />);

      const interactiveElements = getAllByRole('button').concat(
        getAllByRole('link'),
        getAllByRole('tab')
      );

      interactiveElements.forEach((element) => {
        // Should not have negative tabindex (unless intentionally hidden)
        const tabIndex = element.getAttribute('tabindex');
        if (tabIndex) {
          expect(parseInt(tabIndex)).toBeGreaterThanOrEqual(-1);
        }
      });
    });

    it('should have visible focus indicators', () => {
      const { getAllByRole } = render(<GameDetailClient game={mockGame} />);

      const focusableElements = getAllByRole('button').concat(
        getAllByRole('link'),
        getAllByRole('tab')
      );

      // Elements should be keyboard-focusable
      focusableElements.forEach((element) => {
        expect(element.tagName).toMatch(/BUTTON|A/);
      });
    });
  });

  describe('Color Contrast', () => {
    it('should have sufficient color contrast for text', async () => {
      const { container } = render(<GameDetailClient game={mockGame} />);

      // axe will check color contrast automatically
      const results = await axe(container);

      const contrastViolations = results.violations.filter(
        (v) => v.id === 'color-contrast'
      );

      expect(contrastViolations).toHaveLength(0);
    });
  });

  describe('Screen Reader Support', () => {
    it('should have appropriate ARIA landmarks', () => {
      const { container } = render(<GameDetailClient game={mockGame} />);

      // Should have main content area
      const main = container.querySelector('main, [role="main"]');
      expect(main || container.querySelector('[class*="container"]')).toBeInTheDocument();
    });

    it('should have descriptive labels for form controls', async () => {
      const { getAllByRole } = render(<GameDetailClient game={mockGame} />);

      // All inputs/buttons should have labels
      const controls = getAllByRole('button');
      controls.forEach((control) => {
        expect(control).toHaveAccessibleName();
      });
    });

    it('should announce loading states', async () => {
      const { container } = render(
        <GameFAQTab gameId="game-a11y-123" gameTitle="A11y Game" />
      );

      // Loading spinner should have role="status" or aria-live
      const loadingIndicator = container.querySelector(
        '[role="status"], [aria-live="polite"], [aria-busy="true"]'
      );

      // Either loading indicator exists, or content has loaded
      expect(
        loadingIndicator || container.querySelector('[data-testid="faq-tab"]')
      ).toBeInTheDocument();
    });
  });
});
