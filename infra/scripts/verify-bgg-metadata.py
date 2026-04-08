#!/usr/bin/env python3
"""
Query BGG XML API2 for a list of bggIds and return the canonical title,
year published, and description.

Usage: python verify-bgg-metadata.py 181 421006 373106 164928 338111 380607
"""

import sys
import time
import urllib.request
import xml.etree.ElementTree as ET


def fetch_bgg(bgg_id: int) -> dict:
    url = f"https://boardgamegeek.com/xmlapi2/thing?id={bgg_id}&stats=0"
    req = urllib.request.Request(url, headers={"User-Agent": "meepleai-seed-verify/1.0"})
    with urllib.request.urlopen(req, timeout=20) as r:
        xml = r.read().decode("utf-8")

    root = ET.fromstring(xml)
    item = root.find("item")
    if item is None:
        return {"bggId": bgg_id, "error": "no item"}

    # Primary name
    primary = next(
        (n.get("value") for n in item.findall("name") if n.get("type") == "primary"),
        None,
    )

    year_elem = item.find("yearpublished")
    year = year_elem.get("value") if year_elem is not None else None

    desc_elem = item.find("description")
    desc = (desc_elem.text or "").strip() if desc_elem is not None else ""
    desc_preview = desc[:200].replace("\n", " ")

    return {
        "bggId": bgg_id,
        "name": primary,
        "year": year,
        "description_preview": desc_preview,
    }


def main():
    ids = [int(x) for x in sys.argv[1:]]
    if not ids:
        print("Usage: verify-bgg-metadata.py <bggId> [bggId...]", file=sys.stderr)
        sys.exit(1)

    for i, bgg_id in enumerate(ids):
        if i > 0:
            time.sleep(1.5)  # BGG rate limit politeness
        try:
            info = fetch_bgg(bgg_id)
            print(f"\n=== bggId {bgg_id} ===")
            print(f"  Name: {info.get('name')}")
            print(f"  Year: {info.get('year')}")
            print(f"  Desc: {info.get('description_preview')}...")
        except Exception as exc:
            print(f"\n=== bggId {bgg_id} ===")
            print(f"  ERROR: {exc}", file=sys.stderr)


if __name__ == "__main__":
    main()
