#!/usr/bin/env python3
"""
Automated DoD Implementation Verification Script

This script verifies whether DoD items have been actually implemented by checking:
- File existence (services, components, configs, docs)
- API endpoint presence in Program.cs
- Database migrations
- Test files
- Service implementations
"""

import json
import re
import subprocess
from pathlib import Path
from collections import defaultdict

class DodVerifier:
    def __init__(self, repo_root):
        self.repo_root = Path(repo_root)
        self.verification_cache = {}

    def verify_file_exists(self, file_path):
        """Check if a file exists in the repository"""
        # Handle both absolute and relative paths
        path = self.repo_root / file_path.lstrip('/')
        exists = path.exists()

        if exists:
            return True, f"File exists: {path.relative_to(self.repo_root)}"
        else:
            return False, f"File not found: {file_path}"

    def verify_service_implemented(self, service_name):
        """Check if a service class is implemented"""
        # Search for service files
        patterns = [
            f"apps/api/src/Api/Services/{service_name}.cs",
            f"apps/api/src/Api/Services/I{service_name}.cs"
        ]

        for pattern in patterns:
            if (self.repo_root / pattern).exists():
                return True, f"Service found: {pattern}"

        # Fallback: grep for the service name
        try:
            result = subprocess.run(
                ['grep', '-r', service_name, 'apps/api/src/Api/Services/'],
                cwd=self.repo_root,
                capture_output=True,
                text=True,
                timeout=5
            )
            if result.returncode == 0 and result.stdout:
                return True, f"Service referenced in codebase"
        except:
            pass

        return False, f"Service not found: {service_name}"

    def verify_endpoint_exists(self, method, path):
        """Check if an API endpoint exists in Program.cs"""
        program_cs = self.repo_root / "apps/api/src/Api/Program.cs"

        if not program_cs.exists():
            return False, "Program.cs not found"

        # Read Program.cs
        content = program_cs.read_text(encoding='utf-8')

        # Clean up path for matching (remove parameters)
        clean_path = re.sub(r'\{[^}]+\}', '', path)

        # Search for endpoint patterns
        patterns = [
            rf'{method}\s*\(\s*["\'].*{re.escape(clean_path)}',
            rf'Map{method}\s*\(\s*["\'].*{re.escape(clean_path)}',
        ]

        for pattern in patterns:
            if re.search(pattern, content, re.IGNORECASE):
                return True, f"Endpoint found: {method} {path}"

        return False, f"Endpoint not found: {method} {path}"

    def verify_migration_exists(self, table_name):
        """Check if a database migration for a table exists"""
        migrations_dir = self.repo_root / "apps/api/src/Api/Migrations"

        if not migrations_dir.exists():
            return False, "Migrations directory not found"

        # Search in migration files for table name
        for migration_file in migrations_dir.glob("*.cs"):
            content = migration_file.read_text(encoding='utf-8', errors='ignore')
            if re.search(rf'CREATE TABLE.*{table_name}', content, re.IGNORECASE):
                return True, f"Migration found for table: {table_name}"
            if re.search(rf'["\']name["\']:\s*["\']?{table_name}', content, re.IGNORECASE):
                return True, f"Migration references table: {table_name}"

        return False, f"Migration not found for table: {table_name}"

    def verify_test_exists(self, test_name):
        """Check if a test file or test case exists"""
        test_patterns = [
            f"apps/api/tests/**/*{test_name}*.cs",
            f"apps/web/src/**/*{test_name}*.test.tsx",
            f"apps/web/src/**/*{test_name}*.test.ts",
            f"apps/web/e2e/**/*{test_name}*.spec.ts",
        ]

        for pattern in test_patterns:
            matches = list(self.repo_root.glob(pattern))
            if matches:
                rel_path = matches[0].relative_to(self.repo_root)
                return True, f"Test found: {rel_path}"

        return False, f"Test not found: {test_name}"

    def verify_component_exists(self, component_name):
        """Check if a React/Next.js component exists"""
        patterns = [
            f"apps/web/src/**/{component_name}.tsx",
            f"apps/web/src/**/{component_name}.ts",
            f"apps/web/src/**/{component_name}.jsx",
        ]

        for pattern in patterns:
            matches = list(self.repo_root.glob(pattern))
            if matches:
                rel_path = matches[0].relative_to(self.repo_root)
                return True, f"Component found: {rel_path}"

        return False, f"Component not found: {component_name}"

    def verify_dod_item(self, item_text):
        """
        Main verification function - attempts to verify a DoD item
        Returns: (status, evidence)
        - status: 'implemented', 'missing', 'manual', 'unknown'
        - evidence: str describing verification result
        """

        text_lower = item_text.lower()

        # Pattern 1: File creation - "Create `file.ext`"
        file_match = re.search(r'create\s+`([^`]+\.(cs|tsx?|jsx?|json|ya?ml|md|sql))`', item_text, re.IGNORECASE)
        if file_match:
            file_path = file_match.group(1)
            exists, evidence = self.verify_file_exists(file_path)
            return ('implemented' if exists else 'missing', evidence)

        # Pattern 2: Service implementation - "XxxService"
        service_match = re.search(r'(\w+Service)(?:\.cs)?', item_text)
        if service_match and 'service' in text_lower:
            service_name = service_match.group(1)
            exists, evidence = self.verify_service_implemented(service_name)
            return ('implemented' if exists else 'missing', evidence)

        # Pattern 3: API Endpoint - "GET/POST/etc /api/..."
        endpoint_match = re.search(r'(GET|POST|PUT|DELETE|PATCH)\s+(/api/[^\s\)]+)', item_text, re.IGNORECASE)
        if endpoint_match:
            method, path = endpoint_match.groups()
            exists, evidence = self.verify_endpoint_exists(method.upper(), path)
            return ('implemented' if exists else 'missing', evidence)

        # Pattern 4: Database table/migration
        table_match = re.search(r'(?:table|migration).*?`?(\w+_\w+)`?', item_text, re.IGNORECASE)
        if table_match and ('table' in text_lower or 'migration' in text_lower):
            table_name = table_match.group(1)
            exists, evidence = self.verify_migration_exists(table_name)
            return ('implemented' if exists else 'missing', evidence)

        # Pattern 5: Test implementation
        if re.search(r'test', text_lower) and not re.search(r'(manually|manual|qa)', text_lower):
            # Try to extract test name
            test_match = re.search(r'(\w+(?:Test|Tests))', item_text)
            if test_match:
                test_name = test_match.group(1)
                exists, evidence = self.verify_test_exists(test_name)
                return ('implemented' if exists else 'missing', evidence)

        # Pattern 6: Component creation
        component_match = re.search(r'(?:component|page).*?(\w+)\.tsx?', item_text, re.IGNORECASE)
        if component_match:
            component_name = component_match.group(1)
            exists, evidence = self.verify_component_exists(component_name)
            return ('implemented' if exists else 'missing', evidence)

        # Pattern 7: Manual verification items
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
            r'dashboard (works|shows|displays)',
        ]

        for pattern in manual_patterns:
            if re.search(pattern, text_lower):
                return ('manual', 'Requires manual testing/verification')

        # Pattern 8: Documentation
        if re.search(r'documentation|readme|guide', text_lower):
            doc_match = re.search(r'`([^`]+\.md)`', item_text)
            if doc_match:
                doc_path = doc_match.group(1)
                exists, evidence = self.verify_file_exists(doc_path)
                return ('implemented' if exists else 'missing', evidence)
            else:
                return ('manual', 'Documentation needs manual review')

        # Pattern 9: CI/CD process items
        if re.search(r'^(code review|pr|merge|deploy|ci.*green)', text_lower):
            return ('implemented', 'Process item (issue closed = completed)')

        # Pattern 10: Coverage requirements
        coverage_match = re.search(r'coverage.*?(\d+)%', item_text, re.IGNORECASE)
        if coverage_match:
            return ('manual', f'Coverage verification required: {coverage_match.group(1)}%')

        # Default: Unknown
        return ('unknown', 'Could not automatically verify')

