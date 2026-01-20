/**
 * Global Setup for Visual Documentation Tests
 *
 * Cleans all screenshot directories once at the start of the test run.
 * This ensures screenshots are overwritten rather than accumulated.
 */

import * as fs from 'fs';
import * as path from 'path';

const SCREENSHOTS_BASE = path.resolve(__dirname, '../../../../docs/screenshots');

const FLOW_DIRECTORIES = [
  'user-flows/authentication',
  'user-flows/game-discovery',
  'user-flows/library-management',
  'user-flows/ai-chat',
  'user-flows/game-sessions',
  'editor-flows/game-management',
  'editor-flows/document-management',
  'editor-flows/content-management',
  'editor-flows/publication-workflow',
  'admin-flows/approval-workflow',
  'admin-flows/user-management',
  'admin-flows/system-configuration',
  'admin-flows/monitoring',
];

function cleanDirectory(dir: string): void {
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
    return;
  }

  const files = fs.readdirSync(dir);
  let cleaned = 0;
  for (const file of files) {
    if (file.endsWith('.png') || file.endsWith('.json')) {
      const filePath = path.join(dir, file);
      fs.unlinkSync(filePath);
      cleaned++;
    }
  }
  if (cleaned > 0) {
    console.log(`🧹 Cleaned ${cleaned} files from ${path.basename(dir)}`);
  }
}

async function globalSetup(): Promise<void> {
  console.log('\n📸 Visual Documentation - Global Setup\n');
  console.log('Cleaning screenshot directories...\n');

  for (const flowDir of FLOW_DIRECTORIES) {
    const fullPath = path.join(SCREENSHOTS_BASE, flowDir);
    cleanDirectory(fullPath);
  }

  console.log('\n✅ Directories ready for screenshot capture\n');
}

export default globalSetup;
