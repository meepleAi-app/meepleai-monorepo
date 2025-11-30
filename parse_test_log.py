from pathlib import Path
log_path = Path('DOTNET_TEST_WARNINGS.log')
lines = log_path.read_text().splitlines()
warnings = {}
errors = {}
for line in lines:
    if ': warning' in line:
        key = line.split(': warning')[0]
        warnings[key] = warnings.get(key, 0) + 1
    if ': error' in line:
        key = line.split(': error')[0]
        errors[key] = errors.get(key, 0) + 1
with Path('warning_summary.txt').open('w') as f:
    for path, count in sorted(warnings.items(), key=lambda item: -item[1]):
        f.write(f"{path} - {count} warnings\n")
with Path('error_summary.txt').open('w') as f:
    for path, count in sorted(errors.items(), key=lambda item: -item[1]):
        f.write(f"{path} - {count} errors\n")
