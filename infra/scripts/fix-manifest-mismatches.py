#!/usr/bin/env python3
"""
Fix pre-existing BGG scraping mismatches in seed manifests.

For 3 entries where the BGG scraper fetched data for a completely different
game (wrong bggId), clear all bgg-sourced fields and set bggEnhanced=false.
This removes wrong RAG content without requiring us to guess correct values;
the bgg-fetcher tool can be re-run later to populate them properly.

Affected entries (same bug in dev.yml, staging.yml, prod.yml):
  - Voidfall (bggId=338111): has 1957 naval trick-taking card game metadata
  - Great Western Trail: Argentina (bggId=380607): has NZ description/metadata
  - Skytear: Arena of Legends (bggId=373106): has Sky Team metadata
"""

import sys
from pathlib import Path

import yaml

BAD_BGG_IDS = {338111, 380607, 373106}

# Fields populated by bgg-fetcher — all removed when a mismatch is detected
# (fallbackImageUrl/Thumbnail are kept by replacing with a placehold.co URL
# to preserve the CatalogSeederTests.LoadManifest_DevProfile_GamesHaveFallbackImages
# invariant that every game has fallback images for UI rendering.)
BGG_FIELDS = {
    "description",
    "yearPublished",
    "minPlayers",
    "maxPlayers",
    "playingTime",
    "minAge",
    "averageRating",
    "averageWeight",
    "imageUrl",
    "thumbnailUrl",
    "categories",
    "mechanics",
    "designers",
    "publishers",
}


def _placeholder_image(title: str, width: int, height: int) -> str:
    from urllib.parse import quote
    return f"https://placehold.co/{width}x{height}?text={quote(title)}"


def fix_entry(game: dict) -> bool:
    """Clear bgg-sourced fields in place. Returns True if modified."""
    modified = False
    if game.get("bggEnhanced"):
        game["bggEnhanced"] = False
        modified = True
    for field in list(game.keys()):
        if field in BGG_FIELDS:
            del game[field]
            modified = True
    # Replace fallback images with placeholders (test invariant: every dev
    # game must have fallback image + thumbnail URLs)
    title = game.get("title", "Unknown")
    game["fallbackImageUrl"] = _placeholder_image(title, 400, 300)
    game["fallbackThumbnailUrl"] = _placeholder_image(title, 150, 150)
    modified = True
    return modified


def process_manifest(yml_path: Path) -> int:
    with open(yml_path, "r", encoding="utf-8") as f:
        doc = yaml.safe_load(f)

    games = doc["catalog"]["games"]
    fixed = 0
    for g in games:
        if g.get("bggId") in BAD_BGG_IDS:
            if fix_entry(g):
                fixed += 1
                print(f"  fixed: {g['title']} (bggId={g['bggId']})")

    with open(yml_path, "w", encoding="utf-8") as f:
        yaml.dump(doc, f, default_flow_style=False, allow_unicode=True, sort_keys=False, width=120)

    return fixed


def main():
    repo_root = Path(__file__).resolve().parent.parent.parent
    manifest_dir = repo_root / "apps" / "api" / "src" / "Api" / "Infrastructure" / "Seeders" / "Catalog" / "Manifests"

    total = 0
    for name in ("staging.yml", "dev.yml", "prod.yml"):
        yml = manifest_dir / name
        if not yml.exists():
            continue
        print(f"Processing {name}:")
        total += process_manifest(yml)

    print(f"\nTotal entries fixed: {total}")


if __name__ == "__main__":
    main()
