#!/usr/bin/env python3
"""
Fill skeleton entries in staging.yml with real game metadata.

Reads skeletons (entries with title 'TODO: fill from manifest.json (...)') from
staging.yml, maps by slug to data from:
  - data/rulebook/manifest.json (for games in PR #267 manifest)
  - Hardcoded lookup for games not in manifest.json (popular titles)

Preserves existing pdfBlobKey / pdfSha256 / pdfVersion on each skeleton.
"""

import json
import re
import sys
from pathlib import Path

import yaml

# BGG metadata for games not in data/rulebook/manifest.json
# Verified from BGG
HARDCODED_METADATA = {
    "photosynthesis": {
        "title": "Photosynthesis",
        "bggId": 218603,
        "yearPublished": 2017,
        "minPlayers": 2,
        "maxPlayers": 4,
        "playingTime": 60,
        "minAge": 8,
        "designers": ["Hjalmar Hach"],
        "publishers": ["Blue Orange (EU)"],
        "categories": ["Abstract Strategy", "Environmental"],
        "mechanics": ["Area Majority / Influence", "Grid Movement", "Point to Point Movement"],
    },
    "sagrada": {
        "title": "Sagrada",
        "bggId": 199561,
        "yearPublished": 2017,
        "minPlayers": 1,
        "maxPlayers": 4,
        "playingTime": 45,
        "minAge": 14,
        "designers": ["Adrian Adamescu", "Daryl Andrews"],
        "publishers": ["Floodgate Games"],
        "categories": ["Abstract Strategy", "Puzzle"],
        "mechanics": ["Dice Drafting", "Pattern Building", "Set Collection"],
    },
    "santorini": {
        "title": "Santorini",
        "bggId": 194655,
        "yearPublished": 2016,
        "minPlayers": 2,
        "maxPlayers": 4,
        "playingTime": 20,
        "minAge": 8,
        "designers": ["Gordon Hamilton"],
        "publishers": ["Spin Master Ltd."],
        "categories": ["Abstract Strategy", "Mythology"],
        "mechanics": ["Grid Movement", "Pattern Building", "Variable Player Powers"],
    },
    "villainous": {
        "title": "Disney Villainous",
        "bggId": 256382,
        "yearPublished": 2018,
        "minPlayers": 2,
        "maxPlayers": 6,
        "playingTime": 60,
        "minAge": 10,
        "designers": ["Prospero Hall"],
        "publishers": ["Ravensburger"],
        "categories": ["Card Game", "Fantasy", "Movies / TV / Radio theme"],
        "mechanics": ["Action Points", "Hand Management", "Variable Player Powers"],
    },
}


def load_manifest_games(manifest_path: Path) -> dict:
    """Load data/rulebook/manifest.json and return slug -> game dict."""
    with open(manifest_path, "r", encoding="utf-8") as f:
        data = json.load(f)
    result = {}
    for g in data.get("games", []):
        result[g["slug"]] = g
    return result


def manifest_to_seed_fields(mg: dict) -> dict:
    """Convert data/rulebook/manifest.json entry to SeedManifestGame fields."""
    md = mg.get("metadata", {}) or {}
    return {
        "title": mg.get("name", mg["slug"]),
        "bggId": mg.get("bggId", 0) or 0,
        "yearPublished": md.get("yearPublished"),
        "minPlayers": md.get("minPlayers"),
        "maxPlayers": md.get("maxPlayers"),
        "playingTime": md.get("playingTimeMinutes"),
        "minAge": md.get("minAge"),
        "designers": md.get("designers") or [],
        "publishers": md.get("publishers") or [],
        "categories": md.get("categories") or [],
        "mechanics": md.get("mechanics") or [],
    }


def build_filled_entry(slug: str, skeleton: dict, manifest_games: dict) -> dict:
    """Build a fully-populated entry from a skeleton, preserving blob fields."""
    # Preserve blob fields from skeleton
    pdf_blob_key = skeleton.get("pdfBlobKey")
    pdf_sha256 = skeleton.get("pdfSha256")
    pdf_version = skeleton.get("pdfVersion", "1.0")

    # Find metadata source
    if slug in manifest_games:
        fields = manifest_to_seed_fields(manifest_games[slug])
    elif slug in HARDCODED_METADATA:
        fields = HARDCODED_METADATA[slug]
    else:
        raise ValueError(f"No metadata source for slug: {slug}")

    # Build ordered entry (order matters for YAML readability)
    entry = {
        "title": fields["title"],
        "bggId": fields["bggId"],
        "language": skeleton.get("language", "en"),
    }
    if fields.get("yearPublished") is not None:
        entry["yearPublished"] = fields["yearPublished"]
    if fields.get("minPlayers") is not None:
        entry["minPlayers"] = fields["minPlayers"]
    if fields.get("maxPlayers") is not None:
        entry["maxPlayers"] = fields["maxPlayers"]
    if fields.get("playingTime") is not None:
        entry["playingTime"] = fields["playingTime"]
    if fields.get("minAge") is not None:
        entry["minAge"] = fields["minAge"]
    if fields.get("categories"):
        entry["categories"] = fields["categories"]
    if fields.get("mechanics"):
        entry["mechanics"] = fields["mechanics"]
    if fields.get("designers"):
        entry["designers"] = fields["designers"]
    if fields.get("publishers"):
        entry["publishers"] = fields["publishers"]

    entry["pdfBlobKey"] = pdf_blob_key
    entry["pdfSha256"] = pdf_sha256
    entry["pdfVersion"] = pdf_version
    return entry


def main():
    repo_root = Path(__file__).resolve().parent.parent.parent
    manifest_json = repo_root / "data" / "rulebook" / "manifest.json"
    staging_yml = repo_root / "apps" / "api" / "src" / "Api" / "Infrastructure" / "Seeders" / "Catalog" / "Manifests" / "staging.yml"

    if not manifest_json.exists():
        print(f"ERROR: {manifest_json} not found", file=sys.stderr)
        sys.exit(1)
    if not staging_yml.exists():
        print(f"ERROR: {staging_yml} not found", file=sys.stderr)
        sys.exit(1)

    manifest_games = load_manifest_games(manifest_json)
    print(f"Loaded {len(manifest_games)} games from manifest.json")

    with open(staging_yml, "r", encoding="utf-8") as f:
        doc = yaml.safe_load(f)

    games = doc["catalog"]["games"]
    skeleton_pattern = re.compile(r"^TODO: fill from manifest\.json \(([^)]+)\)$")

    filled = 0
    missing = []
    for i, game in enumerate(games):
        title = game.get("title", "")
        m = skeleton_pattern.match(title)
        if not m:
            continue
        slug = m.group(1)
        try:
            new_entry = build_filled_entry(slug, game, manifest_games)
            games[i] = new_entry
            filled += 1
            print(f"  filled: {slug} -> bggId={new_entry['bggId']} ({new_entry['title']})")
        except ValueError as exc:
            missing.append(slug)
            print(f"  MISSING: {exc}", file=sys.stderr)

    if missing:
        print(f"\nERROR: {len(missing)} slugs have no metadata: {missing}", file=sys.stderr)
        sys.exit(2)

    with open(staging_yml, "w", encoding="utf-8") as f:
        yaml.dump(doc, f, default_flow_style=False, allow_unicode=True, sort_keys=False, width=120)

    print(f"\nDone. Filled {filled} skeleton entries in {staging_yml.name}")


if __name__ == "__main__":
    main()
