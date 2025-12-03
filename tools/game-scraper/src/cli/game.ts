import { program } from "commander";
import { loadConfig, resolveOut } from "../lib/config.js";
import fs from "fs-extra";
import path from "node:path";
import { fetchThing } from "../lib/bgg.js";

program
  .description("Fetch BGG game metadata")
  .requiredOption("--game-id <id>", "BGG game id", (v) => Number(v));

program.action(async (opts) => {
  const cfg = loadConfig();
  const outFile = resolveOut("bgg", "games.jsonl");
  await fs.ensureDir(path.dirname(outFile));
  const thing = await fetchThing(opts.gameId);
  if (!thing) {
    console.error(`No thing found for id ${opts.gameId}`);
    process.exit(1);
  }

  const existing = new Map<number, any>();
  if (await fs.pathExists(outFile)) {
    const lines = (await fs.readFile(outFile, "utf8")).split(/\r?\n/).filter(Boolean);
    lines.forEach((l) => {
      try {
        const obj = JSON.parse(l);
        if (obj.game_id) existing.set(Number(obj.game_id), obj);
      } catch {
        /* ignore */
      }
    });
  }

  existing.set(thing.id, { game_id: thing.id, ...thing, fetched_at: new Date().toISOString() });
  const newLines = Array.from(existing.values()).map((o) => JSON.stringify(o)).join("\n") + "\n";
  await fs.writeFile(outFile, newLines);
  console.log(`Saved metadata for id ${thing.id} to ${outFile}`);
});

program.parseAsync();
