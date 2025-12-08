#!/usr/bin/env node
/**
 * Fix Jekyll Liquid syntax errors in markdown files
 * Wraps code blocks containing {{ }} syntax with {% raw %} tags
 */

const fs = require('fs');
const path = require('path');

const filesToFix = [
  'docs/04-frontend/animations-examples.md',
  'docs/04-frontend/improvements/01-ui-analysis.md',
  'docs/04-frontend/improvements/02-improvement-recommendations.md',
  'docs/04-frontend/improvement-roadmap-2025.md',
  'docs/02-development/frontend/GUIDA-SVILUPPATORE-FRONTEND.md',
  'docs/02-development/testing/frontend/known-test-issues.md',
  'docs/02-development/guides/local-debugging-guide.md',
  'docs/02-development/guides/codebase-maintenance.md',
  'docs/02-development/docker-resources-guide.md',
  'docs/02-development/testing/specialized/bgai-039-validation-accuracy-baseline.md',
  'docs/02-development/testing/test-automation-pipeline-guide.md',
  'docs/01-architecture/overview/system-architecture.md',
  'docs/02-development/code-review/infrastructure-comprehensive-review-2025-11-22.md'
];

function wrapCodeBlocks(content) {
  // Match code blocks (```language ... ```)
  const codeBlockRegex = /(```(?:tsx|jsx|typescript|javascript|js|ts|yaml|json)?\n[\s\S]*?\n```)/g;

  return content.replace(codeBlockRegex, (match) => {
    // Check if the code block contains {{ }} or {{. patterns
    if (match.includes('{{') || match.includes('{{.')) {
      // Check if already wrapped
      if (!match.startsWith('{% raw %}')) {
        return `{% raw %}\n${match}\n{% endraw %}`;
      }
    }
    return match;
  });
}

function fixFile(filePath) {
  const fullPath = path.join(process.cwd(), filePath);

  if (!fs.existsSync(fullPath)) {
    console.log(`⚠️  File not found: ${filePath}`);
    return false;
  }

  console.log(`📝 Processing: ${filePath}`);

  const content = fs.readFileSync(fullPath, 'utf8');
  const fixed = wrapCodeBlocks(content);

  if (content !== fixed) {
    fs.writeFileSync(fullPath, fixed, 'utf8');
    console.log(`✅ Fixed: ${filePath}`);
    return true;
  } else {
    console.log(`⏭️  No changes needed: ${filePath}`);
    return false;
  }
}

console.log('🔧 Fixing Jekyll Liquid syntax errors...\n');

let fixedCount = 0;
for (const file of filesToFix) {
  if (fixFile(file)) {
    fixedCount++;
  }
}

console.log(`\n✨ Done! Fixed ${fixedCount} file(s).`);
