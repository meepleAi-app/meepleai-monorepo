import { program } from "commander";
import { loadConfig, resolveOut } from "../lib/config.js";
import { fetchPlays } from "../lib/bgg.js";
import fs from "fs-extra";
import path from "node:path";

program
  .description("Fetch plays for a BGG game")
  .requiredOption("--game-id <id>", "BGG game id", (v) => Number(v))
  .option("--mindate <date>", "Minimum date YYYY-MM-DD");

program.action(async (opts) => {
  const cfg = loadConfig();
  const plays = await fetchPlays(opts.gameId, opts.mindate, cfg);
  const outFile = resolveOut("bgg", "plays", `${opts.gameId}.jsonl`);
  await fs.ensureDir(path.dirname(outFile));
  const lines = plays.map((p) => JSON.stringify(p)).join("\n");
  await fs.writeFile(outFile, lines + (lines ? "\n" : ""));
  console.log(`Saved ${plays.length} plays to ${outFile}`);
});

program.parseAsync();
