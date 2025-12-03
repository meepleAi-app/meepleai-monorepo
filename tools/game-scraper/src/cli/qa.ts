import { program } from "commander";
import { extractPdfText } from "../lib/pdf.js";
import { resolveOut } from "../lib/config.js";
import fs from "fs-extra";
import path from "node:path";

function firstSentence(text: string): string {
  const match = text.match(/[^.!?\n]{10,}?[.!?](\s|$)/);
  return match ? match[0].trim() : text.trim();
}

program
  .description("Generate grounded QA pairs from a rulebook PDF")
  .requiredOption("--rulebook <path>", "Path to PDF rulebook")
  .requiredOption("--game-id <id>", "BGG game id", (v) => Number(v))
  .option("--max <n>", "Max QA to emit", (v) => Number(v), 5);

program.action(async (opts) => {
  const pdfPath = path.resolve(opts.rulebook);
  const { text } = await extractPdfText(pdfPath);
  const pages = text.split(/\f/); // pdf-parse inserts form feed per page
  const qa = [] as any[];
  for (let i = 0; i < pages.length && qa.length < opts.max; i++) {
    const pageText = pages[i].trim();
    if (!pageText) continue;
    const lines = pageText.split(/\r?\n/).map((l) => l.trim()).filter(Boolean);
    if (lines.length === 0) continue;
    const sentence = firstSentence(lines.join(" "));
    if (!sentence) continue;
    qa.push({
      game_id: opts.gameId,
      question: `Cosa afferma il manuale a pagina ${i + 1}?`,
      answer: sentence.slice(0, 400),
      page: i + 1,
      source: opts.rulebook,
    });
  }

  const outFile = resolveOut("rulebooks", "qa", `${opts.gameId}.jsonl`);
  await fs.ensureDir(path.dirname(outFile));
  const linesOut = qa.map((q) => JSON.stringify(q)).join("\n");
  await fs.writeFile(outFile, linesOut + (linesOut ? "\n" : ""));
  console.log(`Wrote ${qa.length} QA pairs to ${outFile}`);
});

program.parseAsync();
