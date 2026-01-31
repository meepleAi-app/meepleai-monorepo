import { program } from "commander";
import fs from "fs-extra";
import path from "node:path";
import { resolveOut } from "../lib/config.js";

program
  .description("Validate scraped JSONL files (basic)")
  .option("--game-id <id>", "BGG game id", (v) => Number(v));

program.action(async (opts) => {
  const files: string[] = [];
  if (opts.gameId) {
    files.push(resolveOut("bgg", "plays", `${opts.gameId}.jsonl`));
    files.push(resolveOut("rulebooks", "qa", `${opts.gameId}.jsonl`));
  } else {
    // scan common dirs
    const root = resolveOut();
    for (const rel of ["bgg/plays", "rulebooks/qa"]) {
      const dir = path.join(root, rel);
      if (await fs.pathExists(dir)) {
        const entries = await fs.readdir(dir);
        files.push(...entries.map((e) => path.join(dir, e)));
      }
    }
  }
  let ok = true;
  for (const f of files) {
    if (!(await fs.pathExists(f))) continue;
    const lines = (await fs.readFile(f, "utf8")).trim().split(/\r?\n/).filter(Boolean);
    lines.forEach((line, i) => {
      try {
        const obj = JSON.parse(line);
        if (obj.answer === "" || obj.answer === undefined) throw new Error("empty answer");
      } catch (err) {
        ok = false;
        console.error(`Invalid JSONL at ${f}:${i + 1} -> ${err}`);
      }
    });
  }
  console.log(ok ? "Validation passed" : "Validation failed");
  process.exit(ok ? 0 : 1);
});

program.parseAsync();
