#!/usr/bin/env python3
"""
Rebuild dev.yml as a clone of staging.yml with the profile marker changed.

Rationale: dev.yml has drifted from staging.yml over time. Notably it is
missing `seedAgent: true` on the 6 core games (Catan, Carcassonne, Pandemic,
Splendor, Ticket to Ride, Descent), which means running the API locally in
Dev profile creates zero AI agents — defeating the purpose of the Dev layer.

This script overwrites dev.yml with staging.yml content, then edits only the
top-level `profile:` field from "staging" to "dev". All games, fallback
images, PDF blob keys, agent flags, and defaultAgent settings are kept
in sync.

Idempotent: safe to re-run.
"""

from pathlib import Path
import yaml

repo_root = Path(__file__).resolve().parent.parent.parent
manifest_dir = repo_root / "apps" / "api" / "src" / "Api" / "Infrastructure" / "Seeders" / "Catalog" / "Manifests"


def main():
    staging_path = manifest_dir / "staging.yml"
    dev_path = manifest_dir / "dev.yml"

    with open(staging_path, "r", encoding="utf-8") as f:
        doc = yaml.safe_load(f)

    if doc.get("profile") != "staging":
        raise SystemExit(f"Expected staging.yml profile='staging', got: {doc.get('profile')}")

    # Flip the profile marker — SeedManifest.Validate() requires profile to
    # match the resource filename/profile enum used by CatalogSeeder.LoadManifest.
    doc["profile"] = "dev"

    with open(dev_path, "w", encoding="utf-8") as f:
        yaml.dump(doc, f, default_flow_style=False, allow_unicode=True, sort_keys=False, width=120)

    games = doc["catalog"]["games"]
    agents = sum(1 for g in games if g.get("seedAgent"))
    blobs = sum(1 for g in games if g.get("pdfBlobKey"))
    print(f"dev.yml rebuilt from staging.yml:")
    print(f"  profile: dev")
    print(f"  games: {len(games)}")
    print(f"  seedAgent=true: {agents}")
    print(f"  pdfBlobKey set: {blobs}")


if __name__ == "__main__":
    main()
