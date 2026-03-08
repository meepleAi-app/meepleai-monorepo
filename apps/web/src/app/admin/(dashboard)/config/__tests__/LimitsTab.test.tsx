import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('@/components/admin/PdfLimitsConfig', () => ({
  PdfLimitsConfig: () => <div data-testid="pdf-limits-config" />,
}));

import { LimitsTab } from '../LimitsTab';

describe('LimitsTab', () => {
  it('renders heading', () => {
    render(<LimitsTab />);
    expect(screen.getByText('Upload & Library Limits')).toBeInTheDocument();
  });

  it('renders description', () => {
    render(<LimitsTab />);
    expect(
      screen.getByText(
        'Configure PDF upload limits, game library limits, and tier-based restrictions.'
      )
    ).toBeInTheDocument();
  });

  it('renders PdfLimitsConfig component', () => {
    render(<LimitsTab />);
    expect(screen.getByTestId('pdf-limits-config')).toBeInTheDocument();
  });
});
