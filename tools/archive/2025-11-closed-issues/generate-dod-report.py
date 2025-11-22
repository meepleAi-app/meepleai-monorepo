#!/usr/bin/env python3
"""
Generate comprehensive Markdown report from DoD analysis JSON
"""

import json
import sys
import re
from pathlib import Path
from datetime import datetime
from collections import defaultdict

def categorize_dod_item(item_text):
    """Categorize a DoD item into implementation categories"""
    text_lower = item_text.lower()

    # Manual verification patterns
    manual_patterns = [
        r'test.*(manually|manual)',
        r'verify.*(browser|ui|dashboard)',
        r'check.*(manually|works|correctly)',
        r'qa.*testing',
        r'user.*testing',
        r'renders?.*correctly',
        r'trigger.*correctly',
        r'alert.*visible',
        r'accessible at',
    ]

    for pattern in manual_patterns:
        if re.search(pattern, text_lower):
            return 'manual_verification'

    # Testing patterns
    test_patterns = [
        r'(unit|integration|e2e).*test',
        r'test.*pass',
        r'coverage.*\d+%',
        r'all tests',
    ]

    for pattern in test_patterns:
        if re.search(pattern, text_lower):
            return 'testing'

    # Documentation patterns
    if re.search(r'(documentation|readme|guide|doc)', text_lower):
        return 'documentation'

    # File creation patterns
    if re.search(r'create.*`[^`]+\.(cs|tsx?|json|yml|yaml|md)`', text_lower):
        return 'file_creation'

    # Service implementation
    if re.search(r'service.*implementation', text_lower):
        return 'service_impl'

    # CI/CD patterns
    if re.search(r'(ci|pipeline|deploy|build|merge)', text_lower):
        return 'cicd'

    # Default
    return 'other'

