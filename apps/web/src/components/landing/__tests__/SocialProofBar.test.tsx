import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { SocialProofBar } from '../SocialProofBar';

expect.extend(toHaveNoViolations);

describe('SocialProofBar', () => {
  it('renders three stats', () => {
    render(<SocialProofBar />);
    expect(screen.getByText('2.400+')).toBeInTheDocument();
    expect(screen.getByText('95%+')).toBeInTheDocument();
    expect(screen.getByText('Gratis')).toBeInTheDocument();
  });

  it('renders stat labels', () => {
    render(<SocialProofBar />);
    expect(screen.getByText('Giochi nel catalogo')).toBeInTheDocument();
    expect(screen.getByText('Accuratezza citazioni')).toBeInTheDocument();
    expect(screen.getByText('Per iniziare')).toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = render(<SocialProofBar />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
