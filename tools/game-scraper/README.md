# Game Scraper package

This folder hosts the BoardGameGeek/rulebook scraper described in ../../GAME_SCRAPER.md.

## Install
```
pnpm install --dir tools/game-scraper
```

## Commands
- `pnpm --dir tools/game-scraper scraper:game --game-id 13`
- `pnpm --dir tools/game-scraper scraper:plays --game-id 13 --mindate 2024-01-01`
- `pnpm --dir tools/game-scraper scraper:qa --rulebook rulebooks/catan.pdf --game-id 13`
- `pnpm --dir tools/game-scraper scraper:validate [--game-id 13]`

Outputs land under `data/` by default (override with `SCRAPER_OUTPUT_DIR`).

## Notes
- Rate limit is hard-coded to 5s/request for BGG plays.
- QA command currently writes placeholder questions; fill real generation logic later.