def generate_report(json_path):
    """Generate comprehensive Markdown report from JSON data"""

    with open(json_path, 'r', encoding='utf-8-sig') as f:
        data = json.load(f)

    timestamp = data.get('timestamp', 'unknown')
    issues = data.get('issues', [])

    # Calculate statistics
    total_issues = len(issues)
    total_dod_items = sum(issue['total_unchecked'] for issue in issues)

    # Categorize all DoD items
    categories = defaultdict(int)
    category_items = defaultdict(list)

    for issue in issues:
        for item in issue['unchecked_dod_items']:
            cat = categorize_dod_item(item)
            categories[cat] += 1
            category_items[cat].append({
                'issue_num': issue['number'],
                'issue_title': issue['title'],
                'item_text': item
            })

    # Generate Markdown report
    report_date = datetime.now().strftime('%Y-%m-%d %H:%M:%S')

    md = []
    md.append(f"# DoD Verification Report - {report_date}\n")
    md.append(f"Analysis Timestamp: `{timestamp}`\n")
    md.append("---\n")

    # Executive Summary
    md.append("## 📊 Executive Summary\n")
    md.append(f"- **Total Issues Analyzed**: {total_issues}")
    md.append(f"- **Total Unchecked DoD Items**: {total_dod_items}")
    md.append(f"- **Average DoD Items per Issue**: {total_dod_items / total_issues:.1f}\n")

    # Category breakdown
    md.append("### DoD Items by Category\n")
    md.append("| Category | Count | Percentage |")
    md.append("|----------|-------|------------|")

    for cat, count in sorted(categories.items(), key=lambda x: x[1], reverse=True):
        pct = (count / total_dod_items) * 100
        md.append(f"| {cat.replace('_', ' ').title()} | {count} | {pct:.1f}% |")
    md.append("")

    # Top 10 issues by unchecked items
    md.append("### Top 10 Issues by Unchecked DoD Items\n")
    top_issues = sorted(issues, key=lambda x: x['total_unchecked'], reverse=True)[:10]

    md.append("| Issue | Title | Unchecked Items |")
    md.append("|-------|-------|-----------------|")
    for issue in top_issues:
        md.append(f"| #{issue['number']} | {issue['title']} | {issue['total_unchecked']} |")
    md.append("")

    # Recommendations
    md.append("## 💡 Recommendations\n")
    md.append("### Immediate Actions\n")

    manual_count = categories.get('manual_verification', 0)
    test_count = categories.get('testing', 0)

    md.append(f"1. **Manual Verification Items ({manual_count} items)**")
    md.append("   - Create consolidated issue: `MANUAL-VERIFICATION: DoD Items Requiring Human Validation`")
    md.append("   - Assign to QA team for systematic verification")
    md.append("   - Priority: Medium (non-blocking but improves quality assurance)\n")

    md.append(f"2. **Testing Items ({test_count} items)**")
    md.append("   - Many items relate to test coverage and test execution")
    md.append("   - Consider: Are these already implemented but checkboxes not updated?")
    md.append("   - Action: Verify actual test coverage vs DoD claims\n")

    file_count = categories.get('file_creation', 0)
    md.append(f"3. **File Creation Items ({file_count} items)**")
    md.append("   - Automate verification: check if mentioned files exist")
    md.append("   - Update checkboxes for existing files")
    md.append("   - Reopen issues for truly missing files\n")

    md.append("### Strategic Approach\n")
    md.append("1. **Phase 1**: Automated file/service existence verification")
    md.append("2. **Phase 2**: Update GitHub issue bodies with verified checkboxes")
    md.append("3. **Phase 3**: Create manual verification issue for remaining items")
    md.append("4. **Phase 4**: Reopen issues with genuinely missing implementations\n")

    # Detailed Analysis by Issue
    md.append("---\n")
    md.append("## 📝 Detailed Analysis by Issue\n")

    for issue in sorted(issues, key=lambda x: x['number'], reverse=True):
        md.append(f"### Issue #{issue['number']}: {issue['title']}\n")
        md.append(f"**Status**: {issue['state']} | **Labels**: {', '.join(issue.get('labels', []))}")
        md.append(f"**Unchecked DoD Items**: {issue['total_unchecked']}\n")

        if issue['total_unchecked'] > 0:
            md.append("<details>")
            md.append(f"<summary>View {issue['total_unchecked']} unchecked items</summary>\n")

            # Group by category
            issue_cats = defaultdict(list)
            for item in issue['unchecked_dod_items']:
                cat = categorize_dod_item(item)
                issue_cats[cat].append(item)

            for cat, items in sorted(issue_cats.items()):
                md.append(f"**{cat.replace('_', ' ').title()}** ({len(items)} items):")
                for item in items:
                    md.append(f"- [ ] {item}")
                md.append("")

            md.append("</details>\n")

    # Appendix: Category Details
    md.append("---\n")
    md.append("## 📎 Appendix: Category Definitions\n")
    md.append("**Manual Verification**: Items requiring human testing/validation (e.g., \"Dashboard works correctly\")")
    md.append("**Testing**: Test coverage, test execution, test passing requirements")
    md.append("**Documentation**: README, guides, documentation updates")
    md.append("**File Creation**: Creating specific files (services, components, configs)")
    md.append("**Service Implementation**: Service class implementations")
    md.append("**CI/CD**: Continuous integration, deployment, build pipeline")
    md.append("**Other**: Miscellaneous or uncategorized items\n")

    return '\n'.join(md)

def main():
    if len(sys.argv) < 2:
        print("Usage: python generate-dod-report.py <json_file>")
        sys.exit(1)

    json_path = sys.argv[1]

    if not Path(json_path).exists():
        print(f"Error: JSON file not found: {json_path}")
        sys.exit(1)

    print(f"Generating report from: {json_path}")

    report = generate_report(json_path)

    # Save report
    json_stem = Path(json_path).stem
    output_path = Path(json_path).parent / f"dod-verification-report-{json_stem.split('-', 3)[-1]}.md"

    with open(output_path, 'w', encoding='utf-8') as f:
        f.write(report)

    print(f"OK Report generated: {output_path}")
    print(f"\nReport Summary:")
    print(f"  Total Issues: {len(json.load(open(json_path, 'r', encoding='utf-8-sig'))['issues'])}")
    print(f"  Total DoD Items: {sum(issue['total_unchecked'] for issue in json.load(open(json_path, 'r', encoding='utf-8-sig'))['issues'])}")

if __name__ == '__main__':
    main()
