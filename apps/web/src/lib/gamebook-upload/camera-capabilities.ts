/**
 * Camera capability detection wrapper for `/gamebook/upload` Step 2
 * (SP6 Phase C.2 Task A — Interactions sub-PR).
 *
 * Implements the 4-permission-state matrix from contract §9:
 *
 *   - `granted`     full UX with native viewfinder + light-meter + page-detection
 *   - `denied`      inline UI swap to file picker (≤500ms, NO modal interruption)
 *   - `prompt`      initial state, "Allow camera access" CTA visible
 *   - `unsupported` `mediaDevices.getUserMedia` undefined → file picker fallback
 *
 * SSR safety: every entry point checks `typeof window === 'undefined'` and
 * `typeof navigator === 'undefined'` before touching DOM globals.
 *
 * Used by:
 *   - `lib/gamebook-upload/hooks/useCameraStream.ts` (Phase C.2 Task B/host-hook)
 *   - Orchestrator step2 cell derivation (Phase C.2 Task C)
 *   - E2E spec `e2e/v2-states/gamebook-upload-step2.spec.ts`
 *
 * Schema reality v1 carryover (Gate B): the `Permissions API` `name: 'camera'`
 * query is supported in Chromium-based browsers but throws in Firefox/Safari
 * for some configurations. The fallback path returns 'prompt' optimistically —
 * actual permission outcome is observed via the `requestCameraStream` rejection
 * shape (`NotAllowedError` → 'denied'). This is documented in §9 and the
 * Phase C.2 Task B host hook is responsible for reconciling Permissions API
 * state with `getUserMedia` outcome.
 */

import type { CameraPermissionState } from './schemas';

/**
 * Detects current camera permission state.
 *
 * Resolution order:
 *   1. SSR / no `navigator` → `unsupported`
 *   2. `navigator.mediaDevices.getUserMedia` undefined → `unsupported`
 *      (e.g., http://localhost without HTTPS, older browsers)
 *   3. `navigator.permissions.query({ name: 'camera' })` succeeds → returned state
 *      (one of `granted` | `denied` | `prompt`)
 *   4. Permissions API throws OR unsupported → optimistic `prompt`
 *      (actual outcome observed via `requestCameraStream` rejection)
 *
 * Pure async — no side effects, no caching. Caller is responsible for memoizing
 * the result if invoked repeatedly.
 *
 * @example
 *   const state = await detectCameraPermissionState();
 *   // 'granted' | 'denied' | 'prompt' | 'unsupported'
 */
export async function detectCameraPermissionState(): Promise<CameraPermissionState> {
  if (typeof window === 'undefined' || typeof navigator === 'undefined') {
    return 'unsupported';
  }

  const mediaDevices = navigator.mediaDevices as MediaDevices | undefined;
  if (!mediaDevices || typeof mediaDevices.getUserMedia !== 'function') {
    return 'unsupported';
  }

  // Permissions API not universal — feature-detect, fall through on error
  const permissions = navigator.permissions as Permissions | undefined;
  if (permissions && typeof permissions.query === 'function') {
    try {
      const status = await permissions.query({ name: 'camera' as PermissionName });
      // Some browsers return additional states; coerce to canonical 4-state set
      if (status.state === 'granted' || status.state === 'denied' || status.state === 'prompt') {
        return status.state;
      }
    } catch {
      // Some browsers throw on `name: 'camera'` — fall through to optimistic 'prompt'
    }
  }

  // Optimistic default — actual outcome observed via getUserMedia call
  return 'prompt';
}

/**
 * Requests a back-camera (`environment` facing) MediaStream.
 *
 * Resolution / rejection contract:
 *   - Returns `MediaStream` on success (caller MUST close tracks via
 *     `stream.getTracks().forEach(t => t.stop())` in cleanup)
 *   - Returns `null` on `unsupported` (SSR, no `mediaDevices`, no `getUserMedia`)
 *   - Returns `null` on `NotAllowedError` (user-denied) — caller can transition
 *     FSM cell to `step2-denied` without inspecting error type
 *   - Re-throws on other unexpected errors (e.g., `OverconstrainedError`,
 *     `NotFoundError`, `NotReadableError`, hardware failures) — caller must
 *     decide between retry/log/fallback
 *
 * Video constraints:
 *   - `facingMode: 'environment'` requests back camera (UX convention for
 *     scanning physical pages)
 *   - `width: { ideal: 1920 }` + `height: { ideal: 1080 }` requests HD; browser
 *     downgrades automatically if hardware does not support it
 *   - `audio: false` — no microphone access required
 *
 * @example
 *   const stream = await requestCameraStream();
 *   if (!stream) {
 *     // unsupported OR denied — show fallback file picker
 *   } else {
 *     videoRef.current.srcObject = stream;
 *     // ... cleanup later: stream.getTracks().forEach(t => t.stop())
 *   }
 */
export async function requestCameraStream(): Promise<MediaStream | null> {
  if (typeof window === 'undefined' || typeof navigator === 'undefined') {
    return null;
  }

  const mediaDevices = navigator.mediaDevices as MediaDevices | undefined;
  if (!mediaDevices || typeof mediaDevices.getUserMedia !== 'function') {
    return null;
  }

  try {
    return await mediaDevices.getUserMedia({
      video: {
        facingMode: 'environment',
        width: { ideal: 1920 },
        height: { ideal: 1080 },
      },
      audio: false,
    });
  } catch (err) {
    // Convert NotAllowedError → null (denied path) so caller does not need
    // to import DOMException type-guards. Other errors propagate.
    if (err instanceof DOMException && err.name === 'NotAllowedError') {
      return null;
    }
    throw err;
  }
}
