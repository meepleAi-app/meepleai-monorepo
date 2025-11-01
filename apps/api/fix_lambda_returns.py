import os
import re
import glob

# Directories to process
test_dir = r"D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"

# Files to fix based on error logs
files_to_fix = [
    "PromptTemplateServiceTests.cs",
    "SetupGuideServiceTests.cs",
    "SetupGuideEndpointIntegrationTests.cs"
]

for filename in files_to_fix:
    filepath = os.path.join(test_dir, filename)
    if not os.path.exists(filepath):
        # Check in subdirectories
        for subpath in glob.glob(os.path.join(test_dir, "**", filename), recursive=True):
            filepath = subpath
            break

    if os.path.exists(filepath):
        with open(filepath, 'r', encoding='utf-8') as f:
            lines = f.readlines()

        changed = False
        new_lines = []
        in_lambda = False
        brace_count = 0
        lambda_start = -1

        for i, line in enumerate(lines):
            new_line = line

            # Detect lambda with OnlyContain
            if '.OnlyContain(' in line and '=>' in line:
                in_lambda = True
                lambda_start = i
                brace_count = line.count('{') - line.count('}')
            elif in_lambda:
                brace_count += line.count('{') - line.count('}')

                # Check if lambda is ending
                if brace_count <= 0 and '}' in line:
                    # Check if there's a return statement in the lambda
                    lambda_text = ''.join(lines[lambda_start:i+1])
                    if 'return ' not in lambda_text and 'return;' not in lambda_text:
                        # Add return true before the closing brace
                        if line.strip() == '});':
                            new_line = line.replace('});', '    return true;\n        });')
                            changed = True
                        elif line.strip().endswith('}'):
                            # Insert return true on previous line
                            new_lines[-1] = new_lines[-1].rstrip() + '\n            return true;\n'
                            changed = True
                    in_lambda = False

            new_lines.append(new_line)

        # Write back if changed
        if changed:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.writelines(new_lines)
            print(f"Fixed: {filename}")
    else:
        print(f"Not found: {filename}")

print("Processing complete!")