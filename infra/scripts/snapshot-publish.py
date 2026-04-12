#!/usr/bin/env python3
"""
Publish baseline snapshot to S3-compatible bucket (R2).

Reads credentials from infra/secrets/storage.secret and uploads:
  - {basename}.dump
  - {basename}.dump.sha256
  - {basename}.meta.json
  - latest.txt (pointer to basename)

Usage:
  python infra/scripts/snapshot-publish.py [--basename NAME]

If --basename is omitted, uses content of data/snapshots/.latest.
"""
import argparse
import os
import sys
from pathlib import Path

import boto3
from botocore.client import Config


REPO_ROOT = Path(__file__).resolve().parents[2]
SNAPSHOTS_DIR = REPO_ROOT / "data" / "snapshots"
SECRET_FILE = REPO_ROOT / "infra" / "secrets" / "storage.secret"
PREFIX = "snapshots"


def load_secrets() -> dict[str, str]:
    secrets: dict[str, str] = {}
    for line in SECRET_FILE.read_text().splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        key, _, value = line.partition("=")
        secrets[key.strip()] = value.strip()
    return secrets


def main() -> int:
    parser = argparse.ArgumentParser(description="Publish baseline snapshot to R2")
    parser.add_argument("--basename", help="Snapshot basename (without extension)")
    args = parser.parse_args()

    if args.basename:
        basename = args.basename
    else:
        latest_file = SNAPSHOTS_DIR / ".latest"
        if not latest_file.exists():
            print("ERROR: data/snapshots/.latest not found", file=sys.stderr)
            return 1
        basename = latest_file.read_text().strip()

    dump = SNAPSHOTS_DIR / f"{basename}.dump"
    sha = SNAPSHOTS_DIR / f"{basename}.dump.sha256"
    meta = SNAPSHOTS_DIR / f"{basename}.meta.json"

    for f in (dump, sha, meta):
        if not f.exists():
            print(f"ERROR: missing {f}", file=sys.stderr)
            return 1

    secrets = load_secrets()
    bucket = secrets.get("SEED_BLOB_BUCKET") or secrets.get("S3_BUCKET_NAME")
    endpoint = secrets["S3_ENDPOINT"]
    access_key = secrets["S3_ACCESS_KEY"]
    secret_key = secrets["S3_SECRET_KEY"]
    region = secrets.get("S3_REGION", "auto")

    s3 = boto3.client(
        "s3",
        endpoint_url=endpoint,
        aws_access_key_id=access_key,
        aws_secret_access_key=secret_key,
        region_name=region,
        config=Config(signature_version="s3v4"),
    )

    print(f"[publish] uploading {dump.name} ({dump.stat().st_size // (1024*1024)} MB)")
    s3.upload_file(str(dump), bucket, f"{PREFIX}/{dump.name}")
    print(f"[publish] uploading {sha.name}")
    s3.upload_file(str(sha), bucket, f"{PREFIX}/{sha.name}")
    print(f"[publish] uploading {meta.name} (atomic marker last)")
    s3.upload_file(str(meta), bucket, f"{PREFIX}/{meta.name}")
    print(f"[publish] updating latest.txt -> {basename}")
    s3.put_object(
        Bucket=bucket,
        Key=f"{PREFIX}/latest.txt",
        Body=basename.encode("utf-8"),
        ContentType="text/plain",
    )
    print(f"[publish] OK — bucket={bucket}, prefix={PREFIX}/")
    return 0


if __name__ == "__main__":
    sys.exit(main())
