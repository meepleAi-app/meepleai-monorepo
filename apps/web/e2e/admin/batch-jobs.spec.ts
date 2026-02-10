/**
 * Batch Job Management System E2E Tests
 * Issue #3693
 *
 * Tests batch job queue viewer functionality:
 * - View batch job queue
 * - Create new batch jobs
 * - Cancel queued/running jobs
 * - Retry failed jobs
 * - View job details and logs
 * - Real-time progress updates
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface BatchJobDto {
  id: string;
  type: 'ResourceForecast' | 'CostAnalysis' | 'DataCleanup' | 'BggSync' | 'AgentBenchmark';
  status: 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';
  progress: number;
  parameters: string;
  resultData?: string;
  resultSummary?: string;
  errorMessage?: string;
  duration: number | null;
  createdAt: string;
  startedAt?: string;
  completedAt?: string;
}

/**
 * Setup mock routes for batch job testing
 */
async function setupBatchJobMocks(page: Page) {
  const generateJobs = (count: number): BatchJobDto[] => {
    const types: BatchJobDto['type'][] = ['ResourceForecast', 'CostAnalysis', 'DataCleanup', 'BggSync'];
    const statuses: BatchJobDto['status'][] = ['Queued', 'Running', 'Completed', 'Failed'];

    return Array.from({ length: count }, (_, i) => {
      const type = types[i % types.length];
      const status = statuses[i % statuses.length];

      return {
        id: `job-${i + 1}`,
        type,
        status,
        progress: status === 'Running' ? 50 : status === 'Completed' ? 100 : 0,
        parameters: JSON.stringify({ days: 30 }),
        resultSummary: status === 'Completed' ? `${type} completed successfully` : undefined,
        errorMessage: status === 'Failed' ? 'Simulated error for testing' : undefined,
        duration: status === 'Completed' ? 120 : null,
        createdAt: new Date(Date.now() - i * 3600000).toISOString(),
        startedAt: status !== 'Queued' ? new Date(Date.now() - i * 3600000 + 1000).toISOString() : undefined,
        completedAt:
          status === 'Completed' || status === 'Failed'
            ? new Date(Date.now() - i * 3600000 + 120000).toISOString()
            : undefined,
      };
    });
  };

  const allJobs = generateJobs(20);

  // Mock admin auth
  await page.route(`${API_BASE}/api/v1/auth/me`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'admin-user-id',
          email: 'admin@meepleai.dev',
          displayName: 'Admin User',
          role: 'Admin',
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // Mock get all batch jobs endpoint with filtering and pagination
  await page.route(`${API_BASE}/api/v1/admin/batch-jobs**`, async (route) => {
    const url = new URL(route.request().url());
    const status = url.searchParams.get('status');
    const page = parseInt(url.searchParams.get('page') || '1');
    const pageSize = parseInt(url.searchParams.get('pageSize') || '20');

    const filteredJobs = status && status !== 'all' ? allJobs.filter((j) => j.status === status) : allJobs;

    const skip = (page - 1) * pageSize;
    const paginatedJobs = filteredJobs.slice(skip, skip + pageSize);

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        jobs: paginatedJobs,
        total: filteredJobs.length,
        page,
        pageSize,
      }),
    });
  });

  // Mock create batch job endpoint
  await page.route(`${API_BASE}/api/v1/admin/batch-jobs`, async (route) => {
    if (route.request().method() === 'POST') {
      const newJob: BatchJobDto = {
        id: `job-new-${Date.now()}`,
        type: 'ResourceForecast',
        status: 'Queued',
        progress: 0,
        parameters: '{"days":30}',
        duration: null,
        createdAt: new Date().toISOString(),
      };

      allJobs.unshift(newJob);

      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({ jobId: newJob.id }),
      });
    }
  });

  // Mock get single job endpoint
  await page.route(`${API_BASE}/api/v1/admin/batch-jobs/*`, async (route) => {
    const jobId = route.request().url().split('/').pop()?.split('?')[0];
    const job = allJobs.find((j) => j.id === jobId);

    if (!job) {
      await route.fulfill({
        status: 404,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Job not found' }),
      });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(job),
    });
  });

  // Mock cancel job endpoint
  await page.route(`${API_BASE}/api/v1/admin/batch-jobs/*/cancel`, async (route) => {
    const jobId = route.request().url().split('/').slice(-2)[0];
    const job = allJobs.find((j) => j.id === jobId);

    if (job && (job.status === 'Queued' || job.status === 'Running')) {
      job.status = 'Cancelled';
      job.completedAt = new Date().toISOString();
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({}),
    });
  });

  // Mock retry job endpoint
  await page.route(`${API_BASE}/api/v1/admin/batch-jobs/*/retry`, async (route) => {
    const jobId = route.request().url().split('/').slice(-2)[0];
    const job = allJobs.find((j) => j.id === jobId);

    if (job && job.status === 'Failed') {
      job.status = 'Queued';
      job.progress = 0;
      job.errorMessage = undefined;
      job.startedAt = undefined;
      job.completedAt = undefined;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({}),
    });
  });

  // Mock delete job endpoint
  await page.route(`${API_BASE}/api/v1/admin/batch-jobs/*`, async (route) => {
    if (route.request().method() === 'DELETE') {
      const jobId = route.request().url().split('/').pop();
      const index = allJobs.findIndex((j) => j.id === jobId);
      if (index !== -1) {
        allJobs.splice(index, 1);
      }

      await route.fulfill({
        status: 204,
      });
    }
  });
}

