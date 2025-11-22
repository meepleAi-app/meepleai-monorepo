#!/usr/bin/env node
/**
 * Automated Inline Styles Migration Script
 * Converts common inline style patterns to Tailwind CSS classes
 *
 * Usage: node scripts/migrate-inline-styles.js <file-path>
 */

const fs = require('fs');
const path = require('path');

// Mapping of inline styles to Tailwind classes
const STYLE_MAPPINGS = [
  // Padding
  { pattern: /padding:\s*4\b/g, replacement: 'p-1' },
  { pattern: /padding:\s*8\b/g, replacement: 'p-2' },
  { pattern: /padding:\s*12\b/g, replacement: 'p-3' },
  { pattern: /padding:\s*16\b/g, replacement: 'p-4' },
  { pattern: /padding:\s*20\b/g, replacement: 'p-5' },
  { pattern: /padding:\s*24\b/g, replacement: 'p-6' },
  { pattern: /padding:\s*32\b/g, replacement: 'p-8' },
  { pattern: /padding:\s*"(\d+)px"/g, replacement: (match, p1) => {
    const val = parseInt(p1);
    if (val === 4) return 'p-1';
    if (val === 8) return 'p-2';
    if (val === 12) return 'p-3';
    if (val === 16) return 'p-4';
    if (val === 20) return 'p-5';
    if (val === 24) return 'p-6';
    if (val === 32) return 'p-8';
    return `p-[${p1}px]`;
  }},

  // Margin
  { pattern: /margin:\s*0\b/g, replacement: 'm-0' },
  { pattern: /margin:\s*"0 auto"/g, replacement: 'mx-auto' },
  { pattern: /margin:\s*"(\d+)px 0"/g, replacement: (match, p1) => {
    const val = parseInt(p1);
    if (val === 8) return 'my-2';
    if (val === 16) return 'my-4';
    if (val === 24) return 'my-6';
    return `my-[${p1}px]`;
  }},
  { pattern: /marginBottom:\s*8\b/g, replacement: 'mb-2' },
  { pattern: /marginBottom:\s*12\b/g, replacement: 'mb-3' },
  { pattern: /marginBottom:\s*16\b/g, replacement: 'mb-4' },
  { pattern: /marginBottom:\s*24\b/g, replacement: 'mb-6' },
  { pattern: /marginTop:\s*0\b/g, replacement: 'mt-0' },
  { pattern: /marginTop:\s*12\b/g, replacement: 'mt-3' },
  { pattern: /marginTop:\s*16\b/g, replacement: 'mt-4' },

  // Gap
  { pattern: /gap:\s*4\b/g, replacement: 'gap-1' },
  { pattern: /gap:\s*8\b/g, replacement: 'gap-2' },
  { pattern: /gap:\s*12\b/g, replacement: 'gap-3' },
  { pattern: /gap:\s*16\b/g, replacement: 'gap-4' },

  // Display & Layout
  { pattern: /display:\s*"flex"/g, replacement: 'flex' },
  { pattern: /display:\s*"grid"/g, replacement: 'grid' },
  { pattern: /display:\s*"inline-block"/g, replacement: 'inline-block' },
  { pattern: /flexDirection:\s*"column"/g, replacement: 'flex-col' },
  { pattern: /flexDirection:\s*"row"/g, replacement: 'flex-row' },
  { pattern: /justifyContent:\s*"space-between"/g, replacement: 'justify-between' },
  { pattern: /justifyContent:\s*"center"/g, replacement: 'justify-center' },
  { pattern: /alignItems:\s*"center"/g, replacement: 'items-center' },
  { pattern: /alignItems:\s*"flex-start"/g, replacement: 'items-start' },

  // Border Radius
  { pattern: /borderRadius:\s*4\b/g, replacement: 'rounded' },
  { pattern: /borderRadius:\s*6\b/g, replacement: 'rounded-md' },
  { pattern: /borderRadius:\s*8\b/g, replacement: 'rounded-lg' },
  { pattern: /borderRadius:\s*12\b/g, replacement: 'rounded-xl' },

  // Font
  { pattern: /fontSize:\s*12\b/g, replacement: 'text-xs' },
  { pattern: /fontSize:\s*14\b/g, replacement: 'text-sm' },
  { pattern: /fontSize:\s*16\b/g, replacement: 'text-base' },
  { pattern: /fontSize:\s*32\b/g, replacement: 'text-3xl' },
  { pattern: /fontWeight:\s*600\b/g, replacement: 'font-semibold' },
  { pattern: /fontWeight:\s*700\b/g, replacement: 'font-bold' },
  { pattern: /fontFamily:\s*"sans-serif"/g, replacement: 'font-sans' },

  // Sizing
  { pattern: /maxWidth:\s*1400\b/g, replacement: 'max-w-7xl' },
  { pattern: /maxWidth:\s*900\b/g, replacement: 'max-w-3xl' },
  { pattern: /maxWidth:\s*600\b/g, replacement: 'max-w-2xl' },
  { pattern: /width:\s*320\b/g, replacement: 'w-80' },

  // Cursor
  { pattern: /cursor:\s*"pointer"/g, replacement: 'cursor-pointer' },

  // Text Decoration
  { pattern: /textDecoration:\s*"none"/g, replacement: 'no-underline' },

  // Border
  { pattern: /border:\s*"none"/g, replacement: 'border-none' },
  { pattern: /border:\s*"1px solid #dadce0"/g, replacement: 'border border-gray-300' },
  { pattern: /border:\s*"1px solid #e5e7eb"/g, replacement: 'border border-border' },
];

// Color mappings (common colors to Tailwind classes)
const COLOR_MAPPINGS = [
  { pattern: /background:\s*"white"/g, replacement: 'bg-white' },
  { pattern: /background:\s*"#f8f9fa"/g, replacement: 'bg-gray-50' },
  { pattern: /background:\s*"#f9fafb"/g, replacement: 'bg-gray-50' },
  { pattern: /color:\s*"#64748b"/g, replacement: 'text-gray-500' },
  { pattern: /color:\s*"#d93025"/g, replacement: 'text-red-600' },
  { pattern: /color:\s*"#1a73e8"/g, replacement: 'text-blue-600' },
  { pattern: /color:\s*"#34a853"/g, replacement: 'text-green-600' },
  { pattern: /color:\s*"#ea4335"/g, replacement: 'text-red-600' },
  { pattern: /color:\s*"white"/g, replacement: 'text-white' },
];

function migrateFile(filePath) {
  if (!fs.existsSync(filePath)) {
    console.error(`File not found: ${filePath}`);
    process.exit(1);
  }

  let content = fs.readFileSync(filePath, 'utf8');
  let changes = 0;

  // Apply style mappings
  STYLE_MAPPINGS.forEach(({ pattern, replacement }) => {
    const before = content;
    if (typeof replacement === 'function') {
      content = content.replace(pattern, replacement);
    } else {
      content = content.replace(pattern, replacement);
    }
    if (content !== before) changes++;
  });

  // Apply color mappings
  COLOR_MAPPINGS.forEach(({ pattern, replacement }) => {
    const before = content;
    content = content.replace(pattern, replacement);
    if (content !== before) changes++;
  });

  // Write back
  fs.writeFileSync(filePath, content, 'utf8');
  console.log(`✅ Migrated ${filePath}: ${changes} pattern replacements`);
}

// Main
const filePath = process.argv[2];
if (!filePath) {
  console.error('Usage: node scripts/migrate-inline-styles.js <file-path>');
  process.exit(1);
}

migrateFile(filePath);
