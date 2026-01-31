"""
PDF → chunks → embeddings → Qdrant upsert.

Prereqs (install in your venv):
  pip install pymupdf qdrant-client requests

Example:
  python tools/pipelines/pdf_embed.py ^
    --pdf data/docs/sample.pdf ^
    --collection meepleai-pdf ^
    --embedding-url http://localhost:8000/embeddings ^
    --qdrant-url http://localhost:6333
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import sys
import time
import uuid
from dataclasses import dataclass
from typing import Iterable, List, Tuple

import fitz  # PyMuPDF
import requests
from qdrant_client import QdrantClient
from qdrant_client.http import models as qm


def log(msg: str) -> None:
    ts = time.strftime("%H:%M:%S")
    print(f"[{ts}] {msg}")


def read_pdf_text(pdf_path: str) -> List[Tuple[int, str]]:
    doc = fitz.open(pdf_path)
    pages: List[Tuple[int, str]] = []
    for i, page in enumerate(doc):
        text = page.get_text("text")
        if text:
            pages.append((i + 1, text))
    doc.close()
    return pages


def sentence_split(text: str) -> List[str]:
    # Lightweight splitter: split on punctuation followed by space/newline.
    import re

    parts = re.split(r"(?<=[\.!\?])\s+", text)
    return [p.strip() for p in parts if p.strip()]


def estimate_tokens(text: str) -> int:
    # Rough heuristic: 1 token ≈ 0.75 word.
    words = len(text.split())
    return int(words / 0.75) if words else 0


def chunk_sentences(sentences: List[str], target_tokens: int, overlap_tokens: int) -> List[str]:
    chunks: List[str] = []
    current: List[str] = []
    current_tokens = 0

    for sent in sentences:
        t = estimate_tokens(sent)
        if current_tokens + t > target_tokens and current:
            chunks.append(" ".join(current))
            # start overlap
            if overlap_tokens > 0:
                overlap = []
                ov_tokens = 0
                for s in reversed(current):
                    ov_tokens += estimate_tokens(s)
                    overlap.append(s)
                    if ov_tokens >= overlap_tokens:
                        break
                overlap.reverse()
                current = overlap + [sent]
                current_tokens = sum(estimate_tokens(s) for s in current)
            else:
                current = [sent]
                current_tokens = t
        else:
            current.append(sent)
            current_tokens += t

    if current:
        chunks.append(" ".join(current))
    return chunks


def hash_text(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def embed_batch(texts: List[str], language: str, url: str, timeout: int = 60) -> List[List[float]]:
    resp = requests.post(url, json={"texts": texts, "language": language}, timeout=timeout)
    resp.raise_for_status()
    data = resp.json()
    return data["embeddings"]


def ensure_collection(client: QdrantClient, name: str, vector_size: int, distance=qm.Distance.COSINE) -> None:
    if name in [c.name for c in client.get_collections().collections]:
        return
    client.create_collection(
        collection_name=name,
        vectors=qm.VectorParams(size=vector_size, distance=distance),
    )
    log(f"created collection {name} (size={vector_size})")


@dataclass
class Chunk:
    text: str
    page: int
    idx: int
    hash: str


def build_chunks(pages: List[Tuple[int, str]], target_tokens: int, overlap_tokens: int) -> List[Chunk]:
    chunks: List[Chunk] = []
    for page_num, text in pages:
        sentences = sentence_split(text)
        page_chunks = chunk_sentences(sentences, target_tokens, overlap_tokens)
        for i, chunk_text in enumerate(page_chunks):
            cleaned = " ".join(chunk_text.split())
            if not cleaned:
                continue
            chunks.append(
                Chunk(
                    text=cleaned,
                    page=page_num,
                    idx=i,
                    hash=hash_text(cleaned),
                )
            )
    # remove exact duplicates by hash
    unique = {}
    for ch in chunks:
        if ch.hash not in unique:
            unique[ch.hash] = ch
    return list(unique.values())


def upsert(client: QdrantClient, collection: str, points: List[qm.PointStruct]) -> None:
    client.upsert(collection_name=collection, wait=True, points=points)


def process(
    pdf_path: str,
    collection: str,
    embedding_url: str,
    qdrant_url: str,
    qdrant_api_key: str | None,
    language: str,
    chunk_tokens: int,
    overlap_tokens: int,
    batch_size: int,
    tenant: str | None,
) -> None:
    doc_id = str(uuid.uuid5(uuid.NAMESPACE_URL, os.path.abspath(pdf_path)))
    pages = read_pdf_text(pdf_path)
    log(f"loaded {len(pages)} pages")

    chunks = build_chunks(pages, chunk_tokens, overlap_tokens)
    log(f"built {len(chunks)} unique chunks")

    sample_embed = embed_batch([chunks[0].text], language, embedding_url)
    dim = len(sample_embed[0])
    log(f"embedding dimension: {dim}")

    client = QdrantClient(url=qdrant_url, api_key=qdrant_api_key)
    ensure_collection(client, collection, dim)

    for start in range(0, len(chunks), batch_size):
        batch = chunks[start : start + batch_size]
        texts = [c.text for c in batch]
        embeds = embed_batch(texts, language, embedding_url)
        points = []
        for c, vec in zip(batch, embeds):
            payload = {
                "doc_id": doc_id,
                "source": pdf_path,
                "page": c.page,
                "chunk_index": c.idx,
                "hash": c.hash,
                "tenant": tenant,
                "language": language,
            }
            points.append(
                qm.PointStruct(
                    id=int(hashlib.md5(f"{doc_id}-{c.page}-{c.idx}".encode()).hexdigest(), 16) % (2**63),
                    vector=vec,
                    payload=payload,
                )
            )
        upsert(client, collection, points)
        log(f"upserted {len(points)} points ({start + len(points)}/{len(chunks)})")

    log("done")


def parse_args(argv: Iterable[str]) -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Embed and index PDF into Qdrant")
    p.add_argument("--pdf", required=True, help="Path to PDF file")
    p.add_argument("--collection", required=True, help="Qdrant collection name")
    p.add_argument("--embedding-url", default="http://localhost:8000/embeddings")
    p.add_argument("--language", default="en", choices=["en", "it", "de", "fr", "es"])
    p.add_argument("--qdrant-url", default="http://localhost:6333")
    p.add_argument("--qdrant-api-key", default=None)
    p.add_argument("--chunk-tokens", type=int, default=512)
    p.add_argument("--overlap-tokens", type=int, default=128)
    p.add_argument("--batch-size", type=int, default=16)
    p.add_argument("--tenant", default=None, help="Optional tenant/namespace label")
    return p.parse_args(argv)


def main(argv: Iterable[str]) -> None:
    args = parse_args(argv)
    if not os.path.isfile(args.pdf):
        sys.exit(f"PDF not found: {args.pdf}")
    process(
        pdf_path=args.pdf,
        collection=args.collection,
        embedding_url=args.embedding_url,
        qdrant_url=args.qdrant_url,
        qdrant_api_key=args.qdrant_api_key,
        language=args.language,
        chunk_tokens=args.chunk_tokens,
        overlap_tokens=args.overlap_tokens,
        batch_size=args.batch_size,
        tenant=args.tenant,
    )


if __name__ == "__main__":
    main(sys.argv[1:])
