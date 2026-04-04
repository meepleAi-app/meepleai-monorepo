/**
 * WizardProgress - Issue #4141
 *
 * Simplified wrapper around WizardSteps for 4-step PDF wizard.
 */

import { WizardSteps } from './WizardSteps';

interface WizardProgressProps {
  currentStep: number; // 1-4
  onStepClick?: (step: number) => void;
  allowSkip?: boolean;
}

const WIZARD_STEPS = [
  { id: '1', label: 'Upload PDF', description: 'Upload and extract' },
  { id: '2', label: 'Preview Data', description: 'Manual fields' },
  { id: '3', label: 'Catalogo Match', description: 'Optional' },
  { id: '4', label: 'Confirm', description: 'Review and submit' },
];

/**
 * WizardProgress component for PDF wizard
 *
 * Displays progress indicator for 4-step wizard flow.
 */
export function WizardProgress({
  currentStep,
  onStepClick,
  allowSkip = false,
}: WizardProgressProps) {
  const handleStepClick = (stepId: string) => {
    if (onStepClick) {
      onStepClick(parseInt(stepId, 10));
    }
  };

  return (
    <WizardSteps
      steps={WIZARD_STEPS}
      currentStep={currentStep.toString()}
      onStepClick={handleStepClick}
      allowSkip={allowSkip}
    />
  );
}
