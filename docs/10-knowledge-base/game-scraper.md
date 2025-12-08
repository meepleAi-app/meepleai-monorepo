# GAME_SCRAPER

Goal: build a small toolchain to pull board-game data from BoardGameGeek (BGG) and local rulebooks, then emit clean datasets for QA and play-by-play examples.

## Outputs
- data/bgg/plays/<game_id>.jsonl: normalized plays (one JSON object per play event or per play summary).
- data/bgg/games.jsonl: metadata for games we touch (id, name, rank, year, min/max players, weight, categories, mechanics).
- data/rulebooks/qa/<game_id>.jsonl: grounded Q&A pairs extracted from local PDF rulebooks.
- data/synthetic/turn_logs/<game_id>.jsonl: synthetic, rule-consistent "Player A does X, Player B does Y" snippets when real logs are absent.

## Data sources
- BGG XML API2
  - search?query=<name>&type=boardgame to resolve name -> id.
  - thing?id=<id>&stats=1 for game metadata.
  - plays?id=<id>&type=thing&mindate=YYYY-MM-DD for public play logs (respect ~5s between requests).
  - https://boardgamegeek.com/data_dumps/bg_ranks for periodic CSV of all games with rank/rating.
- Local rulebooks (PDFs) under rulebooks/. We will OCR only if text layer is missing.

## Architecture (proposed)
- Language: TypeScript (Node 20). Why: easy XML->JSON, good IO, aligns with repo tooling.
- Packages: axios (HTTP), xml2js (XML parsing), p-limit or simple sleep for rate limiting, pdf-parse for text extraction.
- Entry points (CLI):
  - pnpm scraper:plays --game-id 13 --mindate 2024-01-01
  - pnpm scraper:game --game-id 13
  - pnpm scraper:qa --rulebook rulebooks/catan.pdf --game-id 13
- Config: .env with optional BGG_USERNAME, BGG_TOKEN (if BGG enforces keys), SCRAPER_OUTPUT_DIR.

## Normalized schemas
- Play (summary):
  ```json
  {
    "game_id": 13,
    "bgg_play_id": 1234567,
    "date": "2025-02-12",
    "players": [
      {"name": "Alice", "score": 10, "win": true},
      {"name": "Bob", "score": 8, "win": false}
    ],
    "length_minutes": 55,
    "location": "online",
    "comments": ""
  }
  ```
- Play (turn-level, optional when comments/logs allow):
  ```json
  {"game_id":13,"play_id":1234567,"turn":1,"player":"Alice","action":"Builds settlement at A1","state_after":"Alice 1 settlement, 1 road"}
  ```
- QA item (grounded in rulebook):
  ```json
  {
    "game_id":13,
    "question":"Quante carte risorsa posso scambiare col banco?",
    "answer":"Puoi scambiare quattro carte identiche per una risorsa a tua scelta.",
    "page":7,
    "source":"rulebooks/catan.pdf"
  }
  ```

## QA generation flow
1) Extract text from PDF (pdf-parse); if text layer missing, OCR via tesseract (optional flag).
2) Chunk by headings (regex on numbered sections) to keep context windows small.
3) Template or LLM-assisted question drafting (setup, turn structure, costs, edge cases).
4) For each question, quote the exact answer sentence from the chunk; record page for traceability.
5) Validate: reject answers not verbatim in source; keep length <= 2 sentences.

## BGG play ingestion flow
1) Resolve game ids via search or bg_ranks CSV.
2) Pull plays with pagination (page=1..N); sleep 5s per request.
3) Parse <players><player .../> nodes; keep scores/win flags; capture <comments> if present.
4) Store JSONL; dedupe by bgg_play_id if re-running.

## Synthetic turn logs (fallback)
- When real turn-level logs are absent, script can sample legal actions from rulebook-derived action lists to emit 5-10 coherent turns per game for demo/training.

## Initial milestones
- [ ] CLI scaffold + config loading
- [ ] game fetch -> data/bgg/games.jsonl
- [ ] plays fetch with rate limiting -> JSONL
- [ ] PDF text extractor with optional OCR
- [ ] QA generator that emits grounded pairs with page numbers
- [ ] Minimal validation script (no hallucinations, non-empty answers)

## Usage notes
- Always throttle BGG calls (~5s) to avoid 429.
- Keep raw XML responses only for debugging; JSONL is canonical.
- Ground every QA answer in the rulebook text; store page for auditing.
