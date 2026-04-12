#!/usr/bin/env python3
"""
Mirror games present in staging.yml but not in dev.yml/prod.yml.

Computes the set difference by bggId and appends missing entries to
dev.yml and prod.yml. Preserves entry order in the source (staging) and
appends new entries at the end of catalog.games in the target files.

This is useful when new games are added to staging for testing and later
need to be promoted to dev (for local development) and prod (for
production seeding).
"""

import sys
from pathlib import Path

import yaml


def load_manifest(yml_path: Path) -> dict:
    with open(yml_path, "r", encoding="utf-8") as f:
        return yaml.safe_load(f)


def save_manifest(yml_path: Path, doc: dict) -> None:
    with open(yml_path, "w", encoding="utf-8") as f:
        yaml.dump(doc, f, default_flow_style=False, allow_unicode=True, sort_keys=False, width=120)


def bgg_id_set(games: list) -> set:
    return {g["bggId"] for g in games if g.get("bggId")}


def main():
    repo_root = Path(__file__).resolve().parent.parent.parent
    manifest_dir = repo_root / "apps" / "api" / "src" / "Api" / "Infrastructure" / "Seeders" / "Catalog" / "Manifests"

    staging = load_manifest(manifest_dir / "staging.yml")
    dev = load_manifest(manifest_dir / "dev.yml")
    prod = load_manifest(manifest_dir / "prod.yml")

    staging_games = staging["catalog"]["games"]
    dev_games = dev["catalog"]["games"]
    prod_games = prod["catalog"]["games"]

    dev_bgg = bgg_id_set(dev_games)
    prod_bgg = bgg_id_set(prod_games)

    missing_from_dev = [g for g in staging_games if g.get("bggId") and g["bggId"] not in dev_bgg]
    missing_from_prod = [g for g in staging_games if g.get("bggId") and g["bggId"] not in prod_bgg]

    print(f"staging.yml: {len(staging_games)} games")
    print(f"dev.yml:     {len(dev_games)} games  ({len(missing_from_dev)} missing)")
    print(f"prod.yml:    {len(prod_games)} games  ({len(missing_from_prod)} missing)")

    if missing_from_dev:
        print("\nAppending to dev.yml:")
        for g in missing_from_dev:
            print(f"  + {g['title']} (bggId={g['bggId']})")
            dev_games.append(g)
        save_manifest(manifest_dir / "dev.yml", dev)

    if missing_from_prod:
        print("\nAppending to prod.yml:")
        for g in missing_from_prod:
            print(f"  + {g['title']} (bggId={g['bggId']})")
            prod_games.append(g)
        save_manifest(manifest_dir / "prod.yml", prod)

    print(f"\nDone. dev.yml: {len(dev_games)} games, prod.yml: {len(prod_games)} games")


if __name__ == "__main__":
    main()
