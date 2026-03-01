/**
 * CardNavigationFooter - Component Tests
 *
 * @see Issue #4705 - Integration Testing
 * @see Issue #4689 - CardNavigationFooter Component
 */

import { render, screen } from '@testing-library/react';

import { CardNavigationFooter } from '../CardNavigationFooter';
import type { ResolvedNavigationLink } from '@/config/entity-navigation';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

const MOCK_LINKS: ResolvedNavigationLink[] = [
  { entity: 'document', label: 'KB', href: '/games/1/knowledge-base' },
  { entity: 'agent', label: 'Agents', href: '/games/1/agents' },
  { entity: 'chatSession', label: 'Chats', href: '/games/1/chats' },
  { entity: 'session', label: 'Sessions', href: '/games/1/sessions' },
];

describe('CardNavigationFooter', () => {
  it('renders nothing when links array is empty', () => {
    const { container } = render(<CardNavigationFooter links={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders a nav element with correct aria-label', () => {
    render(<CardNavigationFooter links={MOCK_LINKS} />);
    const nav = screen.getByRole('navigation', { name: /navigate to related entities/i });
    expect(nav).toBeInTheDocument();
  });

  it('renders one link per navigation target', () => {
    render(<CardNavigationFooter links={MOCK_LINKS} />);
    const links = screen.getAllByRole('link');
    expect(links).toHaveLength(4);
  });

  it('each link has correct href', () => {
    render(<CardNavigationFooter links={MOCK_LINKS} />);
    const links = screen.getAllByRole('link');
    expect(links[0]).toHaveAttribute('href', '/games/1/knowledge-base');
    expect(links[1]).toHaveAttribute('href', '/games/1/agents');
    expect(links[2]).toHaveAttribute('href', '/games/1/chats');
    expect(links[3]).toHaveAttribute('href', '/games/1/sessions');
  });

  it('each link has title matching label', () => {
    render(<CardNavigationFooter links={MOCK_LINKS} />);
    expect(screen.getByTitle('KB')).toBeInTheDocument();
    expect(screen.getByTitle('Agents')).toBeInTheDocument();
    expect(screen.getByTitle('Chats')).toBeInTheDocument();
    expect(screen.getByTitle('Sessions')).toBeInTheDocument();
  });

  it('displays label text for each link', () => {
    render(<CardNavigationFooter links={MOCK_LINKS} />);
    expect(screen.getByText('KB')).toBeInTheDocument();
    expect(screen.getByText('Agents')).toBeInTheDocument();
    expect(screen.getByText('Chats')).toBeInTheDocument();
    expect(screen.getByText('Sessions')).toBeInTheDocument();
  });

  it('has data-testid on nav element', () => {
    render(<CardNavigationFooter links={MOCK_LINKS} />);
    expect(screen.getByTestId('card-navigation-footer')).toBeInTheDocument();
  });

  it('renders with custom className', () => {
    render(<CardNavigationFooter links={MOCK_LINKS} className="custom-class" />);
    const nav = screen.getByTestId('card-navigation-footer');
    expect(nav.className).toContain('custom-class');
  });

  it('renders single link correctly', () => {
    const singleLink: ResolvedNavigationLink[] = [
      { entity: 'game', label: 'Game', href: '/games/1' },
    ];
    render(<CardNavigationFooter links={singleLink} />);
    expect(screen.getAllByRole('link')).toHaveLength(1);
    expect(screen.getByText('Game')).toBeInTheDocument();
  });
});
