#!/usr/bin/env python3
"""Offline paired McNemar (+ Holm-Bonferroni) analysis over RAG-eval report JSONs.

Consumes the per-sample `samples` array emitted by `POST /api/v1/admin/eval/retrieval`
(added in #3523). For each enhancement it runs a paired McNemar test against the baseline
on a per-sample binary outcome (default: `citation_matched`), then Holm-corrects the family
of p-values across all enhancements so the multiple-comparison error rate is controlled.

Only pure stdlib is used (no numpy/scipy/statsmodels) so it runs anywhere with `python3`.

## Generating the inputs

Run the eval endpoint once per config and save each response body to a file. Baseline is
the grounded path with no enhancements (`enhancements: []`); each enhancement is its flag:

    for cfg in '[]' '["adaptive-routing"]' '["crag-evaluation"]' '["raptor-retrieval"]' \
               '["rag-fusion-queries"]' '["graph-traversal"]'; do
      curl -s -b "$cookie" -X POST .../api/v1/admin/eval/retrieval \
        -H 'Content-Type: application/json' \
        -d "{\"datasetPath\":\"/tmp/golden-resolved.json\",\"enhancements\":$cfg}" > "run_$cfg.json"
    done

## Usage

    python3 mcnemar_holm.py \
        --baseline baseline.json \
        adaptive=adaptive.json crag=crag.json raptor=raptor.json \
        fusion=fusion.json graph=graph.json \
        [--alpha 0.05] [--metric citation_matched|structural_validity] \
        [--structural-threshold 1.0] [--asymptotic] [--json out.json]

    python3 mcnemar_holm.py --selftest      # prove the statistics against known-answer cases

The metric `citation_matched` is the per-sample citation-correct binary (JSON null = ungraded,
excluded from pairing). `structural_validity` binarizes the continuous
`citation_structural_validity` at `--structural-threshold` (default 1.0 => "fully valid").
"""

from __future__ import annotations

import argparse
import json
import math
import os
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Optional


# -----------------------------------------------------------------------------
# Statistics (pure stdlib, no external deps)
# -----------------------------------------------------------------------------

def mcnemar_exact_p(b: int, c: int) -> float:
    """Two-sided exact McNemar p-value (binomial sign test on the discordant pairs).

    Under H0 each discordant pair is equally likely to favor either condition, so the
    count of one type is Binomial(n=b+c, p=0.5). The two-sided p is 2*P(X <= min(b,c)),
    capped at 1.0. Exact - no large-sample approximation - so it is valid for small n.
    """
    n = b + c
    if n == 0:
        return 1.0
    k = min(b, c)
    tail = sum(math.comb(n, i) for i in range(k + 1)) * (0.5 ** n)
    return min(1.0, 2.0 * tail)


def mcnemar_chi2_p(b: int, c: int) -> float:
    """Asymptotic McNemar p-value with Edwards' continuity correction.

    chi2 = (|b - c| - 1)^2 / (b + c), df=1. For df=1 the survival function is
    P(X > x) = erfc(sqrt(x/2)) (since a df-1 chi-square is a squared standard normal).
    Use only when b+c is reasonably large (>= ~25); prefer the exact test otherwise.
    """
    n = b + c
    if n == 0:
        return 1.0
    stat = (abs(b - c) - 1) ** 2 / n
    return math.erfc(math.sqrt(stat / 2.0))


def holm_bonferroni(pvalues: list[float]) -> list[float]:
    """Holm-Bonferroni step-down adjusted p-values, returned in the INPUT order.

    Sort ascending; the i-th smallest (1-indexed) is scaled by (K - i + 1); then a
    running max enforces monotonicity; finally cap at 1.0. Reject H0_i at level alpha
    iff adjusted[i] <= alpha. This controls the family-wise error rate.
    """
    k = len(pvalues)
    if k == 0:
        return []
    order = sorted(range(k), key=lambda i: pvalues[i])
    adjusted = [0.0] * k
    running = 0.0
    for rank, idx in enumerate(order):  # rank = 0..k-1 over ascending p
        scaled = (k - rank) * pvalues[idx]
        running = max(running, scaled)
        adjusted[idx] = min(1.0, running)
    return adjusted


def chi2_sf_df1(stat: float) -> float:
    """Survival function of a chi-square with 1 degree of freedom (exposed for testing)."""
    if stat <= 0.0:
        return 1.0
    return math.erfc(math.sqrt(stat / 2.0))


