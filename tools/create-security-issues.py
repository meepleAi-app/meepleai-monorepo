#!/usr/bin/env python3
"""
Script to create security issues on GitHub using REST API
Prerequisites: GitHub Personal Access Token with 'repo' scope
"""

import os
import sys
import json
import urllib.request
import urllib.error
from pathlib import Path

REPO_OWNER = "DegrassiAaron"
REPO_NAME = "meepleai-monorepo"
GITHUB_API = "https://api.github.com"

ISSUES = [
    {
        "title": "[SECURITY] XSS Vulnerability in Rich Text Editor",
        "body_file": ".github/ISSUE_SECURITY_01_XSS.md",
        "labels": ["security", "xss", "p1-high", "frontend", "editor"],
        "priority": "P1"
    },
    {
        "title": "[SECURITY] Hardcoded Database Credentials in Fallback Configuration",
        "body_file": ".github/ISSUE_SECURITY_02_HARDCODED_CREDENTIALS.md",
        "labels": ["security", "credentials", "p2-medium", "backend", "configuration"],
        "priority": "P2"
    },
    {
        "title": "[SECURITY] Security Improvements: CORS Configuration & JSON Deserialization",
        "body_file": ".github/ISSUE_SECURITY_03_IMPROVEMENTS.md",
        "labels": ["security", "hardening", "p3-low", "backend", "cors", "deserialization", "logging"],
        "priority": "P3"
    }
]


def get_github_token():
    """Get GitHub token from environment or prompt user."""
    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")

    if not token:
        print("⚠️  GitHub token not found in environment variables.")
        print("")
        print("Please set one of these environment variables:")
        print("  export GITHUB_TOKEN='your_token_here'")
        print("  export GH_TOKEN='your_token_here'")
        print("")
        print("Create a token at: https://github.com/settings/tokens")
        print("Required scopes: 'repo' (Full control of private repositories)")
        print("")

        # Try to get from user input
        token = input("Or paste your token here (will not be saved): ").strip()

        if not token:
            print("❌ No token provided. Exiting.")
            sys.exit(1)

    return token


def read_issue_body(file_path):
    """Read issue body from markdown file."""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            return f.read()
    except FileNotFoundError:
        print(f"❌ File not found: {file_path}")
        sys.exit(1)


def create_github_issue(token, title, body, labels):
    """Create a GitHub issue using REST API."""
    url = f"{GITHUB_API}/repos/{REPO_OWNER}/{REPO_NAME}/issues"

    data = {
        "title": title,
        "body": body,
        "labels": labels
    }

    headers = {
        "Authorization": f"token {token}",
        "Accept": "application/vnd.github.v3+json",
        "Content-Type": "application/json"
    }

    request = urllib.request.Request(
        url,
        data=json.dumps(data).encode('utf-8'),
        headers=headers,
        method='POST'
    )

    try:
        with urllib.request.urlopen(request) as response:
            result = json.loads(response.read().decode('utf-8'))
            return result['html_url'], result['number']
    except urllib.error.HTTPError as e:
        error_body = e.read().decode('utf-8')
        error_json = json.loads(error_body)
        print(f"❌ HTTP Error {e.code}: {error_json.get('message', 'Unknown error')}")
        if 'errors' in error_json:
            for error in error_json['errors']:
                print(f"   - {error}")
        return None, None
    except Exception as e:
        print(f"❌ Error: {e}")
        return None, None


def main():
    print("🔒 Creating Security Issues on GitHub")
    print("======================================")
    print("")

    # Get GitHub token
    token = get_github_token()
    print("✅ GitHub token configured")
    print("")

    # Change to repository root
    repo_root = Path(__file__).parent.parent
    os.chdir(repo_root)

    created_issues = []
    failed_issues = []

    # Create each issue
    for i, issue_config in enumerate(ISSUES, 1):
        print(f"Creating Issue {i}/{len(ISSUES)}: {issue_config['title']} ({issue_config['priority']})...")

        # Read issue body
        body = read_issue_body(issue_config['body_file'])

        # Create issue
        url, number = create_github_issue(
            token,
            issue_config['title'],
            body,
            issue_config['labels']
        )

        if url:
            print(f"✅ Created: {url} (#{number})")
            created_issues.append((issue_config['priority'], number, url))
        else:
            print(f"❌ Failed to create issue: {issue_config['title']}")
            failed_issues.append(issue_config['title'])

        print("")

    # Summary
    print("======================================")
    if created_issues:
        print(f"🎉 Successfully created {len(created_issues)}/{len(ISSUES)} security issues!")
        print("")
        print("📋 Issues Created:")
        for priority, number, url in created_issues:
            print(f"  {priority}. #{number} - {url}")
        print("")

    if failed_issues:
        print(f"❌ Failed to create {len(failed_issues)} issue(s):")
        for title in failed_issues:
            print(f"  - {title}")
        print("")

    print("🔗 View all security issues:")
    print(f"  https://github.com/{REPO_OWNER}/{REPO_NAME}/issues?q=is%3Aissue+is%3Aopen+label%3Asecurity")
    print("")
    print("🚀 Next steps:")
    print("  1. Review and prioritize issues")
    print("  2. Assign to team members")
    print("  3. Start with P1 (XSS vulnerability, 6-7h estimate)")
    print("  4. Add to sprint planning")
    print("")


if __name__ == "__main__":
    main()
