/**
 * useWizardStep — state hook for wizard step navigation.
 *
 * Tracks currentIndex, errors, validation state. Exposes goNext/goBack/skip.
 */

import { useState } from 'react';

import { normalizeValidate } from './normalize-validate';

import type { ValidationError, WizardStep } from './wizard-modal-types';

export interface UseWizardStepResult {
  currentIndex: number;
  currentStep: WizardStep;
  isFirst: boolean;
  isLast: boolean;
  errors: ValidationError[];
  isValidating: boolean;
  /**
   * Runs current step's validate (if present), advances to next on success.
   * Returns true if advanced (or already on last step and valid), false if blocked by validation.
   */
  goNext: () => Promise<boolean>;
  goBack: () => void;
  skip: () => void;
  clearErrors: () => void;
}

export function useWizardStep(steps: WizardStep[]): UseWizardStepResult {
  const [currentIndex, setCurrentIndex] = useState(0);
  const [errors, setErrors] = useState<ValidationError[]>([]);
  const [isValidating, setIsValidating] = useState(false);

  const currentStep = steps[currentIndex];
  const isFirst = currentIndex === 0;
  const isLast = currentIndex === steps.length - 1;

  const goNext = async (): Promise<boolean> => {
    setErrors([]);
    if (currentStep.validate) {
      setIsValidating(true);
      try {
        const result = await normalizeValidate(currentStep.validate());
        if (!result.valid) {
          setErrors(result.errors ?? []);
          return false;
        }
      } finally {
        setIsValidating(false);
      }
    }
    if (!isLast) {
      setCurrentIndex(i => i + 1);
    }
    return true;
  };

  const goBack = (): void => {
    if (!isFirst) {
      setErrors([]);
      setCurrentIndex(i => i - 1);
    }
  };

  const skip = (): void => {
    if (currentStep.optional && !isLast) {
      setErrors([]);
      setCurrentIndex(i => i + 1);
    }
  };

  const clearErrors = (): void => setErrors([]);

  return {
    currentIndex,
    currentStep,
    isFirst,
    isLast,
    errors,
    isValidating,
    goNext,
    goBack,
    skip,
    clearErrors,
  };
}
