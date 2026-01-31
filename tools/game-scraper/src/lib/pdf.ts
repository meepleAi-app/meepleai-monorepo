import fs from "fs-extra";
import path from "node:path";
import pdfParse from "pdf-parse";

export async function extractPdfText(pdfPath: string): Promise<{ text: string; pages: number }> {
  const data = await fs.readFile(pdfPath);
  const parsed = await pdfParse(data);
  return { text: parsed.text, pages: parsed.numpages ?? parsed.numrender ?? 0 };
}

export function splitIntoPages(text: string, numPages?: number): string[] {
  // Primary: use form feeds if present (pdf-parse inserts \f between pages when text layer exists)
  if (text.includes("\f")) {
    return text.split("\f").map((p) => p.trim()).filter(Boolean);
  }

  // Secondary: if numPages is known, approximate by equal chunks of lines
  const lines = text.split(/\r?\n/);
  if (numPages && numPages > 0) {
    const chunk = Math.max(1, Math.ceil(lines.length / numPages));
    const pages: string[] = [];
    for (let i = 0; i < lines.length; i += chunk) {
      pages.push(lines.slice(i, i + chunk).join("\n").trim());
    }
    return pages.filter(Boolean);
  }

  // Fallback: large text split every 80 lines
  const fallback: string[] = [];
  for (let i = 0; i < lines.length; i += 80) {
    fallback.push(lines.slice(i, i + 80).join("\n").trim());
  }
  return fallback.filter(Boolean);
}
