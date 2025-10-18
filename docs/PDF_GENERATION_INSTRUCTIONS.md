# PDF Generation Instructions

This directory contains two ways to generate a professional PDF describing the MeepleAI application.

## Option 1: Using Python Script (ReportLab)

### Prerequisites

```bash
pip install reportlab
```

### Generate PDF

```bash
cd docs
python generate_app_description.py
```

This will create `MeepleAI_Application_Description.pdf` in the current directory.

**Features:**
- Professional layout with A4 page size
- Color-coded headers and tables
- Table of contents
- Structured sections
- Brand colors

---

## Option 2: Convert Markdown to PDF

If you don't have ReportLab installed, you can convert the Markdown file to PDF using various tools.

### Using Pandoc (Recommended)

**Install Pandoc:**
- Windows: https://pandoc.org/installing.html
- macOS: `brew install pandoc`
- Linux: `sudo apt-get install pandoc`

**Convert to PDF:**

```bash
cd docs
pandoc MeepleAI_Application_Description.md -o MeepleAI_Application_Description.pdf --pdf-engine=wkhtmltopdf -V geometry:margin=2cm
```

**With custom styling:**

```bash
pandoc MeepleAI_Application_Description.md -o MeepleAI_Application_Description.pdf \
  --pdf-engine=wkhtmltopdf \
  -V geometry:margin=2cm \
  -V fontsize=11pt \
  --toc \
  --highlight-style=tango
```

### Using wkhtmltopdf

**Install wkhtmltopdf:**
- Download from: https://wkhtmltopdf.org/downloads.html

**Convert to PDF:**

```bash
cd docs
wkhtmltopdf MeepleAI_Application_Description.md MeepleAI_Application_Description.pdf
```

### Using Grip (GitHub-style Markdown)

**Install Grip:**

```bash
pip install grip
```

**Convert:**

```bash
cd docs
grip MeepleAI_Application_Description.md --export MeepleAI_Application_Description.html
wkhtmltopdf MeepleAI_Application_Description.html MeepleAI_Application_Description.pdf
```

### Using Chrome/Edge (Manual)

1. Open `MeepleAI_Application_Description.md` in VS Code or any Markdown viewer
2. Use the preview feature
3. Print to PDF (Ctrl+P → Save as PDF)

### Using Online Tools

- **Markdown to PDF**: https://www.markdowntopdf.com/
- **Dillinger**: https://dillinger.io/ (export as PDF)
- **StackEdit**: https://stackedit.io/ (export as PDF)

---

## Files

- `generate_app_description.py` - Python script using ReportLab
- `MeepleAI_Application_Description.md` - Markdown source
- `MeepleAI_Application_Description.pdf` - Generated PDF (after running script)

---

## Troubleshooting

### ReportLab not installed

```bash
pip install reportlab
```

### Python module not found

Ensure Python 3.7+ is installed:

```bash
python --version
pip --version
```

### Pandoc conversion issues

Install PDF engine:

```bash
# wkhtmltopdf
https://wkhtmltopdf.org/downloads.html

# OR LaTeX (for better formatting)
# Windows: MiKTeX https://miktex.org/
# macOS: MacTeX http://www.tug.org/mactex/
# Linux: sudo apt-get install texlive-full
```

---

## Customization

### Python Script

Edit `generate_app_description.py`:
- Colors: Modify `colors.HexColor('#...')`
- Fonts: Change `fontName='Helvetica-Bold'`
- Page size: Use `letter` instead of `A4`
- Margins: Adjust `rightMargin`, `leftMargin`, etc.

### Markdown

Edit `MeepleAI_Application_Description.md` directly.

---

**Generated:** 2025-10-18
