import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { KbDocDetailTabs } from '../KbDocDetailTabs';

vi.mock('next/navigation', async () => {
  const actual = await vi.importActual<typeof import('next/navigation')>('next/navigation');
  return {
    ...actual,
    usePathname: () => '/admin/knowledge-base',
    useSearchParams: () => new URLSearchParams('doc=abc&tab=ingestion'),
  };
});

describe('KbDocDetailTabs', () => {
  it('renders four tabs: Overview, Ingestion log, Used by, Preview', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="overview" />);
    expect(screen.getByRole('link', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /ingestion log/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /used by/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /preview/i })).toBeInTheDocument();
  });

  it('Used by link sets ?tab=used-by and preserves doc param', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="overview" />);
    const usedByLink = screen.getByRole('link', { name: /used by/i });
    expect(usedByLink.getAttribute('href')).toContain('doc=abc');
    expect(usedByLink.getAttribute('href')).toContain('tab=used-by');
  });

  it('marks Used by as active when activeTab="used-by"', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="used-by" />);
    expect(screen.getByRole('link', { name: /used by/i }).getAttribute('aria-current')).toBe(
      'page'
    );
  });

  it('preserves doc param in each tab href', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="overview" />);
    const ingestionLink = screen.getByRole('link', { name: /ingestion log/i });
    expect(ingestionLink.getAttribute('href')).toContain('doc=abc');
    expect(ingestionLink.getAttribute('href')).toContain('tab=ingestion');
  });

  it('marks active tab with aria-current="page"', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="ingestion" />);
    expect(screen.getByRole('link', { name: /ingestion log/i }).getAttribute('aria-current')).toBe(
      'page'
    );
    expect(screen.getByRole('link', { name: /overview/i }).getAttribute('aria-current')).toBeNull();
  });

  it('renders Preview tab and marks aria-current when active', () => {
    render(<KbDocDetailTabs docId="doc-1" activeTab="preview" />);
    const previewLink = screen.getByRole('link', { name: /preview/i });
    expect(previewLink).toHaveAttribute('aria-current', 'page');
    expect(previewLink).toHaveAttribute('href', '/admin/knowledge-base?doc=doc-1&tab=preview');
  });

  it('Overview link does NOT have tab param when activeTab=preview', () => {
    render(<KbDocDetailTabs docId="doc-1" activeTab="preview" />);
    const overviewLink = screen.getByRole('link', { name: /overview/i });
    expect(overviewLink).toHaveAttribute('href', '/admin/knowledge-base?doc=doc-1');
  });
});
