#!/usr/bin/env node
/**
 * Generate PDF documentation organized by topic.
 * Merges markdown files per topic, strips code examples, converts to PDF.
 */

import { readFileSync, writeFileSync, mkdirSync, existsSync, readdirSync, statSync } from 'fs';
import { join, basename, relative } from 'path';
import { execFileSync } from 'child_process';

const DOCS_ROOT = join(process.cwd(), 'docs');
const OUTPUT_DIR = join(process.cwd(), 'docs', '_pdf-output');

// Topic definitions: name -> dirs relative to docs/
const TOPICS = [
  {
    name: '01-Architettura-e-Design',
    title: 'MeepleAI - Architettura e Design del Sistema',
    description: 'Architettura del sistema, decisioni architetturali (ADR), Domain-Driven Design, diagrammi e componenti.',
    dirs: ['architecture'],
  },
  {
    name: '02-Bounded-Contexts',
    title: 'MeepleAI - Bounded Contexts (DDD)',
    description: 'Documentazione dei 15 bounded context del dominio, responsabilita, interazioni e diagrammi.',
    dirs: ['bounded-contexts'],
  },
  {
    name: '03-Sistema-RAG',
    title: 'MeepleAI - Sistema RAG (Retrieval-Augmented Generation)',
    description: 'Architettura RAG a 6 layer, varianti di retrieval, plugin, appendici e configurazione admin.',
    dirs: ['api/rag'],
  },
  {
    name: '04-API-Reference',
    title: 'MeepleAI - API Reference',
    description: 'Documentazione API REST, endpoint, health check, permessi, rate limiting, session tracking.',
    dirs: ['api'],
    excludeDirs: ['api/rag'],
  },
  {
    name: '05-Guida-Sviluppo',
    title: 'MeepleAI - Guida allo Sviluppo',
    description: 'Setup ambiente locale, workflow Git, Docker, configurazione, agent architecture, seeding e monitoring.',
    dirs: ['development'],
  },
  {
    name: '06-Frontend',
    title: 'MeepleAI - Frontend (Next.js)',
    description: 'Componenti UI, layout e navigazione, dashboard admin, design token, MeepleCard, storybook.',
    dirs: ['frontend'],
  },
  {
    name: '07-Testing',
    title: 'MeepleAI - Testing e Quality Assurance',
    description: 'Pattern di testing backend/frontend, E2E con Playwright, performance, sicurezza, accessibilita.',
    dirs: ['testing'],
  },
  {
    name: '08-Deployment-e-DevOps',
    title: 'MeepleAI - Deployment e DevOps',
    description: 'Deployment, Docker, secret management, monitoring, runbook operativi, CI/CD.',
    dirs: ['deployment'],
  },
  {
    name: '09-Sicurezza',
    title: 'MeepleAI - Sicurezza',
    description: 'Review di sicurezza, OWASP Top 10, analisi TOTP, vulnerabilita note.',
    dirs: ['security'],
  },
  {
    name: '10-Guide-Utente',
    title: 'MeepleAI - Guide Utente',
    description: 'Guide per ruolo (admin, editor, utente), flussi di autenticazione, gestione giochi, AI chat, sessioni.',
    dirs: ['user-guides'],
  },
  {
    name: '11-Piani-e-Roadmap',
    title: 'MeepleAI - Piani e Roadmap',
    description: 'PRD, piani di implementazione, epic, roadmap funzionale, specifiche voice input.',
    dirs: ['plans', 'roadmap'],
  },
  {
    name: '12-Ricerca',
    title: 'MeepleAI - Ricerca e Analisi',
    description: 'Analisi di fattibilita, copyright RAG, policy editori, mechanic extractor.',
    dirs: ['research'],
  },
  {
    name: '13-Feature-e-Changelog',
    title: 'MeepleAI - Feature e Changelog',
    description: 'Descrizioni feature, changelog, PDF processing queue, agent chat RAG integration.',
    dirs: ['features'],
  },
];

function getAllMdFiles(dir, excludeDirs = []) {
  const results = [];
  if (!existsSync(dir)) return results;

  const items = readdirSync(dir);
  for (const item of items) {
    const fullPath = join(dir, item);
    const relPath = relative(DOCS_ROOT, fullPath).replace(/\\/g, '/');

    if (excludeDirs.some(ex => relPath.startsWith(ex))) continue;

    const stat = statSync(fullPath);
    if (stat.isDirectory()) {
      results.push(...getAllMdFiles(fullPath, excludeDirs));
    } else if (item.endsWith('.md')) {
      results.push(fullPath);
    }
  }
  return results;
}

function stripCodeBlocks(content) {
  // Remove fenced code blocks (```...```)
  let result = content.replace(/```[\s\S]*?```/g, '*(blocco di codice rimosso)*');

  // Clean up excessive blank lines left after removal
  result = result.replace(/\n{4,}/g, '\n\n\n');

  return result;
}

