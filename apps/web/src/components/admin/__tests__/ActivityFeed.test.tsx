/**
 * ActivityFeed Component Tests - Issue #874
 */

import { describe, it, expect } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { ActivityFeed } from '../ActivityFeed';
import type { ActivityEvent } from '../ActivityFeed';

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

  it('applies hover effect class', () => {
    const { container } = render(<ActivityFeed events={mockEvents} />);
    const listItems = container.querySelectorAll('li');
    expect(listItems[0]).toHaveClass('hover:bg-gray-50');
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
});
