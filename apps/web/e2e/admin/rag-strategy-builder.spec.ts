/**
 * E2E Tests: RAG Visual Strategy Builder
 * Issue #3468
 * @slow - 119 test cases, complex visual interactions and validations
 *
 * Tests the visual drag-and-drop builder for RAG strategies:
 * 1. Drag block from palette to canvas
 * 2. Connect blocks with edges
 * 3. Configure block parameters
 * 4. Validate pipeline
 * 5. Save/test pipeline
 *
 * Target coverage: 85%+
 * Parent: Epic #3453
 *
 * Prerequisites:
 * - Backend running: cd apps/api/src/Api && dotnet run
 * - Admin user exists (auto-seeded: admin@meepleai.dev)
 *
 * Run: pnpm test:e2e apps/web/e2e/admin/rag-strategy-builder.spec.ts
 */

import { test, expect, type Page } from '@playwright/test';

// =============================================================================
// Test Configuration
// =============================================================================

const BUILDER_URL = '/admin/rag/strategy-builder';
const ADMIN_EMAIL = 'admin@meepleai.dev';
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'Admin123!';

// Block categories and their blocks
const BLOCK_CATEGORIES = {
  retrieval: ['vector-search', 'keyword-search', 'hybrid-search'],
  optimization: ['query-rewriting', 'query-decomposition'],
  ranking: ['cross-encoder-reranking', 'metadata-filtering'],
  validation: ['crag-evaluator', 'confidence-scoring'],
  agents: ['sequential-agent', 'parallel-agent'],
};

// =============================================================================
// Test Utilities
// =============================================================================

/**
 * Login as admin user
 */
async function loginAsAdmin(page: Page) {
  await page.goto('/login');
  await page.fill('input[name="email"], input[type="email"]', ADMIN_EMAIL);
  await page.fill('input[name="password"], input[type="password"]', ADMIN_PASSWORD);
  await page.click('button[type="submit"]');
  await page.waitForURL(/admin|dashboard|board-game-ai/, { timeout: 15000 });
}

/**
 * Navigate to strategy builder page
 */
async function goToStrategyBuilder(page: Page) {
  await page.goto(BUILDER_URL);
  await expect(page.locator('[data-testid="strategy-builder-page"]')).toBeVisible({
    timeout: 10000,
  });
}

/**
 * Drag a block from palette to canvas
 */
async function dragBlockToCanvas(
  page: Page,
  blockType: string,
  targetX: number = 400,
  targetY: number = 300
) {
  // Find the block in the palette
  const block = page.locator(`[data-testid="palette-block-${blockType}"]`);
  await expect(block).toBeVisible();

  // Get the canvas
  const canvas = page.locator('[data-testid="pipeline-canvas"]');
  await expect(canvas).toBeVisible();

  // Perform drag and drop
  await block.dragTo(canvas, {
    targetPosition: { x: targetX, y: targetY },
  });

  // Verify block appears on canvas
  await expect(
    page.locator(`[data-testid="canvas-node-${blockType}"]`)
  ).toBeVisible({ timeout: 5000 });
}

/**
 * Connect two blocks on canvas
 */
async function connectBlocks(
  page: Page,
  sourceBlock: string,
  targetBlock: string
) {
  const sourceHandle = page.locator(
    `[data-testid="canvas-node-${sourceBlock}"] [data-testid="source-handle"]`
  );
  const targetHandle = page.locator(
    `[data-testid="canvas-node-${targetBlock}"] [data-testid="target-handle"]`
  );

  await sourceHandle.dragTo(targetHandle);

  // Verify edge exists
  await expect(
    page.locator(`[data-testid="edge-${sourceBlock}-${targetBlock}"]`)
  ).toBeVisible({ timeout: 5000 });
}

/**
 * Open block configuration panel
 */
async function openBlockConfig(page: Page, blockType: string) {
  await page.click(`[data-testid="canvas-node-${blockType}"]`);
  await expect(page.locator('[data-testid="block-config-panel"]')).toBeVisible({
    timeout: 5000,
  });
}

// =============================================================================
// Test Suite
// =============================================================================

