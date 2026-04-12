#!/usr/bin/env python3
"""
Python equivalent of upload-seed-pdfs.sh — uploads rulebook PDFs to the
meepleai-seeds R2 bucket with SHA256 object metadata.

Idempotent: skips uploads whose remote sha256 metadata already matches local.

Usage:
    python infra/scripts/upload-seed-pdfs.py [LOCAL_DIR]

Requires:
    - boto3 (pip install boto3)
    - SEED_BUCKET_* env vars OR infra/secrets/storage.secret sourced
    - Write credentials (not the runtime readonly ones)
"""

import hashlib
import os
import re
import sys
from pathlib import Path

import boto3
from botocore.client import Config
from botocore.exceptions import ClientError


def load_secrets(secret_path: Path) -> dict:
    """Parse a shell-style KEY=value secrets file into a dict."""
    if not secret_path.exists():
        return {}
    env = {}
    with open(secret_path, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            m = re.match(r"^([A-Z_][A-Z0-9_]*)=(.*)$", line)
            if m:
                key, val = m.group(1), m.group(2)
                # Strip surrounding quotes if present
                if len(val) >= 2 and val[0] == val[-1] and val[0] in ('"', "'"):
                    val = val[1:-1]
                env[key] = val
    return env


def sha256_of(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    repo_root = Path(__file__).resolve().parent.parent.parent
    local_dir = Path(sys.argv[1]) if len(sys.argv) > 1 else repo_root / "data" / "rulebook"
    out_tsv = local_dir / ".seed-hashes.tsv"

    # Load secrets if env not set
    env = os.environ.copy()
    if not env.get("SEED_BUCKET_NAME"):
        secret_file = repo_root / "infra" / "secrets" / "storage.secret"
        env.update(load_secrets(secret_file))

    required = [
        "SEED_BUCKET_NAME",
        "SEED_BUCKET_ENDPOINT",
        "SEED_BUCKET_ACCESS_KEY",
        "SEED_BUCKET_SECRET_KEY",
    ]
    missing = [k for k in required if not env.get(k)]
    if missing:
        print(f"ERROR: missing env vars: {', '.join(missing)}", file=sys.stderr)
        sys.exit(1)

    if not local_dir.is_dir():
        print(f"ERROR: local directory not found: {local_dir}", file=sys.stderr)
        sys.exit(1)

    bucket = env["SEED_BUCKET_NAME"]
    endpoint = env["SEED_BUCKET_ENDPOINT"]
    prefix = "rulebooks/v1"

    # Build S3 client pointing at R2
    s3 = boto3.client(
        "s3",
        endpoint_url=endpoint,
        aws_access_key_id=env["SEED_BUCKET_ACCESS_KEY"],
        aws_secret_access_key=env["SEED_BUCKET_SECRET_KEY"],
        region_name=env.get("SEED_BUCKET_REGION", "auto"),
        config=Config(signature_version="s3v4", s3={"addressing_style": "virtual"}),
    )

    uploaded = 0
    skipped = 0
    failed = 0

    # Collect and sort PDFs for determinism
    pdfs = sorted(local_dir.glob("*_rulebook.pdf"))
    total = len(pdfs)
    if total == 0:
        print(f"WARN: no *_rulebook.pdf found in {local_dir}", file=sys.stderr)
        sys.exit(1)

    print(f"Found {total} PDF files in {local_dir}")
    print(f"Target: s3://{bucket}/{prefix}/")
    print()

    with open(out_tsv, "w", encoding="utf-8") as tsv:
        for i, pdf in enumerate(pdfs, start=1):
            base = pdf.name
            slug = base.removesuffix("_rulebook.pdf")
            key = f"{prefix}/{base}"

            try:
                local_sha = sha256_of(pdf)
            except Exception as exc:
                print(f"  [{i:3}/{total}] ERROR sha256 failed for {base}: {exc}", file=sys.stderr)
                failed += 1
                continue

            # Check remote metadata for idempotency
            remote_sha = None
            try:
                head = s3.head_object(Bucket=bucket, Key=key)
                remote_sha = head.get("Metadata", {}).get("sha256")
            except ClientError as exc:
                if exc.response.get("Error", {}).get("Code") not in ("404", "NoSuchKey"):
                    print(f"  [{i:3}/{total}] head_object error for {base}: {exc}", file=sys.stderr)

            if remote_sha == local_sha:
                print(f"  [{i:3}/{total}] SKIP  {slug}  (sha matches)")
                skipped += 1
            else:
                try:
                    with open(pdf, "rb") as fh:
                        s3.put_object(
                            Bucket=bucket,
                            Key=key,
                            Body=fh,
                            ContentType="application/pdf",
                            Metadata={"sha256": local_sha},
                        )
                    size_mb = pdf.stat().st_size / (1024 * 1024)
                    print(f"  [{i:3}/{total}] UP    {slug}  ({local_sha[:12]}..., {size_mb:.1f}MB)")
                    uploaded += 1
                except Exception as exc:
                    print(f"  [{i:3}/{total}] FAIL  {slug}: {exc}", file=sys.stderr)
                    failed += 1
                    continue

            tsv.write(f"{slug}\t{local_sha}\n")

    print()
    print(f"Summary: {uploaded} uploaded, {skipped} skipped, {failed} failed")
    print(f"Wrote {out_tsv}")
    sys.exit(0 if failed == 0 else 2)


if __name__ == "__main__":
    main()
