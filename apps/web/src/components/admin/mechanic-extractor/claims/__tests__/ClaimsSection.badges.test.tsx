/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ValidationBadges } from '../ClaimsSection';

describe('ValidationBadges', () => {
  it('renders one badge per rule with pass/fail/notRun styling + aria-label', () => {
    render(
      <ValidationBadges
        validations={[
          { rule: 'T1', outcome: 'pass', message: null },
          { rule: 'T2', outcome: 'fail', message: 'too long' },
          { rule: 'T3', outcome: 'notRun', message: null },
        ]}
      />
    );
    expect(screen.getByTestId('claim-validation-badge-T1')).toHaveAttribute(
      'aria-label',
      expect.stringMatching(/T1.*pass/i)
    );
    expect(screen.getByTestId('claim-validation-badge-T2')).toHaveAttribute(
      'aria-label',
      expect.stringMatching(/T2.*fail/i)
    );
    expect(screen.getByTestId('claim-validation-badge-T3')).toHaveAttribute(
      'aria-label',
      expect.stringMatching(/T3.*not/i)
    );
  });

  it('returns null when validations is empty', () => {
    const { container } = render(<ValidationBadges validations={[]} />);
    expect(container).toBeEmptyDOMElement();
  });
});
