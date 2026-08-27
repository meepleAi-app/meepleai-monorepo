#!/usr/bin/env python3
"""Genera un PDF minimo, valido e con testo estraibile.

Serve al gate E2E `e2e-pdf-s3-upload.yml` (issue #3846): il gate carica un PDF con
`STORAGE_PROVIDER=s3` e pretende che l'elaborazione arrivi a `Ready` con testo estratto,
cioè che la pipeline abbia davvero riletto l'oggetto dal bucket.

Perché generarlo invece di committare un binario: il testo estratto è l'asserzione del
gate, quindi deve essere leggibile nel sorgente e modificabile senza rigenerare un file
opaco. Il PDF usa Helvetica (font standard, nessuna risorsa incorporata) e una xref
calcolata sugli offset reali — un PDF con xref sbagliata viene rifiutato dai parser
stretti, e il gate fallirebbe per la fixture invece che per il difetto che sorveglia.

Uso:
    python3 tests/fixtures/make-text-pdf.py out.pdf ["testo di una riga" ...]
"""
import sys


def build_pdf(lines: list[str]) -> bytes:
    # Ogni riga è un blocco BT/ET a un'ordinata decrescente: nessun a capo automatico,
    # così il testo estratto è esattamente quello passato.
    content_parts = []
    y = 700
    for line in lines:
        escaped = line.replace("\\", r"\\").replace("(", r"\(").replace(")", r"\)")
        content_parts.append(f"BT /F1 18 Tf 72 {y} Td ({escaped}) Tj ET")
        y -= 30
    content = "\n".join(content_parts).encode("ascii")

    objects = [
        b"<< /Type /Catalog /Pages 2 0 R >>",
        b"<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        b"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
        b"/Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
        b"<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        b"<< /Length " + str(len(content)).encode("ascii") + b" >>\nstream\n" + content + b"\nendstream",
    ]

    out = bytearray(b"%PDF-1.4\n")
    offsets = []
    for number, body in enumerate(objects, start=1):
        offsets.append(len(out))
        out += f"{number} 0 obj\n".encode("ascii") + body + b"\nendobj\n"

    xref_offset = len(out)
    out += f"xref\n0 {len(objects) + 1}\n".encode("ascii")
    out += b"0000000000 65535 f \n"
    for offset in offsets:
        out += f"{offset:010d} 00000 n \n".encode("ascii")

    out += f"trailer\n<< /Size {len(objects) + 1} /Root 1 0 R >>\nstartxref\n{xref_offset}\n%%EOF\n".encode("ascii")
    return bytes(out)


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__, file=sys.stderr)
        return 2

    destination = sys.argv[1]
    lines = sys.argv[2:] or ["MeepleAI S3 upload gate", "Se leggi questa riga, la pipeline"]

    with open(destination, "wb") as handle:
        handle.write(build_pdf(lines))

    print(f"{destination}: {len(build_pdf(lines))} bytes, {len(lines)} righe di testo")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
