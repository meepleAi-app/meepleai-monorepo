import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { KbSubNav } from '../KbSubNav';

// next/navigation is server+client; mock usePathname for tests
const mockPathname = vi.fn();
vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname(),
}));

const mockUseKbNavCounts = vi.fn();
vi.mock('@/hooks/admin/useKbNavCounts', () => ({
  useKbNavCounts: () => mockUseKbNavCounts(),
}));

const TABS = [
  { label: 'Explorer', href: '/admin/knowledge-base' },
  { label: 'Vector Collections', href: '/admin/knowledge-base/vectors' },
  { label: 'Processing Queue', href: '/admin/knowledge-base/queue' },
  { label: 'RAG Pipeline', href: '/admin/knowledge-base/pipeline' },
  { label: 'Embedding', href: '/admin/knowledge-base/embedding' },
  { label: 'Feedback', href: '/admin/knowledge-base/feedback' },
  { label: 'Settings', href: '/admin/knowledge-base/settings' },
  { label: 'Snapshots', href: '/admin/knowledge-base/snapshots' },
];

describe('KbSubNav', () => {
  beforeEach(() => {
    mockPathname.mockReset();
    mockUseKbNavCounts.mockReset().mockReturnValue({
      queue: undefined,
      feedback: undefined,
      loading: false,
      isError: false,
    });
  });

  it('renders all 8 KB tabs with correct hrefs', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base');
    render(<KbSubNav />);
    for (const tab of TABS) {
      const link = screen.getByRole('link', { name: tab.label });
      expect(link).toHaveAttribute('href', tab.href);
    }
  });

  it('marks Explorer active only on /admin/knowledge-base exact', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base');
    render(<KbSubNav />);
    expect(screen.getByRole('link', { name: 'Explorer' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Vector Collections' })).not.toHaveAttribute(
      'aria-current'
    );
  });

  it('marks Vector Collections active on /admin/knowledge-base/vectors', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base/vectors');
    render(<KbSubNav />);
    expect(screen.getByRole('link', { name: 'Vector Collections' })).toHaveAttribute(
      'aria-current',
      'page'
    );
    expect(screen.getByRole('link', { name: 'Explorer' })).not.toHaveAttribute('aria-current');
  });

  it('keeps Vector Collections active on deeper sub-routes', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base/vectors/abc-123');
    render(<KbSubNav />);
    expect(screen.getByRole('link', { name: 'Vector Collections' })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });

  it('does NOT mark Explorer active on KB sub-routes', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base/queue');
    render(<KbSubNav />);
    expect(screen.getByRole('link', { name: 'Explorer' })).not.toHaveAttribute('aria-current');
    expect(screen.getByRole('link', { name: 'Processing Queue' })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });

  it('exposes a tablist role with KB label', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base');
    render(<KbSubNav />);
    expect(screen.getByRole('navigation', { name: /Knowledge Base sezioni/i })).toBeInTheDocument();
  });

  it('marks no tab active when pathname is a KB route not in the tab list', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base/documents');
    render(<KbSubNav />);
    for (const tab of TABS) {
      expect(screen.getByRole('link', { name: tab.label })).not.toHaveAttribute('aria-current');
    }
  });

  it('renders queue badge with count from hook', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base');
    mockUseKbNavCounts.mockReturnValue({ queue: 7, feedback: 23, loading: false, isError: false });
    render(<KbSubNav />);
    expect(screen.getByTestId('kb-nav-badge-queue')).toHaveTextContent('7');
  });

  it('renders feedback badge with count from hook', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base');
    mockUseKbNavCounts.mockReturnValue({ queue: 7, feedback: 23, loading: false, isError: false });
    render(<KbSubNav />);
    expect(screen.getByTestId('kb-nav-badge-feedback')).toHaveTextContent('23');
  });

  it('does NOT render badges on non-counted tabs', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base');
    mockUseKbNavCounts.mockReturnValue({ queue: 5, feedback: 5, loading: false, isError: false });
    render(<KbSubNav />);
    // 8 tabs total but only 2 with badges
    const badges = screen.queryAllByTestId(/^kb-nav-badge-/);
    expect(badges).toHaveLength(2);
  });

  it('renders skeleton when loading and counts are undefined', () => {
    mockPathname.mockReturnValue('/admin/knowledge-base');
    mockUseKbNavCounts.mockReturnValue({
      queue: undefined,
      feedback: undefined,
      loading: true,
      isError: false,
    });
    const { container } = render(<KbSubNav />);
    expect(
      container.querySelector('[data-testid="kb-nav-badge-queue-loading"]')
    ).toBeInTheDocument();
    expect(
      container.querySelector('[data-testid="kb-nav-badge-feedback-loading"]')
    ).toBeInTheDocument();
  });
});
