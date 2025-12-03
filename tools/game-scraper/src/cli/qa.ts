import { program } from "commander";
import { extractPdfText, chunkByHeading } from "../lib/pdf.js";
import { resolveOut } from "../lib/config.js";
import fs from "fs-extra";
import path from "node:path";

program
  .description("Generate QA stubs from a rulebook PDF")
  .requiredOption("--rulebook <path>", "Path to PDF rulebook")
  .requiredOption("--game-id <id>", "BGG game id", (v) => Number(v));

program.action(async (opts) => {
  const pdfPath = path.resolve(opts.rulebook);
  const { text, pages } = await extractPdfText(pdfPath);
  const chunks = chunkByHeading(text).slice(0, 5); // keep small for stub
  const outFile = resolveOut("rulebooks", "qa", `${opts.gameId}.jsonl`);
  await fs.ensureDir(path.dirname(outFile));
  const qa = chunks.map((c, i) => ({
    game_id: opts.gameId,
    question: `Placeholder question ${i + 1}`,
    answer: c.split("\n").find((ln) => ln.trim()) ?? "",
    page: 1,
    source: opts.rulebook,
  }));
  const lines = qa.map((q) => JSON.stringify(q)).join("\n");
  await fs.writeFile(outFile, lines + (lines ? "\n" : ""));
  console.log(`Wrote ${qa.length} QA stubs to ${outFile} (pages detected: ${pages})`);
});

program.parseAsync();
