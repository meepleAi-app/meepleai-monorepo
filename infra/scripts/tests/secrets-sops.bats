#!/usr/bin/env bats
# secrets-sops.sh (#4427, ADR-099): i .secret cifrati nel repository con SOPS + age.
#
# Run with: bats infra/scripts/tests/secrets-sops.bats
#
# Usa sops e age VERI, con una chiave usa-e-getta generata nel setup, in una cartella temporanea:
# nessun segreto del repository viene letto. Uno stub di sops proverebbe solo che lo script chiama
# un comando, non che il round-trip conservi i valori, che è la cosa da proteggere.
#
# In CI gli strumenti li installa il job "Infra Scripts" con versioni e checksum fissati: lì la loro
# assenza è un errore, non un salto, perché un salto renderebbe verde un gate che non ha guardato
# niente. In locale, senza strumenti, i test si saltano con il motivo.

setup() {
    if ! command -v sops >/dev/null 2>&1 || ! command -v age-keygen >/dev/null 2>&1; then
        if [ -n "${CI:-}" ]; then
            echo "sops/age assenti in CI: il job deve installarli prima di bats" >&2
            return 1
        fi
        skip "sops e age-keygen non installati (winget install FiloSottile.age SecretsOPerationS.SOPS)"
    fi

    SCRIPT="$BATS_TEST_DIRNAME/../secrets-sops.sh"
    WORK="$(mktemp -d)"
    export SECRETS_DIR="$WORK/secrets"
    export SOPS_DIR="$SECRETS_DIR/sops"
    export SOPS_CONFIG_FILE="$WORK/.sops.yaml"
    export SOPS_AGE_KEY_FILE="$WORK/age.key"
    mkdir -p "$SECRETS_DIR"

    age-keygen -o "$SOPS_AGE_KEY_FILE" 2>/dev/null
    PUB=$(age-keygen -y "$SOPS_AGE_KEY_FILE")
    printf 'creation_rules:\n  - path_regex: \\.secret\\.enc$\n    age: %s\n' "$PUB" > "$SOPS_CONFIG_FILE"

    # Valori con le forme che un .secret vero contiene: `=` e `#` nei valori, virgolette, spazi.
    printf '# Database credentials\n\nPOSTGRES_USER=meepleai\nSPECIALE=a@b=c#d!\nURL="scheme://host:5432/db?ssl=true&x=y"\nEMPTY=\nSPACED=a b c\n' \
        > "$SECRETS_DIR/database.secret"
    printf '# placeholder\nREDIS_VALORE=change_me\n' > "$SECRETS_DIR/redis.secret.example"
    cp "$SECRETS_DIR/redis.secret.example" "$SECRETS_DIR/redis.secret"
}

teardown() {
    [ -n "${WORK:-}" ] && rm -rf "$WORK"
}

@test "encrypt seguito da decrypt restituisce gli stessi valori (le righe vuote non contano)" {
    run bash "$SCRIPT" encrypt database
    [ "$status" -eq 0 ]
    [ -f "$SOPS_DIR/database.secret.enc" ]

    cp "$SECRETS_DIR/database.secret" "$WORK/original"
    rm "$SECRETS_DIR/database.secret"
    run bash "$SCRIPT" decrypt database
    [ "$status" -eq 0 ]
    [[ "$output" == *"database: creato"* ]]

    diff <(grep -v '^$' "$WORK/original") <(grep -v '^$' "$SECRETS_DIR/database.secret")
}

@test "il file cifrato non contiene nessun valore in chiaro" {
    bash "$SCRIPT" encrypt database
    # Non `! grep -q`: in bats una riga negata che non e' l'ultima del test non lo fa fallire mai.
    # Scritte cosi', queste due righe non hanno verificato nulla fino a #4117.
    [ "$(grep -c 'a@b=c#d' "$SOPS_DIR/database.secret.enc")" -eq 0 ]
    [ "$(grep -c 'meepleai' "$SOPS_DIR/database.secret.enc")" -eq 0 ]
    run bash "$SCRIPT" check
    [ "$status" -eq 0 ]
}

@test "encrypt non cifra un placeholder uguale al suo .example" {
    run bash "$SCRIPT" encrypt redis
    [ "$status" -eq 0 ]
    [[ "$output" == *"redis: saltato"* ]]
    [ ! -f "$SOPS_DIR/redis.secret.enc" ]
}

