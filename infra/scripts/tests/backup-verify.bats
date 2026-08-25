#!/usr/bin/env bats
# Unit tests for backup-verify.sh — offsite (S3/R2) verification logic.
#
# Run with: bats infra/scripts/tests/backup-verify.bats
#
# These tests exist because of #3669. The offsite check used to be:
#
#     S3_LS_OUTPUT=$(aws s3 ls "s3://…" 2>&1 || true)
#     if [[ -n "${S3_LS_OUTPUT}" ]]; then check "… objects exist" 0
#
# `2>&1` folds stderr into the same variable the success test reads, so an AWS
# error message is a NON-EMPTY string and the check reports PASS. Proved against
# the staging bucket on 2026-08-25 with a revoked key:
#
#     aws: [ERROR]: (InvalidAccessKeyId) The AWS Access Key Id you provided …
#     --- verify concluderebbe: PASS (FALSO POSITIVO) ---
#
# Rotate or revoke the IAM key and the daily "All backup checks PASSED" stays
# green while the cross-provider copy is gone. The first test below is the
# regression guard for exactly that.

setup() {
    SCRIPT="$BATS_TEST_DIRNAME/../backup-verify.sh"
    TMP=$(mktemp -d)

    # Both are overridable so the suite never reads /backups or the real
    # secrets/ directory. Without the SECRETS_DIR override the outcome would
    # depend on whether the machine running the tests happens to have a
    # backup.secret — green on a dev laptop, different in CI.
    export BACKUP_ROOT="$TMP/backups"
    export SECRETS_DIR="$TMP/secrets"
    mkdir -p "$SECRETS_DIR"

    # A backup directory that passes checks 1-3, so the S3 check is the only
    # variable under test.
    BK="$BACKUP_ROOT/20260825-030001"
    mkdir -p "$BK"
    # Incompressible payload on purpose: check 2b asserts the COMPRESSED file is
    # over 1KB, and 4KB of a repeated byte gzips to 83 bytes — the fixture would
    # fail the check it is not testing.
    {
        echo "--"
        echo "-- PostgreSQL database cluster dump"
        echo "--"
        head -c 65536 /dev/urandom | base64
    } | gzip > "$BK/postgres.sql.gz"
    LOCAL_PG_SIZE=$(stat -c%s "$BK/postgres.sql.gz")
    export LOCAL_PG_SIZE
    tar czf "$BK/pdf_uploads.tar.gz" -C "$TMP" secrets

    # Fake aws: behaviour driven by env so each test states its own scenario.
    mkdir -p "$TMP/bin"
    cat > "$TMP/bin/aws" <<'FAKE'
#!/usr/bin/env bash
# Marker so a test can prove it exercised THIS binary. Without it, a runner that
# ships a real aws earlier on PATH turns these tests into a measurement of the
# real CLI's "NoCredentials" error — which is how they passed on a dev laptop
# and failed on ubuntu-latest.
: > "${FAKE_AWS_MARKER:-/dev/null}"
printf '%s' "${FAKE_AWS_STDERR:-}" >&2
printf '%s' "${FAKE_AWS_STDOUT:-}"
exit "${FAKE_AWS_EXIT:-0}"
FAKE
    chmod +x "$TMP/bin/aws"
    export PATH="$TMP/bin:$PATH"
    export FAKE_AWS_MARKER="$TMP/aws-was-called"

    # The script prepends BACKUP_CLI_PATH (default /usr/local/bin) to PATH, which
    # on ubuntu-latest holds the REAL aws — it would win over the stub above no
    # matter where $TMP/bin sits. Point the seam at the stub directory instead.
    export BACKUP_CLI_PATH="$TMP/bin"

    export S3_BACKUP_ENABLED=true
    export S3_BACKUP_BUCKET_NAME=meepleai-backups
    export S3_BACKUP_ENDPOINT=https://s3.eu-north-1.amazonaws.com
    export S3_BACKUP_REGION=eu-north-1
    export BACKUP_WEBHOOK_URL=https://hooks.example.invalid/T000/B000
    # Nothing must actually be sent from a unit test.
    export BACKUP_NOTIFY_DISABLED=1
}

teardown() {
    rm -rf "$TMP"
}

# Real `aws s3 ls <prefix>/` output shape: PRE lines for sub-prefixes, then
# "date time size key" for objects at that level.
listing_ok() {
    printf '                           PRE redis/\n2026-08-25 03:00:35   11556731 pdf_uploads.tar.gz.age\n2026-08-25 03:00:35  %10d postgres.sql.gz.age\n' "$((LOCAL_PG_SIZE + 210))"
}

