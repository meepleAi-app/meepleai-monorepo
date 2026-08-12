#!/usr/bin/env bash
# MeepleAI — generatore dell'inventario delle feature
#
#   bash infra/scripts/generate-feature-inventory.sh          # rigenera il documento
#   bash infra/scripts/generate-feature-inventory.sh --check   # esce 1 se è obsoleto (per la CI)
#
# PERCHE' E' GENERATO E NON SCRITTO
#
# Questo repo ha già provato la strada opposta: `.docs-archive/roadmap/2026-03-15-roadmap.md`
# era una vista d'insieme scritta a mano che dichiarava «Open PRs: 0» e «All planned features
# have been implemented». Oggi quelle righe sono false su ogni conteggio, ed è finita fra i 279
# file archiviati. Un documento che afferma uno stato mutevole e viene aggiornato a mano ha una
# durata di validità di ore.
#
# Qui l'inventario è derivato dal codice a ogni esecuzione: non può divergere, perché non esiste
# fra un'esecuzione e l'altra. Il modo per tenerlo onesto è `--check` in CI — se qualcuno
# aggiunge un bounded context o un endpoint senza rigenerare, il gate lo dice.
#
# Cosa NON prova a fare: descrivere a cosa servono le feature, o se funzionano. Conta e localizza
# ciò che esiste. Il «perché» sta negli ADR, il «funziona» nei test.

set -Eeuo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUTPUT="$REPO_ROOT/docs/for-developers/feature-inventory.md"
API_BC="$REPO_ROOT/apps/api/src/Api/BoundedContexts"
API_ROUTING="$REPO_ROOT/apps/api/src/Api/Routing"
WEB_APP="$REPO_ROOT/apps/web/src/app"
ADR_DIR="$REPO_ROOT/docs/for-claude/architecture/adr"

CHECK_ONLY=0
[[ "${1:-}" == "--check" ]] && CHECK_ONLY=1

# `|| true` load-bearing: con `set -e` + `pipefail`, un `find` su una directory inesistente
# esce 1 e abortisce l'intero script a metà documento. I bounded context NON hanno tutti le
# stesse sottocartelle (alcuni Domain/Aggregates, altri Domain/Entities), quindi l'assenza è
# la norma, non un errore.
count_files() { { find "$1" -name "$2" 2>/dev/null || true; } | wc -l | tr -d ' '; }

