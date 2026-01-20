/**
 * Generate Screenshot Index
 *
 * Scans the docs/screenshots directory and generates a comprehensive
 * index.json manifest of all visual documentation screenshots.
 *
 * Usage:
 *   npx ts-node e2e/visual-docs/generate-index.ts
 *   or
 *   pnpm docs:generate-index
 */

import * as fs from 'fs';
import * as path from 'path';

interface ScreenshotMetadata {
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

interface FlowIndex {
  name: string;
  role: 'user' | 'editor' | 'admin';
  description: string;
  directory: string;
  screenshots: ScreenshotMetadata[];
  totalSteps: number;
  generatedAt: string;
}

interface ScreenshotIndex {
  version: string;
  generatedAt: string;
  totalScreenshots: number;
  flows: {
    user: FlowIndex[];
    editor: FlowIndex[];
    admin: FlowIndex[];
  };
  summary: {
    userFlows: number;
    editorFlows: number;
    adminFlows: number;
    totalFlows: number;
  };
}

const SCREENSHOTS_BASE = path.resolve(__dirname, '../../../../docs/screenshots');

/**
 * Scan a flow directory for screenshots and metadata
 */
function scanFlowDirectory(flowDir: string, role: 'user' | 'editor' | 'admin'): FlowIndex | null {
  if (!fs.existsSync(flowDir)) {
    return null;
  }

  const files = fs.readdirSync(flowDir);
  const screenshots: ScreenshotMetadata[] = [];

  // Find all .json metadata files
  const jsonFiles = files.filter(f => f.endsWith('.json') && !f.startsWith('index'));

  for (const jsonFile of jsonFiles) {
    try {
      const metadataPath = path.join(flowDir, jsonFile);
      const metadata = JSON.parse(fs.readFileSync(metadataPath, 'utf-8')) as ScreenshotMetadata;

      // Verify corresponding PNG exists
      const pngFile = jsonFile.replace('.json', '.png');
      if (files.includes(pngFile)) {
        screenshots.push(metadata);
      }
    } catch (error) {
      console.warn(`⚠️ Failed to parse ${jsonFile}:`, error);
    }
  }

  if (screenshots.length === 0) {
    return null;
  }

  // Sort by step number
  screenshots.sort((a, b) => a.screenshot.step - b.screenshot.step);

  // Update navigation links
  for (let i = 0; i < screenshots.length; i++) {
    if (i > 0) {
      screenshots[i].navigation.previousStep = screenshots[i - 1].screenshot.filename;
    }
    if (i < screenshots.length - 1) {
      screenshots[i].navigation.nextStep = screenshots[i + 1].screenshot.filename;
    }
  }

  const flowName = path.basename(flowDir);
  const totalSteps = Math.max(...screenshots.map(s => s.screenshot.totalSteps), screenshots.length);

  return {
    name: flowName,
    role,
    description: getFlowDescription(flowName),
    directory: path.relative(SCREENSHOTS_BASE, flowDir),
    screenshots,
    totalSteps,
    generatedAt: new Date().toISOString(),
  };
}

/**
 * Get human-readable description for a flow
 */
function getFlowDescription(flowName: string): string {
  const descriptions: Record<string, string> = {
    // User flows
    'authentication': 'User authentication flows including login, registration, OAuth, and 2FA',
    'game-discovery': 'Game browsing, searching, and viewing game details',
    'library-management': 'Managing personal game library - add, remove, organize games',
    'ai-chat': 'AI chat interactions - asking questions, viewing history, exporting',
    'game-sessions': 'Game session management - create, track state, player mode',
    // Editor flows
    'game-management': 'Creating and managing games in the shared catalog',
    'document-management': 'Uploading and processing game documents and PDFs',
    'content-management': 'Managing FAQ, errata, and quick questions',
    'publication-workflow': 'Submitting games for approval and publication',
    // Admin flows
    'approval-workflow': 'Reviewing and approving/rejecting submitted content',
    'user-management': 'Managing users - tiers, permissions, suspension',
    'system-configuration': 'System settings - quotas, features, limits',
    'monitoring': 'System monitoring - dashboard, alerts, logs',
  };

  return descriptions[flowName] || `Visual documentation for ${flowName}`;
}

/**
 * Scan all flow directories and build the index
 */
function generateIndex(): ScreenshotIndex {
  const index: ScreenshotIndex = {
    version: '1.0.0',
    generatedAt: new Date().toISOString(),
    totalScreenshots: 0,
    flows: {
      user: [],
      editor: [],
      admin: [],
    },
    summary: {
      userFlows: 0,
      editorFlows: 0,
      adminFlows: 0,
      totalFlows: 0,
    },
  };

  // Scan user flows
  const userFlowsDir = path.join(SCREENSHOTS_BASE, 'user-flows');
  if (fs.existsSync(userFlowsDir)) {
    for (const flowDir of fs.readdirSync(userFlowsDir)) {
      const flowPath = path.join(userFlowsDir, flowDir);
      if (fs.statSync(flowPath).isDirectory()) {
        const flow = scanFlowDirectory(flowPath, 'user');
        if (flow) {
          index.flows.user.push(flow);
          index.totalScreenshots += flow.screenshots.length;
        }
      }
    }
  }

  // Scan editor flows
  const editorFlowsDir = path.join(SCREENSHOTS_BASE, 'editor-flows');
  if (fs.existsSync(editorFlowsDir)) {
    for (const flowDir of fs.readdirSync(editorFlowsDir)) {
      const flowPath = path.join(editorFlowsDir, flowDir);
      if (fs.statSync(flowPath).isDirectory()) {
        const flow = scanFlowDirectory(flowPath, 'editor');
        if (flow) {
          index.flows.editor.push(flow);
          index.totalScreenshots += flow.screenshots.length;
        }
      }
    }
  }

  // Scan admin flows
  const adminFlowsDir = path.join(SCREENSHOTS_BASE, 'admin-flows');
  if (fs.existsSync(adminFlowsDir)) {
    for (const flowDir of fs.readdirSync(adminFlowsDir)) {
      const flowPath = path.join(adminFlowsDir, flowDir);
      if (fs.statSync(flowPath).isDirectory()) {
        const flow = scanFlowDirectory(flowPath, 'admin');
        if (flow) {
          index.flows.admin.push(flow);
          index.totalScreenshots += flow.screenshots.length;
        }
      }
    }
  }

  // Update summary
  index.summary.userFlows = index.flows.user.length;
  index.summary.editorFlows = index.flows.editor.length;
  index.summary.adminFlows = index.flows.admin.length;
  index.summary.totalFlows =
    index.summary.userFlows + index.summary.editorFlows + index.summary.adminFlows;

  return index;
}

/**
 * Main execution
 */
function main(): void {
  console.log('📸 Generating screenshot index...\n');

  // Ensure base directory exists
  if (!fs.existsSync(SCREENSHOTS_BASE)) {
    console.log('⚠️ Screenshots directory does not exist. Run tests first:');
    console.log('   pnpm test:visual-docs\n');
    fs.mkdirSync(SCREENSHOTS_BASE, { recursive: true });
  }

  const index = generateIndex();

  // Write index file
  const indexPath = path.join(SCREENSHOTS_BASE, 'index.json');
  fs.writeFileSync(indexPath, JSON.stringify(index, null, 2));

  // Print summary
  console.log('✅ Index generated successfully!\n');
  console.log('📊 Summary:');
  console.log(`   User Flows:   ${index.summary.userFlows}`);
  console.log(`   Editor Flows: ${index.summary.editorFlows}`);
  console.log(`   Admin Flows:  ${index.summary.adminFlows}`);
  console.log(`   Total Flows:  ${index.summary.totalFlows}`);
  console.log(`   Screenshots:  ${index.totalScreenshots}`);
  console.log(`\n📁 Index saved to: ${indexPath}`);

  // Generate markdown summary
  generateMarkdownSummary(index);
}

/**
 * Generate a markdown summary file
 */
function generateMarkdownSummary(index: ScreenshotIndex): void {
  let markdown = `# Visual Documentation Index

> Auto-generated on ${new Date().toLocaleDateString()}

## Summary

| Role | Flows | Screenshots |
|------|-------|-------------|
| User | ${index.summary.userFlows} | ${index.flows.user.reduce((sum, f) => sum + f.screenshots.length, 0)} |
| Editor | ${index.summary.editorFlows} | ${index.flows.editor.reduce((sum, f) => sum + f.screenshots.length, 0)} |
| Admin | ${index.summary.adminFlows} | ${index.flows.admin.reduce((sum, f) => sum + f.screenshots.length, 0)} |
| **Total** | **${index.summary.totalFlows}** | **${index.totalScreenshots}** |

`;

  // User flows
  if (index.flows.user.length > 0) {
    markdown += `## User Flows\n\n`;
    for (const flow of index.flows.user) {
      markdown += `### ${flow.name.replace(/-/g, ' ').replace(/\b\w/g, c => c.toUpperCase())}\n\n`;
      markdown += `${flow.description}\n\n`;
      markdown += `| Step | Screenshot | Description |\n`;
      markdown += `|------|------------|-------------|\n`;
      for (const ss of flow.screenshots) {
        markdown += `| ${ss.screenshot.step} | [${ss.screenshot.filename}](${flow.directory}/${ss.screenshot.filename}) | ${ss.screenshot.title} |\n`;
      }
      markdown += `\n`;
    }
  }

  // Editor flows
  if (index.flows.editor.length > 0) {
    markdown += `## Editor Flows\n\n`;
    for (const flow of index.flows.editor) {
      markdown += `### ${flow.name.replace(/-/g, ' ').replace(/\b\w/g, c => c.toUpperCase())}\n\n`;
      markdown += `${flow.description}\n\n`;
      markdown += `| Step | Screenshot | Description |\n`;
      markdown += `|------|------------|-------------|\n`;
      for (const ss of flow.screenshots) {
        markdown += `| ${ss.screenshot.step} | [${ss.screenshot.filename}](${flow.directory}/${ss.screenshot.filename}) | ${ss.screenshot.title} |\n`;
      }
      markdown += `\n`;
    }
  }

  // Admin flows
  if (index.flows.admin.length > 0) {
    markdown += `## Admin Flows\n\n`;
    for (const flow of index.flows.admin) {
      markdown += `### ${flow.name.replace(/-/g, ' ').replace(/\b\w/g, c => c.toUpperCase())}\n\n`;
      markdown += `${flow.description}\n\n`;
      markdown += `| Step | Screenshot | Description |\n`;
      markdown += `|------|------------|-------------|\n`;
      for (const ss of flow.screenshots) {
        markdown += `| ${ss.screenshot.step} | [${ss.screenshot.filename}](${flow.directory}/${ss.screenshot.filename}) | ${ss.screenshot.title} |\n`;
      }
      markdown += `\n`;
    }
  }

  markdown += `---\n\n*Generated with visual-docs test suite*\n`;

  const mdPath = path.join(SCREENSHOTS_BASE, 'README.md');
  fs.writeFileSync(mdPath, markdown);
  console.log(`📝 Markdown summary saved to: ${mdPath}`);
}

// Run if executed directly
main();
