import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { SuccessCard } from './success-card';

describe('SuccessCard', () => {
  it('renders emoji, title, body, and CTA', () => {
    render(
      <SuccessCard
        emoji="✅"
        title="Fatto"
        body="Email inviata"
        cta="Vai alla home"
        onCta={() => {}}
      />
    );
    expect(screen.getByText('✅')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Fatto' })).toBeInTheDocument();
    expect(screen.getByText('Email inviata')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Vai alla home' })).toBeInTheDocument();
  });

  it('emoji wrapper is aria-hidden', () => {
    render(<SuccessCard emoji="✅" title="Fatto" body="" cta="OK" onCta={() => {}} />);
    const emoji = screen.getByText('✅');
    expect(emoji.parentElement).toHaveAttribute('aria-hidden', 'true');
  });

  it('fires onCta when CTA clicked', () => {
    const onCta = vi.fn();
    render(<SuccessCard emoji="✅" title="Fatto" body="" cta="OK" onCta={onCta} />);
    fireEvent.click(screen.getByRole('button', { name: 'OK' }));
    expect(onCta).toHaveBeenCalledTimes(1);
  });

  it('accepts body as ReactNode', () => {
    render(
      <SuccessCard
        emoji="✅"
        title="Fatto"
        body={<p data-testid="body-node">Rich content</p>}
        cta="OK"
        onCta={() => {}}
      />
    );
    expect(screen.getByTestId('body-node')).toBeInTheDocument();
  });
});
