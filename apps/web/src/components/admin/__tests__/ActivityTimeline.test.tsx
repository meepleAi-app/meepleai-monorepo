/**
 * ActivityTimeline Component Tests - Issue #2787
 *
 * Test suite for the ActivityTimeline component covering:
 * - Category icon rendering and colors
 * - Empty state handling
 * - Timestamp formatting (Italian locale)
 * - Event truncation (maxEvents)
 * - View All link
 * - Accessibility
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import type { ActivityEvent } from '../ActivityTimeline';
import { ActivityTimeline } from '../ActivityTimeline';

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', () => ({
  motion: {
    li: ({ children, ...props }: React.PropsWithChildren<React.HTMLAttributes<HTMLLIElement>>) => (
      <li {...props}>{children}</li>
    ),
  },
}));

const mockEvents: ActivityEvent[] = [
  {
    id: '1',
    eventType: 'UserRegistered',
    description: 'Nuovo utente registrato',
    userEmail: 'user@example.com',
    timestamp: new Date(Date.now() - 5 * 60 * 1000).toISOString(), // 5 min ago
    severity: 'Info',
  },
  {
    id: '2',
    eventType: 'GameAdded',
    description: 'Nuovo gioco aggiunto al catalogo',
    userEmail: 'admin@example.com',
    timestamp: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(), // 2h ago
    severity: 'Info',
  },
  {
    id: '3',
    eventType: 'ErrorOccurred',
    description: 'Errore elaborazione AI',
    timestamp: new Date(Date.now() - 24 * 60 * 60 * 1000).toISOString(), // 1 day ago
    severity: 'Error',
  },
  {
    id: '4',
    eventType: 'ConfigurationChanged',
    description: 'Configurazione sistema aggiornata',
    userEmail: 'sysadmin@example.com',
    timestamp: new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString(), // 7 days ago
    severity: 'Warning',
  },
];

describe('ActivityTimeline', () => {
  describe('Rendering', () => {
    it('renders header with Italian title', () => {
      render(<ActivityTimeline events={mockEvents} />);
      expect(screen.getByText('Attività Recenti')).toBeInTheDocument();
    });

    it('renders all events when under maxEvents limit', () => {
      render(<ActivityTimeline events={mockEvents} maxEvents={10} />);
      expect(screen.getByText('Nuovo utente registrato')).toBeInTheDocument();
      expect(screen.getByText('Nuovo gioco aggiunto al catalogo')).toBeInTheDocument();
      expect(screen.getByText('Errore elaborazione AI')).toBeInTheDocument();
      expect(screen.getByText('Configurazione sistema aggiornata')).toBeInTheDocument();
    });

    it('truncates events when exceeding maxEvents', () => {
      render(<ActivityTimeline events={mockEvents} maxEvents={2} />);
      expect(screen.getByText('Nuovo utente registrato')).toBeInTheDocument();
      expect(screen.getByText('Nuovo gioco aggiunto al catalogo')).toBeInTheDocument();
      expect(screen.queryByText('Errore elaborazione AI')).not.toBeInTheDocument();
    });

    it('renders empty state when no events', () => {
      render(<ActivityTimeline events={[]} />);
      expect(screen.getByText('Nessuna attività recente')).toBeInTheDocument();
    });
  });

  describe('Category Icons', () => {
    it('renders user icon for user events (UserRegistered)', () => {
      render(<ActivityTimeline events={[mockEvents[0]]} />);
      const iconContainer = screen.getByLabelText('user activity');
      expect(iconContainer).toHaveClass('text-blue-600');
    });

    it('renders game icon for game events (GameAdded)', () => {
      render(<ActivityTimeline events={[mockEvents[1]]} />);
      const iconContainer = screen.getByLabelText('game activity');
      expect(iconContainer).toHaveClass('text-green-600');
    });

    it('renders ai icon for ai events (ErrorOccurred)', () => {
      render(<ActivityTimeline events={[mockEvents[2]]} />);
      const iconContainer = screen.getByLabelText('ai activity');
      expect(iconContainer).toHaveClass('text-purple-600');
    });

    it('renders system icon for system events (ConfigurationChanged)', () => {
      render(<ActivityTimeline events={[mockEvents[3]]} />);
      const iconContainer = screen.getByLabelText('system activity');
      expect(iconContainer).toHaveClass('text-stone-600');
    });
  });

  describe('Timestamp Formatting', () => {
    it('formats timestamps in Italian relative format', () => {
      render(<ActivityTimeline events={mockEvents} />);
      // date-fns locale 'it' produces strings like "5 minuti fa", "2 ore fa", etc.
      const timeElements = screen.getAllByRole('time');
      expect(timeElements).toHaveLength(4);
      // Check that timestamps are formatted (contain "fa" for Italian "ago")
      timeElements.forEach(el => {
        expect(el.textContent).toMatch(/fa$/i);
      });
    });

    it('includes full datetime in title attribute', () => {
      render(<ActivityTimeline events={[mockEvents[0]]} />);
      const timeElement = screen.getByRole('time');
      expect(timeElement).toHaveAttribute('title');
      expect(timeElement.getAttribute('title')).toMatch(/\d{2}\/\d{2}\/\d{4}/); // Italian date format
    });
  });

  describe('View All Link', () => {
    it('renders view all link when events exist', () => {
      render(<ActivityTimeline events={mockEvents} showViewAll />);
      const link = screen.getByRole('link', { name: /vedi tutte le attività/i });
      expect(link).toHaveAttribute('href', '/admin/audit');
    });

    it('uses custom viewAllHref when provided', () => {
      render(<ActivityTimeline events={mockEvents} viewAllHref="/custom/path" />);
      const link = screen.getByRole('link', { name: /vedi tutte le attività/i });
      expect(link).toHaveAttribute('href', '/custom/path');
    });

    it('hides view all link when showViewAll is false', () => {
      render(<ActivityTimeline events={mockEvents} showViewAll={false} />);
      expect(screen.queryByRole('link', { name: /vedi tutte le attività/i })).not.toBeInTheDocument();
    });

    it('hides view all link when no events', () => {
      render(<ActivityTimeline events={[]} showViewAll />);
      expect(screen.queryByRole('link', { name: /vedi tutte le attività/i })).not.toBeInTheDocument();
    });
  });

  describe('User Email Display', () => {
    it('displays user email when available', () => {
      render(<ActivityTimeline events={[mockEvents[0]]} />);
      expect(screen.getByText('user@example.com')).toBeInTheDocument();
    });

    it('hides email when not available', () => {
      const eventWithoutEmail = { ...mockEvents[2], userEmail: null };
      render(<ActivityTimeline events={[eventWithoutEmail]} />);
      expect(screen.queryByText(/@example\.com/)).not.toBeInTheDocument();
    });

    it('includes email in title attribute for truncation', () => {
      render(<ActivityTimeline events={[mockEvents[0]]} />);
      const emailSpan = screen.getByTitle('user@example.com');
      expect(emailSpan).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has proper ARIA labels for timeline region', () => {
      render(<ActivityTimeline events={mockEvents} />);
      expect(screen.getByRole('region', { name: /timeline attività/i })).toBeInTheDocument();
    });

    it('uses semantic list elements', () => {
      render(<ActivityTimeline events={mockEvents} />);
      expect(screen.getByRole('list')).toBeInTheDocument();
    });

    it('provides ARIA label for view all link', () => {
      render(<ActivityTimeline events={mockEvents} />);
      const link = screen.getByRole('link', { name: /vedi tutte le attività/i });
      expect(link).toHaveAttribute('aria-label', 'Vedi tutte le attività');
    });

    it('marks icons as aria-hidden', () => {
      const { container } = render(<ActivityTimeline events={[mockEvents[0]]} />);
      const icons = container.querySelectorAll('[aria-hidden="true"]');
      expect(icons.length).toBeGreaterThan(0);
    });
  });

  describe('Styling', () => {
    it('applies custom className to container', () => {
      const { container } = render(<ActivityTimeline events={mockEvents} className="custom-class" />);
      expect(container.firstChild).toHaveClass('custom-class');
    });

    it('applies hover effect classes to list items', () => {
      const { container } = render(<ActivityTimeline events={[mockEvents[0]]} />);
      const listItem = container.querySelector('li');
      // ActivityFeed uses hover:bg-muted/50 for category icon mode
      expect(listItem).toHaveClass('hover:bg-muted/50');
    });
  });
});
