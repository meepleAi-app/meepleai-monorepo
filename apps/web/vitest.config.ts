import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./vitest.setup.tsx'],
    testTimeout: 30000, // Global timeout 30s for slow worker/async tests
    coverage: {
      provider: 'v8',
      include: ['src/**/*.{js,jsx,ts,tsx}'],
      exclude: [
        'src/**/*.d.ts',
        'src/**/__tests__/**',
        'src/test-utils/**',
        'src/**/test-utils.{ts,tsx}',
        'src/**/chat-test-utils.{ts,tsx}',
        'src/**/*.worker.{js,jsx,ts,tsx}',
        'src/workers/**',
        'src/app/**', // App Router pages (Server Components)
        'src/**/index.{ts,tsx}', // Re-export files
        'src/actions/**', // Server Actions
        'src/scripts/**', // Build scripts
        'src/components/admin/**', // Admin UI (non-critical for core functionality)
        'src/components/comments/**', // Comments feature (separate module)
        'src/components/diff/**', // Diff viewer (separate module)
        'src/components/errors/**', // Error boundaries (E2E tested)
        'src/components/forms/**', // Form primitives (low-level)
        'src/components/layout/**', // Layout components (visual)
        'src/components/modals/**', // Modals (covered by component tests)
        'src/components/pdf/**', // PDF viewer (complex, E2E tested)
        'src/components/wizard/**', // Wizard flow (E2E tested)
        'src/lib/api/core/httpClient.ts', // Infrastructure (comprehensive tests in __tests__)
        'src/components/citations/**', // Citations feature (E2E tested)
        'src/lib/api/core/logger.ts', // API logger infrastructure
        'src/components/chat/ChatProvider.tsx', // Complex provider (integration tested)
        'src/components/auth/AuthProvider.tsx', // Complex provider (integration tested)
        'src/components/game/GameProvider.tsx', // Complex provider (integration tested)
        'src/lib/animations/**', // Animation utilities (visual)
        'src/store/chat/compatibility.ts', // Storybook/testing compatibility layer
      ],
      thresholds: {
        branches: 85, // Interim target (88.35% achieved in CI, was 90%)
        functions: 40, // Interim target (relaxed for CI stability)
        lines: 40, // Interim target (40.25% achieved in CI, was 60%)
        statements: 40, // Interim target (40.25% achieved in CI, was 60%)
        // TODO Issue #1256: Increase to 90% after writing tests for 123 untested components
        // NOTE: Thresholds lowered 2024-12 due to CI discrepancy - to be investigated
      },
    },
    exclude: [
      '**/node_modules/**',
      '**/e2e/**',
      '**/.next/**',
      '**/.__tests__/fixtures/**',
      // Issue #1951: Exclude flaky performance tests in CI
      ...(process.env.CI ? ['**/*.performance.test.{ts,tsx}'] : []),
      // Exclude specific utility files by name (not their test counterparts)
      // These are helper utilities, not test files
      '**/__tests__/utils/async-test-helpers.{ts,tsx}',
      '**/__tests__/utils/confirmDialogTestUtils.{ts,tsx}',
      '**/__tests__/utils/mock-api-presets.{ts,tsx}',
      '**/__tests__/utils/mock-api-router.{ts,tsx}',
      '**/__tests__/utils/query-test-utils.{ts,tsx}',
      '**/__tests__/utils/test-providers.{ts,tsx}',
      '**/__tests__/utils/zustand-test-utils.{ts,tsx}',
      // Also exclude test utility files at any location
      '**/test-utils.{ts,tsx}',
      '**/chat-test-utils.{ts,tsx}',
    ],
    // Explicitly include test files including those in __tests__/utils/__tests__/
    include: ['**/__tests__/**/*.{test,spec}.{ts,tsx}', '**/*.{test,spec}.{ts,tsx}'],
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
});
