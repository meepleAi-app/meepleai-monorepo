import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { HeroGradient } from './hero-gradient';

describe('HeroGradient', () => {
  it('renders string title', () => {
    render(<HeroGradient title="Il tuo assistente AI da tavolo" />);
    expect(
      screen.getByRole('heading', { name: /il tuo assistente ai da tavolo/i })
    ).toBeInTheDocument();
  });

  it('renders ReactNode title with highlighted span', () => {
    render(
      <HeroGradient
        title={
          <>
            Regole <span data-testid="highlight">chiare</span>
          </>
        }
      />
    );
    expect(screen.getByTestId('highlight')).toHaveTextContent('chiare');
  });

  it('renders subtitle when provided', () => {
    render(<HeroGradient title="Titolo" subtitle="Sottotitolo descrittivo" />);
    expect(screen.getByText('Sottotitolo descrittivo')).toBeInTheDocument();
  });

  it('does NOT render subtitle when omitted', () => {
    const { container } = render(<HeroGradient title="Titolo" />);
    expect(container.querySelector('p')).toBeNull();
  });

  it('renders primaryCta button when provided', () => {
    render(
      <HeroGradient title="Titolo" primaryCta={{ label: 'Inizia ora', onClick: () => undefined }} />
    );
    expect(screen.getByRole('button', { name: 'Inizia ora' })).toBeInTheDocument();
  });

  it('renders secondaryCta button when provided', () => {
    render(
      <HeroGradient
        title="Titolo"
        secondaryCta={{ label: 'Scopri di più', onClick: () => undefined }}
      />
    );
    expect(screen.getByRole('button', { name: 'Scopri di più' })).toBeInTheDocument();
  });

  it('fires primaryCta onClick', async () => {
    const user = userEvent.setup();
    const handler = vi.fn();
    render(<HeroGradient title="Titolo" primaryCta={{ label: 'Go', onClick: handler }} />);
    await user.click(screen.getByRole('button', { name: 'Go' }));
    expect(handler).toHaveBeenCalledTimes(1);
  });

  it('renders primaryCta as link when href provided', () => {
    render(<HeroGradient title="Titolo" primaryCta={{ label: 'Vai', href: '/signup' }} />);
    const link = screen.getByRole('link', { name: 'Vai' });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/signup');
  });

  it('renders secondaryCta as link when href provided', () => {
    render(<HeroGradient title="Titolo" secondaryCta={{ label: 'Docs', href: '/docs' }} />);
    expect(screen.getByRole('link', { name: 'Docs' })).toHaveAttribute('href', '/docs');
  });

  it('renders children when provided', () => {
    render(
      <HeroGradient title="Titolo">
        <div data-testid="extras">extra content</div>
      </HeroGradient>
    );
    expect(screen.getByTestId('extras')).toBeInTheDocument();
  });

  it('merges className onto root', () => {
    const { container } = render(<HeroGradient title="Titolo" className="custom-root" />);
    expect(container.firstChild).toHaveClass('custom-root');
  });

  it('applies gradient inline style on root', () => {
    const { container } = render(<HeroGradient title="Titolo" />);
    const root = container.firstChild as HTMLElement;
    expect(root.style.background).toContain('linear-gradient');
  });
});
