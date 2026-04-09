#!/usr/bin/env python3
"""Diff dev.yml vs staging.yml: games, agents, default agent section."""

from pathlib import Path
import yaml

repo_root = Path(__file__).resolve().parent.parent.parent
manifest_dir = repo_root / "apps" / "api" / "src" / "Api" / "Infrastructure" / "Seeders" / "Catalog" / "Manifests"


def load(name):
    with open(manifest_dir / name, "r", encoding="utf-8") as f:
        return yaml.safe_load(f)


dev = load("dev.yml")
stg = load("staging.yml")

dev_games = dev["catalog"]["games"]
stg_games = stg["catalog"]["games"]

dev_bgg = {g["bggId"]: g for g in dev_games if g.get("bggId")}
stg_bgg = {g["bggId"]: g for g in stg_games if g.get("bggId")}

print(f"dev.yml:     {len(dev_games)} games  | defaultAgent={'defaultAgent' in dev['catalog']}")
print(f"staging.yml: {len(stg_games)} games  | defaultAgent={'defaultAgent' in stg['catalog']}")
print()

only_in_stg = set(stg_bgg) - set(dev_bgg)
only_in_dev = set(dev_bgg) - set(stg_bgg)
print(f"Games only in staging: {len(only_in_stg)}")
for bid in sorted(only_in_stg):
    print(f"  + {stg_bgg[bid]['title']} ({bid})")
print(f"Games only in dev: {len(only_in_dev)}")
for bid in sorted(only_in_dev):
    print(f"  - {dev_bgg[bid]['title']} ({bid})")

print()
print(f"seedAgent=true in dev:     {sum(1 for g in dev_games if g.get('seedAgent'))}")
print(f"seedAgent=true in staging: {sum(1 for g in stg_games if g.get('seedAgent'))}")

print()
print("Field completeness (same bggId, different keys):")
common = set(dev_bgg) & set(stg_bgg)
fields_diff = {}
for bid in common:
    d, s = dev_bgg[bid], stg_bgg[bid]
    for k in set(d.keys()) | set(s.keys()):
        if k not in d:
            fields_diff.setdefault(f"only in staging: {k}", []).append(d.get("title") or s.get("title"))
        elif k not in s:
            fields_diff.setdefault(f"only in dev: {k}", []).append(d.get("title"))
for k, titles in sorted(fields_diff.items()):
    print(f"  {k}: {len(titles)} games ({titles[:3]}...)")

if "defaultAgent" in stg["catalog"]:
    print()
    print("staging.yml defaultAgent:")
    print(f"  {stg['catalog']['defaultAgent']}")
if "defaultAgent" in dev["catalog"]:
    print("dev.yml defaultAgent:")
    print(f"  {dev['catalog']['defaultAgent']}")