@test "offsite check FAILS when aws errors (regression: stderr was read as success)" {
    export FAKE_AWS_EXIT=255
    export FAKE_AWS_STDERR="aws: [ERROR]: An error occurred (InvalidAccessKeyId) when calling the ListObjectsV2 operation: The AWS Access Key Id you provided does not exist in our records."
    export FAKE_AWS_STDOUT=""

    run bash "$SCRIPT"

    [ "$status" -ne 0 ]
    [[ "$output" == *"FAIL"*"offsite"* ]]
    # The operator must be told the command failed, not merely that a file is absent.
    [[ "$output" == *"InvalidAccessKeyId"* ]]
}

@test "offsite check PASSES when the postgres dump is present and large enough" {
    export FAKE_AWS_EXIT=0
    export FAKE_AWS_STDOUT="$(listing_ok)"

    run bash "$SCRIPT"

    # Prove the stub ran. If a real aws shadowed it this fails here, naming the
    # cause, instead of failing further down as an unexplained assertion.
    [ -f "$FAKE_AWS_MARKER" ]
    [ "$status" -eq 0 ]
    [[ "$output" == *"PASS"*"postgres.sql.gz.age"* ]]
    [[ "$output" == *"All backup checks PASSED"* ]]
}

@test "offsite check FAILS on an empty listing" {
    export FAKE_AWS_EXIT=0
    export FAKE_AWS_STDOUT=""

    run bash "$SCRIPT"

    [ "$status" -ne 0 ]
    [[ "$output" == *"FAIL"* ]]
}

@test "offsite check FAILS when the postgres dump is missing from an otherwise healthy listing" {
    # The PDF archive uploaded, the database dump did not. "Objects exist" was
    # true here and told you nothing about the artifact that matters.
    export FAKE_AWS_EXIT=0
    export FAKE_AWS_STDOUT="                           PRE redis/
2026-08-25 03:00:35   11556731 pdf_uploads.tar.gz.age"

    run bash "$SCRIPT"

    [ "$status" -ne 0 ]
    [[ "$output" == *"postgres.sql.gz.age"* ]]
}

@test "offsite check FAILS when the uploaded dump is truncated" {
    # age adds ~200 bytes of header; ciphertext is never smaller than plaintext.
    # An object well below the local size means a partial transfer.
    export FAKE_AWS_EXIT=0
    export FAKE_AWS_STDOUT="2026-08-25 03:00:35        512 postgres.sql.gz.age"

    run bash "$SCRIPT"

    [ "$status" -ne 0 ]
    [[ "$output" == *"smaller than the local"* ]]
}

@test "offsite check FAILS when the copy is unencrypted" {
    # Uploading plaintext dumps is the failure this must never ratify.
    export FAKE_AWS_EXIT=0
    export FAKE_AWS_STDOUT="2026-08-25 03:00:35  174275945 postgres.sql.gz"

    run bash "$SCRIPT"

    [ "$status" -ne 0 ]
    [[ "$output" == *"postgres.sql.gz.age"* ]]
}

@test "offsite check FAILS when the aws CLI is not installed" {
    # Was silent: `aws` missing under errexit killed the script mid-check.
    rm -f "$TMP/bin/aws"
    # BACKUP_CLI_PATH points at an empty directory so the script's own
    # /usr/local/bin prepend cannot re-supply an aws the test just removed.
    mkdir -p "$TMP/empty"
    export BACKUP_CLI_PATH="$TMP/empty"

    run env PATH="$TMP/bin:/usr/bin:/bin" BACKUP_CLI_PATH="$TMP/empty" bash "$SCRIPT"

    [ "$status" -ne 0 ]
    [[ "$output" == *"aws"* ]]
}

@test "region comes from S3_BACKUP_REGION, not the legacy S3_REGION only" {
    # backup.sh reads S3_BACKUP_REGION; backup-verify.sh read S3_REGION and fell
    # back to "auto". Staging sets only the former, so the two scripts addressed
    # the same bucket with different regions.
    export FAKE_AWS_STDOUT="$(listing_ok)"
    # Recorded to a side file, not stderr: the script now captures stderr and
    # surfaces it only when aws fails, so a stderr probe would be invisible here.
    export REGION_LOG="$TMP/region.log"
    cat > "$TMP/bin/aws" <<'FAKE'
#!/usr/bin/env bash
prev=""
for a in "$@"; do
  [ "$prev" = "--region" ] && echo "REGION_USED=$a" >> "$REGION_LOG"
  prev="$a"
done
printf '%s' "${FAKE_AWS_STDOUT:-}"
exit 0
FAKE
    chmod +x "$TMP/bin/aws"

    run bash "$SCRIPT"

    [ -f "$REGION_LOG" ]
    grep -q "REGION_USED=eu-north-1" "$REGION_LOG"
    ! grep -q "REGION_USED=auto" "$REGION_LOG"
}

