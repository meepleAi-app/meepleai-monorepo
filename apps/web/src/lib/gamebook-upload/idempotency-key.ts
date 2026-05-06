/**
 * Idempotency-Key composer + parser for `/gamebook/upload` (SP6 Phase C.1.A).
 *
 * Per contract §10:
 *   - Every photo upload request MUST carry an `Idempotency-Key` header
 *   - Server-side dedup based on full key match (Wave 3 spec-panel #732)
 *   - Format: `${batchId}:${pageNumber}:${attemptCount}`
 *   - Same key + same body = idempotent retry (server returns cached response)
 *   - Same key + different body = 409 Conflict (programmer error)
 *
 * The `attemptCount` increments on each retry attempt within a single
 * batch+pageNumber tuple — this lets the server distinguish between
 * "phone retried due to flaky network" and "user submitted same photo twice".
 *
 * Used by:
 *   - `lib/gamebook-upload/api-extension.ts` (Phase C.1.B Interactions)
 *   - `lib/gamebook/hooks/usePhotoBatchUpload.ts` (extended via mutation context)
 *   - `e2e/v2-states/gamebook-upload-offline.spec.ts` (Phase C.1.B Task 4)
 */

/**
 * Composes a deterministic Idempotency-Key for batch upload retries.
 *
 * Inputs are validated for sanity:
 *   - `batchId` must be a non-empty string (UUID format expected, not enforced)
 *   - `pageNumber` must be a positive integer
 *   - `attemptCount` must be a non-negative integer (0 = first attempt)
 *
 * Throws `RangeError` on invalid input — programmer error, not user error.
 *
 * @example
 *   composeIdempotencyKey('a1b2c3d4-...', 5, 0)
 *   // → 'a1b2c3d4-...:5:0'
 *
 *   composeIdempotencyKey('a1b2c3d4-...', 5, 3)
 *   // → 'a1b2c3d4-...:5:3'  (4th attempt, server treats as retry of attempt 0..2)
 */
export function composeIdempotencyKey(
  batchId: string,
  pageNumber: number,
  attemptCount: number
): string {
  if (typeof batchId !== 'string' || batchId.length === 0) {
    throw new RangeError(
      `composeIdempotencyKey: batchId must be a non-empty string, received ${JSON.stringify(batchId)}`
    );
  }
  if (!Number.isInteger(pageNumber) || pageNumber <= 0) {
    throw new RangeError(
      `composeIdempotencyKey: pageNumber must be a positive integer, received ${pageNumber}`
    );
  }
  if (!Number.isInteger(attemptCount) || attemptCount < 0) {
    throw new RangeError(
      `composeIdempotencyKey: attemptCount must be a non-negative integer, received ${attemptCount}`
    );
  }

  return `${batchId}:${pageNumber}:${attemptCount}`;
}

/**
 * Parsed Idempotency-Key triple. Returned by `parseIdempotencyKey` when the
 * input is well-formed; `null` otherwise.
 */
export interface IdempotencyKeyParts {
  readonly batchId: string;
  readonly pageNumber: number;
  readonly attemptCount: number;
}

/**
 * Parses a previously-composed Idempotency-Key string back into its
 * constituent parts. Returns `null` on malformed input — does NOT throw.
 *
 * Validation rules (must hold for a non-null return):
 *   - Exactly 2 colons present (3 segments)
 *   - First segment (batchId) is non-empty
 *   - Second segment parses to a positive integer (no leading zeros, no signs)
 *   - Third segment parses to a non-negative integer (no leading zeros, no signs)
 *
 * @example
 *   parseIdempotencyKey('a1b2c3d4-...:5:0')
 *   // → { batchId: 'a1b2c3d4-...', pageNumber: 5, attemptCount: 0 }
 *
 *   parseIdempotencyKey('malformed')         // → null
 *   parseIdempotencyKey('a:b:c')             // → null (non-numeric)
 *   parseIdempotencyKey('a:5:-1')            // → null (negative attempt)
 *   parseIdempotencyKey('a:0:0')             // → null (pageNumber must be positive)
 *   parseIdempotencyKey('a:5:00')            // → null (leading zeros forbidden)
 */
export function parseIdempotencyKey(key: string | null | undefined): IdempotencyKeyParts | null {
  if (typeof key !== 'string' || key.length === 0) return null;

  const parts = key.split(':');
  if (parts.length !== 3) return null;

  const [batchId, pageNumberStr, attemptCountStr] = parts;
  if (!batchId) return null;

  if (!isStrictPositiveInt(pageNumberStr)) return null;
  if (!isStrictNonNegativeInt(attemptCountStr)) return null;

  const pageNumber = Number(pageNumberStr);
  const attemptCount = Number(attemptCountStr);

  return { batchId, pageNumber, attemptCount };
}

/**
 * True if `s` matches `^[1-9][0-9]*$` (positive integer, no leading zeros).
 */
function isStrictPositiveInt(s: string): boolean {
  return /^[1-9][0-9]*$/.test(s);
}

/**
 * True if `s` matches `^(0|[1-9][0-9]*)$` (non-negative integer, no leading
 * zeros except the literal "0").
 */
function isStrictNonNegativeInt(s: string): boolean {
  return /^(0|[1-9][0-9]*)$/.test(s);
}