@test "encrypt non ricifra un segreto invariato (il diff deve dire quale segreto è cambiato)" {
    bash "$SCRIPT" encrypt database
    before=$(sha256sum "$SOPS_DIR/database.secret.enc")
    run bash "$SCRIPT" encrypt database
    [ "$status" -eq 0 ]
    [[ "$output" == *"database: invariato"* ]]
    [ "$(sha256sum "$SOPS_DIR/database.secret.enc")" = "$before" ]
}

@test "decrypt non sovrascrive un locale diverso dal cifrato, e fallisce" {
    bash "$SCRIPT" encrypt database
    echo 'POSTGRES_USER=modificato_in_locale' >> "$SECRETS_DIR/database.secret"
    run bash "$SCRIPT" decrypt database
    [ "$status" -ne 0 ]
    [[ "$output" == *"il locale differisce"* ]]
    grep -q 'modificato_in_locale' "$SECRETS_DIR/database.secret"
}

@test "decrypt --force sovrascrive e conserva una copia del locale" {
    bash "$SCRIPT" encrypt database
    echo 'POSTGRES_USER=modificato_in_locale' >> "$SECRETS_DIR/database.secret"
    run bash "$SCRIPT" decrypt --force database
    [ "$status" -eq 0 ]
    [ "$(grep -c 'modificato_in_locale' "$SECRETS_DIR/database.secret")" -eq 0 ]
    grep -q 'modificato_in_locale' "$SECRETS_DIR/database.secret.bak"
}

@test "decrypt sostituisce un placeholder: secrets-setup seguito da secrets-sync dà i valori veri" {
    printf 'REDIS_VALORE=vero\n' > "$SECRETS_DIR/redis.secret"
    bash "$SCRIPT" encrypt redis
    cp "$SECRETS_DIR/redis.secret.example" "$SECRETS_DIR/redis.secret"
    run bash "$SCRIPT" decrypt redis
    [ "$status" -eq 0 ]
    [[ "$output" == *"placeholder sostituito"* ]]
    grep -q '^REDIS_VALORE=vero$' "$SECRETS_DIR/redis.secret"
}

@test "check rifiuta un file in chiaro nella cartella dei cifrati, senza stamparne il valore" {
    mkdir -p "$SOPS_DIR"
    printf 'CHIAVE_PROVA=in_chiaro_finto\n' > "$SOPS_DIR/leak.secret.enc"
    run bash "$SCRIPT" check
    [ "$status" -ne 0 ]
    [[ "$output" == *"manca sops_mac"* ]]
    [[ "$output" != *"in_chiaro_finto"* ]]
}

@test "check rifiuta un valore in chiaro aggiunto a mano a un file cifrato" {
    bash "$SCRIPT" encrypt database
    echo 'ALTRA_CHIAVE=in_chiaro' >> "$SOPS_DIR/database.secret.enc"
    run bash "$SCRIPT" check
    [ "$status" -ne 0 ]
    [[ "$output" == *"ALTRA_CHIAVE"* ]]
    [[ "$output" != *"in_chiaro"* ]]
}

@test "check accetta un commento vuoto, che SOPS lascia com'è perché non ha nulla da cifrare" {
    # Visto su due .secret reali: una riga '#' da sola sopravvive alla cifratura in chiaro.
    printf '#\n# testo\nKEY=valore\n' > "$SECRETS_DIR/vuoto.secret"
    bash "$SCRIPT" encrypt vuoto
    grep -qx '#' "$SOPS_DIR/vuoto.secret.enc"
    run bash "$SCRIPT" check
    [ "$status" -eq 0 ]
}

@test "check rifiuta un commento in chiaro con testo, senza stamparne il testo" {
    bash "$SCRIPT" encrypt database
    echo '# token di riserva abc123' >> "$SOPS_DIR/database.secret.enc"
    run bash "$SCRIPT" check
    [ "$status" -ne 0 ]
    [[ "$output" == *"<commento in chiaro>"* ]]
    [[ "$output" != *"abc123"* ]]
}

@test "decrypt con una chiave diversa fallisce invece di scrivere un file vuoto" {
    bash "$SCRIPT" encrypt database
    rm "$SECRETS_DIR/database.secret"
    age-keygen -o "$WORK/other.key" 2>/dev/null
    run env SOPS_AGE_KEY_FILE="$WORK/other.key" bash "$SCRIPT" decrypt database
    [ "$status" -ne 0 ]
    [ ! -s "$SECRETS_DIR/database.secret" ]
}
