# Tesseract OCR integration test — setup

> **Scope**: how to run `TesseractOcrServiceTests.Extract_FromSyntheticPng_ReturnsParagraphsAndConfidence`
> (`apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Infrastructure/Services/`).
> Issue: [#2936](https://github.com/meepleAi-app/meepleai-monorepo/issues/2936).

## Why this test is gated

`TesseractOcrService` wraps the native **Tesseract** engine (`Tesseract` NuGet package),
which needs two things the default CI image does not carry:

1. The native `libtesseract` / `libleptonica` shared libraries (bundled by the NuGet package on
   supported runners, provisioned via the OS package manager otherwise).
2. The English language model file **`eng.traineddata`**, resolved from the directory named by
   the `GAMEBOOK_TESSDATA_DIR` environment variable (the service falls back to `./tessdata`).

The test **self-skips** (observable `Assert.Skip`, never a silent pass) when
`GAMEBOOK_TESSDATA_DIR` is unset or `eng.traineddata` is missing — so it is safe on any runner
and turns itself on the moment the data is present.

## Run locally

### 1. Obtain `eng.traineddata`

| Platform | Command |
|---|---|
| Debian / Ubuntu | `sudo apt-get install -y tesseract-ocr tesseract-ocr-eng` → data lands in `/usr/share/tesseract-ocr/*/tessdata` |
| macOS (Homebrew) | `brew install tesseract` → data in `$(brew --prefix)/share/tessdata` |
| Windows | Install [UB-Mannheim Tesseract](https://github.com/UB-Mannheim/tesseract/wiki) or download [`eng.traineddata`](https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata) into a local `tessdata` folder |

Alternatively, download just the model into any folder:

```bash
mkdir -p ./tessdata
curl -L -o ./tessdata/eng.traineddata \
  https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata
```

### 2. Point the env var at the folder that contains `eng.traineddata`

```bash
# Bash / Git Bash
export GAMEBOOK_TESSDATA_DIR="$PWD/tessdata"

# PowerShell
$env:GAMEBOOK_TESSDATA_DIR = "$PWD\tessdata"
```

### 3. Run the test

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "FullyQualifiedName~TesseractOcrServiceTests"
```

Without the env var / data file the run reports the test as **skipped** with the reason
pointing back to this document.

## Provision in CI (follow-up)

To execute this test in CI, add a provisioning step **before** the shard that runs the
`SessionTracking` integration tests, and export `GAMEBOOK_TESSDATA_DIR`. On the standard
`ubuntu-latest` runner:

```yaml
- name: Provision Tesseract English data (#2936)
  run: |
    sudo apt-get update
    sudo apt-get install -y tesseract-ocr tesseract-ocr-eng
    # tessdata dir varies by distro release; resolve it dynamically:
    echo "GAMEBOOK_TESSDATA_DIR=$(dirname "$(find /usr/share -name eng.traineddata | head -1)")" >> "$GITHUB_ENV"
```

> **Not yet wired**: this step is intentionally left out of `ci.yml` in the #2936 PR. The OCR
> assertion (mean-confidence threshold, header segmentation) has not been exercised on the CI
> image, and a confidence-threshold test can be image/font-render sensitive. Wire the step in a
> follow-up PR where the CI run can be watched, and tune `AverageConfidence` /
> `ContainEquivalentOf` thresholds against the real runner output if needed. Until then the test
> stays green-by-skip everywhere.
