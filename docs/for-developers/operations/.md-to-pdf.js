module.exports = {
  stylesheet: [],
  css: `
    body { font-family: 'Segoe UI', sans-serif; font-size: 11pt; line-height: 1.5; }
    h1:not(:first-of-type) { page-break-before: always; }
    h1 { border-bottom: 2px solid #333; padding-bottom: 0.3em; }
    h2 { border-bottom: 1px solid #ccc; margin-top: 1.5em; padding-bottom: 0.2em; }
    h3 { margin-top: 1.2em; }
    table { border-collapse: collapse; width: 100%; margin: 0.5em 0; }
    th, td { border: 1px solid #ddd; padding: 6px 10px; text-align: left; }
    th { background: #f8f8f8; font-weight: 600; }
    code { background: #f4f4f4; padding: 2px 4px; border-radius: 3px; font-size: 10pt; }
    pre { background: #f4f4f4; padding: 12px; border-radius: 4px; overflow-x: auto; }
    pre code { background: none; padding: 0; }
    blockquote { border-left: 3px solid #ccc; margin-left: 0; padding-left: 1em; color: #555; }
  `,
  pdf_options: {
    format: 'A4',
    margin: { top: '20mm', bottom: '20mm', left: '15mm', right: '15mm' },
    printBackground: true,
    headerTemplate: '<div style="font-size:8pt;text-align:center;width:100%;color:#888;">MeepleAI Operations Manual</div>',
    footerTemplate: '<div style="font-size:8pt;text-align:center;width:100%;color:#888;"><span class="pageNumber"></span> / <span class="totalPages"></span></div>',
    displayHeaderFooter: true,
    launch_options: {
      args: ['--no-sandbox', '--disable-setuid-sandbox'],
    },
  },
  dest: 'operations-manual.pdf',
};
