import { defineConfig } from 'vitest/config';

import baseConfig from './vitest.config';

const adminCoverageInclude = [
  'src/components/admin/AdminAuthGuard.tsx',
  'src/components/admin/AdminLayout.tsx',
  'src/components/admin/AdminHeader.tsx',
  'src/components/admin/AdminSidebar.tsx',
  'src/components/admin/AdminBreadcrumbs.tsx',
  'src/components/admin/AdminCharts.tsx',
  'src/components/admin/StatCard.tsx',
  'src/components/admin/MetricsGrid.tsx',
  'src/components/admin/ActivityFeed.tsx',
  'src/components/admin/QuickActions.tsx',
  'src/components/admin/SystemStatus.tsx',
  'src/components/admin/index.ts',
];

export default defineConfig({
  ...baseConfig,
  test: {
    ...baseConfig.test,
    include: ['src/components/admin/__tests__/**/*.{test,spec}.{ts,tsx}'],
    coverage: {
      provider: 'v8',
      include: adminCoverageInclude,
      exclude: ['**/__tests__/**', '**/*.stories.*'],
      thresholds: {
        statements: 90,
        branches: 80,
        functions: 90,
        lines: 90,
      },
    },
  },
});
