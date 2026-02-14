/**
 * E2E Test: RAG Visual Strategy Builder
 * Issue #3468
 * @slow - 135 test cases, complex drag-and-drop interactions
 *
 * Tests for the RAG Pipeline Builder visual interface:
 * 1. Block Palette - drag blocks to canvas
 * 2. Block Connections - connect blocks via handles
 * 3. Block Configuration - parameter updates
 * 4. Pipeline Validation - error/warning detection
 * 5. Strategy Templates - load pre-built templates
 * 6. Save/Export - persist pipelines
 *
 * Epic: #3453 - Visual RAG Strategy Builder
 */

import { test, expect, type Page, type Locator } from '@playwright/test';

// Test configuration
const BASE_URL = process.env.NEXT_PUBLIC_BASE_URL || 'http://localhost:3000';
const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

// Helper: Login as admin
async function loginAsAdmin(page: Page): Promise<void> {
  await page.goto(`${BASE_URL}/login`);
  await page.fill('input[name="email"]', 'admin@meepleai.dev');
  await page.fill('input[name="password"]', process.env.ADMIN_PASSWORD || 'pVKOMQNK0tFNgGlX');
  await page.click('button[type="submit"]');
  await page.waitForURL(/admin|dashboard/, { timeout: 15000 });
}

// Helper: Navigate to Strategy Builder
async function navigateToStrategyBuilder(page: Page): Promise<void> {
  // Navigate to RAG dashboard or strategy builder page
  // Adjust the URL based on your actual routing
  await page.goto(`${BASE_URL}/admin/rag/builder`);
  await page.waitForSelector('[data-testid="strategy-builder"]', { timeout: 10000 });
}

// Helper: Get canvas element
function getCanvas(page: Page): Locator {
  return page.locator('[data-testid="pipeline-canvas"]');
}

// Helper: Get block palette
function getBlockPalette(page: Page): Locator {
  return page.locator('[data-testid="block-palette"]');
}

// Helper: Get a specific block from palette
function getPaletteBlock(page: Page, blockType: string): Locator {
  return page.locator(`[data-testid="palette-block-${blockType}"]`);
}

// Helper: Get a node on the canvas
function getCanvasNode(page: Page, nodeId: string): Locator {
  return page.locator(`[data-testid="node-${nodeId}"]`);
}

// Helper: Drag block from palette to canvas
async function dragBlockToCanvas(
  page: Page,
  blockType: string,
  targetX: number,
  targetY: number
): Promise<void> {
  const block = getPaletteBlock(page, blockType);
  const canvas = getCanvas(page);

  // Get the bounding boxes
  const blockBox = await block.boundingBox();
  const canvasBox = await canvas.boundingBox();

  if (!blockBox || !canvasBox) {
    throw new Error('Could not get bounding boxes');
  }

  // Calculate drag positions
  const startX = blockBox.x + blockBox.width / 2;
  const startY = blockBox.y + blockBox.height / 2;
  const endX = canvasBox.x + targetX;
  const endY = canvasBox.y + targetY;

  // Perform drag and drop
  await page.mouse.move(startX, startY);
  await page.mouse.down();
  await page.mouse.move(endX, endY, { steps: 10 });
  await page.mouse.up();

  // Wait for node to appear
  await page.waitForTimeout(300);
}

