/**
 * KbEmptyState.test.tsx
 * Issue #1482 Task 3 — KbEmptyState component tests
 *
 * Tests for the discriminated KbEmptyState component supporting:
 * - kind='no-query': landing state with CTA "Start searching"
 * - kind='no-results': post-search state with "No matches for query"
 *
 * A11y: role="status" (no-results), role="region" + aria-label (no-query)
 * jest-axe: no violations on either variant ± CTA
 */

import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it, vi } from 'vitest';

import type { KbEmptyStateProps } from '../KbEmptyState';
import { KbEmptyState } from '../KbEmptyState';

expect.extend(toHaveNoViolations);

describe('KbEmptyState', () => {
  describe('no-query variant (landing)', () => {
    it('renders title + description for kind="no-query"', () => {
      const labels = {
        title: 'Start searching across your library',
        description: 'Explore documents from all your games in one place.',
      };

      render(<KbEmptyState kind="no-query" labels={labels} />);

      expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent(labels.title);
      expect(screen.getByText(labels.description)).toBeInTheDocument();
    });

    it('renders CTA button when labels.cta + onCtaClick provided', () => {
      const handleCtaClick = vi.fn();
      const labels = {
        title: 'Start searching',
        description: 'Explore your KB',
        cta: 'Get Started',
      };

      render(<KbEmptyState kind="no-query" labels={labels} onCtaClick={handleCtaClick} />);

      const button = screen.getByRole('button', { name: /Get Started/i });
      expect(button).toBeInTheDocument();

      button.click();
      expect(handleCtaClick).toHaveBeenCalledTimes(1);
    });

    it('does NOT render CTA when labels.cta is missing', () => {
      const handleCtaClick = vi.fn();
      const labels = {
        title: 'Start searching',
        description: 'Explore',
        // cta intentionally omitted
      };

      render(<KbEmptyState kind="no-query" labels={labels} onCtaClick={handleCtaClick} />);

      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });

    it('does NOT render CTA when onCtaClick is missing', () => {
      const labels = {
        title: 'Start searching',
        description: 'Explore',
        cta: 'Get Started',
      };

      render(<KbEmptyState kind="no-query" labels={labels} />);

      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });

    it('renders with aria-role="region" + aria-label', () => {
      const labels = {
        title: 'Start searching',
        description: 'Explore',
      };

      const { container } = render(<KbEmptyState kind="no-query" labels={labels} />);

      const region = container.querySelector('[role="region"]');
      expect(region).toBeInTheDocument();
      expect(region).toHaveAttribute('aria-label');
    });
  });

  describe('no-results variant (search results empty)', () => {
    it('renders title + description for kind="no-results"', () => {
      const labels = {
        title: 'No matches found',
        description: 'Try different keywords or filters.',
      };

      render(<KbEmptyState kind="no-results" labels={labels} />);

      expect(screen.getByRole('heading', { level: 2 })).toHaveTextContent(labels.title);
      expect(screen.getByText(labels.description)).toBeInTheDocument();
    });

    it('renders aria-role="status" for kind="no-results"', () => {
      const labels = {
        title: 'No results',
        description: 'Try again',
      };

      const { container } = render(<KbEmptyState kind="no-results" labels={labels} />);

      const status = container.querySelector('[role="status"]');
      expect(status).toBeInTheDocument();
    });

    it('renders CTA button when labels.cta + onCtaClick provided (variant no-results)', () => {
      const handleCtaClick = vi.fn();
      const labels = {
        title: 'No results',
        description: 'Try again',
        cta: 'Clear Filters',
      };

      render(<KbEmptyState kind="no-results" labels={labels} onCtaClick={handleCtaClick} />);

      const button = screen.getByRole('button', { name: /Clear Filters/i });
      button.click();
      expect(handleCtaClick).toHaveBeenCalledTimes(1);
    });

    it('does NOT render CTA when labels.cta missing (variant no-results)', () => {
      const labels = {
        title: 'No results',
        description: 'Try again',
      };

      render(<KbEmptyState kind="no-results" labels={labels} />);

      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    });
  });

  describe('icon rendering', () => {
    it('renders custom icon when provided', () => {
      const CustomIcon = () => <span data-testid="custom-icon">🎯</span>;
      const labels = {
        title: 'No data',
        description: 'Custom icon test',
      };

      render(<KbEmptyState kind="no-query" labels={labels} icon={<CustomIcon />} />);

      expect(screen.getByTestId('custom-icon')).toBeInTheDocument();
    });

    it('renders without icon when icon prop is undefined', () => {
      const labels = {
        title: 'No data',
        description: 'No icon test',
      };

      const { container } = render(<KbEmptyState kind="no-query" labels={labels} />);

      // Should not render a default icon element
      expect(container.querySelector('[aria-hidden="true"]')).not.toBeInTheDocument();
    });
  });

  describe('a11y: jest-axe', () => {
    it('no violations on kind="no-query" without CTA', async () => {
      const labels = {
        title: 'Start searching',
        description: 'Explore your KB',
      };

      const { container } = render(<KbEmptyState kind="no-query" labels={labels} />);

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('no violations on kind="no-query" with CTA', async () => {
      const labels = {
        title: 'Start searching',
        description: 'Explore',
        cta: 'Get Started',
      };

      const { container } = render(
        <KbEmptyState kind="no-query" labels={labels} onCtaClick={() => {}} />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('no violations on kind="no-results" without CTA', async () => {
      const labels = {
        title: 'No matches',
        description: 'Try different keywords',
      };

      const { container } = render(<KbEmptyState kind="no-results" labels={labels} />);

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('no violations on kind="no-results" with CTA', async () => {
      const labels = {
        title: 'No matches',
        description: 'Try different keywords',
        cta: 'Clear Search',
      };

      const { container } = render(
        <KbEmptyState kind="no-results" labels={labels} onCtaClick={() => {}} />
      );

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  });

  describe('className prop', () => {
    it('applies custom className to root element', () => {
      const labels = {
        title: 'Title',
        description: 'Description',
      };

      const { container } = render(
        <KbEmptyState kind="no-query" labels={labels} className="custom-class" />
      );

      // Assuming root element has data-testid or similar
      // We'll check by finding the region/status role
      const root = container.querySelector('[role="region"]');
      expect(root?.className).toMatch(/custom-class/);
    });
  });
});
