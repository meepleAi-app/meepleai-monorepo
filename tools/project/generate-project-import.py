#!/usr/bin/env python3
"""
Generate CSV for GitHub Project import
Usage: python generate_project_import.py
"""
import json, csv, sys
from pathlib import Path
from collections import defaultdict

# Find all_issues_raw.json
json_path = Path('apps/web/docs/planning/all_issues_raw.json')
if not json_path.exists():
    print("[ERROR] File not found:", json_path)
    sys.exit(1)

# Load issues
with open(json_path, 'r', encoding='utf-8') as f:
    all_issues = json.load(f)

open_issues = [i for i in all_issues if i['state'] == 'OPEN']

print(f"[INFO] Processing {len(open_issues)} open issues")

# Prepare CSV data
csv_data = []

for issue in sorted(open_issues, key=lambda x: x['number']):
    labels = [l['name'] for l in issue.get('labels', [])]
    milestone = issue.get('milestone', {}).get('title', '') if issue.get('milestone') else ''

    # Determine priority
    title_lower = issue['title'].lower()
    if '[p1]' in title_lower or 'critical' in title_lower:
        priority = 'P1 - Critical'
    elif any(l in labels for l in ['bug', 'bug-fix']):
        priority = 'P2 - High (Bug)'
    elif any(l in labels for l in ['frontend', 'backend', 'testing']):
        priority = 'P3 - Normal'
    else:
        priority = 'P4 - Low'

    # Determine status
    if any(l in labels for l in ['in-progress', 'wip']):
        status = 'In Progress'
    else:
        status = 'Todo'

    # Determine track
    if 'frontend' in labels:
        track = 'Frontend'
    elif 'backend' in labels:
        track = 'Backend'
    elif 'testing' in labels:
        track = 'Testing'
    elif 'documentation' in labels:
        track = 'Documentation'
    elif 'devops' in labels:
        track = 'DevOps'
    else:
        track = 'Other'

    # Determine size (estimate)
    if 'testing' in labels or 'documentation' in labels:
        size = 'Small'
    elif 'frontend' in labels or 'backend' in labels:
        size = 'Medium'
    else:
        size = 'Unknown'

    csv_data.append({
        'Issue #': issue['number'],
        'Title': issue['title'][:100],  # Truncate long titles
        'Status': status,
        'Priority': priority,
        'Milestone': milestone[:50] if milestone else 'No Milestone',
        'Track': track,
        'Size': size,
        'Labels': ', '.join(labels[:4]),
        'URL': f"https://github.com/DegrassiAaron/meepleai-monorepo/issues/{issue['number']}"
    })

# Write CSV
output_path = Path('apps/web/docs/planning/github-project-import.csv')
with open(output_path, 'w', newline='', encoding='utf-8') as f:
    writer = csv.DictWriter(f, fieldnames=[
        'Issue #', 'Title', 'Status', 'Priority', 'Milestone', 'Track', 'Size', 'Labels', 'URL'
    ])
    writer.writeheader()
    writer.writerows(csv_data)

print(f"\n[SUCCESS] CSV generated: {output_path}")
print(f"Total issues: {len(csv_data)}")

# Statistics
print("\n[STATS] By Milestone:")
by_ms = defaultdict(int)
for row in csv_data:
    by_ms[row['Milestone']] += 1

for ms, count in sorted(by_ms.items(), key=lambda x: -x[1])[:10]:
    print(f"  {ms}: {count} issues")

print("\n[STATS] By Track:")
by_track = defaultdict(int)
for row in csv_data:
    by_track[row['Track']] += 1

for track, count in sorted(by_track.items(), key=lambda x: -x[1]):
    print(f"  {track}: {count} issues")

print("\n[STATS] By Priority:")
by_priority = defaultdict(int)
for row in csv_data:
    by_priority[row['Priority']] += 1

for priority, count in sorted(by_priority.items()):
    print(f"  {priority}: {count} issues")

print("\n[DONE] Ready for import to GitHub Project!")
