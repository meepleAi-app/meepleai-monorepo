#!/bin/bash
# Final fix for all remaining Guid/string issues in AdminEndpoints.cs

FILE="src/Api/Routing/AdminEndpoints.cs"

# Line 962: session.User.Id is string, CreatedByUserId is Guid - need to parse
sed -i '962s/CreatedByUserId = session\.User\.Id,/CreatedByUserId = Guid.Parse(session.User.Id),/' "$FILE"

# Line 1014: t.Id is Guid (PromptTemplateEntity.Id), id is string parameter - need to compare with Guid.Parse
sed -i '1014s/\.FirstOrDefaultAsync(t => t\.Id == id, ct)/\.FirstOrDefaultAsync(t => t.Id == Guid.Parse(id), ct)/' "$FILE"

# Line 1068: Same as above
sed -i '1068s/\.FirstOrDefaultAsync(t => t\.Id == id, ct)/\.FirstOrDefaultAsync(t => t.Id == Guid.Parse(id), ct)/' "$FILE"

# Line 1087: v.Id is Guid (PromptVersionEntity.Id), DTO expects string
sed -i '1087s/Id = Guid\.NewGuid()\.ToString(),/Id = v.Id.ToString(),/' "$FILE"

# Line 1088: v.TemplateId is Guid, DTO expects string
sed -i '1088s/TemplateId = id,/TemplateId = v.TemplateId.ToString(),/' "$FILE"

# Line 1092: session.User.Id is string, CreatedByUserId is Guid - need to parse
sed -i '1092s/CreatedByUserId = session\.User\.Id,/CreatedByUserId = Guid.Parse(session.User.Id),/' "$FILE"

# Line 1144: Same as 1014/1068
sed -i '1144s/\.FirstOrDefaultAsync(t => t\.Id == id, ct)/\.FirstOrDefaultAsync(t => t.Id == Guid.Parse(id), ct)/' "$FILE"

# Lines 1158-1163: These are PromptAuditLogDto mappings
# v.Id, v.TemplateId, v.CreatedByUserId are all Guid, need .ToString()
sed -i '1158s/Id = v\.Id,/Id = v.Id.ToString(),/' "$FILE"
sed -i '1159s/TemplateId = v\.TemplateId,/TemplateId = v.TemplateId.ToString(),/' "$FILE"
sed -i '1163s/CreatedByUserId = v\.CreatedByUserId,/CreatedByUserId = v.CreatedByUserId.ToString(),/' "$FILE"

echo "Applied final fixes to $FILE"
