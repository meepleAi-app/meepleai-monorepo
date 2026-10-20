/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { backgroundClasses } from '@/test-utils/background-classes';

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

/**
 * #4548 — il conferma è un `AlertDialogAction`, che compone `buttonVariants()`: le classi della
 * variante `default` col modificatore (`dark:bg-primary/90`, `dark:hover:bg-primary`) battevano
 * il verde nella console admin, sempre scura.
 */
describe('ApproveClaimDialog confirm button (#4548)', () => {
  it('ha solo il fondo verde del consumatore, col testo bianco', () => {
    render(
      <ApproveClaimDialog
        open
        onOpenChange={() => {}}
        onConfirm={() => {}}
        isPending={false}
        claimPreview="ok"
        validations={[]}
      />
    );

    const confirm = screen.getByTestId('approve-claim-confirm');
    expect(backgroundClasses(confirm)).toEqual(['bg-green-700', 'hover:bg-green-800']);
    expect(confirm).toHaveClass('text-white');
  });
});
