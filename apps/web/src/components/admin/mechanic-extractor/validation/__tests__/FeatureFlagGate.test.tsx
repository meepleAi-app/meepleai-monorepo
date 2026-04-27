/**
 * @vitest-environment jsdom
 *
 * Tests for `FeatureFlagGate` (ADR-051 Sprint 2 / Task 25).
 *
 * Verifies the component honours `isMechanicValidationEnabled` at render
 * time: children render iff flag is `'true'`, otherwise the fallback (or
 * `null` by default) is returned.
 */
import { render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { FeatureFlagGate } from '../FeatureFlagGate';

const ENV_KEY = 'NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED';

describe('FeatureFlagGate', () => {
  let original: string | undefined;

  beforeEach(() => {
    original = process.env[ENV_KEY];
  });

  afterEach(() => {
    if (original === undefined) {
      delete process.env[ENV_KEY];
    } else {
      process.env[ENV_KEY] = original;
    }
  });

  it("renders children when the flag is 'true'", () => {
    process.env[ENV_KEY] = 'true';
    render(
      <FeatureFlagGate>
        <button type="button">Recalculate all</button>
      </FeatureFlagGate>
    );
    expect(screen.getByRole('button', { name: /recalculate all/i })).toBeInTheDocument();
  });

  it('renders nothing (null) when the flag is unset', () => {
    delete process.env[ENV_KEY];
    const { container } = render(
      <FeatureFlagGate>
        <button type="button">Recalculate all</button>
      </FeatureFlagGate>
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("renders nothing when the flag is 'false'", () => {
    process.env[ENV_KEY] = 'false';
    const { container } = render(
      <FeatureFlagGate>
        <button type="button">Recalculate all</button>
      </FeatureFlagGate>
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('renders the fallback when provided and the flag is off', () => {
    process.env[ENV_KEY] = 'false';
    render(
      <FeatureFlagGate fallback={<p>feature disabled</p>}>
        <button type="button">Recalculate all</button>
      </FeatureFlagGate>
    );
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
    expect(screen.getByText(/feature disabled/i)).toBeInTheDocument();
  });
});
