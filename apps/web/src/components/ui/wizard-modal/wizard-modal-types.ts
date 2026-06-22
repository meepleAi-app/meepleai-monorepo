/**
 * WizardModal types — multi-step modal with sync/async validation.
 *
 * Asse B WP4 T4 (Issue #1897). MAJ-1 finding: validate must accept
 * boolean | ValidationResult | Promise<...> normalized via helper.
 */

import type { ReactNode } from 'react';

export interface ValidationError {
  field?: string;
  message: string;
}

export interface ValidationResult {
  valid: boolean;
  errors?: ValidationError[];
}

export type ValidateResult = boolean | ValidationResult;

export interface WizardStep {
  title: string;
  content: ReactNode;
  /**
   * Optional validation callback. Supports sync boolean, sync ValidationResult, async Promise<...>.
   * Normalized internally via normalizeValidate().
   */
  validate?: () => ValidateResult | Promise<ValidateResult>;
  /**
   * If true, Skip button appears (advances without running validate).
   */
  optional?: boolean;
}

export interface WizardModalProps {
  steps: WizardStep[];
  /**
   * Called on Complete button (last step). May persist data.
   */
  onComplete: (data: unknown) => Promise<void>;
  /**
   * Called when user confirms cancellation (after ConfirmCancelModal).
   */
  onCancel: () => void;
  /**
   * Open state (controlled).
   */
  open: boolean;
  /**
   * Open state setter (controlled).
   */
  onOpenChange: (open: boolean) => void;
}
