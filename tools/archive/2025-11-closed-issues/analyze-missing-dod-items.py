#!/usr/bin/env python3
"""
Analyze missing DoD items and generate recommendations for issue reopening
"""

import json
from pathlib import Path
from collections import defaultdict

def analyze_missing_items(verification_file):
    """Analyze missing DoD items and create recommendations"""

    with open(verification_file, 'r', encoding='utf-8-sig') as f:
        data = json.load(f)

    # Collect all missing items by issue
    issues_with_missing = []

    for issue in data['issues']:
        verification = issue.get('verification', {})
        missing_items = verification.get('missing', [])

        if missing_items:
            issues_with_missing.append({
                'number': issue['number'],
                'title': issue['title'],
                'labels': issue.get('labels', []),
                'total_dod': issue.get('total_unchecked', len(missing_items)),
                'missing_count': len(missing_items),
                'missing_items': missing_items
            })

    # Generate report
    report = []
    report.append("# Missing DoD Items Analysis")
    report.append("")
    report.append("## Summary")
    report.append("")

    total_missing = sum(issue['missing_count'] for issue in issues_with_missing)
    report.append(f"- **Issues with missing items**: {len(issues_with_missing)}")
    report.append(f"- **Total missing DoD items**: {total_missing}")
    report.append("")

    # Categorize by severity
    critical_issues = []  # > 50% missing
    significant_issues = []  # 25-50% missing
    minor_issues = []  # < 25% missing

    for issue in issues_with_missing:
        missing_pct = (issue['missing_count'] / issue['total_dod']) * 100

        if missing_pct > 50:
            critical_issues.append((issue, missing_pct))
        elif missing_pct > 25:
            significant_issues.append((issue, missing_pct))
        else:
            minor_issues.append((issue, missing_pct))

    # Recommendations
    report.append("## Recommendations")
    report.append("")

    if critical_issues:
        report.append(f"### 🔴 Critical - Reopen Immediately ({len(critical_issues)} issues)")
        report.append("")
        report.append("These issues have >50% missing implementations and should be reopened:")
        report.append("")

        for issue, pct in sorted(critical_issues, key=lambda x: x[1], reverse=True):
            report.append(f"- **Issue #{issue['number']}**: {issue['title']}")
            report.append(f"  - Missing: {issue['missing_count']}/{issue['total_dod']} items ({pct:.1f}%)")
            report.append(f"  - Labels: {', '.join(issue['labels'])}")
            report.append("")

    if significant_issues:
        report.append(f"### 🟡 Significant - Review and Consider Reopening ({len(significant_issues)} issues)")
        report.append("")
        report.append("These issues have 25-50% missing implementations:")
        report.append("")

        for issue, pct in sorted(significant_issues, key=lambda x: x[1], reverse=True):
            report.append(f"- **Issue #{issue['number']}**: {issue['title']}")
            report.append(f"  - Missing: {issue['missing_count']}/{issue['total_dod']} items ({pct:.1f}%)")
            report.append("")

    if minor_issues:
        report.append(f"### 🟢 Minor - Document for Future Work ({len(minor_issues)} issues)")
        report.append("")
        report.append("These issues have <25% missing implementations - may not need reopening:")
        report.append("")

        for issue, pct in sorted(minor_issues, key=lambda x: x[1], reverse=True):
            report.append(f"- **Issue #{issue['number']}**: {issue['title']} - {issue['missing_count']}/{issue['total_dod']} missing ({pct:.1f}%)")

        report.append("")

    # Detailed missing items
    report.append("---")
    report.append("")
    report.append("## Detailed Missing Items by Issue")
    report.append("")

    for issue in sorted(issues_with_missing, key=lambda x: x['missing_count'], reverse=True):
        report.append(f"### Issue #{issue['number']}: {issue['title']}")
        report.append("")
        report.append(f"**Missing**: {issue['missing_count']}/{issue['total_dod']} items ({(issue['missing_count']/issue['total_dod'])*100:.1f}%)")
        report.append("")

        for item in issue['missing_items']:
            report.append(f"- [ ] {item['text']}")
            if item.get('evidence'):
                report.append(f"  - Evidence: {item['evidence']}")

        report.append("")

    # Commands to reopen critical issues
    if critical_issues:
        report.append("---")
        report.append("")
        report.append("## Commands to Reopen Critical Issues")
        report.append("")
        report.append("```bash")

        for issue, pct in critical_issues:
            comment = f"Reopening due to {issue['missing_count']} missing DoD items ({pct:.1f}% incomplete). See MISSING-DOD-ANALYSIS.md for details."
            report.append(f"gh issue reopen {issue['number']} --comment \"{comment}\"")

        report.append("```")
        report.append("")

    return '\n'.join(report)

def main():
    import sys

    if len(sys.argv) < 2:
        print("Usage: python analyze-missing-dod-items.py <verification_json>")
        sys.exit(1)

    verification_file = Path(sys.argv[1])

    if not verification_file.exists():
        print(f"Error: File not found: {verification_file}")
        sys.exit(1)

    # Generate report
    report = analyze_missing_items(verification_file)

    # Save report
    output_file = Path("docs/issue/MISSING-DOD-ANALYSIS.md")
    output_file.write_text(report, encoding='utf-8')

    print(f"Missing DoD analysis generated: {output_file}")
    print("\nNext steps:")
    print("1. Review MISSING-DOD-ANALYSIS.md")
    print("2. Reopen critical issues using provided commands")
    print("3. Update CALENDARIO_ISSUE.md with reopened issues")

if __name__ == '__main__':
    main()
