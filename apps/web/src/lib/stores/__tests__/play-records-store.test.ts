/**
 * play-records-store — wizard step navigation (nextStep / prevStep).
 *
 * The create wizard has 3 steps (Gioco / Quando / Punteggi), so the valid
 * 0-indexed range for `currentStep` is [0, 2]. `nextStep` must cap at the last
 * step index (2), never the out-of-range index 3 — otherwise the wizard renders
 * a broken state (StepIndicator shows all steps "done" with none active, and
 * `STEP_FIELDS[3]` is undefined). Regression guard for the off-by-one cap.
 */
import { describe, it, expect, beforeEach } from 'vitest';

import { usePlayRecordsStore } from '../play-records-store';

describe('play-records-store — wizard step navigation', () => {
  beforeEach(() => {
    usePlayRecordsStore.getState().resetSessionCreation();
  });

  it('nextStep advances through the 3 steps and caps at the last index (2)', () => {
    const store = usePlayRecordsStore;
    expect(store.getState().sessionCreation.currentStep).toBe(0);

    store.getState().nextStep(); // 0 → 1 (Quando)
    expect(store.getState().sessionCreation.currentStep).toBe(1);

    store.getState().nextStep(); // 1 → 2 (Punteggi — last step)
    expect(store.getState().sessionCreation.currentStep).toBe(2);

    store.getState().nextStep(); // 2 → 2 (capped; MUST NOT reach out-of-range 3)
    expect(store.getState().sessionCreation.currentStep).toBe(2);
  });

  it('prevStep decrements and clamps at 0', () => {
    const store = usePlayRecordsStore;
    store.getState().nextStep();
    store.getState().nextStep();
    expect(store.getState().sessionCreation.currentStep).toBe(2);

    store.getState().prevStep(); // 2 → 1
    store.getState().prevStep(); // 1 → 0
    store.getState().prevStep(); // 0 → 0 (clamped)
    expect(store.getState().sessionCreation.currentStep).toBe(0);
  });

  it('resetSessionCreation returns the wizard to step 0', () => {
    const store = usePlayRecordsStore;
    store.getState().nextStep();
    store.getState().nextStep();
    expect(store.getState().sessionCreation.currentStep).toBe(2);

    store.getState().resetSessionCreation();
    expect(store.getState().sessionCreation.currentStep).toBe(0);
  });
});
