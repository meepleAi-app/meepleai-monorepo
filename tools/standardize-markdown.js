#!/usr/bin/env node

/**
 * Standardizza la formattazione Markdown dei file di documentazione
 * - Aggiunge tag linguaggio ai code blocks
 * - Corregge heading hierarchy
 * - Standardizza la formattazione
 */

const fs = require('fs');
const path = require('path');

// Mappa dei pattern comuni per identificare il linguaggio del code block
const languagePatterns = {
  csharp: [
    /^(public|private|internal|protected)\s+(class|interface|enum|struct)/m,
    /^using\s+[\w.]+;/m,
    /^namespace\s+[\w.]+/m,
    /^\[Test\]|\[Fact\]|\[Theory\]/m,
    /\.cs$/,
    /dotnet\s+(build|test|run)/,
  ],
  typescript: [
    /^(export\s+)?(interface|type|enum)\s+\w+/m,
    /^import\s+.*from\s+['"].*['"]/m,
    /:\s*(string|number|boolean|void|any)/m,
    /React\.(FC|Component)/,
  ],
  javascript: [
    /^const\s+\w+\s+=\s+require\(/m,
    /^module\.exports\s*=/m,
    /^function\s+\w+\s*\(/m,
  ],
  bash: [
    /^(cd|ls|mkdir|rm|cp|mv|cat|grep|find|docker|dotnet|pnpm|npm|git)\s+/m,
    /^\$\s+/m,
    /^#!\//m,
  ],
  sql: [
    /^(SELECT|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP)\s+/mi,
    /FROM\s+\w+/i,
    /WHERE\s+/i,
  ],
  json: [
    /^\s*[\{\[]/,
    /:\s*[\{\[\"\d]/,
  ],
  yaml: [
    /^\w+:\s*$/m,
    /^  \w+:/m,
  ],
  powershell: [
    /^\$\w+\s*=/m,
    /^(Get|Set|New|Remove|Invoke|Write|Test)-\w+/m,
    /^pwsh\s+/m,
  ],
};

function detectLanguage(code) {
  // Rimuovi spazi iniziali/finali
  code = code.trim();

  // Controlla pattern specifici
  for (const [lang, patterns] of Object.entries(languagePatterns)) {
    for (const pattern of patterns) {
      if (pattern.test(code)) {
        return lang;
      }
    }
  }

  // Default: nessun linguaggio rilevato
  return null;
}

function standardizeCodeBlocks(content) {
  let modified = false;

  // Trova code blocks senza tag linguaggio
  const codeBlockRegex = /^```\s*\n([\s\S]*?)^```/gm;

  content = content.replace(codeBlockRegex, (match, code) => {
    const detectedLang = detectLanguage(code);

    if (detectedLang) {
      modified = true;
      return '```' + detectedLang + '\n' + code + '```';
    }

    return match;
  });

  return { content, modified };
}

function fixHeadingHierarchy(content, filePath) {
  const lines = content.split('\n');
  const headings = [];
  let modified = false;

  // Trova tutti gli heading
  lines.forEach((line, index) => {
    const match = line.match(/^(#{1,6})\s+(.+)$/);
    if (match) {
      headings.push({
        line: index,
        level: match[1].length,
        text: match[2],
        original: line,
      });
    }
  });

  // Verifica gerarchia: il primo heading dovrebbe essere h1
  if (headings.length > 0 && headings[0].level !== 1) {
    console.log(`  ⚠️  First heading is h${headings[0].level}, should be h1: "${headings[0].text}"`);
  }

  // Verifica che non ci siano salti di livello > 1
  for (let i = 1; i < headings.length; i++) {
    const prev = headings[i - 1];
    const curr = headings[i];

    if (curr.level - prev.level > 1) {
      console.log(`  ⚠️  Heading level jump from h${prev.level} to h${curr.level} at line ${curr.line + 1}`);
    }
  }

  return { content, modified };
}

function standardizeMarkdown(filePath) {
  const content = fs.readFileSync(filePath, 'utf-8');
  let result = content;
  let changes = [];

  // Standardizza code blocks
  const codeBlockResult = standardizeCodeBlocks(result);
  if (codeBlockResult.modified) {
    result = codeBlockResult.content;
    changes.push('code blocks');
  }

  // Verifica heading hierarchy (non modifica, solo report)
  fixHeadingHierarchy(result, filePath);

  // Scrivi il file se modificato
  if (changes.length > 0) {
    fs.writeFileSync(filePath, result, 'utf-8');
    console.log(`✓ ${path.relative(process.cwd(), filePath)}: ${changes.join(', ')}`);
    return true;
  }

  return false;
}

function findMarkdownFiles(dir, fileList = []) {
  const files = fs.readdirSync(dir);

  files.forEach(file => {
    const filePath = path.join(dir, file);
    const stat = fs.statSync(filePath);

    if (stat.isDirectory()) {
      findMarkdownFiles(filePath, fileList);
    } else if (path.extname(file) === '.md') {
      fileList.push(filePath);
    }
  });

  return fileList;
}

// Main
console.log('Standardizing Markdown files...\n');

const docsDir = path.join(process.cwd(), 'docs');
const files = findMarkdownFiles(docsDir);
let modifiedCount = 0;

files.forEach(file => {
  if (standardizeMarkdown(file)) {
    modifiedCount++;
  }
});

console.log(`\n✓ Processed ${files.length} files, modified ${modifiedCount}`);
