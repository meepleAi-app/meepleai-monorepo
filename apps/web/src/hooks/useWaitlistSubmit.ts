/**
 * Public Alpha waitlist — submission FSM hook.
 *
 * Wave A.2 (`/join` route migration). Spec §3.2 JoinFormProps + §3.5 endpoint
 * contract. Wraps `postWaitlistEntry` in a TanStack Query mutation and surfaces
 * a 5-state finite state machine the form component renders against.
 *
 *     default ─submit→ submitting ─{200}→ success
 *                                  ─{409}→ already-on-list   (preserves form data)
 *                                  ─{4xx,5xx,timeout}→ error (preserves form data)
 *     already-on-list ─submit→ submitting (re-attempt with corrected email)
 *     error           ─submit→ submitting (re-attempt)
 *     success         ─reset → default
 *
 * The hook itself is form-agnostic — form data preservation lives in the
 * consuming component. We only model the transport state.
 */

import { useCallback, useMemo } from 'react';

import { useMutation } from '@tanstack/react-query';

import {
  postWaitlistEntry,
  WaitlistValidationError,
  type WaitlistPayload,
  type WaitlistResult,
} from '@/lib/api/waitlist';

export type JoinFormState = 'default' | 'submitting' | 'success' | 'error' | 'already-on-list';

export interface UseWaitlistSubmitReturn {
  readonly state: JoinFormState;
  readonly result: WaitlistResult | null;
  readonly error: Error | null;
  readonly fieldErrors: Readonly<Record<string, string>> | null;
  readonly submit: (payload: WaitlistPayload) => Promise<void>;
  readonly reset: () => void;
}

/**
 * Map mutation lifecycle + result discriminant to FSM state.
 *
 * Why we cannot rely solely on `useMutation` status:
 *   - `success` mutation status fires for both 200 (success) and 409 (already
 *     on list), because we resolve 409 as a successful `WaitlistResult` to
 *     preserve body fields (see waitlist.ts header).
 *   - We need to discriminate the two via `result.alreadyOnList`.
 */
function deriveState(
  mutationStatus: 'idle' | 'pending' | 'success' | 'error',
  result: WaitlistResult | null
): JoinFormState {
  if (mutationStatus === 'pending') return 'submitting';
  if (mutationStatus === 'error') return 'error';
  if (mutationStatus === 'success' && result) {
    return result.alreadyOnList ? 'already-on-list' : 'success';
  }
  return 'default';
}

export function useWaitlistSubmit(): UseWaitlistSubmitReturn {
  const mutation = useMutation<WaitlistResult, Error, WaitlistPayload>({
    mutationFn: postWaitlistEntry,
  });

  const state = useMemo(
    () => deriveState(mutation.status, mutation.data ?? null),
    [mutation.status, mutation.data]
  );

  const fieldErrors = useMemo(() => {
    if (mutation.error instanceof WaitlistValidationError) {
      return mutation.error.fieldErrors ?? null;
    }
    return null;
  }, [mutation.error]);

  const submit = useCallback(
    async (payload: WaitlistPayload) => {
      // mutateAsync allows the form to await transition; errors are still
      // surfaced via mutation.error so we swallow the throw here.
      try {
        await mutation.mutateAsync(payload);
      } catch {
        // Intentionally swallowed — error state is exposed via `error` field.
      }
    },
    [mutation]
  );

  const reset = useCallback(() => {
    mutation.reset();
  }, [mutation]);

  return {
    state,
    result: mutation.data ?? null,
    error: mutation.error ?? null,
    fieldErrors,
    submit,
    reset,
  };
}
