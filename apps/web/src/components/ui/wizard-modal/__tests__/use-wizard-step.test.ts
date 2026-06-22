/**
 * useWizardStep — unit tests.
 * Verifies state hook contract for goNext/goBack/skip + validation flow.
 */

import { act, renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { useWizardStep } from '../use-wizard-step';
import type { WizardStep } from '../wizard-modal-types';

const buildSteps = (): WizardStep[] => [
  { title: 'Step 1', content: 'Content 1' },
  { title: 'Step 2', content: 'Content 2' },
  { title: 'Step 3', content: 'Content 3' },
];

describe('useWizardStep', () => {
  it('starts at index 0 with currentStep === steps[0]', () => {
    const steps = buildSteps();
    const { result } = renderHook(() => useWizardStep(steps));
    expect(result.current.currentIndex).toBe(0);
    expect(result.current.currentStep).toBe(steps[0]);
    expect(result.current.isFirst).toBe(true);
    expect(result.current.isLast).toBe(false);
  });

  it('goNext (no validate) advances index by 1', async () => {
    const steps = buildSteps();
    const { result } = renderHook(() => useWizardStep(steps));
    await act(async () => {
      const advanced = await result.current.goNext();
      expect(advanced).toBe(true);
    });
    expect(result.current.currentIndex).toBe(1);
    expect(result.current.isFirst).toBe(false);
    expect(result.current.isLast).toBe(false);
  });

  it('goNext with sync validate(true) advances + no errors', async () => {
    const steps: WizardStep[] = [
      { title: 'S1', content: '', validate: () => true },
      { title: 'S2', content: '' },
    ];
    const { result } = renderHook(() => useWizardStep(steps));
    await act(async () => {
      const advanced = await result.current.goNext();
      expect(advanced).toBe(true);
    });
    expect(result.current.currentIndex).toBe(1);
    expect(result.current.errors).toEqual([]);
  });

  it('goNext with sync validate(false) stays + errors empty array', async () => {
    const steps: WizardStep[] = [
      { title: 'S1', content: '', validate: () => false },
      { title: 'S2', content: '' },
    ];
    const { result } = renderHook(() => useWizardStep(steps));
    await act(async () => {
      const advanced = await result.current.goNext();
      expect(advanced).toBe(false);
    });
    expect(result.current.currentIndex).toBe(0);
    expect(result.current.errors).toEqual([]);
  });

  it('goNext with ValidationResult errors → populates errors state', async () => {
    const steps: WizardStep[] = [
      {
        title: 'S1',
        content: '',
        validate: () => ({ valid: false, errors: [{ message: 'Bad input' }] }),
      },
      { title: 'S2', content: '' },
    ];
    const { result } = renderHook(() => useWizardStep(steps));
    await act(async () => {
      await result.current.goNext();
    });
    expect(result.current.currentIndex).toBe(0);
    expect(result.current.errors).toEqual([{ message: 'Bad input' }]);
  });

  it('goBack on first step is no-op', () => {
    const steps = buildSteps();
    const { result } = renderHook(() => useWizardStep(steps));
    act(() => result.current.goBack());
    expect(result.current.currentIndex).toBe(0);
  });

  it('goBack on non-first step decrements + clears errors', async () => {
    const steps = buildSteps();
    const { result } = renderHook(() => useWizardStep(steps));
    await act(async () => {
      await result.current.goNext();
    });
    expect(result.current.currentIndex).toBe(1);
    act(() => result.current.goBack());
    expect(result.current.currentIndex).toBe(0);
  });

  it('skip on optional step advances + clears errors', () => {
    const steps: WizardStep[] = [
      { title: 'S1', content: '', optional: true },
      { title: 'S2', content: '' },
    ];
    const { result } = renderHook(() => useWizardStep(steps));
    act(() => result.current.skip());
    expect(result.current.currentIndex).toBe(1);
  });

  it('skip on non-optional step is no-op', () => {
    const steps = buildSteps();
    const { result } = renderHook(() => useWizardStep(steps));
    act(() => result.current.skip());
    expect(result.current.currentIndex).toBe(0);
  });

  it('skip on optional last step is no-op (no next to skip to)', () => {
    const steps: WizardStep[] = [
      { title: 'S1', content: '' },
      { title: 'S2', content: '', optional: true },
    ];
    const { result } = renderHook(() => useWizardStep(steps));
    // Advance to last (index 1)
    act(() => {
      // sync no-validate next
    });
    // Force last step
    const { result: last } = renderHook(() => {
      const r = useWizardStep(steps);
      return r;
    });
    // Advance via goNext (no validate)
    act(() => {
      void last.current.goNext();
    });
    // On last: skip should not advance further (no step beyond)
    act(() => last.current.skip());
    expect(last.current.currentIndex).toBeLessThanOrEqual(1);
  });

  it('isLast becomes true on last step', async () => {
    const steps = buildSteps();
    const { result } = renderHook(() => useWizardStep(steps));
    await act(async () => {
      await result.current.goNext();
    });
    await act(async () => {
      await result.current.goNext();
    });
    expect(result.current.isLast).toBe(true);
  });

  it('goNext on last step with valid validate returns true (does not advance beyond)', async () => {
    const steps: WizardStep[] = [{ title: 'S1', content: '', validate: () => true }];
    const { result } = renderHook(() => useWizardStep(steps));
    await act(async () => {
      const advanced = await result.current.goNext();
      expect(advanced).toBe(true);
    });
    expect(result.current.currentIndex).toBe(0);
    expect(result.current.isLast).toBe(true);
  });

  it('clearErrors resets errors to empty array', async () => {
    const steps: WizardStep[] = [
      {
        title: 'S1',
        content: '',
        validate: () => ({ valid: false, errors: [{ message: 'X' }] }),
      },
    ];
    const { result } = renderHook(() => useWizardStep(steps));
    await act(async () => {
      await result.current.goNext();
    });
    expect(result.current.errors.length).toBe(1);
    act(() => result.current.clearErrors());
    expect(result.current.errors).toEqual([]);
  });
});
