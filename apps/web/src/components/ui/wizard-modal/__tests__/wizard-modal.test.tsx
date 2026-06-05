/**
 * WizardModal — integration tests.
 *
 * Verifies multi-step navigation, sync/async validate flow, Skip/Cancel/Complete UX,
 * error rendering with role="alert".
 */

import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { WizardModal } from '../wizard-modal';
import type { WizardStep } from '../wizard-modal-types';

interface HarnessProps {
  steps: WizardStep[];
  onComplete?: (data: unknown) => Promise<void>;
  onCancel?: () => void;
  initialOpen?: boolean;
}

function Harness({
  steps,
  onComplete = vi.fn().mockResolvedValue(undefined),
  onCancel = vi.fn(),
  initialOpen = true,
}: HarnessProps) {
  const [open, setOpen] = useState(initialOpen);
  return (
    <WizardModal
      steps={steps}
      onComplete={onComplete}
      onCancel={onCancel}
      open={open}
      onOpenChange={setOpen}
    />
  );
}

const basicSteps: WizardStep[] = [
  { title: 'Step One', content: <div data-testid="step1-body">Body 1</div> },
  { title: 'Step Two', content: <div data-testid="step2-body">Body 2</div> },
  { title: 'Step Three', content: <div data-testid="step3-body">Body 3</div> },
];

