import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { TopBarLogo } from '../TopBarLogo';

describe('TopBarLogo', () => {
  it('renders the wordmark', () => {
    render(<TopBarLogo />);
    expect(screen.getByText('Meeple')).toBeInTheDocument();
    expect(screen.getByText('Ai')).toBeInTheDocument();
  });

  it('has accessible label', () => {
    render(<TopBarLogo />);
    expect(screen.getByRole('link', { name: /meepleai home/i })).toBeInTheDocument();
  });

  it('links to /', () => {
    render(<TopBarLogo />);
    const link = screen.getByRole('link', { name: /meepleai home/i });
    expect(link).toHaveAttribute('href', '/');
  });
});
