#!/bin/bash
# Fix all Guid/string conversion errors in AdminEndpoints.cs

FILE="src/Api/Routing/AdminEndpoints.cs"

# Backup original
cp "$FILE" "$FILE.bak"

# Fix line 53: log.Id to log.Id.ToString()
sed -i '53s/log\.Id,/log.Id.ToString(),/' "$FILE"

# Fix line 221: Guid.Parse(log.Id) to log.Id (since log.Id is already Guid)
sed -i '221s/Guid\.Parse(log\.Id)/log.Id/' "$FILE"

# Fix line 529: userId (string) to Guid.Parse(userId)
sed -i '529s/GetAllSessionsAsync(userId,/GetAllSessionsAsync(string.IsNullOrEmpty(userId) ? (Guid?)null : Guid.Parse(userId),/' "$FILE"

# Fix line 547: sessionId (string) to Guid.Parse(sessionId)
sed -i '547s/RevokeSessionAsync(sessionId,/RevokeSessionAsync(Guid.Parse(sessionId),/' "$FILE"

# Fix line 571: userId (string) to Guid.Parse(userId)
sed -i '571s/RevokeAllUserSessionsAsync(userId,/RevokeAllUserSessionsAsync(Guid.Parse(userId),/' "$FILE"

# Fix line 904, 908: t.Id (Guid) to t.Id.ToString()
sed -i '904s/Id = t\.Id,/Id = t.Id.ToString(),/' "$FILE"
sed -i '908s/CreatedByUserId = t\.CreatedByUserId,/CreatedByUserId = t.CreatedByUserId?.ToString(),/' "$FILE"

# Fix line 958, 962: Guid.NewGuid().ToString() to Guid.NewGuid(), session.User.Id to session.User.Id.ToString()
sed -i '958s/Id = Guid\.NewGuid()\.ToString(),/Id = Guid.NewGuid(),/' "$FILE"
sed -i '962s/CreatedByUserId = session\.User\.Id,/CreatedByUserId = session.User.Id.ToString(),/' "$FILE"

# Fix line 974, 978: t.Id, CreatedByUserId (Guid to string)
sed -i '974s/Id = t\.Id,/Id = t.Id.ToString(),/' "$FILE"
sed -i '978s/CreatedByUserId = t\.CreatedByUserId,/CreatedByUserId = t.CreatedByUserId?.ToString(),/' "$FILE"

# Fix line 1014: t.CreatedByUserId == userId (Guid == string)
sed -i '1014s/t\.CreatedByUserId == userId/t.CreatedByUserId.ToString() == userId/' "$FILE"

# Fix line 1068: v.CreatedByUserId == userId (Guid == string)
sed -i '1068s/v\.CreatedByUserId == userId/v.CreatedByUserId.ToString() == userId/' "$FILE"

# Fix line 1087-1092: string to Guid conversions
sed -i '1087s/Id = v\.Id,/Id = Guid.Parse(v.Id),/' "$FILE"
sed -i '1088s/TemplateId = v\.TemplateId,/TemplateId = Guid.Parse(v.TemplateId),/' "$FILE"
sed -i '1092s/CreatedByUserId = v\.CreatedByUserId,/CreatedByUserId = string.IsNullOrEmpty(v.CreatedByUserId) ? (Guid?)null : Guid.Parse(v.CreatedByUserId),/' "$FILE"

# Fix line 1106-1111: Guid to string conversions
sed -i '1106s/Id = version\.Id,/Id = version.Id.ToString(),/' "$FILE"
sed -i '1107s/TemplateId = version\.TemplateId,/TemplateId = version.TemplateId.ToString(),/' "$FILE"
sed -i '1111s/CreatedByUserId = version\.CreatedByUserId,/CreatedByUserId = version.CreatedByUserId?.ToString(),/' "$FILE"

# Fix line 1144: audit.UserId == userId (Guid == string)
sed -i '1144s/audit\.UserId == userId/audit.UserId.ToString() == userId/' "$FILE"

# Fix line 1154: audit.UserId == userId (Guid == string)
sed -i '1154s/audit\.UserId == userId/audit.UserId.ToString() == userId/' "$FILE"

# Fix line 1200: string to Guid conversions
sed -i '1200s/templateId, versionId, userId/Guid.Parse(templateId), Guid.Parse(versionId), Guid.Parse(userId)/' "$FILE"

# Fix line 1653: templateId (string to Guid)
sed -i '1653s/GetTemplateAsync(templateId)/GetTemplateAsync(Guid.Parse(templateId))/' "$FILE"

# Fix line 1706: userId (string to Guid)
sed -i '1706s/CreateTemplateAsync(request, userId)/CreateTemplateAsync(request, Guid.Parse(userId))/' "$FILE"

# Fix line 1742: templateId, userId (string to Guid)
sed -i '1742s/UpdateTemplateAsync(templateId, request, userId)/UpdateTemplateAsync(Guid.Parse(templateId), request, Guid.Parse(userId))/' "$FILE"

# Fix line 1783: templateId (string to Guid)
sed -i '1783s/DeleteTemplateAsync(templateId)/DeleteTemplateAsync(Guid.Parse(templateId))/' "$FILE"

# Fix line 1820: templateId, userId (string to Guid)
sed -i '1820s/CreateVersionAsync(templateId, request, userId)/CreateVersionAsync(Guid.Parse(templateId), request, Guid.Parse(userId))/' "$FILE"

# Fix line 1858: userId (string to Guid)
sed -i '1858s/GetVersionHistoryAsync(request, userId)/GetVersionHistoryAsync(request, Guid.Parse(userId))/' "$FILE"

# Fix line 1945: userId (string to Guid)
sed -i '1945s/GetAuditLogsAsync(request, userId)/GetAuditLogsAsync(request, Guid.Parse(userId))/' "$FILE"

# Fix line 2072: templateId (string to Guid)
sed -i '2072s/EvaluateVersionAsync(templateId, versionId, request)/EvaluateVersionAsync(Guid.Parse(templateId), versionId, request)/' "$FILE"

# Fix line 2102: templateId, userId (string to Guid)
sed -i '2102s/CompareVersionsAsync(templateId, request, userId)/CompareVersionsAsync(Guid.Parse(templateId), request, Guid.Parse(userId))/' "$FILE"

# Fix line 2235: evaluation.UserId == userId (Guid == string)
sed -i '2235s/evaluation\.UserId == userId/evaluation.UserId.ToString() == userId/' "$FILE"

# Fix line 2335: userId (string to Guid)
sed -i '2335s/GetEvaluationHistoryAsync(request, userId)/GetEvaluationHistoryAsync(request, Guid.Parse(userId))/' "$FILE"

# Fix line 2369: userId (string to Guid)
sed -i '2369s/GetEvaluationReportAsync(evaluationId, format, userId)/GetEvaluationReportAsync(evaluationId, format, Guid.Parse(userId))/' "$FILE"

# Fix line 2454: userId (string to Guid)
sed -i '2454s/GetEvaluationReportAsync(evaluationId, format, userId)/GetEvaluationReportAsync(evaluationId, format, Guid.Parse(userId))/' "$FILE"

echo "Fixed all errors in $FILE"
echo "Backup saved to $FILE.bak"
