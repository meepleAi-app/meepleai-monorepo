#!/usr/bin/env python3
"""
Add placehold.co fallback image URLs to entries missing them.

The CatalogSeederTests.LoadManifest_DevProfile_GamesHaveFallbackImages test
requires every dev game to have both fallbackImageUrl and fallbackThumbnailUrl.
Entries created via fill-skeleton-entries.py don't set these fields, so this
script patches all 3 manifests to add placehold.co URLs where missing.

Idempotent: skips entries that already have both fields set.
"""

import sys
from pathlib import Path
from urllib.parse import quote

import yaml


def placeholder(title: str, width: int, height: int) -> str:
    return f"https://placehold.co/{width}x{height}?text={quote(title)}"


def process(yml_path: Path) -> int:
    with open(yml_path, "r", encoding="utf-8") as f:
        doc = yaml.safe_load(f)

    games = doc["catalog"]["games"]
    patched = 0
    for g in games:
        title = g.get("title", "Unknown")
        if not g.get("fallbackImageUrl"):
            g["fallbackImageUrl"] = placeholder(title, 400, 300)
            patched += 1
        if not g.get("fallbackThumbnailUrl"):
            g["fallbackThumbnailUrl"] = placeholder(title, 150, 150)
            patched += 1

    with open(yml_path, "w", encoding="utf-8") as f:
        yaml.dump(doc, f, default_flow_style=False, allow_unicode=True, sort_keys=False, width=120)

    return patched


def main():
    repo_root = Path(__file__).resolve().parent.parent.parent
    manifest_dir = repo_root / "apps" / "api" / "src" / "Api" / "Infrastructure" / "Seeders" / "Catalog" / "Manifests"

    total = 0
    for name in ("staging.yml", "dev.yml", "prod.yml"):
        yml = manifest_dir / name
        if yml.exists():
            patched = process(yml)
            print(f"{name}: {patched} field(s) patched")
            total += patched

    print(f"\nTotal: {total} fields patched")


if __name__ == "__main__":
    main()
