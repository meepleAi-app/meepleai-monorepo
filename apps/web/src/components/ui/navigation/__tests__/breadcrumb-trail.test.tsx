/**
 * BreadcrumbTrail - Component Tests
 *
 * @see Issue #4705 - Integration Testing
 * @see Issue #4704 - Connected Navigation + Breadcrumb Trail
 */

import { render, screen, fireEvent } from '@testing-library/react';

import { BreadcrumbTrail } from '../breadcrumb-trail';
import * as trailHook from '@/hooks/use-navigation-trail';
import type { BreadcrumbStep } from '@/hooks/use-navigation-trail';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

const MOCK_TRAIL: BreadcrumbStep[] = [
  { entity: 'game', label: 'Catan', href: '/games/1' },
  { entity: 'agent', label: 'RulesMaster', href: '/agents/1' },
  { entity: 'chatSession', label: 'Rules Q&A', href: '/chat/1' },
];

const mockNavigateTo = vi.fn();
const mockClear = vi.fn();
const mockPush = vi.fn();
const mockClearHighlight = vi.fn();

function mockTrailHook(trail: BreadcrumbStep[] = []) {
  vi.spyOn(trailHook, 'useNavigationTrail').mockReturnValue({
    trail,
    push: mockPush,
    navigateTo: mockNavigateTo,
    clear: mockClear,
    highlightEntity: null,
    clearHighlight: mockClearHighlight,
  });
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe('BreadcrumbTrail', () => {
  it('renders nothing when trail is empty', () => {
    mockTrailHook([]);
    const { container } = render(<BreadcrumbTrail />);
    expect(container.firstChild).toBeNull();
  });

  it('renders nav element when trail has steps', () => {
    mockTrailHook(MOCK_TRAIL);
    render(<BreadcrumbTrail />);
    expect(screen.getByTestId('breadcrumb-trail')).toBeInTheDocument();
  });

  it('renders a Home link', () => {
    mockTrailHook(MOCK_TRAIL);
    render(<BreadcrumbTrail />);
    const homeLink = screen.getByTitle('Home');
    expect(homeLink).toBeInTheDocument();
    expect(homeLink).toHaveAttribute('href', '/');
  });

  it('renders all step labels for short trails', () => {
    mockTrailHook(MOCK_TRAIL);
    render(<BreadcrumbTrail />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('RulesMaster')).toBeInTheDocument();
    expect(screen.getByText('Rules Q&A')).toBeInTheDocument();
  });

  it('clear button calls clear()', () => {
    mockTrailHook(MOCK_TRAIL);
    render(<BreadcrumbTrail />);
    const clearBtn = screen.getByLabelText('Clear navigation trail');
    fireEvent.click(clearBtn);
    expect(mockClear).toHaveBeenCalledTimes(1);
  });

  it('clicking a non-last step calls navigateTo with its index', () => {
    mockTrailHook(MOCK_TRAIL);
    render(<BreadcrumbTrail />);
    const catanLink = screen.getByText('Catan').closest('a')!;
    fireEvent.click(catanLink);
    expect(mockNavigateTo).toHaveBeenCalledWith(0);
  });

  it('collapses middle steps for long trails', () => {
    const longTrail: BreadcrumbStep[] = [
      { entity: 'game', label: 'Step1', href: '/1' },
      { entity: 'agent', label: 'Step2', href: '/2' },
      { entity: 'session', label: 'Step3', href: '/3' },
      { entity: 'chatSession', label: 'Step4', href: '/4' },
    ];
    mockTrailHook(longTrail);
    render(<BreadcrumbTrail />);
    // First and last should always be visible
    expect(screen.getByText('Step1')).toBeInTheDocument();
    expect(screen.getByText('Step4')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    mockTrailHook(MOCK_TRAIL);
    render(<BreadcrumbTrail className="my-class" />);
    const nav = screen.getByTestId('breadcrumb-trail');
    expect(nav.className).toContain('my-class');
  });
});
