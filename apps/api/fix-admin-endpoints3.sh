#!/bin/bash
# Fix final Guid/string conversion errors in AdminEndpoints.cs

FILE="src/Api/Routing/AdminEndpoints.cs"

# Fix line 91 (SessionCacheService): GetUserSessionsSetKey expects string
# This is in another file, skip for now

# Fix line 908: t.CreatedByUserId?.ToString() should just work, but compiler says ?. is invalid on Guid
# Change to: t.CreatedByUserId.ToString() (since CreatedByUserId is not nullable Guid)
sed -i '908s/CreatedByUserId = t\.CreatedByUserId?\.ToString()/CreatedByUserId = t.CreatedByUserId.ToString()/' "$FILE"

# Fix line 962: session.User.Id is Guid, CreatedByUserId is Guid, but currently assigned as-is
# Entity property is Guid, so we need just session.User.Id (no ToString)
sed -i '962s/CreatedByUserId = session\.User\.Id\.ToString()/CreatedByUserId = session.User.Id/' "$FILE"

# Fix line 974, 978: template.Id and template.CreatedByUserId are Guid, but DTO expects string
sed -i '974s/Id = template\.Id,/Id = template.Id.ToString(),/' "$FILE"
sed -i '978s/CreatedByUserId = template\.CreatedByUserId,/CreatedByUserId = template.CreatedByUserId.ToString(),/' "$FILE"

# Fix line 1014: t.CreatedByUserId (Guid) == userId (string)
sed -i '1014s/t\.CreatedByUserId.ToString() == userId/t.CreatedByUserId == Guid.Parse(userId)/' "$FILE"

# Fix line 1068: v.CreatedByUserId (Guid) == userId (string)
sed -i '1068s/v\.CreatedByUserId.ToString() == userId/v.CreatedByUserId == Guid.Parse(userId)/' "$FILE"

# Fix lines 1087-1092: v.Id, v.TemplateId, v.CreatedByUserId are all strings in DTO, need to convert back to Guid for entity comparison
# These look like they're in a LINQ query mapping FROM entity TO DTO
# Entity has Guid, DTO expects string, so we need .ToString()
sed -i '1087s/Id = Guid\.Parse(v\.Id)/Id = v.Id.ToString()/' "$FILE"
sed -i '1088s/TemplateId = Guid\.Parse(v\.TemplateId)/TemplateId = v.TemplateId.ToString()/' "$FILE"
sed -i '1092s/CreatedByUserId = string\.IsNullOrEmpty(v\.CreatedByUserId) ? (Guid?)null : Guid\.Parse(v\.CreatedByUserId)/CreatedByUserId = v.CreatedByUserId.ToString()/' "$FILE"

# Fix line 1111: version.CreatedByUserId?.ToString() - but compiler says ?. is invalid on Guid (not nullable)
sed -i '1111s/CreatedByUserId = version\.CreatedByUserId?\.ToString()/CreatedByUserId = version.CreatedByUserId.ToString()/' "$FILE"

# Fix line 1144: audit.UserId (Guid) == userId (string)
sed -i '1144s/audit\.UserId.ToString() == userId/audit.UserId == Guid.Parse(userId)/' "$FILE"

# Fix lines 1158-1163: audit.Id, audit.TemplateId, audit.UserId are Guid, DTO expects string
sed -i '1158s/Id = audit\.Id,/Id = audit.Id.ToString(),/' "$FILE"
sed -i '1159s/TemplateId = audit\.TemplateId,/TemplateId = audit.TemplateId.ToString(),/' "$FILE"
sed -i '1163s/UserId = audit\.UserId,/UserId = audit.UserId.ToString(),/' "$FILE"

echo "Fixed final errors in $FILE"
