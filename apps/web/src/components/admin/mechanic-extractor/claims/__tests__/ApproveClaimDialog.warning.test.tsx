/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ApproveClaimDialog } from '../ApproveClaimDialog';

describe('ApproveClaimDialog fail warning', () => {
  it('shows a warning when the claim carries a fail validation', () => {
    render(
      <ApproveClaimDialog
        open
        onOpenChange={() => {}}
        onConfirm={() => {}}
        isPending={false}
        claimPreview="some claim"
        validations={[{ rule: 'T2', outcome: 'fail', message: 'long verbatim', score: null }]}
      />
    );
    expect(screen.getByTestId('approve-fail-warning')).toBeInTheDocument();
  });

  it('renders no warning when all validations pass', () => {
    render(
      <ApproveClaimDialog
        open
        onOpenChange={() => {}}
        onConfirm={() => {}}
        isPending={false}
        claimPreview="ok"
        validations={[{ rule: 'T1', outcome: 'pass', message: null, score: null }]}
      />
    );
    expect(screen.queryByTestId('approve-fail-warning')).not.toBeInTheDocument();
  });
});
