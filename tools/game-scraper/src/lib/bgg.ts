import axios from "axios";
import xml2js from "xml2js";
import { setTimeout as sleep } from "node:timers/promises";
import type { ScraperConfig } from "./config.js";

export type BggPlay = {
  id: number;
  date: string;
  lengthMinutes?: number;
  location?: string;
  players: { name: string; score?: number; win?: boolean }[];
  comments?: string;
};

export async function fetchPlays(gameId: number, mindate?: string, cfg?: ScraperConfig): Promise<BggPlay[]> {
  const plays: BggPlay[] = [];
  let page = 1;
  const parser = new xml2js.Parser({ explicitArray: false, mergeAttrs: true });
  while (true) {
    const url = `https://boardgamegeek.com/xmlapi2/plays?id=${gameId}&type=thing&page=${page}${mindate ? `&mindate=${mindate}` : ""}`;
    const res = await axios.get(url, { responseType: "text" });
    const json = await parser.parseStringPromise(res.data);
    const playNodes = json?.plays?.play;
    if (!playNodes || (Array.isArray(playNodes) && playNodes.length === 0)) break;
    const list = Array.isArray(playNodes) ? playNodes : [playNodes];
    for (const p of list) {
      const players = (Array.isArray(p.players?.player) ? p.players.player : [p.players?.player])
        .filter(Boolean)
        .map((pl: any) => ({ name: pl.name, score: num(pl.score), win: pl.win === "1" }));
      plays.push({
        id: num(p.id),
        date: p.date,
        lengthMinutes: num(p.length),
        location: p.location,
        comments: p.comments,
        players,
      });
    }
    if (json?.plays?._total && Number(json.plays._total) <= plays.length) break;
    page += 1;
    await sleep(5000); // be kind to BGG
  }
  return plays;
}

function num(v: any): number | undefined {
  const n = Number(v);
  return Number.isFinite(n) ? n : undefined;
}