generate() {
  cat <<'HEADER'
# Inventario delle feature

> **Documento generato — non modificarlo a mano.**
> Rigeneralo con `make feature-inventory` (o `bash infra/scripts/generate-feature-inventory.sh`).
> Ogni modifica manuale viene persa alla prossima esecuzione, ed è voluto: un inventario
> scritto a mano diverge dal codice in pochi giorni. Il predecessore di questo documento
> — una roadmap redatta a mano — dichiarava «All planned features have been implemented»
> e vive in `.docs-archive/`.

Conta e localizza **ciò che esiste**. Non dice a cosa serve (→ gli [ADR](../for-claude/architecture/adr/))
né se funziona (→ i test).

HEADER

  echo "**Generato da**: commit \`$(git -C "$REPO_ROOT" rev-parse --short HEAD 2>/dev/null || echo 'n/d')\` · rigenera per aggiornare i conteggi"
  echo

  # ── Backend: bounded context ────────────────────────────────────────────────
  echo "## Backend — bounded context"
  echo
  echo "Aggregati e entità di dominio, comandi e query per contesto. Un contesto con pochi"
  echo "aggregati e zero comandi è tipicamente un'area appena abbozzata, non una feature completa."
  echo
  echo "| Contesto | Dominio | Comandi | Query | Aggregati principali |"
  echo "|---|---:|---:|---:|---|"

  local total_dom=0 total_cmd=0 total_qry=0 bc_count=0
  for dir in "$API_BC"/*/; do
    [[ -d "$dir" ]] || continue
    local name dom cmd qry aggs
    name="$(basename "$dir")"
    dom=$(count_files "$dir/Domain" "*.cs")
    cmd=$(count_files "$dir/Application" "*Command.cs")
    qry=$(count_files "$dir/Application" "*Query.cs")

    # Gli aggregati stanno in Domain/Aggregates/ in alcuni contesti e Domain/Entities/ in altri.
    aggs=$({ find "$dir/Domain/Aggregates" "$dir/Domain/Entities" -maxdepth 1 -name "*.cs" 2>/dev/null || true; } \
      | xargs -r -n1 basename 2>/dev/null | sed 's/\.cs$//' | sort | head -6 \
      | paste -sd',' - | sed 's/,/, /g')
    [[ -z "$aggs" ]] && aggs="—"

    echo "| $name | $dom | $cmd | $qry | $aggs |"
    total_dom=$((total_dom + dom)); total_cmd=$((total_cmd + cmd))
    total_qry=$((total_qry + qry)); bc_count=$((bc_count + 1))
  done
  echo "| **$bc_count contesti** | **$total_dom** | **$total_cmd** | **$total_qry** | |"
  echo

  # ── Backend: endpoint HTTP ──────────────────────────────────────────────────
  echo "## Backend — endpoint HTTP"
  echo
  local ep_total
  ep_total=$(grep -rhoE '\.Map(Get|Post|Put|Patch|Delete)\("' "$API_ROUTING" 2>/dev/null | wc -l | tr -d ' ')
  echo "**$ep_total** endpoint registrati, per file di routing:"
  echo
  echo "| File | Endpoint |"
  echo "|---|---:|"
  for f in "$API_ROUTING"/*.cs; do
    [[ -f "$f" ]] || continue
    local n
    # `grep -c` stampa già 0 quando non trova nulla: un `|| echo 0` ne aggiungerebbe un
    # secondo, e "0\n0" fa esplodere il confronto aritmetico sotto. Serve solo assorbire
    # l'exit code 1 di "nessun match", che con `set -e` abortirebbe lo script.
    n=$(grep -cE '\.Map(Get|Post|Put|Patch|Delete)\("' "$f" 2>/dev/null || true)
    [[ -n "$n" && "$n" -gt 0 ]] && echo "| \`$(basename "$f")\` | $n |"
  done | sort -t'|' -k3 -rn
  echo

  # ── Frontend ────────────────────────────────────────────────────────────────
  echo "## Frontend — pagine"
  echo
  local pg_total
  pg_total=$(find "$WEB_APP" -name "page.tsx" 2>/dev/null | wc -l | tr -d ' ')
  echo "**$pg_total** pagine (App Router), per gruppo di route — il gruppo segmenta"
  echo "**chi** accede, non la feature:"
  echo
  echo "| Gruppo | Pagine |"
  echo "|---|---:|"
  find "$WEB_APP" -name "page.tsx" 2>/dev/null \
    | sed "s|$WEB_APP/||" \
    | awk -F/ '{ if ($1 ~ /^\(/) print $1; else print "(root)" }' \
    | sort | uniq -c | sort -rn \
    | awk '{ printf "| `%s` | %s |\n", $2, $1 }'
  echo

  # ── Decisioni ───────────────────────────────────────────────────────────────
  echo "## Decisioni architetturali"
  echo
  local adr_total
  adr_total=$(count_files "$ADR_DIR" "adr-*.md")
  echo "**$adr_total** ADR in \`docs/for-claude/architecture/adr/\`. Sono la fonte del **perché**:"
  echo "questo inventario dice cosa esiste, gli ADR dicono per quale ragione e a quali condizioni."
  echo
  echo "I cinque più recenti:"
  echo
  for f in $(ls -t "$ADR_DIR"/adr-*.md 2>/dev/null | head -5); do
    echo "- [\`$(basename "$f")\`](../for-claude/architecture/adr/$(basename "$f")) — $(head -1 "$f" | sed 's/^#\+ *//')"
  done
}

if [[ "$CHECK_ONLY" -eq 1 ]]; then
  if [[ ! -f "$OUTPUT" ]]; then
    echo "❌ $OUTPUT non esiste. Genera con: make feature-inventory" >&2
    exit 1
  fi
  # Il commit di generazione cambia a ogni commit e non è una divergenza di contenuto:
  # confronta tutto il resto.
  if diff -q <(generate | grep -v '^\*\*Generato da\*\*') \
             <(grep -v '^\*\*Generato da\*\*' "$OUTPUT") >/dev/null 2>&1; then
    echo "✅ L'inventario delle feature è aggiornato."
    exit 0
  fi
  echo "❌ L'inventario delle feature è obsoleto rispetto al codice." >&2
  echo "   Rigeneralo con: make feature-inventory" >&2
  diff <(generate | grep -v '^\*\*Generato da\*\*') \
       <(grep -v '^\*\*Generato da\*\*' "$OUTPUT") | head -20 >&2 || true
  exit 1
fi

generate > "$OUTPUT"
echo "✅ Scritto $OUTPUT"