describe('WizardModal (Issue #1897 WP4 T4)', () => {
  it('renders first step initially with title + step indicator', () => {
    render(<Harness steps={basicSteps} />);
    expect(screen.getByText('Step One')).toBeInTheDocument();
    expect(screen.getByTestId('wizard-step-indicator')).toHaveTextContent('Step 1 of 3');
    expect(screen.getByTestId('step1-body')).toBeInTheDocument();
  });

  it('Next button advances to step 2 (no validate)', async () => {
    render(<Harness steps={basicSteps} />);
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() => {
      expect(screen.getByText('Step Two')).toBeInTheDocument();
    });
    expect(screen.getByTestId('wizard-step-indicator')).toHaveTextContent('Step 2 of 3');
  });

  it('sync validate returning boolean true advances', async () => {
    const steps: WizardStep[] = [
      {
        title: 'Alpha',
        content: <span data-testid="body-alpha">Body A</span>,
        validate: () => true,
      },
      { title: 'Beta', content: <span data-testid="body-beta">Body B</span> },
    ];
    render(<Harness steps={steps} />);
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() => {
      expect(screen.getByTestId('body-beta')).toBeInTheDocument();
    });
  });

  it('sync validate returning boolean false stays on step + shows error region absent (empty errors)', async () => {
    const steps: WizardStep[] = [
      {
        title: 'Alpha',
        content: <span data-testid="body-alpha">Body A</span>,
        validate: () => false,
      },
      { title: 'Beta', content: <span data-testid="body-beta">Body B</span> },
    ];
    render(<Harness steps={steps} />);
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    // Should remain on Alpha (body-beta should NOT appear)
    await waitFor(() => {
      expect(screen.getByTestId('body-alpha')).toBeInTheDocument();
    });
    expect(screen.queryByTestId('body-beta')).not.toBeInTheDocument();
    // No error list rendered because errors === [] (empty)
    expect(screen.queryByTestId('wizard-error-list')).not.toBeInTheDocument();
  });

  it('sync validate returning ValidationResult with errors renders alert + stays', async () => {
    const steps: WizardStep[] = [
      {
        title: 'Alpha',
        content: <span data-testid="body-alpha">Body A</span>,
        validate: () => ({
          valid: false,
          errors: [{ message: 'Name is required' }],
        }),
      },
      { title: 'Beta', content: <span data-testid="body-beta">Body B</span> },
    ];
    render(<Harness steps={steps} />);
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() => {
      expect(screen.getByText('Name is required')).toBeInTheDocument();
    });
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByTestId('body-alpha')).toBeInTheDocument();
  });

  it('async validate returning Promise<ValidationResult> with errors renders alert', async () => {
    const steps: WizardStep[] = [
      {
        title: 'Alpha',
        content: <span data-testid="body-alpha">Body A</span>,
        validate: () =>
          Promise.resolve({
            valid: false,
            errors: [{ message: 'Async error X' }],
          }),
      },
      { title: 'Beta', content: <span data-testid="body-beta">Body B</span> },
    ];
    render(<Harness steps={steps} />);
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() => {
      expect(screen.getByText('Async error X')).toBeInTheDocument();
    });
  });

  it('Skip button appears on optional step + advances when clicked', async () => {
    const steps: WizardStep[] = [
      {
        title: 'Alpha',
        content: <span data-testid="body-alpha">Body A</span>,
        optional: true,
      },
      { title: 'Beta', content: <span data-testid="body-beta">Body B</span> },
    ];
    render(<Harness steps={steps} />);
    expect(screen.getByRole('button', { name: 'Skip' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Skip' }));
    await waitFor(() => {
      expect(screen.getByTestId('body-beta')).toBeInTheDocument();
    });
  });

  it('Skip button does NOT appear on non-optional step', () => {
    render(<Harness steps={basicSteps} />);
    expect(screen.queryByRole('button', { name: 'Skip' })).not.toBeInTheDocument();
  });

  it('Skip button does NOT appear on last step (even if optional)', async () => {
    const steps: WizardStep[] = [
      { title: 'Alpha', content: <span data-testid="body-alpha">Body A</span> },
      {
        title: 'Beta',
        content: <span data-testid="body-beta">Body B</span>,
        optional: true,
      },
    ];
    render(<Harness steps={steps} />);
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() => {
      expect(screen.getByTestId('body-beta')).toBeInTheDocument();
    });
    expect(screen.queryByRole('button', { name: 'Skip' })).not.toBeInTheDocument();
  });

  it('Back button is absent on first step', () => {
    render(<Harness steps={basicSteps} />);
    expect(screen.queryByRole('button', { name: 'Back' })).not.toBeInTheDocument();
  });

  it('Back button returns to previous step', async () => {
    render(<Harness steps={basicSteps} />);
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() => {
      expect(screen.getByText('Step Two')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByRole('button', { name: 'Back' }));
    await waitFor(() => {
      expect(screen.getByText('Step One')).toBeInTheDocument();
    });
  });

  it('Complete button on last step calls onComplete + closes modal', async () => {
    const onComplete = vi.fn().mockResolvedValue(undefined);
    const steps: WizardStep[] = [
      { title: 'Alpha', content: <span data-testid="body-alpha">Body A</span> },
      { title: 'Beta', content: <span data-testid="body-beta">Body B</span> },
    ];
    render(<Harness steps={steps} onComplete={onComplete} />);
    fireEvent.click(screen.getByRole('button', { name: 'Next' }));
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Complete' })).toBeInTheDocument();
    });
    await act(async () => {
      fireEvent.click(screen.getByRole('button', { name: 'Complete' }));
    });
    await waitFor(() => {
      expect(onComplete).toHaveBeenCalledTimes(1);
    });
  });

  it('Cancel button opens confirmation dialog', async () => {
    render(<Harness steps={basicSteps} />);
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => {
      expect(screen.getByText('Confirm Cancellation')).toBeInTheDocument();
    });
    expect(screen.getByRole('button', { name: 'Cancel Wizard' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Continue' })).toBeInTheDocument();
  });

  it('Cancel confirmation → Continue button keeps wizard open', async () => {
    const onCancel = vi.fn();
    render(<Harness steps={basicSteps} onCancel={onCancel} />);
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => {
      expect(screen.getByText('Confirm Cancellation')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByRole('button', { name: 'Continue' }));
    await waitFor(() => {
      expect(screen.queryByText('Confirm Cancellation')).not.toBeInTheDocument();
    });
    expect(onCancel).not.toHaveBeenCalled();
    // Wizard still shows step 1
    expect(screen.getByText('Step One')).toBeInTheDocument();
  });

  it('Cancel confirmation → Cancel Wizard button calls onCancel + closes', async () => {
    const onCancel = vi.fn();
    render(<Harness steps={basicSteps} onCancel={onCancel} />);
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    await waitFor(() => {
      expect(screen.getByText('Confirm Cancellation')).toBeInTheDocument();
    });
    fireEvent.click(screen.getByRole('button', { name: 'Cancel Wizard' }));
    await waitFor(() => {
      expect(onCancel).toHaveBeenCalledTimes(1);
    });
  });

  it('renders dialog overlay with proper Z-index test id', () => {
    render(<Harness steps={basicSteps} />);
    expect(screen.getByTestId('wizard-modal-overlay')).toBeInTheDocument();
  });

  it('does not render when open=false', () => {
    render(<Harness steps={basicSteps} initialOpen={false} />);
    expect(screen.queryByText('Step One')).not.toBeInTheDocument();
  });

  it('has aria-labelledby pointing to title id for a11y', () => {
    render(<Harness steps={basicSteps} />);
    const titles = screen.getAllByRole('dialog');
    expect(titles[0]).toHaveAttribute('aria-labelledby', 'wizard-title');
  });
});
