/**
 * Unified ActivityFeed Component Tests - Issue #2803
 *
 * Tests for consolidated ActivityFeed component with configurable:
 * - Icon modes (severity vs category)
 * - Animation (framer-motion)
 * - i18n (en vs it)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { ActivityFeed } from '../ActivityFeed';
import type { ActivityEvent } from '../ActivityFeed';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

const mockEvents: ActivityEvent[] = [
  {
    id: '1',
    eventType: 'UserRegistered',
    description: 'New user registered: john.doe@example.com',
    userId: 'user-123',
    userEmail: 'john.doe@example.com',
    timestamp: '2025-12-08T14:30:00Z',
    severity: 'Info',
  },
  {
    id: '2',
    eventType: 'PdfUploaded',
    description: 'PDF uploaded: Catan-Rules.pdf',
    userId: 'user-456',
    userEmail: 'alice@example.com',
    timestamp: '2025-12-08T14:25:00Z',
    severity: 'Info',
  },
  {
    id: '3',
    eventType: 'ErrorOccurred',
    description: 'AI Request failed: Rate limit exceeded',
    timestamp: '2025-12-08T14:20:00Z',
    severity: 'Error',
  },
];

describe('ActivityFeed', () => {
  // Mock Date.now for consistent relative timestamps
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2025-12-08T15:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders title "Recent Activity"', () => {
    render(<ActivityFeed events={mockEvents} />);

    expect(screen.getByText('Recent Activity')).toBeInTheDocument();
  });

  it('renders all events', () => {
    render(<ActivityFeed events={mockEvents} />);

    expect(screen.getByText(/New user registered/)).toBeInTheDocument();
    expect(screen.getByText(/PDF uploaded/)).toBeInTheDocument();
    expect(screen.getByText(/AI Request failed/)).toBeInTheDocument();
  });

  it('displays event timestamps in Italian format', () => {
    render(<ActivityFeed events={mockEvents} />);

    const timestamps = screen.getAllByRole('time');
    expect(timestamps.length).toBe(mockEvents.length);
  });

  it('shows user email when available', () => {
    render(<ActivityFeed events={mockEvents} />);

    expect(screen.getByText('john.doe@example.com')).toBeInTheDocument();
    expect(screen.getByText('alice@example.com')).toBeInTheDocument();
  });

  it('handles empty events array', () => {
    render(<ActivityFeed events={[]} />);

    expect(screen.getByText('No recent activity')).toBeInTheDocument();
  });

  it('limits events to maxEvents prop', () => {
    const manyEvents: ActivityEvent[] = Array.from({ length: 20 }, (_, i) => ({
      id: `event-${i}`,
      eventType: 'SystemEvent',
      description: `Event ${i + 1}`,
      timestamp: new Date().toISOString(),
      severity: 'Info',
    }));

    render(<ActivityFeed events={manyEvents} maxEvents={5} />);

    const list = screen.getByRole('list');
    const items = within(list).getAllByRole('listitem');
    expect(items.length).toBe(5);
  });

  it('applies severity styling - Info (blue)', () => {
    render(<ActivityFeed events={[mockEvents[0]]} />);
    const { container } = render(<ActivityFeed events={[mockEvents[0]]} />);
    const icon = container.querySelector('[class*="bg-blue-50"]');
    expect(icon).toBeInTheDocument();
  });

  it('applies severity styling - Error (red)', () => {
    const errorEvent: ActivityEvent = {
      id: '1',
      eventType: 'ErrorOccurred',
      description: 'Error occurred',
      timestamp: new Date().toISOString(),
      severity: 'Error',
    };

    const { container } = render(<ActivityFeed events={[errorEvent]} />);
    const icon = container.querySelector('[class*="bg-red-50"]');
    expect(icon).toBeInTheDocument();
  });

  it('applies severity styling - Warning (yellow)', () => {
    const warningEvent: ActivityEvent = {
      id: '1',
      eventType: 'AlertCreated',
      description: 'Warning alert',
      timestamp: new Date().toISOString(),
      severity: 'Warning',
    };

    const { container } = render(<ActivityFeed events={[warningEvent]} />);
    const icon = container.querySelector('[class*="bg-yellow-50"]');
    expect(icon).toBeInTheDocument();
  });

  it('renders correct icon for UserRegistered event', () => {
    render(<ActivityFeed events={[mockEvents[0]]} />);
    // UserPlusIcon should be rendered
    expect(screen.getByLabelText('Info event')).toBeInTheDocument();
  });

  it('renders correct icon for PdfUploaded event', () => {
    render(<ActivityFeed events={[mockEvents[1]]} />);
    // FileUpIcon should be rendered
    expect(screen.getByLabelText('Info event')).toBeInTheDocument();
  });

  it('renders correct icon for ErrorOccurred event', () => {
    render(<ActivityFeed events={[mockEvents[2]]} />);
    // XCircleIcon should be rendered
    expect(screen.getByLabelText('Error event')).toBeInTheDocument();
  });

  it('applies hover effect class for severity mode', () => {
    const { container } = render(<ActivityFeed events={mockEvents} iconMode="severity" />);
    const listItems = container.querySelectorAll('li');
    expect(listItems[0].className).toContain('hover:bg-meeple-light-orange');
  });

  it('applies custom className to card', () => {
    const { container } = render(<ActivityFeed events={mockEvents} className="custom-feed" />);
    const card = container.querySelector('.custom-feed');
    expect(card).toBeInTheDocument();
  });

  it('has accessible list structure', () => {
    render(<ActivityFeed events={mockEvents} />);

    const list = screen.getByRole('list');
    expect(list).toBeInTheDocument();

    const items = within(list).getAllByRole('listitem');
    expect(items.length).toBe(mockEvents.length);
  });

  it('renders timestamp with dateTime attribute for accessibility', () => {
    render(<ActivityFeed events={[mockEvents[0]]} />);

    const timeElement = screen.getByRole('time');
    expect(timeElement).toHaveAttribute('dateTime', mockEvents[0].timestamp);
  });

  it('truncates long email addresses with ellipsis', () => {
    const longEmailEvent: ActivityEvent = {
      id: '1',
      eventType: 'UserRegistered',
      description: 'User registered',
      userId: 'user-999',
      userEmail: 'very.long.email.address.that.should.truncate@example.com',
      timestamp: new Date().toISOString(),
      severity: 'Info',
    };

    const { container } = render(<ActivityFeed events={[longEmailEvent]} />);
    const email = container.querySelector('[class*="truncate"]');
    expect(email).toBeInTheDocument();
  });

  describe('View All link', () => {
    it('renders View All link by default when events exist', () => {
      render(<ActivityFeed events={mockEvents} />);

      const viewAllLink = screen.getByRole('link', { name: /view all/i });
      expect(viewAllLink).toBeInTheDocument();
      expect(viewAllLink).toHaveAttribute('href', '/admin/activity');
    });

    it('renders View All link with custom href', () => {
      render(<ActivityFeed events={mockEvents} viewAllHref="/custom/activity" />);

      const viewAllLink = screen.getByRole('link', { name: /view all/i });
      expect(viewAllLink).toHaveAttribute('href', '/custom/activity');
    });

    it('hides View All link when showViewAll is false', () => {
      render(<ActivityFeed events={mockEvents} showViewAll={false} />);

      expect(screen.queryByRole('link', { name: /view all/i })).not.toBeInTheDocument();
    });

    it('does not render View All link for empty events with showViewAll false', () => {
      render(<ActivityFeed events={[]} showViewAll={false} />);

      expect(screen.queryByRole('link', { name: /view all/i })).not.toBeInTheDocument();
    });
  });

  describe('Relative timestamps', () => {
    it('displays relative timestamps in English by default', () => {
      render(<ActivityFeed events={mockEvents} />);

      // With mocked time at 15:00 and events at 14:30, 14:25, 14:20
      // Should show relative times like "30 minutes ago"
      const timestamps = screen.getAllByRole('time');
      expect(timestamps.length).toBe(mockEvents.length);

      // Check that at least one contains English relative text
      const timestampTexts = timestamps.map(t => t.textContent);
      expect(timestampTexts.some(text => text?.includes('ago') || text?.includes('minute'))).toBe(
        true
      );
    });

    it('displays relative timestamps in Italian when locale="it"', () => {
      render(<ActivityFeed events={mockEvents} locale="it" />);

      const timestamps = screen.getAllByRole('time');
      expect(timestamps.length).toBe(mockEvents.length);

      // Check that at least one contains Italian relative text
      const timestampTexts = timestamps.map(t => t.textContent);
      expect(timestampTexts.some(text => text?.includes('fa') || text?.includes('minut'))).toBe(
        true
      );
    });

    it('shows full date in title attribute for accessibility', () => {
      render(<ActivityFeed events={[mockEvents[0]]} />);

      const timeElement = screen.getByRole('time');
      expect(timeElement).toHaveAttribute('title');
    });
  });

  describe('Scrollable container', () => {
    it('has scrollable container for events', () => {
      const { container } = render(<ActivityFeed events={mockEvents} />);

      const scrollContainer = container.querySelector('[class*="overflow-y-auto"]');
      expect(scrollContainer).toBeInTheDocument();
    });

    it('has max-height constraint on scrollable container', () => {
      const { container } = render(<ActivityFeed events={mockEvents} />);

      const scrollContainer = container.querySelector('[class*="max-h-"]');
      expect(scrollContainer).toBeInTheDocument();
    });

    it('has accessible region role for activity feed (English)', () => {
      render(<ActivityFeed events={mockEvents} locale="en" />);

      const region = screen.getByRole('region', { name: /activity feed/i });
      expect(region).toBeInTheDocument();
    });

    it('has accessible region role for timeline (Italian)', () => {
      render(<ActivityFeed events={mockEvents} locale="it" />);

      const region = screen.getByRole('region', { name: /timeline attività/i });
      expect(region).toBeInTheDocument();
    });
  });

  describe('Empty state', () => {
    it('shows enhanced empty state with icon (English)', () => {
      const { container } = render(<ActivityFeed events={[]} locale="en" />);

      expect(screen.getByText('No recent activity')).toBeInTheDocument();
      // Check for empty state icon
      const emptyIcon = container.querySelector('svg');
      expect(emptyIcon).toBeInTheDocument();
    });

    it('shows empty state in Italian when locale="it"', () => {
      render(<ActivityFeed events={[]} locale="it" />);

      expect(screen.getByText('Nessuna attività recente')).toBeInTheDocument();
    });
  });

  describe('Icon modes - Issue #2803', () => {
    it('uses severity icons by default', () => {
      render(<ActivityFeed events={[mockEvents[0]]} />);

      // Should have severity-based aria-label
      expect(screen.getByLabelText('Info event')).toBeInTheDocument();
    });

    it('uses category icons when iconMode="category"', () => {
      render(<ActivityFeed events={[mockEvents[0]]} iconMode="category" />);

      // UserRegistered maps to 'user' category
      expect(screen.getByLabelText('user activity')).toBeInTheDocument();
    });

    it('applies category hover effect for category mode', () => {
      const { container } = render(<ActivityFeed events={mockEvents} iconMode="category" />);
      const listItems = container.querySelectorAll('li');
      expect(listItems[0].className).toContain('hover:bg-muted');
    });
  });

  describe('Warning/Error highlighting - Issue #2849', () => {
    it('highlights warning events with yellow background', () => {
      const warningEvent: ActivityEvent = {
        id: '1',
        eventType: 'AlertCreated',
        description: 'Warning alert',
        timestamp: new Date().toISOString(),
        severity: 'Warning',
      };

      const { container } = render(<ActivityFeed events={[warningEvent]} iconMode="severity" />);
      const listItem = container.querySelector('li');
      expect(listItem?.className).toContain('bg-yellow-50');
      expect(listItem?.className).toContain('border-l-yellow-500');
    });

    it('highlights error events with red background', () => {
      const errorEvent: ActivityEvent = {
        id: '1',
        eventType: 'ErrorOccurred',
        description: 'Error occurred',
        timestamp: new Date().toISOString(),
        severity: 'Error',
      };

      const { container } = render(<ActivityFeed events={[errorEvent]} iconMode="severity" />);
      const listItem = container.querySelector('li');
      expect(listItem?.className).toContain('bg-red-50');
      expect(listItem?.className).toContain('border-l-red-500');
    });

    it('does not highlight info events', () => {
      const { container } = render(<ActivityFeed events={[mockEvents[0]]} iconMode="severity" />);
      const listItem = container.querySelector('li');
      expect(listItem?.className).toContain('border-l-transparent');
    });

    it('highlighting only applies in severity mode, not category mode', () => {
      const errorEvent: ActivityEvent = {
        id: '1',
        eventType: 'ErrorOccurred',
        description: 'Error occurred',
        timestamp: new Date().toISOString(),
        severity: 'Error',
      };

      const { container } = render(<ActivityFeed events={[errorEvent]} iconMode="category" />);
      const listItem = container.querySelector('li');
      // Category mode does not add severity highlighting
      expect(listItem?.className).not.toContain('bg-red-50');
    });
  });

  describe('i18n - Issue #2803', () => {
    it('renders English UI by default', () => {
      render(<ActivityFeed events={mockEvents} />);

      expect(screen.getByText('Recent Activity')).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /view all/i })).toBeInTheDocument();
    });

    it('renders Italian UI when locale="it"', () => {
      render(<ActivityFeed events={mockEvents} locale="it" />);

      expect(screen.getByText('Attività Recenti')).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /vedi tutte/i })).toBeInTheDocument();
    });
  });
});