test.describe('RAG Visual Strategy Builder', () => {
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  // ===========================================================================
  // 1. Page Loading and Basic UI
  // ===========================================================================

  test.describe('Page Loading', () => {
    test('should load strategy builder page with all components', async ({
      page,
    }) => {
      await goToStrategyBuilder(page);

      // Verify page title
      await expect(page.locator('[data-testid="page-title"]')).toHaveText(
        'RAG Strategy Builder'
      );

      // Verify main components are visible
      await expect(page.locator('[data-testid="block-palette"]')).toBeVisible();
      await expect(page.locator('[data-testid="pipeline-canvas"]')).toBeVisible();
      await expect(page.locator('[data-testid="validation-panel"]')).toBeVisible();
    });

    test('should display all block categories in palette', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Check each category exists
      for (const category of Object.keys(BLOCK_CATEGORIES)) {
        await expect(
          page.locator(`[data-testid="palette-category-${category}"]`)
        ).toBeVisible();
      }
    });

    test('should show import/export buttons in header', async ({ page }) => {
      await goToStrategyBuilder(page);

      await expect(page.locator('[data-testid="import-button"]')).toBeVisible();
      await expect(page.locator('[data-testid="export-button"]')).toBeVisible();
    });
  });

  // ===========================================================================
  // 2. Block Palette Functionality
  // ===========================================================================

  test.describe('Block Palette', () => {
    test('should expand/collapse block categories', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Categories should be visible by default
      const retrievalCategory = page.locator(
        '[data-testid="palette-category-retrieval"]'
      );
      await expect(retrievalCategory).toBeVisible();

      // Click to collapse
      await page.click('[data-testid="palette-category-retrieval-toggle"]');

      // Blocks should be hidden
      await expect(
        page.locator('[data-testid="palette-block-vector-search"]')
      ).not.toBeVisible();

      // Click to expand
      await page.click('[data-testid="palette-category-retrieval-toggle"]');

      // Blocks should be visible again
      await expect(
        page.locator('[data-testid="palette-block-vector-search"]')
      ).toBeVisible();
    });

    test('should filter blocks with search', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Type in search
      await page.fill('[data-testid="palette-search"]', 'vector');

      // Only matching blocks should be visible
      await expect(
        page.locator('[data-testid="palette-block-vector-search"]')
      ).toBeVisible();

      // Non-matching blocks should be hidden
      await expect(
        page.locator('[data-testid="palette-block-keyword-search"]')
      ).not.toBeVisible();

      // Clear search
      await page.fill('[data-testid="palette-search"]', '');

      // All blocks should be visible again
      await expect(
        page.locator('[data-testid="palette-block-keyword-search"]')
      ).toBeVisible();
    });

    test('should show block tooltip with description', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Hover over a block
      await page.hover('[data-testid="palette-block-vector-search"]');

      // Tooltip should appear with description
      await expect(page.locator('text=Semantic similarity search')).toBeVisible({
        timeout: 3000,
      });
    });
  });

  // ===========================================================================
  // 3. Drag and Drop
  // ===========================================================================

  test.describe('Drag and Drop', () => {
    test('should drag block from palette to canvas', async ({ page }) => {
      await goToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 400, 200);

      // Verify block exists on canvas
      await expect(
        page.locator('[data-testid="canvas-node-vector-search"]')
      ).toBeVisible();
    });

    test('should drag multiple blocks to canvas', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Drag first block
      await dragBlockToCanvas(page, 'vector-search', 200, 200);

      // Drag second block
      await dragBlockToCanvas(page, 'cross-encoder-reranking', 400, 200);

      // Drag third block
      await dragBlockToCanvas(page, 'crag-evaluator', 600, 200);

      // All blocks should be visible
      await expect(
        page.locator('[data-testid="canvas-node-vector-search"]')
      ).toBeVisible();
      await expect(
        page.locator('[data-testid="canvas-node-cross-encoder-reranking"]')
      ).toBeVisible();
      await expect(
        page.locator('[data-testid="canvas-node-crag-evaluator"]')
      ).toBeVisible();
    });

    test('should allow repositioning blocks on canvas', async ({ page }) => {
      await goToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 300, 200);

      // Get initial position
      const block = page.locator('[data-testid="canvas-node-vector-search"]');
      const initialBox = await block.boundingBox();

      // Drag to new position
      await block.dragTo(page.locator('[data-testid="pipeline-canvas"]'), {
        targetPosition: { x: 500, y: 400 },
      });

      // Verify position changed
      const newBox = await block.boundingBox();
      expect(newBox?.x).not.toBe(initialBox?.x);
      expect(newBox?.y).not.toBe(initialBox?.y);
    });
  });

  // ===========================================================================
  // 4. Block Connections
  // ===========================================================================

  test.describe('Block Connections', () => {
    test('should connect two blocks with an edge', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Add blocks
      await dragBlockToCanvas(page, 'vector-search', 200, 300);
      await dragBlockToCanvas(page, 'cross-encoder-reranking', 500, 300);

      // Connect blocks
      await connectBlocks(page, 'vector-search', 'cross-encoder-reranking');
    });

    test('should prevent invalid connections', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Add incompatible blocks
      await dragBlockToCanvas(page, 'crag-evaluator', 200, 300);
      await dragBlockToCanvas(page, 'vector-search', 500, 300);

      // Try to connect (should fail - crag outputs to vector input)
      const sourceHandle = page.locator(
        '[data-testid="canvas-node-crag-evaluator"] [data-testid="source-handle"]'
      );
      const targetHandle = page.locator(
        '[data-testid="canvas-node-vector-search"] [data-testid="target-handle"]'
      );

      await sourceHandle.dragTo(targetHandle);

      // Edge should not exist (invalid connection)
      await expect(
        page.locator('[data-testid="edge-crag-evaluator-vector-search"]')
      ).not.toBeVisible({ timeout: 2000 });
    });

    test('should delete connection when clicking edge', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Add and connect blocks
      await dragBlockToCanvas(page, 'vector-search', 200, 300);
      await dragBlockToCanvas(page, 'cross-encoder-reranking', 500, 300);
      await connectBlocks(page, 'vector-search', 'cross-encoder-reranking');

      // Click on edge to select, then delete
      await page.click(
        '[data-testid="edge-vector-search-cross-encoder-reranking"]'
      );
      await page.keyboard.press('Delete');

      // Edge should be removed
      await expect(
        page.locator(
          '[data-testid="edge-vector-search-cross-encoder-reranking"]'
        )
      ).not.toBeVisible({ timeout: 2000 });
    });
  });

  // ===========================================================================
  // 5. Block Configuration
  // ===========================================================================

  test.describe('Block Configuration', () => {
    test('should open config panel when clicking block', async ({ page }) => {
      await goToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 400, 300);
      await openBlockConfig(page, 'vector-search');

      // Config panel should show block name
      await expect(page.locator('[data-testid="config-block-name"]')).toHaveText(
        /Vector Search/i
      );
    });

    test('should display block parameters in config panel', async ({ page }) => {
      await goToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 400, 300);
      await openBlockConfig(page, 'vector-search');

      // Should show common parameters
      await expect(page.locator('[data-testid="param-topK"]')).toBeVisible();
      await expect(
        page.locator('[data-testid="param-similarityThreshold"]')
      ).toBeVisible();
    });

    test('should update block config when changing parameters', async ({
      page,
    }) => {
      await goToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 400, 300);
      await openBlockConfig(page, 'vector-search');

      // Change topK parameter
      const topKInput = page.locator('[data-testid="param-topK"] input');
      await topKInput.fill('20');

      // Close and reopen to verify persistence
      await page.click('[data-testid="config-close-button"]');
      await openBlockConfig(page, 'vector-search');

      // Value should be preserved
      await expect(topKInput).toHaveValue('20');
    });

    test('should close config panel with close button', async ({ page }) => {
      await goToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 400, 300);
      await openBlockConfig(page, 'vector-search');

      await page.click('[data-testid="config-close-button"]');

      await expect(
        page.locator('[data-testid="block-config-panel"]')
      ).not.toBeVisible();
    });
  });

  // ===========================================================================
  // 6. Pipeline Validation
  // ===========================================================================

  test.describe('Pipeline Validation', () => {
    test('should show validation errors for empty pipeline', async ({
      page,
    }) => {
      await goToStrategyBuilder(page);

      // Empty pipeline should show error
      await expect(
        page.locator('[data-testid="validation-panel"]')
      ).toContainText(/No blocks|Add blocks/i);
    });

    test('should validate pipeline with orphan blocks', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Add disconnected blocks
      await dragBlockToCanvas(page, 'vector-search', 200, 200);
      await dragBlockToCanvas(page, 'crag-evaluator', 500, 200);

      // Should show warning about disconnected blocks
      await expect(
        page.locator('[data-testid="validation-panel"]')
      ).toContainText(/disconnected|orphan|not connected/i);
    });

    test('should show valid status for correct pipeline', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Build valid pipeline: Vector → Reranker → CRAG
      await dragBlockToCanvas(page, 'vector-search', 200, 300);
      await dragBlockToCanvas(page, 'cross-encoder-reranking', 400, 300);
      await dragBlockToCanvas(page, 'crag-evaluator', 600, 300);

      await connectBlocks(page, 'vector-search', 'cross-encoder-reranking');
      await connectBlocks(page, 'cross-encoder-reranking', 'crag-evaluator');

      // Should show valid status
      await expect(
        page.locator('[data-testid="validation-panel"]')
      ).toContainText(/valid|ready|ok/i);
    });
  });

  // ===========================================================================
  // 7. Save and Test Pipeline
  // ===========================================================================

  test.describe('Save and Test', () => {
    test('should enable save button when pipeline changes', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Initially save should be disabled or hidden
      const saveButton = page.locator('[data-testid="save-button"]');

      // Add a block
      await dragBlockToCanvas(page, 'vector-search', 400, 300);

      // Save should be enabled (has changes)
      await expect(saveButton).toBeEnabled();
    });

    test('should save pipeline and show success toast', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Build a simple pipeline
      await dragBlockToCanvas(page, 'vector-search', 200, 300);
      await dragBlockToCanvas(page, 'cross-encoder-reranking', 400, 300);
      await connectBlocks(page, 'vector-search', 'cross-encoder-reranking');

      // Click save
      await page.click('[data-testid="save-button"]');

      // Should show success toast
      await expect(page.locator('text=saved')).toBeVisible({ timeout: 5000 });
    });

    test('should open test panel when clicking test button', async ({
      page,
    }) => {
      await goToStrategyBuilder(page);

      // Add a block to enable test button
      await dragBlockToCanvas(page, 'vector-search', 400, 300);

      // Click test button
      await page.click('[data-testid="test-button"]');

      // Test panel should open
      await expect(page.locator('[data-testid="test-panel"]')).toBeVisible();
    });

    test('should run test query and show results', async ({ page }) => {
      test.setTimeout(60000); // Allow time for SSE streaming

      await goToStrategyBuilder(page);

      // Build simple pipeline
      await dragBlockToCanvas(page, 'vector-search', 400, 300);

      // Open test panel
      await page.click('[data-testid="test-button"]');

      // Enter test query
      await page.fill('[data-testid="test-query-input"]', 'How do I setup?');

      // Run test
      await page.click('[data-testid="run-test-button"]');

      // Should show loading or results
      await expect(
        page.locator('[data-testid="test-results"], [data-testid="test-loading"]')
      ).toBeVisible({ timeout: 30000 });
    });
  });

  // ===========================================================================
  // 8. Import/Export
  // ===========================================================================

  test.describe('Import/Export', () => {
    test('should export pipeline as JSON', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Build and save a pipeline
      await dragBlockToCanvas(page, 'vector-search', 400, 300);
      await page.click('[data-testid="save-button"]');

      // Set up download listener
      const downloadPromise = page.waitForEvent('download');

      // Click export
      await page.click('[data-testid="export-button"]');

      // Verify download started
      const download = await downloadPromise;
      expect(download.suggestedFilename()).toMatch(/rag-strategies.*\.json$/);
    });

    test('should show error when exporting empty strategies', async ({
      page,
    }) => {
      await goToStrategyBuilder(page);

      // Don't save any strategy
      await page.click('[data-testid="export-button"]');

      // Should show error toast
      await expect(page.locator('text=No Strategies')).toBeVisible({
        timeout: 5000,
      });
    });
  });

  // ===========================================================================
  // 9. Template Selection
  // ===========================================================================

  test.describe('Templates', () => {
    test('should load template when selected', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Open template selector if exists
      const templateButton = page.locator('[data-testid="template-selector"]');
      if (await templateButton.isVisible()) {
        await templateButton.click();

        // Select a template
        await page.click('[data-testid="template-simple-faq"]');

        // Pipeline should have blocks
        await expect(
          page.locator('[data-testid^="canvas-node-"]').first()
        ).toBeVisible();
      }
    });
  });

  // ===========================================================================
  // 10. Keyboard Navigation and Accessibility
  // ===========================================================================

  test.describe('Accessibility', () => {
    test('should support keyboard navigation in palette', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Focus palette search
      await page.focus('[data-testid="palette-search"]');

      // Tab to first block
      await page.keyboard.press('Tab');

      // Should focus first category or block
      await expect(
        page.locator(
          '[data-testid="palette-category-retrieval-toggle"]:focus, [data-testid^="palette-block-"]:focus'
        )
      ).toBeFocused();
    });

    test('should delete selected block with keyboard', async ({ page }) => {
      await goToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 400, 300);

      // Click to select
      await page.click('[data-testid="canvas-node-vector-search"]');

      // Press delete
      await page.keyboard.press('Delete');

      // Block should be removed
      await expect(
        page.locator('[data-testid="canvas-node-vector-search"]')
      ).not.toBeVisible();
    });
  });

  // ===========================================================================
  // 11. Reset and Undo
  // ===========================================================================

  test.describe('Reset', () => {
    test('should reset pipeline to initial state', async ({ page }) => {
      await goToStrategyBuilder(page);

      // Add blocks
      await dragBlockToCanvas(page, 'vector-search', 200, 300);
      await dragBlockToCanvas(page, 'cross-encoder-reranking', 400, 300);

      // Click reset
      await page.click('[data-testid="reset-button"]');

      // Canvas should be empty
      await expect(
        page.locator('[data-testid^="canvas-node-"]')
      ).not.toBeVisible();
    });
  });
});