@test "a missing notification channel is a failed check, not a silent skip" {
    # #3669 DoD: "l'esito è visibile senza leggere i log a mano". On staging
    # BACKUP_WEBHOOK_URL was empty, so notify_webhook returned 0 immediately and
    # the degraded/failure signal built by PR #3690 had no transport at all.
    export FAKE_AWS_EXIT=0
    export FAKE_AWS_STDOUT="$(listing_ok)"
    unset BACKUP_WEBHOOK_URL

    run bash "$SCRIPT"

    [ "$status" -ne 0 ]
    [[ "$output" == *"notification channel"* ]]
}

@test "offsite check is skipped, not failed, when the offsite copy is disabled" {
    export S3_BACKUP_ENABLED=false
    unset BACKUP_WEBHOOK_URL

    run bash "$SCRIPT"

    [ "$status" -eq 0 ]
    [[ "$output" == *"skipping"* ]]
}

@test "a revoked webhook is a failed check, not a configured one" {
    # The URL in monitoring.secret was present and non-empty the whole time and
    # Slack answered HTTP 404 "no_service": the hook had been revoked. Proved on
    # staging 2026-08-25 — backup.sh's very first live notification came back
    # "DELIVERY FAILED". Configured-but-dead must not read as configured.
    export FAKE_AWS_EXIT=0
    export FAKE_AWS_STDOUT="$(listing_ok)"
    unset BACKUP_NOTIFY_DISABLED

    cat > "$TMP/bin/curl" <<'FAKECURL'
#!/usr/bin/env bash
# Only the probe is expected here; answer as a revoked Slack hook does.
printf '404'
exit 0
FAKECURL
    chmod +x "$TMP/bin/curl"

    run bash "$SCRIPT"

    [ "$status" -ne 0 ]
    [[ "$output" == *"REVOKED"* ]]
}

@test "a live webhook passes the probe" {
    export FAKE_AWS_EXIT=0
    export FAKE_AWS_STDOUT="$(listing_ok)"
    unset BACKUP_NOTIFY_DISABLED

    # A hook that still exists rejects the empty probe body as 400
    # invalid_payload — alive, and no message posted.
    cat > "$TMP/bin/curl" <<'FAKECURL'
#!/usr/bin/env bash
printf '400'
exit 0
FAKECURL
    chmod +x "$TMP/bin/curl"

    run bash "$SCRIPT"

    [ "$status" -eq 0 ]
    [[ "$output" == *"reachable"* ]]
}

@test "a transient network error does not cry wolf" {
    # curl exit 0 with code 000 means it never got an answer. That is Slack
    # having a bad minute, not a revoked hook: failing here would train people
    # to ignore this check, which is how the previous one stopped being read.
    export FAKE_AWS_EXIT=0
    export FAKE_AWS_STDOUT="$(listing_ok)"
    unset BACKUP_NOTIFY_DISABLED

    cat > "$TMP/bin/curl" <<'FAKECURL'
#!/usr/bin/env bash
printf '000'
exit 0
FAKECURL
    chmod +x "$TMP/bin/curl"

    run bash "$SCRIPT"

    [ "$status" -eq 0 ]
    [[ "$output" == *"reachability unknown"* ]]
}

@test "SMTP alone is a channel when no webhook is configured" {
    # The email fallback exists because the webhook is dead; a host with working
    # SMTP and no webhook must not be reported as having no way to alert.
    export FAKE_AWS_EXIT=0
    export FAKE_AWS_STDOUT="$(listing_ok)"
    unset BACKUP_WEBHOOK_URL
    export SMTP_HOST=smtp.example.invalid
    export SMTP_USER=ops@example.invalid
    export SMTP_PASSWORD=secret
    export SMTP_FROM_EMAIL=ops@example.invalid

    run bash "$SCRIPT"

    [ "$status" -eq 0 ]
    [[ "$output" == *"notification channel configured"* ]]
}

@test "a revoked webhook with a working email fallback warns instead of failing" {
    # Alerts still arrive, so this is a configuration smell, not a broken path.
    # Failing daily for something that still delivers is how the previous check
    # stopped being read.
    export FAKE_AWS_EXIT=0
    export FAKE_AWS_STDOUT="$(listing_ok)"
    unset BACKUP_NOTIFY_DISABLED
    export SMTP_HOST=smtp.example.invalid
    export SMTP_USER=ops@example.invalid
    export SMTP_PASSWORD=secret
    export SMTP_FROM_EMAIL=ops@example.invalid

    cat > "$TMP/bin/curl" <<'FAKECURL'
#!/usr/bin/env bash
printf '404'
exit 0
FAKECURL
    chmod +x "$TMP/bin/curl"

    run bash "$SCRIPT"

    [ "$status" -eq 0 ]
    [[ "$output" == *"WARN"*"REVOKED"* ]]
    [[ "$output" == *"email fallback"* ]]
}
