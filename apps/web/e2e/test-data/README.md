# E2E Test Data - PDF Rulebooks

This directory contains PDF rulebooks for E2E testing of the admin wizard.

## 📄 Required PDFs

For complete E2E testing, you need rulebook PDFs for the seeded games:

### Critical (for main tests)
- `pandemic_rulebook.pdf` - Pandemic (2-4 players, BGG #30549)
- `wingspan_en_rulebook.pdf` - Wingspan (1-5 players, BGG #266192)
- `azul_rulebook.pdf` - Azul (2-4 players, BGG #230802)

### Additional (for comprehensive testing)
- `7-wonders_rulebook.pdf` - 7 Wonders (BGG #13)
- `agricola_rulebook.pdf` - Agricola (BGG #31260)
- `carcassone_rulebook.pdf` - Carcassonne (BGG #822)
- `splendor_rulebook.pdf` - Splendor (BGG #148228)
- `ticket-to-ride_rulebook.pdf` - Ticket to Ride (BGG #9209)
- `scacchi-fide_2017_rulebook.pdf` - Chess (IT language)

## 🔍 Where to Get PDFs

### Option 1: Official Publisher Sites
Download from official game publisher websites (most reliable, legal)

### Option 2: BoardGameGeek
Many games have official rulebooks in their BGG Files section:
- Visit: `https://boardgamegeek.com/boardgame/{BGG_ID}`
- Navigate to "Files" section
- Download official rulebook PDF

### Option 3: Generate Mock PDFs (Testing Only)
For testing without real processing:

```bash
# Install PDF generation tool
npm install -g pdfkit

# Generate minimal PDFs
node ../scripts/generate-test-pdfs.js
```

## 📏 Size Constraints

**Max size**: 10 MB per PDF (enforced by backend)
**Recommended**: 2-5 MB for fast CI/CD tests

## 🔒 Licensing & Copyright

**IMPORTANT**:
- Only use rulebooks you have legal right to use for testing
- Official rulebooks are copyrighted by publishers
- For CI/CD, consider using mock/generated PDFs
- Never commit copyrighted material to git repository

## 📝 File Naming Convention

Follow the pattern from `SharedGameSeeder.cs`:
- `{game-name}_rulebook.pdf` (English)
- `{game-name}_{lang}_rulebook.pdf` (Other languages)

Examples:
- `pandemic_rulebook.pdf` ✅
- `wingspan_en_rulebook.pdf` ✅
- `scacchi-fide_2017_rulebook.pdf` ✅
- `Pandemic Rules.pdf` ❌ (wrong format)

## 🧪 Mock PDF Generation

### Minimal Valid PDF
```javascript
const PDFDocument = require('pdfkit');
const fs = require('fs');

const doc = new PDFDocument();
doc.pipe(fs.createWriteStream('pandemic_rulebook.pdf'));

doc.fontSize(18).text('Pandemic Rulebook', 100, 100);
doc.fontSize(12).text('Players: 2-4', 100, 150);
doc.text('Playing Time: 45 minutes', 100, 170);
doc.text('Rules: [Mock content for testing]', 100, 200);

doc.end();
```

### Using Script
```bash
cd apps/web/e2e/scripts
node generate-mock-pdfs.js --games pandemic,wingspan,azul
```

This creates minimal valid PDFs for each game with basic metadata.

## 🎯 Using in Tests

```typescript
// In your test
const testPdfPath = 'e2e/test-data/pandemic_rulebook.pdf';

await fileInput.setInputFiles(testPdfPath);
```

## 🗑️ Cleanup

PDFs in this directory are:
- ✅ Git-ignored (`.gitignore` prevents accidental commits)
- ✅ Local-only (each developer maintains their own)
- ✅ Replaceable (regenerate anytime with script)

## 📊 Test Data Inventory

Current status: `Empty` (setup with script or manual download)

To check inventory:
```bash
ls -lh apps/web/e2e/test-data/*.pdf
```

Expected output after setup:
```
-rw-r--r-- pandemic_rulebook.pdf     2.3M
-rw-r--r-- wingspan_en_rulebook.pdf  3.1M
-rw-r--r-- azul_rulebook.pdf         1.8M
```
