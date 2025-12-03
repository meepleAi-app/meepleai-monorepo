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

export type BggThing = {
  id: number;
  name: string;
  yearPublished?: number;
  minPlayers?: number;
  maxPlayers?: number;
  playingTime?: number;
  minPlayTime?: number;
  maxPlayTime?: number;
  weight?: number;
  rank?: number;
  usersRated?: number;
  categories?: string[];
  mechanics?: string[];
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
        id: num(p.id) as number,
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

export async function fetchThing(gameId: number): Promise<BggThing | undefined> {
  const parser = new xml2js.Parser({ explicitArray: false, mergeAttrs: true });
  const url = `https://boardgamegeek.com/xmlapi2/thing?id=${gameId}&stats=1`;
  const res = await axios.get(url, { responseType: "text" });
  const json = await parser.parseStringPromise(res.data);
  const thing = json?.items?.item;
  if (!thing) return undefined;
  const pollRank =
    thing.statistics?.ratings?.ranks?.rank &&
    (Array.isArray(thing.statistics.ratings.ranks.rank) ? thing.statistics.ratings.ranks.rank : [thing.statistics.ratings.ranks.rank]).find(
      (r: any) => r.name === "boardgame"
    );
  const list = (obj: any, type: string) =>
    (Array.isArray(obj?.link) ? obj.link : obj?.link ? [obj.link] : [])
      .filter((l: any) => l.type === type)
      .map((l: any) => l.value);

  return {
    id: num(thing.id) as number,
    name: thing.name?.value ?? "",
    yearPublished: num(thing.yearpublished?.value),
    minPlayers: num(thing.minplayers?.value),
    maxPlayers: num(thing.maxplayers?.value),
    playingTime: num(thing.playingtime?.value),
    minPlayTime: num(thing.minplaytime?.value),
    maxPlayTime: num(thing.maxplaytime?.value),
    weight: num(thing.statistics?.ratings?.averageweight?.value),
    rank: num(pollRank?.value),
    usersRated: num(thing.statistics?.ratings?.usersrated?.value),
    categories: list(thing, "boardgamecategory"),
    mechanics: list(thing, "boardgamemechanic"),
  };
}

function num(v: any): number | undefined {
  const n = Number(v);
  return Number.isFinite(n) ? n : undefined;
}
