/**
 * Type-contract assertions for `useGetParagraph` — issue #1303, AC-7.
 *
 * This file is a typecheck-gate. Its body is never executed; the test runner
 * compiles it via the project's `tsconfig.json` and the `@ts-expect-error`
 * comments fail the compile if the underlying contract regresses.
 *
 * Adding a new caller that should be rejected? Add another `@ts-expect-error`
 * line below the offending call. Vitest itself only runs the trivial `it.skip`
 * marker so the file shows up in the test report.
 */

import { describe, it } from 'vitest';

import { useGetParagraph } from '../useGetParagraph';

describe('useGetParagraph — type contract (AC-7)', () => {
  it.skip('compile-time only — the assertions live in @ts-expect-error comments', () => {
    const batchId: string = 'unused';

    // Valid shapes — must compile cleanly. These two lines must NOT trigger
    // any TS error; if they do, the contract has regressed.
    useGetParagraph({ batchId, paragraphRef: { type: 'page', value: 5 } });
    useGetParagraph({ batchId, paragraphRef: { type: 'paragraph', value: 42 } });
    useGetParagraph({ batchId, pageNumber: 5 });

    // AC-7: string value is a compile error on the discriminated union.
    useGetParagraph({
      batchId,
      // @ts-expect-error value must be `number`, not `string`
      paragraphRef: { type: 'paragraph', value: '42' },
    });

    // Bonus: unknown discriminator literal is rejected.
    useGetParagraph({
      batchId,
      // @ts-expect-error type must be 'page' | 'paragraph'
      paragraphRef: { type: 'chapter', value: 1 },
    });

    // Bonus: missing paragraphRef in the new shape is rejected.
    useGetParagraph({
      batchId,
      // @ts-expect-error paragraphRef is required when not using the legacy pageNumber shape
      hint: 'something',
    });
  });
});
