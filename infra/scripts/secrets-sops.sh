#!/usr/bin/env bash
# secrets-sops.sh — i .secret cifrati nel repository con SOPS + age (#4427, ADR-099).
#
# La sorgente di verità sono i file cifrati in infra/secrets/sops/<nome>.secret.enc. I .secret in
# chiaro restano dove l'API e compose li cercano (infra/secrets/<nome>.secret) e restano ignorati
# da git: questo script passa dall'una all'altra forma.
#
#   decrypt [--force] [nome…]  sops/<nome>.secret.enc → <nome>.secret   (make secrets-sync)
#   encrypt [nome…]            <nome>.secret → sops/<nome>.secret.enc   (make secrets-encrypt)
#   status                     per ogni nome: allineato, diverso, solo locale, solo cifrato
#   check [file…]              ogni file cifrato non contiene valori in chiaro
#
# Senza nomi, decrypt lavora su tutti i file cifrati ed encrypt su tutti i .secret locali.
#
# Tre scelte che non si vedono dal codice:
#   - decrypt NON sovrascrive un .secret locale che differisce dal cifrato, a meno di --force:
#     potrebbe contenere una modifica non ancora pubblicata con encrypt. Un placeholder (uguale al
#     suo .secret.example) invece si sovrascrive, così `make secrets-setup && make secrets-sync`
#     su un checkout nuovo produce i valori veri.
#   - encrypt NON ricifra un file il cui contenuto è già quello cifrato: SOPS genera a ogni
#     esecuzione una chiave dati e IV nuovi, quindi ricifrare tutto renderebbe diverso ogni file a
#     ogni esecuzione, e il diff smetterebbe di dire quale segreto è cambiato.
#   - Il formato dotenv di SOPS non conserva le righe vuote: i confronti le ignorano. Valori,
#     virgolette, `=` e `#` nei valori e commenti sopravvivono identici (provato con sops 3.13.3).
#
# La chiave privata age la cerca SOPS: SOPS_AGE_KEY_FILE, altrimenti il percorso predefinito
# (%AppData%\sops\age\keys.txt su Windows, ~/.config/sops/age/keys.txt su Linux).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SECRETS_DIR="${SECRETS_DIR:-$REPO_ROOT/infra/secrets}"
SOPS_DIR="${SOPS_DIR:-$SECRETS_DIR/sops}"
SOPS_CONFIG_FILE="${SOPS_CONFIG_FILE:-$REPO_ROOT/.sops.yaml}"

die() { echo "❌ $*" >&2; exit 1; }

