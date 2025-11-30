#!/usr/bin/env tsx
/**
 * Automated Component Test Generator
 *
 * Generates baseline Jest tests for React components without existing tests.
 * Part of Issue #992: Frontend component testing (Jest 90%+)
 *
 * Strategy:
 * - Parse component file to extract structure
 * - Generate minimal test suite (smoke + props validation)
 * - Create __tests__ directory if needed
 * - Follow existing test patterns
 *
 * Usage:
 *   tsx tools/generate-component-tests.ts [--dry-run] [--component path/to/Component.tsx]
 */

import * as fs from 'fs';
import * as path from 'path';
import { execSync } from 'child_process';

// ============================================================================
// Configuration
// ============================================================================

const APPS_WEB_ROOT = path.join(__dirname, '../apps/web');
const COMPONENTS_DIR = path.join(APPS_WEB_ROOT, 'src/components');

interface ComponentInfo {
  filePath: string;
  componentName: string;
  hasProps: boolean;
  propsInterface?: string;
  hasEvents: boolean;
  isFormComponent: boolean;
  directory: string;
}

// ============================================================================
// Component Analysis
// ============================================================================

function analyzeComponent(filePath: string): ComponentInfo | null {
  const content = fs.readFileSync(filePath, 'utf-8');
  const fileName = path.basename(filePath, '.tsx');
  const directory = path.dirname(filePath);

  // Skip if already has tests
  const testDir = path.join(directory, '__tests__');
  const possibleTestFiles = [
    path.join(testDir, `${fileName}.test.tsx`),
    path.join(testDir, `${fileName}.rendering.test.tsx`),
    path.join(testDir, `${fileName}.interactions.test.tsx`),
  ];

  if (possibleTestFiles.some(f => fs.existsSync(f))) {
    return null; // Already has tests
  }

  // Extract component info
  const componentName = fileName;
  const hasProps = content.includes('interface') || content.includes('type') && content.includes('Props');
  const propsInterfaceMatch = content.match(/(?:interface|type)\s+(\w+Props)\s*[={]/);
  const propsInterface = propsInterfaceMatch ? propsInterfaceMatch[1] : undefined;

  const hasEvents = /on[A-Z]\w+/.test(content); // onClick, onChange, etc.
  const isFormComponent = content.includes('useForm') || content.includes('react-hook-form');

  return {
    filePath,
    componentName,
    hasProps,
    propsInterface,
    hasEvents,
    isFormComponent,
    directory,
  };
}

// ============================================================================
// Test Template Generation
// ============================================================================

function generateTestTemplate(info: ComponentInfo): string {
  const { componentName, hasProps, propsInterface, hasEvents, isFormComponent } = info;

  const importPath = `../${componentName}`;

  let template = `/**
 * Tests for ${componentName} component
 * Auto-generated baseline tests - Issue #992
 * TODO: Enhance with component-specific test cases
 */

import { render, screen } from '@testing-library/react';
${hasEvents ? "import userEvent from '@testing-library/user-event';\n" : ""}import { ${componentName} } from '${importPath}';

describe('${componentName}', () => {
  describe('Rendering', () => {
    it('should render without crashing', () => {
      ${generateRenderCall(info)}
      expect(screen.getByRole('${guessRole(componentName)}')).toBeInTheDocument();
    });

    it('should render with default props', () => {
      const { container } = ${generateRenderCall(info)}
      expect(container.firstChild).toBeInTheDocument();
    });
  });
`;

  // Add props validation tests if component has props
  if (hasProps && propsInterface) {
    template += `
  describe('Props', () => {
    it('should accept and render with custom props', () => {
      // TODO: Add specific prop tests based on ${propsInterface}
      ${generateRenderCall(info)}
      expect(screen.getByRole('${guessRole(componentName)}')).toBeInTheDocument();
    });
  });
`;
  }

  // Add interaction tests if component has event handlers
  if (hasEvents) {
    template += `
  describe('Interactions', () => {
    it('should handle user interactions', async () => {
      const user = userEvent.setup();
      ${generateRenderCall(info)}

      // TODO: Add interaction tests (click, input, etc.)
      // Example: await user.click(screen.getByRole('button'));
    });
  });
`;
  }

  // Add form-specific tests
  if (isFormComponent) {
    template += `
  describe('Form Behavior', () => {
    it('should handle form submission', async () => {
      const handleSubmit = vi.fn();
      ${generateRenderWithHandler(info, 'handleSubmit')}

      // TODO: Fill form fields and submit
      // const submitButton = screen.getByRole('button', { name: /submit/i });
      // await user.click(submitButton);
      // expect(handleSubmit).toHaveBeenCalled();
    });

    it('should validate form fields', async () => {
      ${generateRenderCall(info)}

      // TODO: Test validation rules
    });
  });
`;
  }

  template += `
  describe('Accessibility', () => {
    it('should have accessible role', () => {
      ${generateRenderCall(info)}
      expect(screen.getByRole('${guessRole(componentName)}')).toBeInTheDocument();
    });

    // TODO: Add more a11y tests (aria-labels, keyboard navigation, etc.)
  });
});
`;

  return template;
}

function generateRenderCall(info: ComponentInfo): string {
  const { componentName, hasProps } = info;

  if (!hasProps) {
    return `render(<${componentName} />);`;
  }

  // Generate minimal props
  return `render(<${componentName} ${generateMinimalProps(info)} />);`;
}

function generateRenderWithHandler(info: ComponentInfo, handlerName: string): string {
  const { componentName } = info;
  // Don't duplicate onSubmit if minimal props already includes it
  const minimalProps = generateMinimalProps(info);
  const hasOnSubmit = minimalProps.includes('onSubmit');

  if (hasOnSubmit) {
    return `render(<${componentName} ${minimalProps.replace('onSubmit={vi.fn()}', `onSubmit={${handlerName}}`)} />);`;
  }
  return `render(<${componentName} onSubmit={${handlerName}} ${minimalProps} />);`;
}

function generateMinimalProps(info: ComponentInfo): string {
  const { componentName } = info;

  // Common prop patterns
  if (componentName.toLowerCase().includes('button')) {
    return 'children="Test Button"';
  }

  if (componentName.toLowerCase().includes('modal')) {
    return 'isOpen={true} onClose={vi.fn()}';
  }

  if (componentName.toLowerCase().includes('form')) {
    return 'onSubmit={vi.fn()}';
  }

  if (componentName.toLowerCase().includes('input')) {
    return 'value="" onChange={vi.fn()}';
  }

  // Default: children prop
  return 'children={<div>Test Content</div>}';
}

function guessRole(componentName: string): string {
  const lower = componentName.toLowerCase();

  if (lower.includes('button')) return 'button';
  if (lower.includes('input')) return 'textbox';
  if (lower.includes('select')) return 'combobox';
  if (lower.includes('checkbox')) return 'checkbox';
  if (lower.includes('radio')) return 'radio';
  if (lower.includes('link')) return 'link';
  if (lower.includes('heading')) return 'heading';
  if (lower.includes('dialog') || lower.includes('modal')) return 'dialog';
  if (lower.includes('alert')) return 'alert';
  if (lower.includes('form')) return 'form';

  // Default to generic
  return 'region';
}

// ============================================================================
// File System Operations
// ============================================================================

function createTestFile(info: ComponentInfo, dryRun: boolean = false): void {
  const { directory, componentName } = info;
  const testDir = path.join(directory, '__tests__');
  const testFilePath = path.join(testDir, `${componentName}.test.tsx`);

  // Create __tests__ directory if needed
  if (!fs.existsSync(testDir)) {
    if (!dryRun) {
      fs.mkdirSync(testDir, { recursive: true });
      console.log(`  ✓ Created directory: ${path.relative(APPS_WEB_ROOT, testDir)}`);
    } else {
      console.log(`  [DRY RUN] Would create: ${path.relative(APPS_WEB_ROOT, testDir)}`);
    }
  }

  // Generate test content
  const testContent = generateTestTemplate(info);

  if (!dryRun) {
    fs.writeFileSync(testFilePath, testContent, 'utf-8');
    console.log(`  ✓ Generated test: ${path.relative(APPS_WEB_ROOT, testFilePath)}`);
  } else {
    console.log(`  [DRY RUN] Would create: ${path.relative(APPS_WEB_ROOT, testFilePath)}`);
  }
}

// ============================================================================
// Main Execution
// ============================================================================

function findUntestedComponents(): string[] {
  const untestedFile = '/tmp/untested-components.txt';

  // Check if we already have the list
  if (fs.existsSync(untestedFile)) {
    return fs.readFileSync(untestedFile, 'utf-8')
      .split('\n')
      .filter(line => line.trim())
      .map(line => path.join(APPS_WEB_ROOT, line.trim()));
  }

  // Otherwise, find them
  console.log('🔍 Finding untested components...');
  const result = execSync(
    `cd ${APPS_WEB_ROOT} && bash /tmp/find-untested.sh`,
    { encoding: 'utf-8' }
  );

  const components = result
    .split('\n')
    .filter(line => line.trim())
    .map(line => path.join(APPS_WEB_ROOT, line.trim()));

  console.log(`Found ${components.length} untested components\n`);
  return components;
}

function main() {
  const args = process.argv.slice(2);
  const dryRun = args.includes('--dry-run');
  const specificComponent = args.find(arg => arg.startsWith('--component='))?.split('=')[1];

  console.log('🧪 Component Test Generator - Issue #992\n');
  console.log(`Mode: ${dryRun ? 'DRY RUN' : 'GENERATE'}\n`);

  let componentsToProcess: string[];

  if (specificComponent) {
    componentsToProcess = [path.join(APPS_WEB_ROOT, specificComponent)];
  } else {
    componentsToProcess = findUntestedComponents();
  }

  let generated = 0;
  let skipped = 0;
  let errors = 0;

  for (const componentPath of componentsToProcess) {
    try {
      if (!fs.existsSync(componentPath)) {
        console.log(`⚠️  File not found: ${componentPath}`);
        skipped++;
        continue;
      }

      const info = analyzeComponent(componentPath);

      if (!info) {
        console.log(`⏭️  Skipped (already has tests): ${path.basename(componentPath)}`);
        skipped++;
        continue;
      }

      console.log(`\n📝 Processing: ${info.componentName}`);
      createTestFile(info, dryRun);
      generated++;

    } catch (error) {
      console.error(`❌ Error processing ${componentPath}:`, error);
      errors++;
    }
  }

  console.log('\n' + '='.repeat(60));
  console.log('📊 Summary:');
  console.log(`  Generated: ${generated}`);
  console.log(`  Skipped:   ${skipped}`);
  console.log(`  Errors:    ${errors}`);
  console.log('='.repeat(60));

  if (dryRun) {
    console.log('\n💡 Run without --dry-run to generate test files');
  } else {
    console.log('\n✅ Test generation complete!');
    console.log('📋 Next steps:');
    console.log('  1. Review generated tests');
    console.log('  2. Run: cd apps/web && pnpm test');
    console.log('  3. Enhance critical component tests (Auth, Chat)');
  }
}

// Run if executed directly
if (require.main === module) {
  main();
}
