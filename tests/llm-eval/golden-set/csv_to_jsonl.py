"""CSV → JSONL converter for golden set entries.

Usage:
    python csv_to_jsonl.py qa-questions.csv qa-questions.jsonl --schema qa
    python csv_to_jsonl.py translations.csv translation-paragraphs.jsonl --schema translation
"""

import argparse
import csv
import json
import sys
from pathlib import Path
from typing import Callable


def parse_qa_row(row: dict) -> dict:
    """Parse a CSV row into qa-question schema."""
    primary_pages = [int(p.strip()) for p in (row.get("primary_pages") or "").split(",") if p.strip()]
    return {
        "id": row["id"].strip(),
        "game": row["game"].strip(),
        "game_id": row["game_id"].strip(),
        "question_it": row["question_it"].strip(),
        "expected_answer_it": row["expected_answer_it"].strip(),
        "expected_citations": {
            "primary_pages": primary_pages,
            "match_policy": row.get("match_policy", "overlap_at_least_one").strip(),
        },
        "category": row["category"].strip(),
        "difficulty": row["difficulty"].strip(),
        "expected_confidence": row["expected_confidence"].strip(),
    }


def parse_translation_row(row: dict) -> dict:
    """Parse a CSV row into translation-paragraph schema."""
    glossary = {}
    if row.get("glossary_pairs"):
        # Format: "EN1=IT1;EN2=IT2"
        for pair in row["glossary_pairs"].split(";"):
            if "=" in pair:
                k, v = pair.split("=", 1)
                glossary[k.strip()] = v.strip()
    return {
        "id": row["id"].strip(),
        "game": row["game"].strip(),
        "game_id": row["game_id"].strip(),
        "source_lang": row.get("source_lang", "en").strip(),
        "paragraph_id": row["paragraph_id"].strip(),
        "source_text": row["source_text"],
        "expected_translation_it": row["expected_translation_it"],
        "tone": row.get("tone", "neutral").strip(),
        "glossary": glossary,
        "evaluation_criteria": {
            "preserve_glossary": True,
            "preserve_tone": row.get("tone", "neutral").strip(),
            "max_bleu_delta": float(row.get("max_bleu_delta") or 0.3),
            "min_human_score": int(row.get("min_human_score") or 4),
        },
    }


PARSERS: dict[str, Callable[[dict], dict]] = {
    "qa": parse_qa_row,
    "translation": parse_translation_row,
}


def convert(csv_path: Path, jsonl_path: Path, parser: Callable[[dict], dict]) -> int:
    """Convert CSV to JSONL. Returns count of rows."""
    count = 0
    with csv_path.open(encoding="utf-8") as f_in, jsonl_path.open("w", encoding="utf-8") as f_out:
        reader = csv.DictReader(f_in)
        for row in reader:
            try:
                entry = parser(row)
            except KeyError as e:
                print(f"ERROR row {count + 1}: missing field {e}", file=sys.stderr)
                sys.exit(1)
            except ValueError as e:
                print(f"ERROR row {count + 1}: parse error {e}", file=sys.stderr)
                sys.exit(1)
            f_out.write(json.dumps(entry, ensure_ascii=False) + "\n")
            count += 1
    return count


def main() -> int:
    parser = argparse.ArgumentParser(description="Convert CSV golden set to JSONL")
    parser.add_argument("csv_path", type=Path)
    parser.add_argument("jsonl_path", type=Path)
    parser.add_argument("--schema", choices=PARSERS.keys(), required=True)
    args = parser.parse_args()

    if not args.csv_path.exists():
        print(f"ERROR: {args.csv_path} not found", file=sys.stderr)
        return 1

    count = convert(args.csv_path, args.jsonl_path, PARSERS[args.schema])
    print(f"Converted {count} rows: {args.csv_path} → {args.jsonl_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