require_tools() {
  local missing=()
  for t in sops age; do command -v "$t" >/dev/null 2>&1 || missing+=("$t"); done
  [ ${#missing[@]} -eq 0 ] && return 0
  die "mancano: ${missing[*]}. Installa con: winget install FiloSottile.age SecretsOPerationS.SOPS (Windows), brew install age sops (macOS), oppure i binari delle release GitHub."
}

# Contenuto confrontabile: senza righe vuote e senza CR, perché il round-trip di SOPS le perde.
normalized() { tr -d '\r' < "$1" | grep -v '^[[:space:]]*$' || true; }

same_content() { [ "$(normalized "$1")" = "$(normalized "$2")" ]; }

is_placeholder() {
  local example="$SECRETS_DIR/$(basename "$1").example"
  [ -f "$example" ] && same_content "$1" "$example"
}

sops_cmd() { sops --config "$SOPS_CONFIG_FILE" "$@"; }

decrypt_to() { sops_cmd decrypt --input-type dotenv --output-type dotenv "$1" > "$2"; }

cmd_decrypt() {
  local force=0 names=() failed=0
  for a in "$@"; do
    if [ "$a" = "--force" ]; then force=1; else names+=("$a"); fi
  done
  if [ ${#names[@]} -eq 0 ]; then
    for f in "$SOPS_DIR"/*.secret.enc; do
      [ -e "$f" ] && names+=("$(basename "$f" .secret.enc)")
    done
  fi
  [ ${#names[@]} -gt 0 ] || die "nessun file cifrato in $SOPS_DIR"

  for n in "${names[@]}"; do
    local enc="$SOPS_DIR/$n.secret.enc" target="$SECRETS_DIR/$n.secret"
    [ -f "$enc" ] || { echo "⚠️  $n: nessun file cifrato"; failed=1; continue; }
    decrypt_to "$enc" "$TMP" || { echo "❌ $n: decifratura non riuscita (chiave age assente o sbagliata?)"; failed=1; continue; }
    if [ ! -f "$target" ]; then
      cp "$TMP" "$target"; echo "✅ $n: creato"
    elif same_content "$target" "$TMP"; then
      echo "   $n: invariato"
    elif is_placeholder "$target"; then
      cp "$TMP" "$target"; echo "✅ $n: placeholder sostituito con il valore cifrato"
    elif [ "$force" -eq 1 ]; then
      cp "$target" "$target.bak"; cp "$TMP" "$target"
      echo "⚠️  $n: sovrascritto con --force (copia del locale in $n.secret.bak)"
    else
      echo "❌ $n: il locale differisce dal cifrato. 'encrypt $n' pubblica il locale, 'decrypt --force $n' lo sostituisce"
      failed=1
    fi
  done
  return "$failed"
}

cmd_encrypt() {
  local names=() failed=0
  if [ $# -gt 0 ]; then names=("$@"); else
    for f in "$SECRETS_DIR"/*.secret; do
      [ -e "$f" ] && names+=("$(basename "$f" .secret)")
    done
  fi
  [ ${#names[@]} -gt 0 ] || die "nessun .secret in $SECRETS_DIR"
  [ -f "$SOPS_CONFIG_FILE" ] || die "manca $SOPS_CONFIG_FILE con i destinatari age"
  mkdir -p "$SOPS_DIR"

  for n in "${names[@]}"; do
    local src="$SECRETS_DIR/$n.secret" enc="$SOPS_DIR/$n.secret.enc"
    [ -f "$src" ] || { echo "⚠️  $n: nessun .secret locale"; failed=1; continue; }
    if is_placeholder "$src"; then
      echo "   $n: saltato, è il placeholder del .example"; continue
    fi
    if [ -f "$enc" ] && decrypt_to "$enc" "$TMP" 2>/dev/null && same_content "$src" "$TMP"; then
      echo "   $n: invariato, non ricifrato"; continue
    fi
    if sops_cmd encrypt --filename-override "$enc" --input-type dotenv --output-type dotenv "$src" > "$TMP"; then
      cp "$TMP" "$enc"; echo "✅ $n: cifrato"
    else
      echo "❌ $n: cifratura non riuscita"; failed=1
    fi
  done
  return "$failed"
}

cmd_status() {
  local names
  names=$( { for f in "$SECRETS_DIR"/*.secret; do [ -e "$f" ] && basename "$f" .secret; done
             for f in "$SOPS_DIR"/*.secret.enc; do [ -e "$f" ] && basename "$f" .secret.enc; done; } | sort -u)
  [ -n "$names" ] || { echo "nessun segreto"; return 0; }
  while read -r n; do
    local src="$SECRETS_DIR/$n.secret" enc="$SOPS_DIR/$n.secret.enc" state
    if [ -f "$src" ] && [ ! -f "$enc" ]; then
      if is_placeholder "$src"; then state="solo placeholder locale"; else state="SOLO LOCALE: da cifrare"; fi
    elif [ ! -f "$src" ]; then
      state="solo cifrato: 'decrypt' lo crea"
    elif ! decrypt_to "$enc" "$TMP" 2>/dev/null; then
      state="cifrato non leggibile con la chiave age attuale"
    elif same_content "$src" "$TMP"; then
      state="allineato"
    elif is_placeholder "$src"; then
      state="locale è il placeholder: 'decrypt' lo sostituisce"
    else
      state="DIVERSO: locale e cifrato non coincidono"
    fi
    printf '%-28s %s\n' "$n" "$state"
  done <<< "$names"
}

# Un file cifrato è valido se ha la firma di SOPS e nessun valore o commento in chiaro.
# Ammessi: `CHIAVE=ENC[…]`, `CHIAVE=` (vuoto), `#ENC[…]` (commento cifrato), righe `sops_…`, e
# `#` da solo: un commento vuoto non ha nulla da cifrare e SOPS lo lascia com'è (visto su due file reali).
cmd_check() {
  local files=() failed=0
  if [ $# -gt 0 ]; then files=("$@"); else
    for f in "$SOPS_DIR"/*.secret.enc; do [ -e "$f" ] && files+=("$f"); done
  fi
  if [ ${#files[@]} -eq 0 ]; then echo "   nessun file cifrato da controllare"; return 0; fi
  for f in "${files[@]}"; do
    local bad
    if ! grep -q '^sops_mac=ENC\[' "$f"; then
      echo "❌ $f: manca sops_mac, non è un file cifrato da SOPS"; failed=1; continue
    fi
    # Di ogni riga rifiutata si stampa solo il nome della chiave; di un commento, nemmeno quello:
    # non ha un `=`, e tagliarlo al primo `=` ne mostrerebbe tutto il testo.
    bad=$(tr -d '\r' < "$f" \
      | grep -vE '^[[:space:]]*$|^#[[:space:]]*$|^sops_[a-z0-9_]+=|^#ENC\[|^[A-Za-z_][A-Za-z0-9_]*=(ENC\[.*\])?$' \
      | awk '/^#/ { print "<commento in chiaro>"; next } { sub(/=.*/, ""); print }' || true)
    if [ -n "$bad" ]; then
      echo "❌ $f: righe in chiaro (mostro solo il nome): $(echo "$bad" | tr '\n' ' ')"; failed=1
    else
      echo "   $f: cifrato"
    fi
  done
  return "$failed"
}

main() {
  local cmd="${1:-}"; shift || true
  TMP="$(mktemp)"
  trap 'rm -f "$TMP"' EXIT
  case "$cmd" in
    decrypt) require_tools; cmd_decrypt "$@" ;;
    encrypt) require_tools; cmd_encrypt "$@" ;;
    status)  require_tools; cmd_status ;;
    check)   cmd_check "$@" ;;
    *) sed -n '2,13p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 2 ;;
  esac
}

main "$@"
