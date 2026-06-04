import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ExportCatalogButton } from './ExportCatalogButton';

describe('ExportCatalogButton', () => {
  it('renders "Export catalog" label (NOT "Export history")', () => {
    render(<ExportCatalogButton />);
    expect(screen.getByRole('link', { name: /Export catalog/i })).toBeInTheDocument();
    expect(screen.queryByText(/Export history/i)).not.toBeInTheDocument();
  });

  it('links to /excel-export endpoint', () => {
    render(<ExportCatalogButton />);
    const link = screen.getByRole('link', { name: /Export catalog/i });
    expect(link).toHaveAttribute('href', expect.stringContaining('/excel-export'));
  });
});