# -----------------------------------------------------------------------------
# Report parsing + pairing
# -----------------------------------------------------------------------------

@dataclass
class SampleOutcome:
    sample_id: str
    citation_matched: Optional[bool]
    structural_validity: float
    answer_correctness: float
    is_success: bool


@dataclass
class Report:
    label: str
    path: str
    dataset: str
    emitted_citation_accuracy: Optional[float]
    samples: dict[str, SampleOutcome]  # keyed by sample_id


def load_report(label: str, path: str) -> Report:
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    raw_samples = data.get("samples")
    if raw_samples is None:
        raise ValueError(
            f"{path}: no `samples` array. Regenerate the eval JSON with the #3523 build "
            f"(the endpoint must emit per-sample results)."
        )
    samples: dict[str, SampleOutcome] = {}
    for s in raw_samples:
        sid = s.get("sample_id")
        if sid is None:
            raise ValueError(f"{path}: a `samples` entry is missing 'sample_id'.")
        if sid in samples:
            raise ValueError(
                f"{path}: duplicate sample_id '{sid}'. Pairing is ambiguous - the dataset "
                f"must have unique sample ids."
            )
        samples[sid] = SampleOutcome(
            sample_id=sid,
            citation_matched=s.get("citation_matched"),
            structural_validity=float(s.get("citation_structural_validity", 0.0)),
            answer_correctness=float(s.get("answer_correctness", 0.0)),
            is_success=bool(s.get("is_success", False)),
        )
    metrics = data.get("metrics") or {}
    return Report(
        label=label,
        path=path,
        dataset=str(data.get("dataset", "")),
        emitted_citation_accuracy=metrics.get("citation_accuracy"),
        samples=samples,
    )


def sample_binary(
    outcome: SampleOutcome, metric: str, structural_threshold: float
) -> Optional[bool]:
    """Return the per-sample binary outcome, or None if the sample must be excluded.

    `citation_matched`: the nullable citation-correct binary (None => ungraded => excluded).
    `structural_validity`: binarize the continuous score at the threshold; a failed sample
    is excluded because its structural_validity defaults to 1.0 and would fabricate a pass.
    """
    if metric == "citation_matched":
        # Exclude failed samples so the McNemar binary matches EXACTLY the set the aggregate
        # citation_accuracy averages (EvaluationMetrics.Compute: is_success && CitationMatched.HasValue).
        # The current emitter only sets citation_matched on the success path, so this is redundant
        # today — but the JSON contract carries the two fields independently, so guard explicitly to
        # stay correct for any partial-failure / alternate producer.
        if not outcome.is_success:
            return None
        return outcome.citation_matched  # None when ungraded
    if metric == "structural_validity":
        if not outcome.is_success:
            return None
        return outcome.structural_validity >= structural_threshold
    raise ValueError(f"unknown metric: {metric}")


@dataclass
class PairResult:
    label: str
    n_paired: int          # samples graded in BOTH runs
    both_correct: int      # a
    base_correct_only: int  # b (favors baseline)
    enh_correct_only: int  # c (favors enhancement)
    both_wrong: int        # d
    p_raw: float
    method: str            # "exact" | "chi2"
    base_rate: float       # baseline correct-rate over paired samples
    enh_rate: float        # enhancement correct-rate over paired samples

    @property
    def discordant(self) -> int:
        return self.base_correct_only + self.enh_correct_only

    @property
    def direction(self) -> str:
        if self.enh_correct_only > self.base_correct_only:
            return "improvement"
        if self.base_correct_only > self.enh_correct_only:
            return "regression"
        return "neutral"


def pair_mcnemar(
    baseline: Report,
    enhancement: Report,
    metric: str,
    structural_threshold: float,
    asymptotic: bool,
) -> PairResult:
    common_ids = sorted(set(baseline.samples) & set(enhancement.samples))
    a = b = c = d = 0
    base_hits = enh_hits = paired = 0
    for sid in common_ids:
        bo = sample_binary(baseline.samples[sid], metric, structural_threshold)
        eo = sample_binary(enhancement.samples[sid], metric, structural_threshold)
        if bo is None or eo is None:
            continue  # graded in both required
        paired += 1
        base_hits += 1 if bo else 0
        enh_hits += 1 if eo else 0
        if bo and eo:
            a += 1
        elif bo and not eo:
            b += 1
        elif not bo and eo:
            c += 1
        else:
            d += 1
    use_chi2 = asymptotic and (b + c) >= 25
    p = mcnemar_chi2_p(b, c) if use_chi2 else mcnemar_exact_p(b, c)
    return PairResult(
        label=enhancement.label,
        n_paired=paired,
        both_correct=a,
        base_correct_only=b,
        enh_correct_only=c,
        both_wrong=d,
        p_raw=p,
        method="chi2" if use_chi2 else "exact",
        base_rate=(base_hits / paired) if paired else 0.0,
        enh_rate=(enh_hits / paired) if paired else 0.0,
    )


