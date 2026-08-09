/**
 * @vitest-environment jsdom
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { Cover } from '../Cover';

describe('Cover — punto focale (#3611)', () => {
  it('non emette alcuno stile inline quando coverFocal è assente (contratto invariato)', () => {
    render(<Cover entity="game" variant="grid" imageUrl="https://r2.example/c.webp" alt="c" />);
    expect(screen.getByRole('img')).not.toHaveAttribute('style');
  });

  it('traduce il punto focale in object-position', () => {
    render(
      <Cover
        entity="game"
        variant="hero"
        imageUrl="https://r2.example/c.webp"
        alt="c"
        coverFocal={{ x: 0.5, y: 0.2 }}
      />
    );
    expect(screen.getByRole('img')).toHaveStyle({ objectPosition: '50% 20%' });
  });
});
