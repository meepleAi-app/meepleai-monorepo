/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ValidationBadges } from '../ClaimsSection';

describe('ValidationBadges 5-rule + score', () => {
  it('renders all 5 rules and shows the T3b score', () => {
    render(
      <ValidationBadges
        validations={[
          { rule: 'T1', outcome: 'pass', message: null, score: null },
          { rule: 'T2', outcome: 'fail', message: 'long verbatim', score: null },
          { rule: 'T3a', outcome: 'pass', message: null, score: null },
          { rule: 'T3b', outcome: 'pass', message: null, score: 0.82 },
          { rule: 'T4', outcome: 'notRun', message: null, score: null },
        ]}
      />
    );
    for (const rule of ['T1', 'T2', 'T3a', 'T3b', 'T4']) {
      expect(screen.getByTestId(`claim-validation-badge-${rule}`)).toBeInTheDocument();
    }
    expect(screen.getByTestId('claim-validation-badge-T3b')).toHaveAttribute(
      'title',
      expect.stringMatching(/0\.82/)
    );
  });

  it('keeps the message in the title/aria-label when no score is present', () => {
    render(
      <ValidationBadges
        validations={[{ rule: 'T2', outcome: 'fail', message: 'long verbatim', score: null }]}
      />
    );
    const badge = screen.getByTestId('claim-validation-badge-T2');
    expect(badge).toHaveAttribute('title', expect.stringMatching(/long verbatim/));
  });
});
