/**
 * PublicFooter Component Tests - Issue #2230
 *
 * Test coverage:
 * - Rendering base con 3 colonne
 * - Social links
 * - Copyright con anno dinamico
 * - Responsiveness
 * - Accessibility
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PublicFooter } from '../PublicFooter';

// Mock Next.js Link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

// Mock MeepleLogo to avoid styled-jsx issues in tests
vi.mock('@/components/ui/meeple/meeple-logo', () => ({
  MeepleLogo: () => <div data-testid="meeple-logo">MeepleAI</div>,
}));

describe('PublicFooter', () => {
  describe('Base Rendering', () => {
    it('renders footer element', () => {
      const { container } = render(<PublicFooter />);

      const footer = container.querySelector('footer');
      expect(footer).toBeInTheDocument();
    });

    it('renders MeepleAI logo', () => {
      render(<PublicFooter />);

      // Logo should be present (using testid from mock)
      const logo = screen.getByTestId('meeple-logo');
      expect(logo).toBeInTheDocument();
    });

    it('renders description text', () => {
      render(<PublicFooter />);

      const description = screen.getByText(/il tuo assistente ai per le regole/i);
      expect(description).toBeInTheDocument();
    });
  });

  describe('Quick Links Column', () => {
    it('renders "Link Rapidi" heading', () => {
      render(<PublicFooter />);

      const heading = screen.getByText('Link Rapidi');
      expect(heading).toBeInTheDocument();
    });

    it('renders all quick links', () => {
      render(<PublicFooter />);

      expect(screen.getByRole('link', { name: /giochi/i })).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /chat ai/i })).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /faq/i })).toBeInTheDocument();
    });

    it('quick links have correct hrefs', () => {
      render(<PublicFooter />);

      expect(screen.getByRole('link', { name: /giochi/i })).toHaveAttribute('href', '/games');
      expect(screen.getByRole('link', { name: /chat ai/i })).toHaveAttribute('href', '/chat/new');
      expect(screen.getByRole('link', { name: /dashboard/i })).toHaveAttribute('href', '/library');
      expect(screen.getByRole('link', { name: /faq/i })).toHaveAttribute('href', '/faq');
    });
  });

  describe('About Column', () => {
    it('renders "Chi Siamo" heading', () => {
      render(<PublicFooter />);

      const headings = screen.getAllByText('Chi Siamo');
      // Should have at least the heading
      expect(headings.length).toBeGreaterThan(0);
    });

    it('renders all about links', () => {
      render(<PublicFooter />);

      const chiSiamoLinks = screen.getAllByRole('link', { name: /chi siamo/i });
      const comeFunzionaLink = screen.getByRole('link', { name: /come funziona/i });
      const blogLink = screen.getByRole('link', { name: /blog/i });
      const contattiLink = screen.getByRole('link', { name: /contatti/i });

      expect(chiSiamoLinks.length).toBeGreaterThan(0);
      expect(comeFunzionaLink).toBeInTheDocument();
      expect(blogLink).toBeInTheDocument();
      expect(contattiLink).toBeInTheDocument();
    });

    it('about links have correct hrefs', () => {
      render(<PublicFooter />);

      const chiSiamoLinks = screen.getAllByRole('link', { name: /chi siamo/i });
      const comeFunzionaLink = screen.getByRole('link', { name: /come funziona/i });
      const blogLink = screen.getByRole('link', { name: /blog/i });
      const contattiLink = screen.getByRole('link', { name: /contatti/i });

      // At least one "Chi Siamo" link should have correct href
      const hasCorrectHref = chiSiamoLinks.some(link => link.getAttribute('href') === '/about');
      expect(hasCorrectHref).toBe(true);

      expect(comeFunzionaLink).toHaveAttribute('href', '/how-it-works');
      expect(blogLink).toHaveAttribute('href', '/blog');
      expect(contattiLink).toHaveAttribute('href', '/contact');
    });
  });

  describe('Legal Column', () => {
    it('renders "Legale" heading', () => {
      render(<PublicFooter />);

      const heading = screen.getByText('Legale');
      expect(heading).toBeInTheDocument();
    });

    it('renders all legal links', () => {
      render(<PublicFooter />);

      expect(screen.getByRole('link', { name: /privacy policy/i })).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /termini di servizio/i })).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /cookie policy/i })).toBeInTheDocument();
    });

    it('legal links have correct hrefs', () => {
      render(<PublicFooter />);

      expect(screen.getByRole('link', { name: /privacy policy/i })).toHaveAttribute(
        'href',
        '/privacy'
      );
      expect(screen.getByRole('link', { name: /termini di servizio/i })).toHaveAttribute(
        'href',
        '/terms'
      );
      expect(screen.getByRole('link', { name: /cookie policy/i })).toHaveAttribute(
        'href',
        '/cookies'
      );
    });
  });

  describe('Social Links', () => {
    it('renders all social links', () => {
      render(<PublicFooter />);

      expect(screen.getByLabelText('GitHub')).toBeInTheDocument();
      expect(screen.getByLabelText('Twitter')).toBeInTheDocument();
      expect(screen.getByLabelText('Discord')).toBeInTheDocument();
    });

    it('social links open in new tab', () => {
      render(<PublicFooter />);

      const githubLink = screen.getByLabelText('GitHub');
      expect(githubLink).toHaveAttribute('target', '_blank');
      expect(githubLink).toHaveAttribute('rel', 'noopener noreferrer');
    });

    it('social links have correct hrefs', () => {
      render(<PublicFooter />);

      expect(screen.getByLabelText('GitHub')).toHaveAttribute(
        'href',
        'https://github.com/meepleai'
      );
      expect(screen.getByLabelText('Twitter')).toHaveAttribute(
        'href',
        'https://twitter.com/meepleai'
      );
      expect(screen.getByLabelText('Discord')).toHaveAttribute(
        'href',
        'https://discord.gg/meepleai'
      );
    });
  });

  describe('Copyright', () => {
    it('renders current year in copyright', () => {
      render(<PublicFooter />);

      const currentYear = new Date().getFullYear();
      const copyright = screen.getByText(new RegExp(`© ${currentYear} MeepleAI`, 'i'));
      expect(copyright).toBeInTheDocument();
    });

    it('renders "Tutti i diritti riservati"', () => {
      render(<PublicFooter />);

      const rightsReserved = screen.getByText(/tutti i diritti riservati/i);
      expect(rightsReserved).toBeInTheDocument();
    });

    it('renders tagline', () => {
      render(<PublicFooter />);

      const tagline = screen.getByText(/realizzato con.*per gli appassionati/i);
      expect(tagline).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('all links have proper focus styles', () => {
      render(<PublicFooter />);

      const links = screen.getAllByRole('link');
      links.forEach(link => {
        expect(link).toHaveClass(/focus-visible:ring/);
      });
    });

    it('social links have proper aria-labels', () => {
      render(<PublicFooter />);

      expect(screen.getByLabelText('GitHub')).toBeInTheDocument();
      expect(screen.getByLabelText('Twitter')).toBeInTheDocument();
      expect(screen.getByLabelText('Discord')).toBeInTheDocument();
    });

    it('all links are keyboard accessible', () => {
      render(<PublicFooter />);

      const links = screen.getAllByRole('link');
      links.forEach(link => {
        expect(link.tagName).toBe('A');
        expect(link).toHaveAttribute('href');
      });
    });
  });

  describe('Custom Styling', () => {
    it('accepts custom className', () => {
      const { container } = render(<PublicFooter className="custom-class" />);

      const footer = container.querySelector('footer');
      expect(footer).toHaveClass('custom-class');
    });
  });

  describe('Responsive Layout', () => {
    it('uses grid layout for columns', () => {
      const { container } = render(<PublicFooter />);

      const grid = container.querySelector('.grid');
      expect(grid).toBeInTheDocument();
      expect(grid).toHaveClass('grid-cols-1');
      expect(grid).toHaveClass('md:grid-cols-3');
    });

    it('uses flex layout for copyright section', () => {
      const { container } = render(<PublicFooter />);

      const copyrightSection = container.querySelector('.flex');
      expect(copyrightSection).toBeInTheDocument();
    });
  });

  describe('Newsletter Prop', () => {
    it('renders without newsletter by default', () => {
      render(<PublicFooter />);

      // Newsletter section should not be present
      expect(screen.queryByText(/newsletter/i)).not.toBeInTheDocument();
    });

    it('accepts showNewsletter prop', () => {
      render(<PublicFooter showNewsletter />);

      // Even if true, newsletter is not implemented yet, so it should still not show
      expect(screen.queryByText(/newsletter/i)).not.toBeInTheDocument();
    });
  });
});
