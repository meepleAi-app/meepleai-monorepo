#!/bin/bash
# Fix the last 4 remaining errors

FILE="src/Api/Routing/AdminEndpoints.cs"

# Line 1028: template.Id (Guid) → DTO expects string
sed -i '1028s/Id = template\.Id,/Id = template.Id.ToString(),/' "$FILE"

# Line 1032: template.CreatedByUserId (Guid) → DTO expects string
sed -i '1032s/CreatedByUserId = template\.CreatedByUserId,/CreatedByUserId = template.CreatedByUserId.ToString(),/' "$FILE"

# Line 1087: v.Id.ToString() is wrong - should be Guid.NewGuid() since it's a new entity
sed -i '1087s/Id = v\.Id\.ToString(),/Id = Guid.NewGuid(),/' "$FILE"

# Line 1088: v.TemplateId.ToString() is wrong - should reference the template id parameter
# Need to find what variable holds the template id in this context
# Looking at context, 'id' is the template id parameter (string), need to parse it
sed -i '1088s/TemplateId = v\.TemplateId\.ToString(),/TemplateId = Guid.Parse(id),/' "$FILE"

echo "Fixed last 4 errors"
