import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { FailedItemsPanel } from './FailedItemsPanel';

describe('FailedItemsPanel', () => {
  it('renders MVP placeholder with link to #1874', () => {
    render(<FailedItemsPanel />);
    expect(screen.getByText(/Failed items \(last 30gg\)/i)).toBeInTheDocument();
    expect(screen.getByText(/feature in arrivo/i)).toBeInTheDocument();
    const link = screen.getByRole('link', { name: /#1874/i });
    expect(link).toHaveAttribute('href', expect.stringContaining('1874'));
  });
});
