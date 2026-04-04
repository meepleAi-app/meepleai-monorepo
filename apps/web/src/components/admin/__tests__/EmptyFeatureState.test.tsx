import { render, screen } from '@testing-library/react';

import { EmptyFeatureState } from '@/components/admin/EmptyFeatureState';

describe('EmptyFeatureState', () => {
  it('renders title and description', () => {
    render(<EmptyFeatureState title="Test Feature" description="Not available yet" />);
    expect(screen.getByText('Test Feature')).toBeInTheDocument();
    expect(screen.getByText('Not available yet')).toBeInTheDocument();
  });

  it('renders GitHub issue link when issueNumber provided', () => {
    render(<EmptyFeatureState title="Test" description="Desc" issueNumber={896} />);
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('href', expect.stringContaining('896'));
    expect(link).toHaveAttribute('target', '_blank');
  });

  it('does not render link when no issueNumber', () => {
    render(<EmptyFeatureState title="Test" description="Desc" />);
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });
});