def reconstruct_citation_accuracy(report: Report) -> Optional[float]:
    """Reconstruct the aggregate citation_accuracy from the per-sample binaries.

    Mirrors EvaluationMetrics.Compute: mean over graded (is_success && citation_matched != null)
    samples of (citation_matched ? 1 : 0). Used as a consistency check vs the emitted aggregate.
    """
    graded = [
        s for s in report.samples.values()
        if s.is_success and s.citation_matched is not None
    ]
    if not graded:
        return None
    return sum(1 for s in graded if s.citation_matched) / len(graded)


# -----------------------------------------------------------------------------
# Reporting
# -----------------------------------------------------------------------------

def analyze(
    baseline: Report,
    enhancements: list[Report],
    metric: str,
    structural_threshold: float,
    asymptotic: bool,
    alpha: float,
) -> dict:
    pairs = [
        pair_mcnemar(baseline, e, metric, structural_threshold, asymptotic)
        for e in enhancements
    ]
    adjusted = holm_bonferroni([pr.p_raw for pr in pairs])
    results = []
    for pr, p_adj in zip(pairs, adjusted):
        results.append({
            "enhancement": pr.label,
            "n_paired": pr.n_paired,
            "b_base_only": pr.base_correct_only,
            "c_enh_only": pr.enh_correct_only,
            "discordant": pr.discordant,
            "base_rate": round(pr.base_rate, 4),
            "enh_rate": round(pr.enh_rate, 4),
            "delta": round(pr.enh_rate - pr.base_rate, 4),
            "method": pr.method,
            "p_raw": pr.p_raw,
            "p_holm": p_adj,
            "significant": p_adj <= alpha,
            "direction": pr.direction,
        })
    return {
        "metric": metric,
        "alpha": alpha,
        "baseline": {
            "label": baseline.label,
            "dataset": baseline.dataset,
            "emitted_citation_accuracy": baseline.emitted_citation_accuracy,
            "reconstructed_citation_accuracy": reconstruct_citation_accuracy(baseline),
        },
        "results": results,
    }


def format_table(report: dict) -> str:
    lines = []
    lines.append(f"Paired McNemar + Holm - metric={report['metric']}  alpha={report['alpha']}")
    base = report["baseline"]
    lines.append(f"baseline: {base['label']}  dataset={base['dataset']}")
    emitted = base["emitted_citation_accuracy"]
    recon = base["reconstructed_citation_accuracy"]
    if emitted is not None and recon is not None:
        # Compare at the displayed precision (4 dp): the emitted aggregate may be full-precision
        # or rounded upstream, and a 1e-6 gate would falsely flag e.g. 0.6667 vs 0.666666...
        consistent = abs(round(emitted, 4) - round(recon, 4)) < 1e-9
        flag = "OK" if consistent else "MISMATCH"
        lines.append(
            f"citation_accuracy: emitted={emitted:.4f} reconstructed={recon:.4f} [{flag}]"
        )
    lines.append("")
    header = f"{'enhancement':<20} {'n':>3} {'b':>3} {'c':>3} {'delta':>8} {'method':>6} {'p_raw':>9} {'p_holm':>9} {'sig':>4} {'direction':>12}"
    lines.append(header)
    lines.append("-" * len(header))
    for r in report["results"]:
        lines.append(
            f"{r['enhancement']:<20} {r['n_paired']:>3} {r['b_base_only']:>3} {r['c_enh_only']:>3} "
            f"{r['delta']:>+8.4f} {r['method']:>6} {r['p_raw']:>9.5f} {r['p_holm']:>9.5f} "
            f"{('yes' if r['significant'] else 'no'):>4} {r['direction']:>12}"
        )
    sig = [r for r in report["results"] if r["significant"]]
    lines.append("")
    if sig:
        lines.append("Significant after Holm correction:")
        for r in sig:
            lines.append(f"  - {r['enhancement']}: {r['direction']} (p_holm={r['p_holm']:.5f})")
    else:
        lines.append("No enhancement is significant after Holm correction.")
    return "\n".join(lines)


