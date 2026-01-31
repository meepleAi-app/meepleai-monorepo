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
  let plays;
  try {
    plays = await fetchPlays(opts.gameId, opts.mindate, cfg);
  } catch (err: any) {
    console.error(`Failed to fetch plays for ${opts.gameId}:`, err?.message ?? err);
    process.exit(1);
  }
  const outFile = resolveOut("bgg", "plays", `${opts.gameId}.jsonl`);
  await fs.ensureDir(path.dirname(outFile));

  const existingIds = new Set<number>();
  if (await fs.pathExists(outFile)) {
    const lines = (await fs.readFile(outFile, "utf8")).split(/\r?\n/).filter(Boolean);
    lines.forEach((l) => {
      try {
        const obj = JSON.parse(l);
        if (obj.id) existingIds.add(Number(obj.id));
      } catch {
        /* ignore bad lines */
      }
    });
  }

  const fresh = plays.filter((p) => !existingIds.has(p.id));
  if (fresh.length === 0) {
    console.log("No new plays to append");
    return;
  }

  const lines = fresh.map((p) => JSON.stringify(p)).join("\n");
  await fs.appendFile(outFile, lines + "\n");
  console.log(`Appended ${fresh.length} plays to ${outFile}`);
});

program.parseAsync();
