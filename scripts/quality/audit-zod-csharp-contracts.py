"""
Confronta gli schemi Zod del frontend con i record C# omonimi, campo per campo (#3853).

    python scripts/quality/audit-zod-csharp-contracts.py .

Segnala i campi che uno schema Zod dichiara OBBLIGATORI e che il record omonimo non ha:
sono i casi in cui il backend risponde 200 con dati validi e il client li scarta, senza
dire nulla all'utente.

## Perche' esiste

La prima passata di #3870 cercava `page`/`pageSize` sui CONTENITORI paginati. Per
costruzione non poteva vedere il difetto di `BatchJobDtoSchema`, che sta sull'ELEMENTO
della lista: tre chiavi obbligatorie (`parameters`, `results`, `duration`) che il backend
non manda mai. E con `jobs: []` lo schema dell'elemento non viene MAI esercitato — quindi
la pagina funzionava finche' non c'era nulla da mostrare, e si rompeva esattamente quando
qualcosa c'era. Stessa dinamica per `BadgeSummaryDtoSchema` con `topBadges: []`.

## Cosa NON e'

Un inventario. Il match e' per nome (`XxxSchema` -> `Xxx`), che e' un **filtro**, non una
prova, e sbaglia in due modi opposti:

- **collisioni di nome**: `BudgetStatus` qui e' il budget di token del context
  engineering, mentre lo schema Zod omonimo descrive js/css/images. Per questo i record
  fuori da `Application/` sono elencati a parte invece che mescolati ai candidati.
- **omonimi in contesti diversi**: lo schema `SessionPlayerDto` descrive una risposta di
  `SessionTracking`, il record omonimo sta in `GameManagement`. Il confronto e' privo di
  senso, ma il nome combacia.

Restringe da ~800 schemi a una decina di candidati. La verifica — risalire al query
handler e guardare cosa proietta davvero — resta manuale, e va fatta prima di correggere.
La tecnica che proverebbe davvero il contratto e' eseguire gli schemi contro risposte
reali; questo strumento serve a decidere dove guardare per primo.
"""
import re
import sys
from pathlib import Path

ROOT = Path(sys.argv[1] if len(sys.argv) > 1 else ".")
WEB = ROOT / "apps/web/src/lib/api/schemas"
API = ROOT / "apps/api/src/Api"

# ---------- lato Zod ----------

ZOD_DECL = re.compile(r"export const (\w+Schema)\s*=\s*z\.object\(\{", re.M)


def balanced_body(text: str, open_idx: int) -> str:
    """Ritorna il corpo dell'oggetto a partire dalla graffa aperta in open_idx."""
    depth = 0
    for i in range(open_idx, len(text)):
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return text[open_idx + 1 : i]
    return ""


def zod_fields(body: str) -> dict[str, bool]:
    """nome campo -> e' opzionale (chiave assente ammessa)."""
    fields: dict[str, bool] = {}
    depth = 0
    current = ""
    for ch in body:
        if ch in "{[(":
            depth += 1
        elif ch in "}])":
            depth -= 1
        if ch == "," and depth == 0:
            _collect(current, fields)
            current = ""
        else:
            current += ch
    _collect(current, fields)
    return fields


def _collect(chunk: str, out: dict[str, bool]) -> None:
    chunk = chunk.strip()
    m = re.match(r"^([A-Za-z_$][\w$]*)\s*:", chunk)
    if not m:
        return
    # .optional() e .default() ammettono la chiave assente; .nullable() NO.
    optional = ".optional()" in chunk or ".default(" in chunk
    out[m.group(1)] = optional


def load_zod() -> dict[str, tuple[dict[str, bool], Path]]:
    schemas: dict[str, tuple[dict[str, bool], Path]] = {}
    for f in WEB.rglob("*.ts"):
        text = f.read_text(encoding="utf-8", errors="replace")
        for m in ZOD_DECL.finditer(text):
            body = balanced_body(text, m.end() - 1)
            schemas[m.group(1)] = (zod_fields(body), f)
    return schemas


# ---------- lato C# ----------

