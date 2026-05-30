/**
 * downloadAsFile — create an in-memory Blob and trigger a browser download.
 *
 * Extracted from IngestionActions.tsx so it can be shared by KbDocActions
 * (export chunks JSON) and any future admin action that needs file download.
 * Issue #1653.
 */
export function downloadAsFile(content: string, filename: string): void {
  const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  a.style.display = 'none';
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  setTimeout(() => URL.revokeObjectURL(url), 0);
}
