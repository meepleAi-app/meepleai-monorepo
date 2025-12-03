import fs from "fs-extra";
import path from "node:path";
import pdfParse from "pdf-parse";

export async function extractPdfText(pdfPath: string): Promise<{ text: string; pages: number }> {
  const data = await fs.readFile(pdfPath);
  const parsed = await pdfParse(data);
  return { text: parsed.text, pages: parsed.numpages ?? parsed.numrender ?? 0 };
}

export function chunkByHeading(text: string): string[] {
  const lines = text.split(/\r?\n/);
  const chunks: string[] = [];
  let buf: string[] = [];
  const flush = () => {
    if (buf.length) {
      chunks.push(buf.join("\n").trim());
      buf = [];
    }
  };
  for (const line of lines) {
    if (/^\d+(\.\d+)*\s+/.test(line.trim())) flush();
    buf.push(line);
  }
  flush();
  return chunks.filter(Boolean);
}