test.describe('RAG Visual Strategy Builder', () => {
  test.beforeEach(async ({ page }) => {
    // Skip login for mock tests, use auth in real E2E
    test.skip(process.env.CI === 'true', 'Requires running backend');
  });

  test.describe('Block Palette', () => {
    test('should display block palette with categories', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      const palette = getBlockPalette(page);
      await expect(palette).toBeVisible();

      // Check for category headers
      await expect(page.locator('[data-testid="category-retrieval"]')).toBeVisible();
      await expect(page.locator('[data-testid="category-ranking"]')).toBeVisible();
      await expect(page.locator('[data-testid="category-validation"]')).toBeVisible();
      await expect(page.locator('[data-testid="category-optimization"]')).toBeVisible();
      await expect(page.locator('[data-testid="category-agents"]')).toBeVisible();
    });

    test('should display Tier 1 blocks for User tier', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Tier 1 blocks should be visible
      await expect(getPaletteBlock(page, 'vector-search')).toBeVisible();
      await expect(getPaletteBlock(page, 'keyword-search')).toBeVisible();
      await expect(getPaletteBlock(page, 'hybrid-search')).toBeVisible();
      await expect(getPaletteBlock(page, 'cross-encoder-reranking')).toBeVisible();
      await expect(getPaletteBlock(page, 'crag-evaluator')).toBeVisible();
      await expect(getPaletteBlock(page, 'confidence-scoring')).toBeVisible();
      await expect(getPaletteBlock(page, 'citation-verification')).toBeVisible();
    });

    test('should collapse/expand palette categories', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Click category header to collapse
      const categoryHeader = page.locator('[data-testid="category-header-retrieval"]');
      await categoryHeader.click();

      // Blocks should be hidden
      await expect(getPaletteBlock(page, 'vector-search')).not.toBeVisible();

      // Click again to expand
      await categoryHeader.click();
      await expect(getPaletteBlock(page, 'vector-search')).toBeVisible();
    });

    test('should toggle palette collapse', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      const palette = getBlockPalette(page);
      await expect(palette).toBeVisible();

      // Click collapse button
      await page.click('[data-testid="toggle-palette"]');

      // Palette should be collapsed
      await expect(palette).toHaveAttribute('data-collapsed', 'true');

      // Click again to expand
      await page.click('[data-testid="toggle-palette"]');
      await expect(palette).toHaveAttribute('data-collapsed', 'false');
    });
  });

  test.describe('Drag and Drop', () => {
    test('should drag block from palette to canvas', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Drag vector search to canvas
      await dragBlockToCanvas(page, 'vector-search', 200, 200);

      // Verify node appears on canvas
      const nodes = page.locator('[data-testid^="node-"]');
      await expect(nodes).toHaveCount(1);

      // Verify node type
      const node = nodes.first();
      await expect(node).toHaveAttribute('data-node-type', 'vector-search');
    });

    test('should support multiple blocks on canvas', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Drag multiple blocks
      await dragBlockToCanvas(page, 'vector-search', 100, 200);
      await dragBlockToCanvas(page, 'cross-encoder-reranking', 400, 200);
      await dragBlockToCanvas(page, 'confidence-scoring', 700, 200);

      // Verify all nodes present
      const nodes = page.locator('[data-testid^="node-"]');
      await expect(nodes).toHaveCount(3);
    });

    test('should show drop indicator on valid drop zone', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      const block = getPaletteBlock(page, 'vector-search');
      const canvas = getCanvas(page);

      const blockBox = await block.boundingBox();
      const canvasBox = await canvas.boundingBox();

      if (blockBox && canvasBox) {
        // Start drag
        await page.mouse.move(blockBox.x + 10, blockBox.y + 10);
        await page.mouse.down();
        await page.mouse.move(canvasBox.x + 200, canvasBox.y + 200, { steps: 5 });

        // Check for drop indicator
        await expect(canvas).toHaveClass(/drop-target/);

        await page.mouse.up();
      }
    });
  });

  test.describe('Block Connections', () => {
    test('should connect two blocks via handles', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Add two blocks
      await dragBlockToCanvas(page, 'vector-search', 100, 200);
      await dragBlockToCanvas(page, 'cross-encoder-reranking', 400, 200);

      // Get output handle of first block and input handle of second
      const outputHandle = page.locator('[data-testid^="node-"] [data-handletype="source"]').first();
      const inputHandle = page.locator('[data-testid^="node-"] [data-handletype="target"]').last();

      const outputBox = await outputHandle.boundingBox();
      const inputBox = await inputHandle.boundingBox();

      if (outputBox && inputBox) {
        // Drag from output to input
        await page.mouse.move(outputBox.x + 5, outputBox.y + 5);
        await page.mouse.down();
        await page.mouse.move(inputBox.x + 5, inputBox.y + 5, { steps: 10 });
        await page.mouse.up();

        // Verify edge created
        const edges = page.locator('.react-flow__edge');
        await expect(edges).toHaveCount(1);
      }
    });

    test('should validate connection compatibility', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Add two incompatible blocks
      await dragBlockToCanvas(page, 'hallucination-detection', 100, 200);
      await dragBlockToCanvas(page, 'vector-search', 400, 200);

      // Try to connect (hallucination detection can't connect to vector search)
      const outputHandle = page.locator('[data-testid^="node-"] [data-handletype="source"]').first();
      const inputHandle = page.locator('[data-testid^="node-"] [data-handletype="target"]').last();

      const outputBox = await outputHandle.boundingBox();
      const inputBox = await inputHandle.boundingBox();

      if (outputBox && inputBox) {
        await page.mouse.move(outputBox.x + 5, outputBox.y + 5);
        await page.mouse.down();
        await page.mouse.move(inputBox.x + 5, inputBox.y + 5, { steps: 10 });
        await page.mouse.up();

        // Connection should not be created (or show error)
        const edges = page.locator('.react-flow__edge');
        await expect(edges).toHaveCount(0);
      }
    });

    test('should show animated connection while connecting', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 100, 200);
      await dragBlockToCanvas(page, 'cross-encoder-reranking', 400, 200);

      const outputHandle = page.locator('[data-testid^="node-"] [data-handletype="source"]').first();
      const outputBox = await outputHandle.boundingBox();

      if (outputBox) {
        // Start connecting
        await page.mouse.move(outputBox.x + 5, outputBox.y + 5);
        await page.mouse.down();
        await page.mouse.move(outputBox.x + 100, outputBox.y, { steps: 5 });

        // Check for connection line
        const connectionLine = page.locator('.react-flow__connectionline');
        await expect(connectionLine).toBeVisible();

        await page.mouse.up();
      }
    });
  });

  test.describe('Block Configuration', () => {
    test('should open config panel on node click', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 200, 200);

      // Click on the node
      const node = page.locator('[data-testid^="node-"]').first();
      await node.click();

      // Config panel should open
      const configPanel = page.locator('[data-testid="block-config-panel"]');
      await expect(configPanel).toBeVisible();

      // Should show vector search config
      await expect(configPanel).toContainText('Vector Search');
    });

    test('should update parameter values', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 200, 200);

      // Open config
      await page.locator('[data-testid^="node-"]').first().click();

      // Find topK input and change it
      const topKInput = page.locator('[data-testid="param-topK"] input');
      await topKInput.fill('10');
      await topKInput.blur();

      // Verify node displays updated value
      const node = page.locator('[data-testid^="node-"]').first();
      await expect(node).toContainText('10');
    });

    test('should update embedding model selection', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 200, 200);
      await page.locator('[data-testid^="node-"]').first().click();

      // Find model selector
      const modelSelect = page.locator('[data-testid="param-embeddingModel"] select');
      await modelSelect.selectOption('voyage-large-2');

      // Verify selection
      await expect(modelSelect).toHaveValue('voyage-large-2');
    });

    test('should validate parameter ranges', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 200, 200);
      await page.locator('[data-testid^="node-"]').first().click();

      // Try invalid value (topK max is 50)
      const topKInput = page.locator('[data-testid="param-topK"] input');
      await topKInput.fill('100');
      await topKInput.blur();

      // Should show validation error
      const errorMessage = page.locator('[data-testid="param-topK-error"]');
      await expect(errorMessage).toBeVisible();
    });
  });

  test.describe('Pipeline Validation', () => {
    test('should show validation panel', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      const validationPanel = page.locator('[data-testid="validation-panel"]');
      await expect(validationPanel).toBeVisible();
    });

    test('should show warning for empty pipeline', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Empty pipeline should show warning
      const emptyWarning = page.locator('[data-testid="validation-warning-empty"]');
      await expect(emptyWarning).toBeVisible();
    });

    test('should validate pipeline on changes', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Add a block
      await dragBlockToCanvas(page, 'vector-search', 200, 200);

      // Should still show incomplete warning (no output connection)
      const incompleteWarning = page.locator('[data-testid="validation-warning-incomplete"]');
      await expect(incompleteWarning).toBeVisible();
    });

    test('should show success for valid pipeline', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Build a valid pipeline
      await dragBlockToCanvas(page, 'vector-search', 100, 200);
      await dragBlockToCanvas(page, 'confidence-scoring', 400, 200);

      // Connect them
      const outputHandle = page.locator('[data-testid^="node-"] [data-handletype="source"]').first();
      const inputHandle = page.locator('[data-testid^="node-"] [data-handletype="target"]').last();

      const outputBox = await outputHandle.boundingBox();
      const inputBox = await inputHandle.boundingBox();

      if (outputBox && inputBox) {
        await page.mouse.move(outputBox.x + 5, outputBox.y + 5);
        await page.mouse.down();
        await page.mouse.move(inputBox.x + 5, inputBox.y + 5, { steps: 10 });
        await page.mouse.up();
      }

      // Should show valid status
      const validBadge = page.locator('[data-testid="validation-valid"]');
      await expect(validBadge).toBeVisible();
    });

    test('should display estimated metrics', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 200, 200);

      // Check metrics display
      const metricsSection = page.locator('[data-testid="pipeline-metrics"]');
      await expect(metricsSection).toBeVisible();
      await expect(metricsSection).toContainText('Tokens');
      await expect(metricsSection).toContainText('Latency');
      await expect(metricsSection).toContainText('Cost');
    });
  });

  test.describe('Strategy Templates', () => {
    test('should display template selector', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Open templates
      await page.click('[data-testid="open-templates"]');

      const templateSelector = page.locator('[data-testid="template-selector"]');
      await expect(templateSelector).toBeVisible();
    });

    test('should show 4 pre-built templates', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await page.click('[data-testid="open-templates"]');

      // Check for all templates
      await expect(page.locator('[data-testid="template-simple-faq-bot"]')).toBeVisible();
      await expect(page.locator('[data-testid="template-balanced-plus"]')).toBeVisible();
      await expect(page.locator('[data-testid="template-research-assistant"]')).toBeVisible();
      await expect(page.locator('[data-testid="template-maximum-quality"]')).toBeVisible();
    });

    test('should load template into canvas', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await page.click('[data-testid="open-templates"]');

      // Click on Balanced+ template
      await page.click('[data-testid="template-balanced-plus"]');
      await page.click('[data-testid="use-template-btn"]');

      // Verify nodes are added
      const nodes = page.locator('[data-testid^="node-"]');
      await expect(nodes).toHaveCount(5); // Balanced+ has 5 nodes

      // Verify edges are created
      const edges = page.locator('.react-flow__edge');
      await expect(edges).toHaveCount(4); // Balanced+ has 4 connections
    });

    test('should show template metrics preview', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await page.click('[data-testid="open-templates"]');

      // Hover over template card
      const templateCard = page.locator('[data-testid="template-balanced-plus"]');
      await templateCard.hover();

      // Check metrics are visible
      await expect(templateCard).toContainText('80%'); // Accuracy
      await expect(templateCard).toContainText('850ms'); // Latency
    });

    test('should lock templates based on user tier', async ({ page }) => {
      // Login as regular user (not admin)
      await page.goto(`${BASE_URL}/login`);
      await page.fill('input[name="email"]', 'user@meepleai.dev');
      await page.fill('input[name="password"]', 'userpassword');
      await page.click('button[type="submit"]');
      await page.waitForURL(/dashboard/, { timeout: 15000 });

      await page.goto(`${BASE_URL}/admin/rag/builder`);
      await page.click('[data-testid="open-templates"]');

      // Admin-tier templates should be locked
      const maxQualityTemplate = page.locator('[data-testid="template-maximum-quality"]');
      await expect(maxQualityTemplate).toHaveClass(/locked/);
    });
  });

  test.describe('Save and Export', () => {
    test('should enable save button after changes', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Save button initially disabled
      const saveButton = page.locator('[data-testid="save-pipeline-btn"]');
      await expect(saveButton).toBeDisabled();

      // Add a block
      await dragBlockToCanvas(page, 'vector-search', 200, 200);

      // Save button should be enabled
      await expect(saveButton).toBeEnabled();
    });

    test('should save pipeline successfully', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 100, 200);
      await dragBlockToCanvas(page, 'confidence-scoring', 400, 200);

      // Connect them
      const outputHandle = page.locator('[data-testid^="node-"] [data-handletype="source"]').first();
      const inputHandle = page.locator('[data-testid^="node-"] [data-handletype="target"]').last();

      const outputBox = await outputHandle.boundingBox();
      const inputBox = await inputHandle.boundingBox();

      if (outputBox && inputBox) {
        await page.mouse.move(outputBox.x + 5, outputBox.y + 5);
        await page.mouse.down();
        await page.mouse.move(inputBox.x + 5, inputBox.y + 5, { steps: 10 });
        await page.mouse.up();
      }

      // Save
      await page.click('[data-testid="save-pipeline-btn"]');

      // Should show success message
      const toast = page.locator('[data-testid="toast-success"]');
      await expect(toast).toBeVisible();
      await expect(toast).toContainText('saved');
    });

    test('should export pipeline as JSON', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 200, 200);

      // Open export menu
      await page.click('[data-testid="export-menu"]');

      // Click export JSON
      const downloadPromise = page.waitForEvent('download');
      await page.click('[data-testid="export-json"]');
      const download = await downloadPromise;

      // Verify download
      expect(download.suggestedFilename()).toContain('.json');
    });

    test('should reset pipeline', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 200, 200);

      // Verify node exists
      await expect(page.locator('[data-testid^="node-"]')).toHaveCount(1);

      // Click reset
      await page.click('[data-testid="reset-pipeline-btn"]');

      // Confirm reset
      await page.click('[data-testid="confirm-reset"]');

      // Canvas should be empty
      await expect(page.locator('[data-testid^="node-"]')).toHaveCount(0);
    });
  });

  test.describe('Test Panel', () => {
    test('should open test panel', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await page.click('[data-testid="test-pipeline-btn"]');

      const testPanel = page.locator('[data-testid="pipeline-test-panel"]');
      await expect(testPanel).toBeVisible();
    });

    test('should run test query', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Build a simple pipeline
      await dragBlockToCanvas(page, 'vector-search', 100, 200);
      await dragBlockToCanvas(page, 'confidence-scoring', 400, 200);

      // Connect
      const outputHandle = page.locator('[data-testid^="node-"] [data-handletype="source"]').first();
      const inputHandle = page.locator('[data-testid^="node-"] [data-handletype="target"]').last();

      const outputBox = await outputHandle.boundingBox();
      const inputBox = await inputHandle.boundingBox();

      if (outputBox && inputBox) {
        await page.mouse.move(outputBox.x + 5, outputBox.y + 5);
        await page.mouse.down();
        await page.mouse.move(inputBox.x + 5, inputBox.y + 5, { steps: 10 });
        await page.mouse.up();
      }

      // Open test panel
      await page.click('[data-testid="test-pipeline-btn"]');

      // Enter test query
      await page.fill('[data-testid="test-query-input"]', 'How does vector search work?');

      // Run test
      await page.click('[data-testid="run-test-btn"]');

      // Wait for results (SSE streaming)
      const resultPanel = page.locator('[data-testid="test-result"]');
      await expect(resultPanel).toBeVisible({ timeout: 30000 });

      // Should show streaming response
      await expect(resultPanel).toContainText('Retrieving');
    });
  });

  test.describe('Keyboard Shortcuts', () => {
    test('should delete selected node with Delete key', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 200, 200);
      await expect(page.locator('[data-testid^="node-"]')).toHaveCount(1);

      // Select node
      await page.locator('[data-testid^="node-"]').first().click();

      // Press Delete
      await page.keyboard.press('Delete');

      // Node should be removed
      await expect(page.locator('[data-testid^="node-"]')).toHaveCount(0);
    });

    test('should undo with Ctrl+Z', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 200, 200);
      await expect(page.locator('[data-testid^="node-"]')).toHaveCount(1);

      // Delete node
      await page.locator('[data-testid^="node-"]').first().click();
      await page.keyboard.press('Delete');
      await expect(page.locator('[data-testid^="node-"]')).toHaveCount(0);

      // Undo
      await page.keyboard.press('Control+z');
      await expect(page.locator('[data-testid^="node-"]')).toHaveCount(1);
    });

    test('should select all with Ctrl+A', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 100, 200);
      await dragBlockToCanvas(page, 'cross-encoder-reranking', 400, 200);

      // Focus canvas
      await getCanvas(page).click();

      // Select all
      await page.keyboard.press('Control+a');

      // Both nodes should be selected
      const selectedNodes = page.locator('[data-testid^="node-"].selected');
      await expect(selectedNodes).toHaveCount(2);
    });
  });

  test.describe('Accessibility', () => {
    test('should have proper ARIA labels', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Check canvas ARIA
      const canvas = getCanvas(page);
      await expect(canvas).toHaveAttribute('role', 'application');
      await expect(canvas).toHaveAttribute('aria-label', /pipeline/i);

      // Check palette ARIA
      const palette = getBlockPalette(page);
      await expect(palette).toHaveAttribute('role', 'navigation');
    });

    test('should support keyboard navigation in palette', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      // Focus palette
      await getBlockPalette(page).focus();

      // Navigate with arrow keys
      await page.keyboard.press('ArrowDown');
      const focusedItem = page.locator('[data-testid^="palette-block-"]:focus');
      await expect(focusedItem).toBeVisible();
    });

    test('should announce changes to screen readers', async ({ page }) => {
      await loginAsAdmin(page);
      await navigateToStrategyBuilder(page);

      await dragBlockToCanvas(page, 'vector-search', 200, 200);

      // Check for live region announcement
      const liveRegion = page.locator('[aria-live="polite"]');
      await expect(liveRegion).toContainText(/added/i);
    });
  });
});
