// Global teardown for Jest tests
// Runs once after all test suites have completed

import { execSync } from 'child_process';
import { platform } from 'os';

export default async function globalTeardown() {
  console.log('\n🧹 Running cleanup script to kill hanging test processes...');

  try {
    // Only run on Windows (PowerShell script)
    if (platform() === 'win32') {
      const scriptPath = '../../tools/cleanup-test-processes.ps1';
      const command = `powershell.exe -ExecutionPolicy Bypass -File ${scriptPath}`;

      // Execute cleanup script (non-verbose, real execution)
      execSync(command, {
        stdio: 'inherit', // Show output in console
        cwd: __dirname
      });

      console.log('✅ Cleanup script completed');
    } else {
      console.log('ℹ️ Cleanup script skipped (Windows-only)');
    }
  } catch (error) {
    console.error('⚠️ Cleanup script failed (non-fatal):', error.message);
    // Don't fail tests if cleanup fails
  }
}
