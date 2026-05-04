"""OCR validation runner — HTTP call to smoldocling-service.

Sprint 0 prep artifact for Task 0.1 step 5 (Aaron runs after manual procurement).
Pre-requisite: Task 1.4a smoldocling /preprocess endpoint deployed in dev.
"""

import json
import os
import requests
from pathlib import Path

SMOLDOCLING_URL = os.environ.get("SMOLDOCLING_URL", "http://localhost:8500")
RESULTS_PATH = Path(__file__).parent / "results.json"
MANUALS_DIR = Path(__file__).parent / "manuals"


def validate_page(img_path: Path) -> dict:
    """POST a smoldocling-service /preprocess endpoint (Task 1.4 dependency)."""
    print(f"  processing {img_path.name}...", flush=True)
    with open(img_path, "rb") as f:
        files = {"image": (img_path.name, f, "image/jpeg")}
        data = {"preprocessing_mode": "photo-camera"}
        try:
            response = requests.post(
                f"{SMOLDOCLING_URL}/preprocess", files=files, data=data, timeout=30
            )
            response.raise_for_status()
            payload = response.json()
        except requests.RequestException as e:
            return {"page": img_path.stem, "confidence": 0.0, "error": str(e)}
        except (json.JSONDecodeError, ValueError) as e:
            return {"page": img_path.stem, "confidence": 0.0, "error": f"invalid JSON: {e}"}

    return {
        "page": img_path.stem,
        "confidence": float(payload.get("confidence") or 0.0),
        "char_count": len(payload.get("extracted_text", "")),
        "warnings": payload.get("warnings", []),
        "is_blank": payload.get("is_blank", False),
    }


def validate_manual_condition(game_name: str, condition: str) -> dict:
    condition_dir = MANUALS_DIR / game_name / condition
    if not condition_dir.exists():
        return {"game": game_name, "condition": condition, "error": "directory missing"}

    pages = sorted(condition_dir.glob("*.jpg"))
    if not pages:
        return {"game": game_name, "condition": condition, "error": "no pages found"}

    results = [validate_page(p) for p in pages]
    valid_results = [r for r in results if "error" not in r]

    if not valid_results:
        return {
            "game": game_name,
            "condition": condition,
            "all_failed": True,
            "results": results,
        }

    confidences = [r["confidence"] for r in valid_results]
    return {
        "game": game_name,
        "condition": condition,
        "page_count": len(valid_results),
        "avg_confidence": sum(confidences) / len(valid_results),
        "min_confidence": min(confidences),
        "max_confidence": max(confidences),
        "high_conf_pct": sum(1 for c in confidences if c >= 0.85) / len(valid_results) * 100,
        "low_conf_pct": sum(1 for c in confidences if c < 0.7) / len(valid_results) * 100,
        "results": results,
    }


if __name__ == "__main__":
    games = ["tainted-grail", "iss-vanguard", "stuffed-fables", "andor", "7th-continent"]
    conditions = ["good-light", "evening-light", "angled"]
    all_results = []

    for game in games:
        for cond in conditions:
            result = validate_manual_condition(game, cond)
            all_results.append(result)
            avg = result.get("avg_confidence")
            low_pct = result.get("low_conf_pct")
            avg_str = f"{avg:.2f}" if isinstance(avg, (int, float)) else "N/A"
            low_str = f"{low_pct:.0f}%" if isinstance(low_pct, (int, float)) else "N/A"
            print(f"{game}/{cond}: avg={avg_str}, low_pct={low_str}")

    RESULTS_PATH.write_text(json.dumps(all_results, indent=2))
    print(f"\nResults written to {RESULTS_PATH}")
