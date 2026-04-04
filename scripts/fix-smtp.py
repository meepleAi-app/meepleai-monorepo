import sys

filepath = sys.argv[1]

with open(filepath, "r") as f:
    content = f.read()

# Fix the broken lines from sed
content = content.replace(
    '      Email__SmtpUsername: "\n Email__SmtpPassword: "\\n      Email__EnableSsl: "true"',
    '      Email__SmtpUsername: "${SMTP_USER}"\n      Email__SmtpPassword: "${SMTP_PASSWORD}"\n      Email__EnableSsl: "true"'
)

with open(filepath, "w") as f:
    f.write(content)

print("OK")
