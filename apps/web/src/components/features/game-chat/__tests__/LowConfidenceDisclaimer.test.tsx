import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { LowConfidenceDisclaimer } from '../LowConfidenceDisclaimer';

describe('LowConfidenceDisclaimer', () => {
  it('renders summary text and warning marker', () => {
    render(
      <LowConfidenceDisclaimer
        summary="Probabilmente si rimescolano gli scarti, ma il manuale non lo dice."
        alternatives={[]}
      />
    );
    expect(screen.getByText(/non sono certo/i)).toBeInTheDocument();
    expect(screen.getByText(/rimescolano gli scarti/i)).toBeInTheDocument();
  });
  it('renders empty list gracefully when no alternatives', () => {
    render(<LowConfidenceDisclaimer summary="..." alternatives={[]} />);
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });
  it('renders alternatives list with kb and external entries', () => {
    render(
      <LowConfidenceDisclaimer
        summary="..."
        alternatives={[
          { label: 'FAQ ufficiale p.6-7', kind: 'kb' },
          { label: 'BGG forum thread', kind: 'external', url: 'https://bgg.example/t/1' },
        ]}
      />
    );
    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(2);
    const link = screen.getByRole('link', { name: /BGG forum/ });
    expect(link).toHaveAttribute('href', 'https://bgg.example/t/1');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', expect.stringContaining('noopener'));
  });
});