CS_POSITIONAL = re.compile(
    r"public\s+(?:sealed\s+)?record\s+(?:class\s+)?(\w+)\s*\(([^)]*)\)", re.S
)
CS_BODY = re.compile(r"public\s+(?:sealed\s+)?(?:record|class)\s+(\w+)\s*\{", re.S)
CS_PROP = re.compile(r"public\s+[\w<>?\[\],\s]+?\s+(\w+)\s*\{\s*get", re.M)


COMMENT = re.compile(r"//[^\n]*")


def load_csharp() -> dict[str, tuple[set[str], Path]]:
    records: dict[str, tuple[set[str], Path]] = {}
    for f in API.rglob("*.cs"):
        if "/bin/" in f.as_posix() or "/obj/" in f.as_posix():
            continue
        text = f.read_text(encoding="utf-8", errors="replace")
        # I commenti vanno tolti PRIMA di leggere la lista dei parametri: un `(games)`
        # dentro un commento chiude il gruppo del regex e tronca il record a meta',
        # facendo sparire i campi che seguono. E' cosi' che GameNightDto risultava
        # privo di CreatedAt, che invece dichiara alla riga 28.
        text = COMMENT.sub("", text)

        for m in CS_POSITIONAL.finditer(text):
            params = m.group(2)
            names = set()
            for p in params.split(","):
                p = p.strip()
                if not p:
                    continue
                tok = re.sub(r"=.*$", "", p).strip().split()
                if len(tok) >= 2:
                    names.add(tok[-1])
            if names:
                records.setdefault(m.group(1), (names, f))

        for m in CS_BODY.finditer(text):
            body = balanced_body(text, m.end() - 1)
            props = set(CS_PROP.findall(body))
            if props:
                existing = records.get(m.group(1))
                if existing:
                    records[m.group(1)] = (existing[0] | props, existing[1])
                else:
                    records[m.group(1)] = (props, f)
    return records


def camel(name: str) -> str:
    return name[0].lower() + name[1:] if name else name


def main() -> int:
    zod = load_zod()
    cs = load_csharp()

    matched = 0
    unmatched = 0
    broken: list[str] = []
    name_collisions: list[str] = []

    for schema_name, (fields, zf) in sorted(zod.items()):
        record_name = schema_name[: -len("Schema")]
        hit = cs.get(record_name)
        if not hit:
            unmatched += 1
            continue
        matched += 1
        props, cf = hit
        camel_props = {camel(p) for p in props}

        # Campi che lo schema pretende e che il record non ha: la validazione
        # fallisce sul caso valido, e il client scarta dati buoni.
        missing = sorted(
            f for f, opt in fields.items() if not opt and f not in camel_props
        )
        if not missing:
            continue

        entry = (
            f"{schema_name}  ({zf.relative_to(ROOT).as_posix()})\n"
            f"    record: {record_name}  ({cf.relative_to(ROOT).as_posix()})\n"
            f"    obbligatori nello schema e assenti nel record: {', '.join(missing)}"
        )

        # Un record omonimo sul piano di dominio o infrastruttura non e' quasi mai il
        # DTO che l'endpoint serializza: BudgetStatus qui e' il budget di token del
        # context engineering, mentre lo schema Zod omonimo descrive js/css/images.
        # Separarli invece di mescolarli: un elenco che confonde una collisione di nome
        # con un contratto rotto non e' piu' utilizzabile di nessun elenco.
        is_application_dto = "/Application/" in cf.as_posix()
        (broken if is_application_dto else name_collisions).append(entry)

    print(f"schemi Zod totali            : {len(zod)}")
    print(f"  con record C# omonimo      : {matched}")
    print(f"  senza record omonimo       : {unmatched}  (fuori dalla portata di questo confronto)")
    print()
    print(f"DISALLINEATI sul piano applicativo (candidati veri): {len(broken)}")
    print(f"omonimi fuori da Application/ (probabili collisioni): {len(name_collisions)}")
    print()
    print("=" * 70)
    print("CANDIDATI VERI")
    print("=" * 70)
    for b in broken:
        print(b)
        print()
    print("=" * 70)
    print("OMONIMI FUORI DAL PIANO APPLICATIVO — da guardare, non da correggere alla cieca")
    print("=" * 70)
    for b in name_collisions:
        print(b)
        print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
