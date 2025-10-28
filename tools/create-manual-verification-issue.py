#!/usr/bin/env python3
"""
Create consolidated MANUAL-VERIFICATION issue from verification data
"""

import json
from pathlib import Path
from collections import defaultdict

def create_manual_verification_issue(verification_file):
    """Generate issue body for manual verification items"""

    with open(verification_file, 'r', encoding='utf-8-sig') as f:
        data = json.load(f)

    # Collect all manual verification items
    manual_items_by_issue = defaultdict(list)

    for issue in data['issues']:
        verification = issue.get('verification', {})
        manual_items = verification.get('manual', [])

        if manual_items:
            manual_items_by_issue[issue['number']] = {
                'title': issue['title'],
                'items': manual_items
            }

    # Generate issue body
    body = []
    body.append("# MANUAL-VERIFICATION: DoD Items Requiring Human Validation")
    body.append("")
    body.append("## Overview")
    body.append("")
    body.append(f"This issue consolidates **{sum(len(v['items']) for v in manual_items_by_issue.values())} manual verification items** from {len(manual_items_by_issue)} closed issues that require human validation.")
    body.append("")
    body.append("These items cannot be automatically verified and need manual testing, visual inspection, or QA validation.")
    body.append("")
    body.append("## Priority")
    body.append("**Medium** - Non-blocking but important for quality assurance")
    body.append("")
    body.append("## Assignment")
    body.append("**QA Team** / **Product Owner**")
    body.append("")
    body.append("## Acceptance Criteria")
    body.append("")
    body.append("- [ ] All manual verification items below have been tested/validated")
    body.append("- [ ] Issues with failed validations documented in new issues")
    body.append("- [ ] This tracking issue closed when all verifications complete")
    body.append("")
    body.append("---")
    body.append("")
    body.append("## Manual Verification Items by Issue")
    body.append("")

    # Group by issue
    for issue_num in sorted(manual_items_by_issue.keys(), reverse=True):
        issue_data = manual_items_by_issue[issue_num]

        body.append(f"### Issue #{issue_num}: {issue_data['title']}")
        body.append("")
        body.append(f"**Manual items to verify**: {len(issue_data['items'])}")
        body.append("")

        for item in issue_data['items']:
            body.append(f"- [ ] {item['text']}")
            if item.get('evidence'):
                body.append(f"  - Note: {item['evidence']}")

        body.append("")

    body.append("---")
    body.append("")
    body.append("## Verification Instructions")
    body.append("")
    body.append("For each item above:")
    body.append("1. Perform the manual test/verification described")
    body.append("2. Check the box if validation passes")
    body.append("3. If validation fails:")
    body.append("   - Document the failure in a comment on the original issue")
    body.append("   - Consider reopening the original issue if critical")
    body.append("   - Create new issue if fix requires significant work")
    body.append("")
    body.append("## Timeline")
    body.append("Target completion: 2 weeks from creation")
    body.append("")
    body.append("## Related")
    body.append("- DoD Verification Report: `docs/issue/dod-verification-report-*.md`")
    body.append("- Automated verification: `tools/verify-dod-implementation.py`")
    body.append("")

    return '\n'.join(body)

def main():
    import sys

    if len(sys.argv) < 2:
        print("Usage: python create-manual-verification-issue.py <verification_json>")
        sys.exit(1)

    verification_file = Path(sys.argv[1])

    if not verification_file.exists():
        print(f"Error: File not found: {verification_file}")
        sys.exit(1)

    # Generate issue body
    issue_body = create_manual_verification_issue(verification_file)

    # Save to temp file
    output_file = Path("docs/issue/manual-verification-issue-body.md")
    output_file.write_text(issue_body, encoding='utf-8')

    print(f"Manual verification issue body generated: {output_file}")
    print(f"\nTo create the issue on GitHub, run:")
    print(f"  gh issue create --title 'MANUAL-VERIFICATION: DoD Items Requiring Human Validation' --body-file {output_file} --label 'qa,testing,manual-verification'")

if __name__ == '__main__':
    main()
