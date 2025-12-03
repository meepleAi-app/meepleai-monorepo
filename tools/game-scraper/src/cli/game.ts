import { program } from "commander";
import { loadConfig, resolveOut } from "../lib/config.js";
import fs from "fs-extra";
import path from "node:path";

program
  .description("Fetch BGG game metadata (stub)")
  .requiredOption("--game-id <id>", "BGG game id", (v) => Number(v));

program.action(async (opts) => {
  const cfg = loadConfig();
  const outFile = resolveOut("bgg", "games.jsonl");
  await fs.ensureDir(path.dirname(outFile));
  const record = { game_id: opts.gameId, fetched_at: new Date().toISOString(), note: "TODO: implement thing?id" };
  await fs.appendFile(outFile, JSON.stringify(record) + "\n");
  console.log(`Saved stub metadata to ${outFile}`);
});

program.parseAsync();