function buildMergedMarkdown(topic) {
  const allFiles = [];

  for (const dir of topic.dirs) {
    const fullDir = join(DOCS_ROOT, dir);
    const files = getAllMdFiles(fullDir, topic.excludeDirs || []);
    allFiles.push(...files);
  }

  if (allFiles.length === 0) {
    console.log(`  Warning: No files found for topic: ${topic.name}`);
    return null;
  }

  // Sort: README first, then alphabetically
  allFiles.sort((a, b) => {
    const aIsReadme = basename(a).toLowerCase() === 'readme.md';
    const bIsReadme = basename(b).toLowerCase() === 'readme.md';
    if (aIsReadme && !bIsReadme) return -1;
    if (!aIsReadme && bIsReadme) return 1;
    return a.localeCompare(b);
  });

  let merged = '';

  // Title page
  merged += `# ${topic.title}\n\n`;
  merged += `${topic.description}\n\n`;
  merged += `**Data generazione**: ${new Date().toLocaleDateString('it-IT', { year: 'numeric', month: 'long', day: 'numeric' })}\n\n`;
  merged += `**File inclusi**: ${allFiles.length}\n\n`;
  merged += '---\n\n';

  // Table of contents
  merged += '## Indice\n\n';
  for (let i = 0; i < allFiles.length; i++) {
    const relPath = relative(DOCS_ROOT, allFiles[i]).replace(/\\/g, '/');
    merged += `${i + 1}. ${relPath}\n`;
  }
  merged += '\n---\n\n';

  // Content
  for (const file of allFiles) {
    const relPath = relative(DOCS_ROOT, file).replace(/\\/g, '/');
    let content = readFileSync(file, 'utf-8');

    // Strip code blocks
    content = stripCodeBlocks(content);

    // Add file header with page break
    merged += `\n\n<div style="page-break-before: always;"></div>\n\n`;
    merged += `## ${relPath}\n\n`;
    merged += content;
    merged += '\n\n---\n\n';
  }

  return merged;
}

function generatePdf(mdPath) {
  try {
    const configPath = join(process.cwd(), 'scripts', 'pdf-config.json');
    // Use md-to-pdf directly via its full path, with shell on Windows
    execFileSync('md-to-pdf', [mdPath, '--config-file', configPath], {
      stdio: 'pipe',
      timeout: 180000,
      shell: true,
    });
    return true;
  } catch (err) {
    console.error(`  PDF generation failed: ${err.stderr?.toString().substring(0, 200) || err.message?.substring(0, 200)}`);
    return false;
  }
}

// Main
async function main() {
  console.log('MeepleAI Documentation PDF Generator\n');

  // Create output directory
  mkdirSync(OUTPUT_DIR, { recursive: true });

  // Create PDF config
  const pdfConfig = {
    pdf_options: {
      format: 'A4',
      margin: { top: '25mm', bottom: '25mm', left: '20mm', right: '20mm' },
      printBackground: true,
    },
    stylesheet: join(process.cwd(), 'scripts', 'pdf-style.css'),
    body_class: 'markdown-body',
  };
  writeFileSync(join(process.cwd(), 'scripts', 'pdf-config.json'), JSON.stringify(pdfConfig, null, 2));

  let generated = 0;
  let failed = 0;

  for (const topic of TOPICS) {
    console.log(`\nProcessing: ${topic.name}`);

    const merged = buildMergedMarkdown(topic);
    if (!merged) {
      failed++;
      continue;
    }

    // Write merged markdown
    const mdPath = join(OUTPUT_DIR, `${topic.name}.md`);
    writeFileSync(mdPath, merged, 'utf-8');
    console.log(`  Merged markdown created (${allFiles_count(topic)} files)`);

    // Generate PDF
    console.log(`  Generating PDF...`);
    const success = generatePdf(mdPath);
    const pdfPath = mdPath.replace('.md', '.pdf');

    if (success && existsSync(pdfPath)) {
      const sizeMb = (statSync(pdfPath).size / 1024 / 1024).toFixed(2);
      console.log(`  PDF generated: ${basename(pdfPath)} (${sizeMb} MB)`);
      generated++;
    } else {
      console.log(`  PDF not generated, markdown file preserved`);
      failed++;
    }
  }

  console.log(`\nResults: ${generated} PDFs generated, ${failed} failed`);
  console.log(`Output directory: ${OUTPUT_DIR}`);
}

function allFiles_count(topic) {
  let count = 0;
  for (const dir of topic.dirs) {
    const fullDir = join(DOCS_ROOT, dir);
    count += getAllMdFiles(fullDir, topic.excludeDirs || []).length;
  }
  return count;
}

main().catch(console.error);
