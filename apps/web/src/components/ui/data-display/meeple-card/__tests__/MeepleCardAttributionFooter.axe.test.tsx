import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, expect, it } from 'vitest';

import { MeepleCardAttributionFooter } from '../MeepleCardAttributionFooter';

describe('MeepleCardAttributionFooter a11y — Issue #2055 Phase G AC-G6', () => {
  it('has no axe AA violations with full attribution', async () => {
    const { container } = render(
      <MeepleCardAttributionFooter
        license="CC-BY-SA 4.0"
        attribution="Klaus Teuber"
        sourceUrl="https://commons.wikimedia.org/wiki/File:Catan.jpg"
      />
    );
    expect(await axe(container)).toHaveNoViolations();
  });

  it('has no axe AA violations with license-only attribution', async () => {
    const { container } = render(
      <MeepleCardAttributionFooter license="Public domain" attribution={null} sourceUrl={null} />
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
