#!/usr/bin/env node

/**
 * Strumento di ricerca documentazione MeepleAI
 *
 * Cerca nei file Markdown della documentazione per trovare informazioni rilevanti.
 * Supporta ricerca full-text con ranking e highlighting.
 *
 * Uso:
 *   node tools/search-docs.js "query"
 *   node tools/search-docs.js "authentication flow"
 *   node tools/search-docs.js "RAG pipeline" --limit 5
 */

const fs = require('fs');
const path = require('path');

class DocSearch {
  constructor(docsDir) {
    this.docsDir = docsDir;
    this.index = [];
  }

  /**
   * Indicizza tutti i file Markdown
   */
  buildIndex() {
    console.log('Building documentation index...\n');

    const files = this.findMarkdownFiles(this.docsDir);

    files.forEach(filePath => {
      const content = fs.readFileSync(filePath, 'utf-8');
      const relativePath = path.relative(this.docsDir, filePath);

      // Estrai metadati dal contenuto
      const lines = content.split('\n');
      const title = this.extractTitle(lines);
      const headings = this.extractHeadings(content);

      this.index.push({
        path: relativePath,
        fullPath: filePath,
        title,
        headings,
        content,
        contentLower: content.toLowerCase(),
      });
    });

    console.log(`Indexed ${this.index.length} documents\n`);
  }

  /**
   * Cerca nella documentazione
   */
  search(query, options = {}) {
    const limit = options.limit || 10;
    const queryLower = query.toLowerCase();
    const queryTerms = queryLower.split(/\s+/).filter(t => t.length > 2);

    const results = [];

    this.index.forEach(doc => {
      let score = 0;
      const matches = [];

      // Cerca nel titolo (peso alto)
      if (doc.title.toLowerCase().includes(queryLower)) {
        score += 10;
        matches.push({ type: 'title', text: doc.title });
      }

      // Cerca negli heading (peso medio)
      doc.headings.forEach(heading => {
        if (heading.toLowerCase().includes(queryLower)) {
          score += 5;
          matches.push({ type: 'heading', text: heading });
        }
      });

      // Cerca nel contenuto (peso basso)
      queryTerms.forEach(term => {
        const regex = new RegExp(`\\b${this.escapeRegex(term)}\\w*`, 'gi');
        const contentMatches = doc.content.match(regex);

        if (contentMatches) {
          score += contentMatches.length * 0.5;
        }
      });

      // Estrai snippet con contesto
      if (score > 0) {
        const snippets = this.extractSnippets(doc.content, queryLower, 3);

        results.push({
          path: doc.path,
          fullPath: doc.fullPath,
          title: doc.title,
          score,
          matches,
          snippets,
        });
      }
    });

    // Ordina per score decrescente
    results.sort((a, b) => b.score - a.score);

    return results.slice(0, limit);
  }

  /**
   * Estrai snippet con contesto
   */
  extractSnippets(content, query, maxSnippets = 3) {
    const lines = content.split('\n');
    const snippets = [];
    const queryLower = query.toLowerCase();

    for (let i = 0; i < lines.length && snippets.length < maxSnippets; i++) {
      const line = lines[i];
      const lineLower = line.toLowerCase();

      if (lineLower.includes(queryLower)) {
        // Prendi 2 righe prima e 2 dopo per contesto
        const start = Math.max(0, i - 2);
        const end = Math.min(lines.length, i + 3);
        const snippet = lines.slice(start, end).join('\n');

        snippets.push({
          lineNumber: i + 1,
          text: this.truncate(snippet, 200),
        });
      }
    }

    return snippets;
  }

  /**
   * Estrai titolo dal documento
   */
  extractTitle(lines) {
    for (const line of lines) {
      const match = line.match(/^#\s+(.+)$/);
      if (match) {
        return match[1];
      }
    }
    return 'Untitled';
  }

  /**
   * Estrai tutti gli heading
   */
  extractHeadings(content) {
    const headings = [];
    const regex = /^#{1,6}\s+(.+)$/gm;
    let match;

    while ((match = regex.exec(content)) !== null) {
      headings.push(match[1]);
    }

    return headings;
  }

  /**
   * Trova tutti i file Markdown ricorsivamente
   */
  findMarkdownFiles(dir, fileList = []) {
    const files = fs.readdirSync(dir);

    files.forEach(file => {
      const filePath = path.join(dir, file);
      const stat = fs.statSync(filePath);

      if (stat.isDirectory()) {
        this.findMarkdownFiles(filePath, fileList);
      } else if (path.extname(file) === '.md') {
        fileList.push(filePath);
      }
    });

    return fileList;
  }

  /**
   * Escape regex special characters
   */
  escapeRegex(str) {
    return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  /**
   * Trunca testo a lunghezza massima
   */
  truncate(text, maxLength) {
    if (text.length <= maxLength) return text;
    return text.substring(0, maxLength) + '...';
  }

  /**
   * Formatta risultati per console
   */
  formatResults(results, query) {
    if (results.length === 0) {
      console.log(`No results found for: "${query}"\n`);
      return;
    }

    console.log(`Found ${results.length} results for: "${query}"\n`);
    console.log('='.repeat(80) + '\n');

    results.forEach((result, index) => {
      console.log(`${index + 1}. ${result.title}`);
      console.log(`   Path: ${result.path}`);
      console.log(`   Score: ${result.score.toFixed(2)}\n`);

      if (result.snippets.length > 0) {
        console.log('   Snippets:');
        result.snippets.forEach((snippet, i) => {
          console.log(`   ${i + 1}) Line ${snippet.lineNumber}:`);
          const preview = snippet.text.split('\n').map(l => `      ${l}`).join('\n');
          console.log(preview);
          console.log('');
        });
      }

      console.log('-'.repeat(80) + '\n');
    });
  }
}

// Main
function main() {
  const args = process.argv.slice(2);

  if (args.length === 0) {
    console.log('Usage: node tools/search-docs.js "query" [--limit N]');
    console.log('\nExamples:');
    console.log('  node tools/search-docs.js "authentication"');
    console.log('  node tools/search-docs.js "RAG pipeline" --limit 5');
    console.log('  node tools/search-docs.js "streaming SSE"');
    process.exit(1);
  }

  // Parse arguments
  let query = '';
  let limit = 10;

  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--limit' && i + 1 < args.length) {
      limit = parseInt(args[i + 1], 10);
      i++;
    } else {
      query += (query ? ' ' : '') + args[i];
    }
  }

  // Search
  const docsDir = path.join(__dirname, '../docs');
  const search = new DocSearch(docsDir);

  search.buildIndex();
  const results = search.search(query, { limit });
  search.formatResults(results, query);
}

main();
