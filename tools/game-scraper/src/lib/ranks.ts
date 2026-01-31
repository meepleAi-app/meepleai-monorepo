import axios from "axios";
import { parse } from "csv-parse/sync";

export type RankEntry = { id: number; name: string; rank: number };

const RANKS_URL = "https://boardgamegeek.com/data_dumps/bg_ranks";

export async function fetchTopRanks(limit = 10): Promise<RankEntry[]> {
  const res = await axios.get(RANKS_URL, { responseType: "text" });
  const records: any[] = parse(res.data, { columns: true, skip_empty_lines: true });
  return records
    .map((r) => ({ id: Number(r.objectid), name: r.name, rank: Number(r.rank) }))
    .filter((r) => Number.isFinite(r.rank))
    .sort((a, b) => a.rank - b.rank)
    .slice(0, limit);
}
