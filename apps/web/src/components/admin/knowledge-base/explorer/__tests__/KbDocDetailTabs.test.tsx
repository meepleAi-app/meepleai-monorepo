import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { KbDocDetailTabs } from '../KbDocDetailTabs';

vi.mock('next/navigation', async () => {
  const actual = await vi.importActual<typeof import('next/navigation')>('next/navigation');
  return {
    ...actual,
    usePathname: () => '/admin/knowledge-base',
    useSearchParams: () => new URLSearchParams('docId=abc&tab=ingestion'),
  };
});

describe('KbDocDetailTabs', () => {
  it('renders three tabs: Overview, Ingestion log, Used by', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="overview" />);
    expect(screen.getByRole('link', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /ingestion log/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /used by/i })).toBeInTheDocument();
  });

  it('Used by link sets ?tab=used-by and preserves docId', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="overview" />);
    const usedByLink = screen.getByRole('link', { name: /used by/i });
    expect(usedByLink.getAttribute('href')).toContain('docId=abc');
    expect(usedByLink.getAttribute('href')).toContain('tab=used-by');
  });

  it('marks Used by as active when activeTab="used-by"', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="used-by" />);
    expect(screen.getByRole('link', { name: /used by/i }).getAttribute('aria-current')).toBe(
      'page'
    );
  });

  it('preserves docId in each tab href', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="overview" />);
    const ingestionLink = screen.getByRole('link', { name: /ingestion log/i });
    expect(ingestionLink.getAttribute('href')).toContain('docId=abc');
    expect(ingestionLink.getAttribute('href')).toContain('tab=ingestion');
  });

  it('marks active tab with aria-current="page"', () => {
    render(<KbDocDetailTabs docId="abc" activeTab="ingestion" />);
    expect(screen.getByRole('link', { name: /ingestion log/i }).getAttribute('aria-current')).toBe(
      'page'
    );
    expect(screen.getByRole('link', { name: /overview/i }).getAttribute('aria-current')).toBeNull();
  });
});
