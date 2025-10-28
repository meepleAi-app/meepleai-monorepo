#!/usr/bin/env python3
"""
Update GitHub issue bodies to check off verified DoD items

This script:
1. Reads the verification JSON with implemented DoD items
2. For each issue, updates the body to check off implemented items
3. Uses gh CLI to update issues on GitHub
4. Creates backup of original bodies
"""

import json
import subprocess
import re
from pathlib import Path
from datetime import datetime

class GitHubIssueUpdater:
    def __init__(self, verification_file, dry_run=True):
        self.verification_file = Path(verification_file)
        self.dry_run = dry_run
        self.backup_dir = Path("docs/issue/backups")
        self.backup_dir.mkdir(exist_ok=True)

    def load_verification_data(self):
        """Load verification results"""
        with open(self.verification_file, 'r', encoding='utf-8-sig') as f:
            return json.load(f)

    def get_issue_body(self, issue_number):
        """Fetch current issue body from GitHub"""
        try:
            result = subprocess.run(
                ['gh', 'issue', 'view', str(issue_number), '--json', 'body'],
                capture_output=True,
                text=True,
                encoding='utf-8',
                errors='replace',
                check=True
            )
            data = json.loads(result.stdout)
            return data.get('body', '')
        except Exception as e:
            print(f"  Error fetching issue #{issue_number}: {e}")
            return None

    def update_dod_checkboxes(self, body, implemented_items):
        """
        Update checkboxes in issue body for implemented items

        Strategy:
        1. For each implemented item text, find matching unchecked checkbox
        2. Replace "- [ ]" with "- [x]" for that specific line
        """
        updated_body = body
        updates_made = 0

        for item_data in implemented_items:
            item_text = item_data['text']

            # Escape special regex characters in item text
            escaped_text = re.escape(item_text)

            # Pattern: "- [ ] {item_text}" -> "- [x] {item_text}"
            # Use word boundaries and be flexible with whitespace
            pattern = r'^(\s*-\s*\[\s*\])\s+(' + escaped_text + r')\s*$'

            # Try direct match first
            if re.search(pattern, updated_body, re.MULTILINE):
                updated_body = re.sub(
                    pattern,
                    r'- [x] \2',
                    updated_body,
                    count=1,
                    flags=re.MULTILINE
                )
                updates_made += 1
            else:
                # Try fuzzy match - item text might have slight differences
                # Match by key words (at least 50% of significant words)
                lines = updated_body.split('\n')
                for i, line in enumerate(lines):
                    if line.strip().startswith('- [ ]'):
                        # Extract the text after checkbox
                        checkbox_text = line.split('- [ ]', 1)[1].strip()

                        # Calculate similarity (simple word overlap)
                        item_words = set(w.lower() for w in re.findall(r'\w+', item_text) if len(w) > 3)
                        checkbox_words = set(w.lower() for w in re.findall(r'\w+', checkbox_text) if len(w) > 3)

                        if item_words and checkbox_words:
                            overlap = len(item_words & checkbox_words)
                            similarity = overlap / len(item_words)

                            if similarity >= 0.7:  # 70% word overlap threshold
                                # Replace this line
                                lines[i] = line.replace('- [ ]', '- [x]', 1)
                                updated_body = '\n'.join(lines)
                                updates_made += 1
                                break

        return updated_body, updates_made

    def backup_issue_body(self, issue_number, body):
        """Save backup of original issue body"""
        timestamp = datetime.now().strftime('%Y%m%d-%H%M%S')
        backup_file = self.backup_dir / f"issue-{issue_number}-{timestamp}.md"

        with open(backup_file, 'w', encoding='utf-8') as f:
            f.write(body)

        return backup_file

    def update_issue_on_github(self, issue_number, new_body):
        """Update issue body on GitHub using gh CLI"""
        try:
            # Create temporary file for body (avoid shell escaping issues)
            import tempfile
            temp_file = Path(tempfile.gettempdir()) / f"issue-{issue_number}-body.md"
            temp_file.write_text(new_body, encoding='utf-8')

            # Use gh issue edit with body from file
            result = subprocess.run(
                ['gh', 'issue', 'edit', str(issue_number), '--body-file', str(temp_file)],
                capture_output=True,
                text=True,
                encoding='utf-8',
                errors='replace',
                check=True
            )

            # Clean up temp file
            temp_file.unlink()

            return True, "Updated successfully"
        except Exception as e:
            return False, str(e)

    def process_issues(self):
        """Main processing loop"""
        data = self.load_verification_data()

        print(f"\n=== GitHub Issue DoD Checkbox Updater ===")
        print(f"Mode: {'DRY RUN' if self.dry_run else 'LIVE UPDATE'}")
        print(f"Verification file: {self.verification_file.name}\n")

        stats = {
            'total_issues': 0,
            'issues_with_updates': 0,
            'total_checkboxes_updated': 0,
            'errors': 0
        }

        issues_to_update = []

        for issue in data['issues']:
            issue_num = issue['number']
            verification = issue.get('verification', {})
            implemented = verification.get('implemented', [])

            if not implemented:
                continue  # Skip issues with no implemented items

            stats['total_issues'] += 1

            print(f"Issue #{issue_num}: {issue['title']}")
            print(f"  Implemented items to check off: {len(implemented)}")

            # Fetch current body
            current_body = self.get_issue_body(issue_num)

            if current_body is None:
                print(f"  [ERROR] Could not fetch issue body")
                stats['errors'] += 1
                continue

            # Update checkboxes
            updated_body, updates_made = self.update_dod_checkboxes(current_body, implemented)

            print(f"  Checkboxes updated: {updates_made}")

            if updates_made == 0:
                print(f"  [WARNING] No checkboxes updated (items may already be checked)")
                continue

            stats['issues_with_updates'] += 1
            stats['total_checkboxes_updated'] += updates_made

            issues_to_update.append({
                'number': issue_num,
                'title': issue['title'],
                'original_body': current_body,
                'updated_body': updated_body,
                'updates_count': updates_made
            })

            if not self.dry_run:
                # Backup original
                backup_file = self.backup_issue_body(issue_num, current_body)
                print(f"  Backup saved: {backup_file.name}")

                # Update on GitHub
                success, message = self.update_issue_on_github(issue_num, updated_body)

                if success:
                    print(f"  [SUCCESS] Issue updated on GitHub")
                else:
                    print(f"  [ERROR] Failed to update: {message}")
                    stats['errors'] += 1
            else:
                print(f"  [DRY RUN] Would update issue body")

            print()

        # Summary
        print(f"\n=== Summary ===")
        print(f"Issues processed: {stats['total_issues']}")
        print(f"Issues with updates: {stats['issues_with_updates']}")
        print(f"Total checkboxes updated: {stats['total_checkboxes_updated']}")
        print(f"Errors: {stats['errors']}")

        if self.dry_run:
            print(f"\n[DRY RUN] No changes applied to GitHub")
            print(f"Run with --live to apply updates")
        else:
            print(f"\n[LIVE] Updates applied to GitHub")
            print(f"Backups saved in: {self.backup_dir}")

        # Save update log
        log_file = self.backup_dir / f"update-log-{datetime.now().strftime('%Y%m%d-%H%M%S')}.json"
        with open(log_file, 'w', encoding='utf-8') as f:
            json.dump({
                'timestamp': datetime.now().isoformat(),
                'dry_run': self.dry_run,
                'stats': stats,
                'issues_updated': [{
                    'number': iss['number'],
                    'title': iss['title'],
                    'updates_count': iss['updates_count']
                } for iss in issues_to_update]
            }, f, indent=2, ensure_ascii=False)

        print(f"Update log saved: {log_file}")

def main():
    import sys
    import argparse

    parser = argparse.ArgumentParser(description='Update GitHub issue DoD checkboxes')
    parser.add_argument('verification_file', help='Path to verification JSON file')
    parser.add_argument('--live', action='store_true', help='Apply updates to GitHub (default: dry run)')

    args = parser.parse_args()

    if not Path(args.verification_file).exists():
        print(f"Error: File not found: {args.verification_file}")
        sys.exit(1)

    # Confirm if live mode
    if args.live:
        print("\n" + "="*60)
        print("WARNING: LIVE MODE - This will update issues on GitHub!")
        print("="*60)
        response = input("\nAre you sure you want to proceed? (yes/no): ")
        if response.lower() != 'yes':
            print("Aborted.")
            sys.exit(0)

    updater = GitHubIssueUpdater(args.verification_file, dry_run=not args.live)
    updater.process_issues()

if __name__ == '__main__':
    main()
