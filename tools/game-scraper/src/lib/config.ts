import path from "node:path";
import { fileURLToPath } from "node:url";
import fs from "fs-extra";
import dotenv from "dotenv";

dotenv.config();

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

export type ScraperConfig = {
  outputDir: string;
  bggUsername?: string;
  bggToken?: string;
};

export function loadConfig(): ScraperConfig {
  const outputDir = process.env.SCRAPER_OUTPUT_DIR ?? path.resolve(__dirname, "../../..", "data");
  const cfg: ScraperConfig = {
    outputDir,
    bggUsername: process.env.BGG_USERNAME,
    bggToken: process.env.BGG_TOKEN,
  };
  fs.ensureDirSync(outputDir);
  return cfg;
}

export function resolveOut(...segments: string[]): string {
  return path.join(loadConfig().outputDir, ...segments);
}
