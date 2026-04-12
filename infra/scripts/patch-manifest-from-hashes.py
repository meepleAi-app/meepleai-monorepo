#!/usr/bin/env python3
"""
Python equivalent of patch-manifest-from-hashes.sh — rewrites the YAML
manifests in apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/
by converting existing `pdf:` entries to `pdfBlobKey:` + `pdfSha256:`
based on the .seed-hashes.tsv file produced by upload-seed-pdfs.py.

For slugs that exist in .seed-hashes.tsv but have no matching manifest entry,
appends a skeleton entry at the bottom of staging.yml for manual completion.

Usage:
    python infra/scripts/patch-manifest-from-hashes.py [HASHES_TSV]

Requires:
    - PyYAML (pip install PyYAML)
"""

import sys
from pathlib import Path

import yaml


def load_hashes(tsv_path: Path) -> dict:
    """Load slug->sha256 mapping from a TSV file."""
    result = {}
    if not tsv_path.exists():
        print(f"ERROR: hashes file not found: {tsv_path}", file=sys.stderr)
        sys.exit(1)
    with open(tsv_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            parts = line.split("\t")
            if len(parts) != 2:
                continue
            slug, sha = parts
            if slug and sha:
                result[slug] = sha
    return result


def patch_manifest(yml_path: Path, hashes: dict) -> tuple[int, int]:
    """
    Patch a single manifest YAML in place. Returns (matched, appended).

    For each slug in hashes:
      - If an entry has `pdf: {slug}_rulebook.pdf`, replace with pdfBlobKey/Sha/Version.
      - Otherwise skip (appends to staging only in a separate pass).
    """
    if not yml_path.exists():
        print(f"  SKIP (missing): {yml_path}")
        return (0, 0)

    with open(yml_path, "r", encoding="utf-8") as f:
        doc = yaml.safe_load(f)

    if not doc or "catalog" not in doc or "games" not in doc["catalog"]:
        print(f"  SKIP (no catalog.games): {yml_path}")
        return (0, 0)

    games = doc["catalog"]["games"]
    matched = 0
    seen_blob_keys = set()

    # Pass 1: rewrite existing pdf: entries
    for game in games:
        blob_key_existing = game.get("pdfBlobKey")
        if blob_key_existing:
            seen_blob_keys.add(blob_key_existing)
            continue

        pdf_value = game.get("pdf")
        if not pdf_value:
            continue

        # Filename can be "slug_rulebook.pdf"
        if not pdf_value.endswith("_rulebook.pdf"):
            continue
        slug = pdf_value.removesuffix("_rulebook.pdf")

        sha = hashes.get(slug)
        if not sha:
            continue

        blob_key = f"rulebooks/v1/{slug}_rulebook.pdf"
        game["pdfBlobKey"] = blob_key
        game["pdfSha256"] = sha
        game["pdfVersion"] = game.get("pdfVersion", "1.0")
        game.pop("pdf", None)
        seen_blob_keys.add(blob_key)
        matched += 1

    # Write back (preserve key order by using default_flow_style=False)
    with open(yml_path, "w", encoding="utf-8") as f:
        yaml.dump(doc, f, default_flow_style=False, allow_unicode=True, sort_keys=False, width=120)

    print(f"  patched {matched} entries: {yml_path.name}")
    return (matched, 0)


def append_missing(yml_path: Path, hashes: dict) -> int:
    """
    For slugs in hashes that don't have a matching entry (by pdfBlobKey),
    append a skeleton entry at the bottom of the manifest.
    Returns count of appended entries.
    """
    with open(yml_path, "r", encoding="utf-8") as f:
        doc = yaml.safe_load(f)

    games = doc["catalog"]["games"]
    existing_keys = {g.get("pdfBlobKey") for g in games if g.get("pdfBlobKey")}

    appended = 0
    for slug, sha in sorted(hashes.items()):
        blob_key = f"rulebooks/v1/{slug}_rulebook.pdf"
        if blob_key in existing_keys:
            continue
        games.append({
            "title": f"TODO: fill from manifest.json ({slug})",
            "bggId": 0,
            "language": "en",
            "pdfBlobKey": blob_key,
            "pdfSha256": sha,
            "pdfVersion": "1.0",
        })
        appended += 1

    with open(yml_path, "w", encoding="utf-8") as f:
        yaml.dump(doc, f, default_flow_style=False, allow_unicode=True, sort_keys=False, width=120)

    print(f"  appended {appended} skeleton entries to {yml_path.name}")
    return appended


def main():
    repo_root = Path(__file__).resolve().parent.parent.parent
    hashes_tsv = Path(sys.argv[1]) if len(sys.argv) > 1 else repo_root / "data" / "rulebook" / ".seed-hashes.tsv"

    manifest_dir = repo_root / "apps" / "api" / "src" / "Api" / "Infrastructure" / "Seeders" / "Catalog" / "Manifests"
    staging = manifest_dir / "staging.yml"
    dev = manifest_dir / "dev.yml"
    prod = manifest_dir / "prod.yml"

    hashes = load_hashes(hashes_tsv)
    print(f"Loaded {len(hashes)} slug->sha256 mappings from {hashes_tsv.name}")
    print()

    print("=== Patching existing entries ===")
    for yml in (staging, dev, prod):
        if yml.exists():
            print(f"Patching: {yml.name}")
            patch_manifest(yml, hashes)
    print()

    print("=== Appending skeleton entries (staging only) ===")
    append_missing(staging, hashes)
    print()

    print("Done. Review diffs with:")
    print("  git diff apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/")


if __name__ == "__main__":
    main()
