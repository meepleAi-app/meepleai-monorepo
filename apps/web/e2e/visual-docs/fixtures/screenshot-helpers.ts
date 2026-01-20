/**
 * Screenshot Helpers for Visual Documentation
 *
 * Provides utilities for capturing screenshots with annotations,
 * generating metadata JSON files, and organizing visual documentation.
 *
 * @module visual-docs/fixtures/screenshot-helpers
 */

import { Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

// ============================================================================
// Types & Interfaces
// ============================================================================

export interface Annotation {
  selector: string;
  label: string;
  color?: string;
  type?: 'highlight' | 'arrow' | 'box';
}

export interface CaptureOptions {
  step: number;
  title: string;
  description: string;
  annotations?: Annotation[];
  fullPage?: boolean;
  previousAction?: string;
  nextAction?: string;
}

export interface ScreenshotMetadata {
  screenshot: {
    id: string;
    filename: string;
    flow: string;
    role: string;
    step: number;
    totalSteps: number;
    title: string;
    description: string;
  };
  capture: {
    timestamp: string;
    viewport: { width: number; height: number };
    browser: string;
    fullPage: boolean;
  };
  interaction: {
    previousAction: string | null;
    nextAction: string | null;
    highlightedElements: Array<{
      selector: string;
      type: string;
      label: string;
    }>;
  };
  navigation: {
    url: string;
    previousStep: string | null;
    nextStep: string | null;
  };
}

export interface ScreenshotHelperConfig {
  outputDir: string;
  flow: string;
  role: 'user' | 'editor' | 'admin';
  totalSteps?: number;
}

// ============================================================================
// Annotation Colors
// ============================================================================

export const ANNOTATION_COLORS = {
  primary: '#3B82F6', // Blue - main action
  success: '#10B981', // Green - success states
  warning: '#F59E0B', // Amber - caution
  error: '#EF4444', // Red - errors
  info: '#6366F1', // Indigo - information
  neutral: '#6B7280', // Gray - secondary elements
} as const;

// ============================================================================
// ScreenshotHelper Class
// ============================================================================

export class ScreenshotHelper {
  private config: ScreenshotHelperConfig;
  private capturedSteps: string[] = [];
  private baseOutputDir: string;
  private static cleanedDirectories: Set<string> = new Set();

  constructor(config: ScreenshotHelperConfig) {
    this.config = config;
    this.baseOutputDir = path.resolve(process.cwd(), '..', '..', config.outputDir);
    this.ensureDirectoryExists(this.baseOutputDir);
    // NOTE: Cleaning is now done in global-setup.ts before test run starts
    // This avoids issues with static Set state not persisting across tests
  }

  /**
   * Clean the output directory (only once per directory per test run)
   * This ensures we overwrite old screenshots instead of accumulating them
   */
  private cleanDirectoryOnce(): void {
    if (ScreenshotHelper.cleanedDirectories.has(this.baseOutputDir)) {
      return; // Already cleaned in this test run
    }

    // Remove all .png and .json files in the directory
    if (fs.existsSync(this.baseOutputDir)) {
      const files = fs.readdirSync(this.baseOutputDir);
      for (const file of files) {
        if (file.endsWith('.png') || file.endsWith('.json')) {
          const filePath = path.join(this.baseOutputDir, file);
          fs.unlinkSync(filePath);
        }
      }
    }

    ScreenshotHelper.cleanedDirectories.add(this.baseOutputDir);
    console.log(`🧹 Cleaned: ${this.config.flow}`);
  }

  /**
   * Reset cleaned directories tracking (for fresh test runs)
   */
  static resetCleanedDirectories(): void {
    ScreenshotHelper.cleanedDirectories.clear();
  }

  /**
   * Capture a screenshot with optional annotations and metadata
   */
  async capture(page: Page, options: CaptureOptions): Promise<string> {
    const { step, title, description, annotations = [], fullPage = true } = options;

    // Generate filename
    const stepStr = String(step).padStart(2, '0');
    const slugTitle = this.slugify(title);
    const filename = `${stepStr}-${slugTitle}.png`;
    const filepath = path.join(this.baseOutputDir, filename);

    // Add annotations to the page
    if (annotations.length > 0) {
      await this.addAnnotations(page, annotations);
    }

    // Wait for any animations to settle
    await page.waitForTimeout(150);

    // Capture screenshot
    await page.screenshot({
      path: filepath,
      fullPage,
      animations: 'disabled',
    });

    // Remove annotations after screenshot
    if (annotations.length > 0) {
      await this.removeAnnotations(page);
    }

    // Generate and save metadata
    const metadata = await this.generateMetadata(page, options, filename);
    const metadataPath = filepath.replace('.png', '.json');
    fs.writeFileSync(metadataPath, JSON.stringify(metadata, null, 2));

    // Track captured steps
    this.capturedSteps.push(filename);

    console.log(`📸 Step ${step}: ${title} → ${filename}`);

    return filepath;
  }

  /**
   * Add visual annotations to the page before screenshot
   * Uses Playwright locators to find elements (supports text selectors)
   * then creates DOM overlays for the screenshot
   */
  private async addAnnotations(page: Page, annotations: Annotation[]): Promise<void> {
    // First, add the annotation styles
    await page.evaluate(() => {
      const style = document.createElement('style');
      style.id = 'screenshot-annotation-styles';
      style.textContent = `
        .screenshot-annotation {
          position: fixed;
          pointer-events: none;
          z-index: 99999;
          box-sizing: border-box;
        }
        .screenshot-annotation-label {
          position: absolute;
          top: -28px;
          left: -2px;
          background: var(--annotation-color);
          color: white;
          padding: 4px 10px;
          font-size: 12px;
          font-weight: 600;
          font-family: system-ui, -apple-system, sans-serif;
          border-radius: 4px;
          white-space: nowrap;
          box-shadow: 0 2px 4px rgba(0,0,0,0.2);
        }
        .screenshot-annotation-box {
          border: 3px solid var(--annotation-color);
          border-radius: 6px;
          background: rgba(59, 130, 246, 0.05);
        }
      `;
      document.head.appendChild(style);
    });

    // Then, find each element using Playwright's locator and create overlays
    for (let index = 0; index < annotations.length; index++) {
      const annotation = annotations[index];
      const color = annotation.color || '#3B82F6';

      // Try multiple selectors if provided (comma-separated)
      const selectors = annotation.selector.split(',').map(s => s.trim());
      let boundingBox: { x: number; y: number; width: number; height: number } | null = null;

      for (const selector of selectors) {
        try {
          const locator = page.locator(selector).first();
          const isVisible = await locator.isVisible({ timeout: 500 }).catch(() => false);
          if (isVisible) {
            boundingBox = await locator.boundingBox();
            if (boundingBox) break;
          }
        } catch {
          // Selector not found or error, try next
          continue;
        }
      }

      if (!boundingBox) {
        console.warn(`Annotation target not found: ${annotation.selector}`);
        continue;
      }

      // Create the annotation overlay in the browser
      await page.evaluate(
        ({ index, color, label, box }) => {
          const overlay = document.createElement('div');
          overlay.className = 'screenshot-annotation screenshot-annotation-box';
          overlay.id = `screenshot-annotation-${index}`;
          overlay.style.cssText = `
            --annotation-color: ${color};
            top: ${box.y - 4}px;
            left: ${box.x - 4}px;
            width: ${box.width + 8}px;
            height: ${box.height + 8}px;
            border-color: ${color};
          `;

          const labelEl = document.createElement('div');
          labelEl.className = 'screenshot-annotation-label';
          labelEl.textContent = label;
          labelEl.style.background = color;
          overlay.appendChild(labelEl);

          document.body.appendChild(overlay);
        },
        { index, color, label: annotation.label, box: boundingBox }
      );
    }
  }

  /**
   * Remove all annotations from the page
   */
  private async removeAnnotations(page: Page): Promise<void> {
    await page.evaluate(() => {
      // Remove style element
      const style = document.getElementById('screenshot-annotation-styles');
      style?.remove();

      // Remove all annotation overlays
      document.querySelectorAll('.screenshot-annotation').forEach(el => el.remove());
    });
  }

  /**
   * Generate metadata for the screenshot
   */
  private async generateMetadata(
    page: Page,
    options: CaptureOptions,
    filename: string
  ): Promise<ScreenshotMetadata> {
    const { step, title, description, annotations = [], fullPage = true } = options;
    const viewport = page.viewportSize() || { width: 1280, height: 900 };

    const totalSteps = this.config.totalSteps || step;
    const previousStep =
      step > 1
        ? `${String(step - 1).padStart(2, '0')}-${this.capturedSteps[step - 2]?.split('-').slice(1).join('-') || 'previous'}`
        : null;

    return {
      screenshot: {
        id: `${this.config.role}-${this.config.flow}-${String(step).padStart(2, '0')}`,
        filename,
        flow: this.config.flow,
        role: this.config.role,
        step,
        totalSteps,
        title,
        description,
      },
      capture: {
        timestamp: new Date().toISOString(),
        viewport,
        browser: 'chromium',
        fullPage,
      },
      interaction: {
        previousAction: options.previousAction || null,
        nextAction: options.nextAction || null,
        highlightedElements: annotations.map(a => ({
          selector: a.selector,
          type: a.type || 'box',
          label: a.label,
        })),
      },
      navigation: {
        url: page.url(),
        previousStep,
        nextStep: null, // Will be updated by generate-index script
      },
    };
  }

  /**
   * Set the total number of steps (called after flow completion)
   */
  setTotalSteps(total: number): void {
    this.config.totalSteps = total;
  }

  /**
   * Get list of captured screenshots
   */
  getCapturedSteps(): string[] {
    return [...this.capturedSteps];
  }

  /**
   * Ensure directory exists, create if not
   */
  private ensureDirectoryExists(dir: string): void {
    if (!fs.existsSync(dir)) {
      fs.mkdirSync(dir, { recursive: true });
    }
  }

  /**
   * Convert title to URL-friendly slug
   */
  private slugify(text: string): string {
    return text
      .toLowerCase()
      .replace(/[^\w\s-]/g, '')
      .replace(/\s+/g, '-')
      .replace(/-+/g, '-')
      .trim();
  }
}

// ============================================================================
// Flow Configuration Types
// ============================================================================

export interface FlowConfig {
  name: string;
  role: 'user' | 'editor' | 'admin';
  outputDir: string;
  description: string;
}

export const USER_FLOWS: Record<string, FlowConfig> = {
  authentication: {
    name: 'authentication',
    role: 'user',
    outputDir: 'docs/screenshots/user-flows/authentication',
    description: 'User authentication flows including login, registration, OAuth, and 2FA',
  },
  gameDiscovery: {
    name: 'game-discovery',
    role: 'user',
    outputDir: 'docs/screenshots/user-flows/game-discovery',
    description: 'Game browsing, searching, and viewing game details',
  },
  libraryManagement: {
    name: 'library-management',
    role: 'user',
    outputDir: 'docs/screenshots/user-flows/library-management',
    description: 'Managing personal game library - add, remove, organize games',
  },
  aiChat: {
    name: 'ai-chat',
    role: 'user',
    outputDir: 'docs/screenshots/user-flows/ai-chat',
    description: 'AI chat interactions - asking questions, viewing history, exporting',
  },
  gameSessions: {
    name: 'game-sessions',
    role: 'user',
    outputDir: 'docs/screenshots/user-flows/game-sessions',
    description: 'Game session management - create, track state, player mode',
  },
};

export const EDITOR_FLOWS: Record<string, FlowConfig> = {
  gameManagement: {
    name: 'game-management',
    role: 'editor',
    outputDir: 'docs/screenshots/editor-flows/game-management',
    description: 'Creating and managing games in the shared catalog',
  },
  documentManagement: {
    name: 'document-management',
    role: 'editor',
    outputDir: 'docs/screenshots/editor-flows/document-management',
    description: 'Uploading and processing game documents and PDFs',
  },
  contentManagement: {
    name: 'content-management',
    role: 'editor',
    outputDir: 'docs/screenshots/editor-flows/content-management',
    description: 'Managing FAQ, errata, and quick questions',
  },
  publicationWorkflow: {
    name: 'publication-workflow',
    role: 'editor',
    outputDir: 'docs/screenshots/editor-flows/publication-workflow',
    description: 'Submitting games for approval and publication',
  },
};

export const ADMIN_FLOWS: Record<string, FlowConfig> = {
  approvalWorkflow: {
    name: 'approval-workflow',
    role: 'admin',
    outputDir: 'docs/screenshots/admin-flows/approval-workflow',
    description: 'Reviewing and approving/rejecting submitted content',
  },
  userManagement: {
    name: 'user-management',
    role: 'admin',
    outputDir: 'docs/screenshots/admin-flows/user-management',
    description: 'Managing users - tiers, permissions, suspension',
  },
  systemConfiguration: {
    name: 'system-configuration',
    role: 'admin',
    outputDir: 'docs/screenshots/admin-flows/system-configuration',
    description: 'System settings - quotas, features, limits',
  },
  monitoring: {
    name: 'monitoring',
    role: 'admin',
    outputDir: 'docs/screenshots/admin-flows/monitoring',
    description: 'System monitoring - dashboard, alerts, logs',
  },
};

// ============================================================================
// Utility Functions
// ============================================================================

/**
 * Create a ScreenshotHelper for a specific flow
 */
export function createFlowHelper(flowConfig: FlowConfig): ScreenshotHelper {
  return new ScreenshotHelper({
    outputDir: flowConfig.outputDir,
    flow: flowConfig.name,
    role: flowConfig.role,
  });
}

/**
 * Wait for page to be stable for screenshots
 */
export async function waitForStableState(page: Page): Promise<void> {
  await page.waitForLoadState('domcontentloaded');
  await page.waitForTimeout(100);
  // Wait for any lazy-loaded images
  await page.evaluate(() => {
    return Promise.all(
      Array.from(document.images)
        .filter(img => !img.complete)
        .map(
          img =>
            new Promise(resolve => {
              img.onload = img.onerror = resolve;
            })
        )
    );
  });
}

/**
 * Disable animations for stable screenshots
 */
export async function disableAnimations(page: Page): Promise<void> {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.addStyleTag({
    content: `
      *, *::before, *::after {
        animation-duration: 0s !important;
        animation-delay: 0s !important;
        transition-duration: 0s !important;
        transition-delay: 0s !important;
      }
    `,
  });
}