test.describe('Batch Job Management', () => {
  test.beforeEach(async ({ page }) => {
    await setupBatchJobMocks(page);
  });

  test('should display batch job queue viewer', async ({ page }) => {
    // Navigate to batch jobs tab
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Should display page header
    await expect(page.getByRole('heading', { name: /Batch Jobs/i })).toBeVisible();
    await expect(
      page.getByText(/Manage background processing jobs and tasks/i)
    ).toBeVisible();

    // Should display job table
    await expect(page.getByRole('table')).toBeVisible();

    // Should have Create Job button
    await expect(page.getByRole('button', { name: /Create Job/i })).toBeVisible();

    // Should have status filter
    await expect(page.getByRole('combobox', { name: /Filter status/i })).toBeVisible();
  });

  test('should display job entries with status badges', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Should display status badges
    await expect(page.getByText('Queued')).toBeVisible();
    await expect(page.getByText('Running')).toBeVisible();
    await expect(page.getByText('Completed')).toBeVisible();
    await expect(page.getByText('Failed')).toBeVisible();

    // Should display job types
    await expect(page.getByText('ResourceForecast')).toBeVisible();
    await expect(page.getByText('CostAnalysis')).toBeVisible();
  });

  test('should show progress bars for running jobs', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Find running job row
    const runningRow = page.locator('[data-testid^="batch-job-row-"]').filter({
      has: page.getByText('Running'),
    }).first();

    // Should display progress bar
    await expect(runningRow.locator('[role="progressbar"]')).toBeVisible();

    // Should display progress percentage
    await expect(runningRow.getByText(/50%/)).toBeVisible();
  });

  test('should filter jobs by status', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Click status filter
    await page.getByRole('combobox').click();

    // Select "Queued" status
    await page.getByRole('option', { name: 'Queued' }).click();

    // Wait for filtered results
    await page.waitForTimeout(500);

    // Should only show queued jobs
    await expect(page.getByText('Queued')).toBeVisible();
    await expect(page.getByText('Running')).not.toBeVisible();
    await expect(page.getByText('Completed')).not.toBeVisible();
  });

  test('should open create job modal', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Click Create Job button
    await page.getByRole('button', { name: /Create Job/i }).click();

    // Should display modal
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByRole('heading', { name: /Create Batch Job/i })).toBeVisible();

    // Should display job type selector
    await expect(page.getByLabel(/Job Type/i)).toBeVisible();

    // Should have Create button
    await expect(page.getByRole('button', { name: /Create/i })).toBeVisible();
  });

  test('should create new batch job', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Open create modal
    await page.getByRole('button', { name: /Create Job/i }).click();

    // Select job type
    await page.getByLabel(/Job Type/i).click();
    await page.getByRole('option', { name: 'ResourceForecast' }).click();

    // Fill parameters (if needed)
    // await page.getByLabel(/Parameters/i).fill('{"days":30}');

    // Submit
    await page.getByRole('button', { name: /^Create$/i }).click();

    // Should close modal and show success toast
    await expect(page.getByRole('dialog')).not.toBeVisible();
    await expect(page.getByText(/Job created successfully/i)).toBeVisible();
  });

  test('should open job detail modal on row click', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Click on first job row
    await page.locator('[data-testid^="batch-job-row-"]').first().click();

    // Should display detail modal
    await expect(page.getByRole('dialog')).toBeVisible();

    // Should display job details
    await expect(page.getByText(/Parameters/i)).toBeVisible();
    await expect(page.getByText(/Status/i)).toBeVisible();
  });

  test('should cancel queued job', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Find queued job row
    const queuedRow = page.locator('[data-testid^="batch-job-row-"]').filter({
      has: page.getByText('Queued'),
    }).first();

    // Click cancel button (stop icon)
    await queuedRow.getByRole('button', { name: /Cancel job/i }).click();

    // Should show success toast
    await expect(page.getByText(/Job cancelled successfully/i)).toBeVisible();
  });

  test('should retry failed job', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Find failed job row
    const failedRow = page.locator('[data-testid^="batch-job-row-"]').filter({
      has: page.getByText('Failed'),
    }).first();

    // Click retry button
    await failedRow.getByRole('button', { name: /Retry job/i }).click();

    // Should show success toast
    await expect(page.getByText(/Job retried successfully/i)).toBeVisible();
  });

  test('should delete job with confirmation', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Setup dialog handler for confirmation
    page.on('dialog', (dialog) => {
      expect(dialog.message()).toContain('Are you sure');
      dialog.accept();
    });

    // Find first job row
    const firstRow = page.locator('[data-testid^="batch-job-row-"]').first();

    // Click delete button
    await firstRow.getByRole('button', { name: /Delete job/i }).click();

    // Should show success toast
    await expect(page.getByText(/Job deleted successfully/i)).toBeVisible();
  });

  test('should display job details in modal', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Click on completed job
    const completedRow = page.locator('[data-testid^="batch-job-row-"]').filter({
      has: page.getByText('Completed'),
    }).first();

    await completedRow.click();

    // Should display modal with details
    await expect(page.getByRole('dialog')).toBeVisible();

    // Should display status
    await expect(page.getByText('Completed')).toBeVisible();

    // Should display result summary
    await expect(page.getByText(/completed successfully/i)).toBeVisible();

    // Should have close button
    await expect(page.getByRole('button', { name: /Close/i })).toBeVisible();
  });

  test('should display error details for failed jobs', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Click on failed job
    const failedRow = page.locator('[data-testid^="batch-job-row-"]').filter({
      has: page.getByText('Failed'),
    }).first();

    await failedRow.click();

    // Should display modal with error details
    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page.getByText('Failed')).toBeVisible();
    await expect(page.getByText(/Simulated error/i)).toBeVisible();
  });

  test('should auto-refresh when running jobs exist', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Should have at least one running job
    await expect(page.getByText('Running')).toBeVisible();

    // Wait for refresh interval (5s)
    await page.waitForTimeout(5500);

    // Should still display jobs (auto-refresh working)
    await expect(page.getByRole('table')).toBeVisible();
  });

  test('should display pagination when total exceeds page size', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Should display pagination info
    await expect(page.getByText(/Showing \d+ of \d+ jobs/i)).toBeVisible();

    // Should have Previous/Next buttons
    await expect(page.getByRole('button', { name: /Previous/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Next/i })).toBeVisible();
  });

  test('should refresh job list manually', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Click refresh button
    await page.getByRole('button', { name: /Refresh/i }).click();

    // Should show loading state briefly
    await expect(page.locator('.animate-spin')).toBeVisible();

    // Should reload jobs
    await expect(page.getByRole('table')).toBeVisible();
  });

  test('should display empty state when no jobs exist', async ({ page }) => {
    // Mock empty response
    await page.route(`${API_BASE}/api/v1/admin/batch-jobs**`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          jobs: [],
          total: 0,
          page: 1,
          pageSize: 20,
        }),
      });
    });

    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Should display empty state
    await expect(page.getByText(/No batch jobs found/i)).toBeVisible();
  });

  test('should display duration in human-readable format', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Find completed job with duration
    const completedRow = page.locator('[data-testid^="batch-job-row-"]').filter({
      has: page.getByText('Completed'),
    }).first();

    // Should display duration (e.g., "2m 0s")
    await expect(completedRow.getByText(/\d+m \d+s|\d+s/)).toBeVisible();
  });

  test('should be accessible', async ({ page }) => {
    await page.goto('/admin/enterprise/batch-jobs');
    await page.waitForLoadState('networkidle');

    // Check for proper heading hierarchy
    await expect(page.getByRole('heading', { level: 2, name: /Batch Jobs/i })).toBeVisible();

    // Check table has proper ARIA labels
    await expect(page.getByRole('table')).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /Type/i })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: /Status/i })).toBeVisible();

    // Check buttons have proper labels
    const buttons = page.getByRole('button');
    await expect(buttons.filter({ hasText: /Create Job/i })).toBeVisible();
  });
});
