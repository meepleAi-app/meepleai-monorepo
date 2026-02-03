#!/usr/bin/env node
/**
 * Generate Mock PDF Rulebooks for E2E Testing
 *
 * Creates minimal valid PDF files for each seeded game.
 * These PDFs contain basic game metadata but no real content.
 *
 * Usage:
 *   node generate-mock-pdfs.js
 *   node generate-mock-pdfs.js --games pandemic,wingspan
 *   node generate-mock-pdfs.js --all
 */

const fs = require('fs');
const path = require('path');

// Game metadata from SharedGameSeeder.cs
const GAME_METADATA = {
  '7-wonders': {
    title: '7 Wonders',
    players: '2-7',
    time: '30 min',
    age: '10+',
    bggId: 13,
    language: 'en',
  },
  agricola: {
    title: 'Agricola',
    players: '1-4',
    time: '30-120 min',
    age: '12+',
    bggId: 31260,
    language: 'en',
  },
  azul: {
    title: 'Azul',
    players: '2-4',
    time: '30-45 min',
    age: '8+',
    bggId: 230802,
    language: 'en',
  },
  carcassonne: {
    title: 'Carcassonne',
    players: '2-5',
    time: '30-45 min',
    age: '7+',
    bggId: 822,
    language: 'en',
  },
  pandemic: {
    title: 'Pandemic',
    players: '2-4',
    time: '45 min',
    age: '8+',
    bggId: 30549,
    language: 'en',
  },
  chess: {
    title: 'Chess',
    players: '2',
    time: '60+ min',
    age: '6+',
    bggId: null,
    language: 'it',
  },
  splendor: {
    title: 'Splendor',
    players: '2-4',
    time: '30 min',
    age: '10+',
    bggId: 148228,
    language: 'en',
  },
  'ticket-to-ride': {
    title: 'Ticket to Ride',
    players: '2-5',
    time: '30-60 min',
    age: '8+',
    bggId: 9209,
    language: 'en',
  },
  wingspan: {
    title: 'Wingspan',
    players: '1-5',
    time: '40-70 min',
    age: '10+',
    bggId: 266192,
    language: 'en',
  },
};

/**
 * Generates a minimal valid PDF with game metadata
 * Uses PDF 1.4 format for maximum compatibility
 */
function generateMockPdf(gameKey, metadata) {
  const filename =
    metadata.language === 'en' ? `${gameKey}_rulebook.pdf` : `${gameKey}_${metadata.language}_rulebook.pdf`;

  // PDF content with basic structure
  const pdfContent = `%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
>>
endobj

2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj

3 0 obj
<<
/Type /Page
/Parent 2 0 R
/Resources <<
/Font <<
/F1 4 0 R
>>
>>
/MediaBox [0 0 612 792]
/Contents 5 0 R
>>
endobj

4 0 obj
<<
/Type /Font
/Subtype /Type1
/BaseFont /Helvetica
>>
endobj

5 0 obj
<<
/Length 350
>>
stream
BT
/F1 24 Tf
50 700 Td
(${metadata.title} - Rulebook) Tj
ET

BT
/F1 12 Tf
50 650 Td
(Mock PDF for E2E Testing) Tj
ET

BT
/F1 10 Tf
50 600 Td
(Players: ${metadata.players}) Tj
0 -20 Td
(Playing Time: ${metadata.time}) Tj
0 -20 Td
(Age: ${metadata.age}) Tj
0 -20 Td
(BGG ID: ${metadata.bggId || 'N/A'}) Tj
0 -20 Td
(Language: ${metadata.language.toUpperCase()}) Tj
ET
endstream
endobj

xref
0 6
0000000000 65535 f
0000000009 00000 n
0000000058 00000 n
0000000115 00000 n
0000000262 00000 n
0000000341 00000 n
trailer
<<
/Size 6
/Root 1 0 R
>>
startxref
742
%%EOF
`;

  return { filename, content: pdfContent };
}

/**
 * Main execution
 */
function main() {
  const args = process.argv.slice(2);
  const outputDir = path.join(__dirname, '../test-data');

  // Parse arguments
  let selectedGames = [];
  if (args.includes('--all')) {
    selectedGames = Object.keys(GAME_METADATA);
  } else {
    const gamesArg = args.find((arg) => arg.startsWith('--games='));
    if (gamesArg) {
      selectedGames = gamesArg.replace('--games=', '').split(',');
    } else {
      // Default: Generate only critical games for testing
      selectedGames = ['pandemic', 'wingspan', 'azul'];
    }
  }

  // Ensure output directory exists
  if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir, { recursive: true });
  }

  console.warn('🏗️  Generating mock PDF rulebooks...\n');

  let generatedCount = 0;
  let skippedCount = 0;

  selectedGames.forEach((gameKey) => {
    const metadata = GAME_METADATA[gameKey];

    if (!metadata) {
      console.warn(`⚠️  Unknown game: ${gameKey} (skipped)`);
      skippedCount++;
      return;
    }

    const { filename, content } = generateMockPdf(gameKey, metadata);
    const outputPath = path.join(outputDir, filename);

    // Skip if file already exists
    if (fs.existsSync(outputPath)) {
      console.warn(`⏭️  ${filename} (already exists)`);
      skippedCount++;
      return;
    }

    // Write PDF file
    fs.writeFileSync(outputPath, content, 'utf-8');
    const stats = fs.statSync(outputPath);
    const sizeKB = (stats.size / 1024).toFixed(2);

    console.warn(`✅ ${filename} (${sizeKB} KB)`);
    generatedCount++;
  });

  console.warn(`\n📊 Summary: ${generatedCount} generated, ${skippedCount} skipped`);
  console.warn(`📂 Output: ${outputDir}`);
  console.warn('\n✅ Mock PDFs ready for E2E testing!\n');
}

// Run if called directly
if (require.main === module) {
  main();
}

module.exports = { generateMockPdf, GAME_METADATA };