def main():
    import sys

    if len(sys.argv) < 2:
        print("Usage: python verify-dod-implementation.py <json_analysis_file>")
        sys.exit(1)

    json_file = Path(sys.argv[1])

    if not json_file.exists():
        print(f"Error: JSON file not found: {json_file}")
        sys.exit(1)

    # Load analysis data
    with open(json_file, 'r', encoding='utf-8-sig') as f:
        data = json.load(f)

    print(f"\n=== DoD Implementation Verification ===")
    print(f"Analyzing: {json_file.name}\n")

    # Initialize verifier
    repo_root = Path.cwd()
    verifier = DodVerifier(repo_root)

    # Process each issue
    stats = {
        'implemented': 0,
        'missing': 0,
        'manual': 0,
        'unknown': 0
    }

    verified_issues = []

    for issue in data['issues']:
        print(f"Issue #{issue['number']}: {issue['title']}")

        verified_items = {
            'implemented': [],
            'missing': [],
            'manual': [],
            'unknown': []
        }

        for item_text in issue['unchecked_dod_items']:
            status, evidence = verifier.verify_dod_item(item_text)
            stats[status] += 1

            verified_items[status].append({
                'text': item_text,
                'evidence': evidence
            })

        # Print summary for this issue
        impl_count = len(verified_items['implemented'])
        miss_count = len(verified_items['missing'])
        manual_count = len(verified_items['manual'])
        unknown_count = len(verified_items['unknown'])

        print(f"  [+] Implemented: {impl_count}")
        print(f"  [-] Missing: {miss_count}")
        print(f"  [!] Manual: {manual_count}")
        print(f"  [?] Unknown: {unknown_count}\n")

        # Add to verified issues
        verified_issues.append({
            **issue,
            'verification': verified_items,
            'verification_summary': {
                'implemented': impl_count,
                'missing': miss_count,
                'manual': manual_count,
                'unknown': unknown_count
            }
        })

    # Save verified results
    output_data = {
        **data,
        'verification_timestamp': Path(__file__).stat().st_mtime,
        'verification_stats': stats,
        'issues': verified_issues
    }

    output_file = json_file.parent / f"dod-verification-{json_file.stem.split('-', 3)[-1]}.json"
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(output_data, f, indent=2, ensure_ascii=False)

    print(f"\n=== Verification Complete ===")
    print(f"Total DoD Items: {sum(stats.values())}")
    print(f"[+] Implemented: {stats['implemented']} ({stats['implemented']/sum(stats.values())*100:.1f}%)")
    print(f"[-] Missing: {stats['missing']} ({stats['missing']/sum(stats.values())*100:.1f}%)")
    print(f"[!] Manual: {stats['manual']} ({stats['manual']/sum(stats.values())*100:.1f}%)")
    print(f"[?] Unknown: {stats['unknown']} ({stats['unknown']/sum(stats.values())*100:.1f}%)")
    print(f"\nOutput saved to: {output_file}")

if __name__ == '__main__':
    main()
