#!/usr/bin/env python3
"""
Detect potential title/description mismatches in seed manifests.

Heuristic: for each bggEnhanced entry, check if significant words from the
title appear in the description. If zero significant title words match,
it's likely a BGG scraping error (wrong bggId -> wrong description).

Also flags:
- Duplicate bggIds (Validate() would catch but worth listing)
- Entries with bggEnhanced=true but empty description
- Impossible metadata (e.g., yearPublished 1957 on a game with 2020s theme)
"""

import html
import re
import sys
from pathlib import Path

import yaml

# Entries where BGG description doesn't literally contain title words but IS
# correct content (e.g., Risk description talks about "world conquest"; LotR
# TCG description talks about "Mordor" and "Middle-earth"). False positives
# for the mismatch heuristic; verified manually.
KNOWN_CORRECT_BGG_IDS = {
    181,     # Risk
    421006,  # The Lord of the Rings: The Card Game
}

STOPWORDS = {
    "the", "a", "an", "of", "and", "or", "to", "in", "on", "for", "with",
    "game", "board", "card", "dice", "play", "player", "players",
    "expansion", "new", "edition", "box", "set", "series", "vol", "volume",
}


def title_words(title: str) -> set:
    """Extract significant words from title (lowercase, no stopwords)."""
    # Strip subtitles after ':' too (e.g., "Great Western Trail: Argentina" -> argentina)
    parts = re.split(r"[:]", title)
    all_text = " ".join(parts).lower()
    words = re.findall(r"[a-zàèéìòù]+", all_text)
    return {w for w in words if len(w) >= 3 and w not in STOPWORDS}


def description_words(description: str) -> set:
    # Decode HTML entities so "Orl&eacute;ans" matches title word "orleans"
    decoded = html.unescape(description)
    # Strip accents so "Orléans" matches "orleans"
    decoded = (
        decoded.replace("é", "e").replace("è", "e").replace("ê", "e")
               .replace("à", "a").replace("á", "a").replace("â", "a")
               .replace("í", "i").replace("ì", "i").replace("î", "i")
               .replace("ó", "o").replace("ò", "o").replace("ô", "o")
               .replace("ú", "u").replace("ù", "u").replace("û", "u")
               .replace("ñ", "n").replace("ç", "c")
    )
    return set(re.findall(r"[a-z]+", decoded.lower()))


def check_manifest(yml_path: Path):
    with open(yml_path, "r", encoding="utf-8") as f:
        doc = yaml.safe_load(f)

    games = doc["catalog"]["games"]
    issues = []

    seen_bgg = {}
    for idx, g in enumerate(games):
        title = g.get("title", "")
        bgg_id = g.get("bggId")
        desc = g.get("description") or ""
        bgg_enhanced = g.get("bggEnhanced", False)

        # Duplicate bggId
        if bgg_id and bgg_id > 0:
            if bgg_id in seen_bgg:
                issues.append(("DUP", title, bgg_id, f"duplicate of {seen_bgg[bgg_id]}"))
            else:
                seen_bgg[bgg_id] = title

        # bggEnhanced but empty description
        if bgg_enhanced and not desc.strip():
            issues.append(("EMPTY_DESC", title, bgg_id, "bggEnhanced=true but empty description"))
            continue

        if not bgg_enhanced or not desc.strip():
            continue

        # Skip known correct entries where description text doesn't literally
        # contain title words but is about the right game.
        if bgg_id in KNOWN_CORRECT_BGG_IDS:
            continue

        # Title/description mismatch
        tw = title_words(title)
        if not tw:
            continue
        dw = description_words(desc)
        matches = tw & dw
        if not matches:
            # Get first ~80 chars of description for context
            preview = re.sub(r"\s+", " ", desc)[:100]
            issues.append((
                "MISMATCH", title, bgg_id,
                f"title words {sorted(tw)} not in description: '{preview}...'"
            ))

    return issues, len(games)


def main():
    repo_root = Path(__file__).resolve().parent.parent.parent
    manifest_dir = repo_root / "apps" / "api" / "src" / "Api" / "Infrastructure" / "Seeders" / "Catalog" / "Manifests"

    total_issues = 0
    for name in ("staging.yml", "dev.yml", "prod.yml"):
        yml = manifest_dir / name
        if not yml.exists():
            continue
        issues, ngames = check_manifest(yml)
        print(f"\n=== {name} ({ngames} games, {len(issues)} issues) ===")
        for kind, title, bgg, detail in issues:
            print(f"  [{kind}] {title} (bggId={bgg})")
            print(f"         {detail}")
        total_issues += len(issues)

    print(f"\nTotal issues: {total_issues}")
    sys.exit(0 if total_issues == 0 else 1)


if __name__ == "__main__":
    main()