# -----------------------------------------------------------------------------
# Self-test - proves the statistics against known-answer cases
# -----------------------------------------------------------------------------

def _approx(a: float, b: float, tol: float = 1e-4) -> bool:
    return abs(a - b) <= tol


def selftest() -> int:
    failures = []

    def check(name: str, got: float, want: float, tol: float = 1e-4) -> None:
        if not _approx(got, want, tol):
            failures.append(f"{name}: got {got!r}, want {want!r}")

    # Exact McNemar. n=12, min=2 => 2*(C(12,0)+C(12,1)+C(12,2))/2^12 = 2*79/4096 = 0.0385742
    check("exact b=10,c=2", mcnemar_exact_p(10, 2), 0.03857421875)
    check("exact b=2,c=10 (symmetric)", mcnemar_exact_p(2, 10), 0.03857421875)
    # b==c => no evidence => capped at 1.0
    check("exact b=5,c=5", mcnemar_exact_p(5, 5), 1.0)
    # no discordant pairs => 1.0
    check("exact b=0,c=0", mcnemar_exact_p(0, 0), 1.0)
    # strong effect: b=8,c=0 => 2*C(8,0)/2^8 = 2/256 = 0.0078125
    check("exact b=8,c=0", mcnemar_exact_p(8, 0), 0.0078125)

    # Chi-square df=1 survival at the 0.05 critical value 3.8414588 => ~0.05
    check("chi2 sf @3.8414588", chi2_sf_df1(3.8414588), 0.05, tol=1e-4)
    # chi2 sf at 0 => 1.0
    check("chi2 sf @0", chi2_sf_df1(0.0), 1.0)
    # Edwards continuity-corrected: b=13,c=3 => (|10|-1)^2/16 = 81/16 = 5.0625 => sf
    check("mcnemar_chi2 b=13,c=3", mcnemar_chi2_p(13, 3), chi2_sf_df1(5.0625))

    # Holm-Bonferroni. raw=[0.01,0.04,0.03], K=3.
    # ascending: 0.01(x3)=0.03; 0.03(x2)=0.06 (>0.03); 0.04(x1)=0.04 -> max(0.06,0.04)=0.06
    # back to input order: [0.03, 0.06, 0.06]
    holm = holm_bonferroni([0.01, 0.04, 0.03])
    check("holm[0]", holm[0], 0.03)
    check("holm[1]", holm[1], 0.06)
    check("holm[2]", holm[2], 0.06)
    # Holm monotonicity + cap: raw=[0.5,0.5,0.5] => 3*0.5=1.5->1.0 for all
    holm2 = holm_bonferroni([0.5, 0.5, 0.5])
    for i, v in enumerate(holm2):
        check(f"holm-cap[{i}]", v, 1.0)
    # Single hypothesis: Holm == raw
    check("holm-single", holm_bonferroni([0.023])[0], 0.023)

    # End-to-end pairing: baseline vs enhancement over synthetic samples.
    def mk(label, rows):
        # rows: list of (sample_id, citation_matched, is_success)
        rep = Report(label=label, path="<mem>", dataset="d", emitted_citation_accuracy=None, samples={})
        for sid, cm, ok in rows:
            rep.samples[sid] = SampleOutcome(sid, cm, 1.0, 0.9, ok)
        return rep
    # s1 both correct; s2 base-only(b); s3 enh-only(c); s4 both wrong; s5 ungraded in enh (excluded);
    # s6 only in baseline (excluded by intersection)
    # s7: is_success=False but citation_matched=True in BOTH -> must be EXCLUDED (Finding 1 guard),
    # otherwise a failed sample would leak into the paired test.
    base = mk("baseline", [("s1", True, True), ("s2", True, True), ("s3", False, True),
                           ("s4", False, True), ("s5", True, True), ("s6", True, True),
                           ("s7", True, False)])
    enh = mk("enh", [("s1", True, True), ("s2", False, True), ("s3", True, True),
                     ("s4", False, True), ("s5", None, False), ("s7", True, False)])
    pr = pair_mcnemar(base, enh, "citation_matched", 1.0, asymptotic=False)
    check("pair n", pr.n_paired, 4)          # s1..s4 (s5 ungraded-in-enh, s6 not-in-enh, s7 failed)
    check("pair b", pr.base_correct_only, 1)  # s2
    check("pair c", pr.enh_correct_only, 1)   # s3
    check("pair a", pr.both_correct, 1)       # s1
    check("pair d", pr.both_wrong, 1)         # s4
    check("pair p", pr.p_raw, mcnemar_exact_p(1, 1))
    if pr.direction != "neutral":
        failures.append(f"pair direction: got {pr.direction}, want neutral")

    # reconstruct_citation_accuracy over base: s1..s6 all is_success, citation_matched non-null =
    # [True, True, False, False, True, True] -> 4 correct / 6 graded (s7 excluded: is_success=False).
    check("reconstruct base acc", reconstruct_citation_accuracy(base), 4 / 6)

    # load_report error contract (Finding 3): every structural problem raises a clean ValueError.
    def expect_valueerror(name, payload):
        fd, tmp = tempfile.mkstemp(suffix=".json")
        try:
            with os.fdopen(fd, "w", encoding="utf-8") as f:
                json.dump(payload, f)
            load_report("x", tmp)
            failures.append(f"{name}: expected ValueError, none raised")
        except ValueError:
            pass
        except Exception as e:  # noqa: BLE001 - selftest wants the exact type
            failures.append(f"{name}: expected ValueError, got {type(e).__name__}")
        finally:
            os.unlink(tmp)

    expect_valueerror("no samples key", {"dataset": "d", "metrics": {}})
    expect_valueerror("missing sample_id", {"samples": [{"citation_matched": True}]})
    expect_valueerror("duplicate sample_id", {"samples": [{"sample_id": "a"}, {"sample_id": "a"}]})

    if failures:
        print("SELFTEST FAILED:")
        for f in failures:
            print("  -", f)
        return 1
    print(f"SELFTEST PASSED ({'all cases'})")
    return 0


