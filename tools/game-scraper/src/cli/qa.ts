import { program } from "commander";
import { extractPdfText, splitIntoPages } from "../lib/pdf.js";
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
  const { text, pages: numPages } = await extractPdfText(pdfPath);
  const pages = splitIntoPages(text, numPages);
  const qa = [] as any[];
  pages.forEach((pageText, idx) => {
    if (qa.length >= opts.max) return;
    const lines = pageText.split(/\r?\n/).map((l) => l.trim()).filter(Boolean);
    if (lines.length === 0) return;
    const pageBody = lines.join(" ");
    const sentence = firstSentence(pageBody);
    if (!sentence || sentence.length < 15) return;
    qa.push({
      game_id: opts.gameId,
      question: `Cosa dice il manuale a pagina ${idx + 1} riguardo alle regole?`,
      answer: sentence.slice(0, 400),
      page: idx + 1,
      context: pageBody.slice(0, 800),
      source: opts.rulebook,
    });
  });

  const outFile = resolveOut("rulebooks", "qa", `${opts.gameId}.jsonl`);
  await fs.ensureDir(path.dirname(outFile));
  const linesOut = qa.map((q) => JSON.stringify(q)).join("\n");
  await fs.writeFile(outFile, linesOut + (linesOut ? "\n" : ""));
  console.log(`Wrote ${qa.length} QA pairs to ${outFile}`);
});

program.parseAsync();
