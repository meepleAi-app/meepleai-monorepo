import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { QueuePendingPanel } from './QueuePendingPanel';

describe('QueuePendingPanel', () => {
  it('renders MVP placeholder with link to #1874', () => {
    render(<QueuePendingPanel />);
    expect(screen.getByText(/Queue pending re-sync/i)).toBeInTheDocument();
    expect(screen.getByText(/feature in arrivo/i)).toBeInTheDocument();
    const link = screen.getByRole('link', { name: /#1874/i });
    expect(link).toHaveAttribute('href', expect.stringContaining('1874'));
  });
});