# -----------------------------------------------------------------------------
# CLI
# -----------------------------------------------------------------------------

def main(argv: Optional[list[str]] = None) -> int:
    parser = argparse.ArgumentParser(
        description="Paired McNemar + Holm-Bonferroni over RAG-eval report JSONs.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--baseline", help="Path to the baseline eval JSON.")
    parser.add_argument(
        "enhancements", nargs="*",
        help="Enhancement reports as LABEL=PATH (e.g. adaptive=adaptive.json).",
    )
    parser.add_argument("--alpha", type=float, default=0.05, help="Significance level (default 0.05).")
    parser.add_argument(
        "--metric", choices=["citation_matched", "structural_validity"],
        default="citation_matched", help="Per-sample binary to test (default citation_matched).",
    )
    parser.add_argument(
        "--structural-threshold", type=float, default=1.0,
        help="Threshold to binarize citation_structural_validity (metric=structural_validity).",
    )
    parser.add_argument(
        "--asymptotic", action="store_true",
        help="Use chi-square (continuity-corrected) when discordant pairs >= 25; else exact.",
    )
    parser.add_argument("--json", help="Write the machine-readable result to this path.")
    parser.add_argument("--selftest", action="store_true", help="Run known-answer self-tests and exit.")
    args = parser.parse_args(argv)

    if args.selftest:
        return selftest()

    if not args.baseline or not args.enhancements:
        parser.error("--baseline and at least one LABEL=PATH enhancement are required "
                     "(or use --selftest).")

    baseline = load_report("baseline", args.baseline)
    enhancements = []
    for spec in args.enhancements:
        if "=" not in spec:
            parser.error(f"enhancement '{spec}' must be LABEL=PATH")
        label, path = spec.split("=", 1)
        enhancements.append(load_report(label, path))

    # Warn on sample-id set divergence (a dataset-quality issue that shrinks the paired set).
    base_ids = set(baseline.samples)
    for e in enhancements:
        missing = base_ids ^ set(e.samples)
        if missing:
            print(
                f"WARNING: sample_id set differs between baseline and '{e.label}' "
                f"({len(missing)} unmatched ids); only the intersection is paired.",
                file=sys.stderr,
            )

    report = analyze(
        baseline, enhancements, args.metric, args.structural_threshold,
        args.asymptotic, args.alpha,
    )
    print(format_table(report))
    if args.json:
        Path(args.json).write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(f"\nWrote machine-readable result to {args.json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
