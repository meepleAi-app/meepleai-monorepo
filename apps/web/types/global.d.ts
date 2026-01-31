/**
 * Global type declarations for test utilities and browser APIs
 */

import type { Citation } from '@/lib/api/schemas/streaming.schemas';

interface StreamingTestState {
  answer: string;
  citations: Citation[];
  confidence?: number | null;
  completed?: boolean;
  isStreaming?: boolean;
}

declare global {
  interface Window {
    /**
     * Test utility to expose streaming state for E2E tests
     * @see apps/web/src/store/chat/ChatStoreProvider.tsx
     * @see apps/web/e2e/helpers/citation-test-utils.ts
     */
    __TEST_STREAMING_STATE__?: StreamingTestState;
  }
}

export {};
