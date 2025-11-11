#!/bin/bash
# Fix remaining Guid/string conversion errors in AdminEndpoints.cs

FILE="src/Api/Routing/AdminEndpoints.cs"

# Fix line 1154: v.TemplateId == id (Guid == string)
sed -i '1154s/v\.TemplateId == id/v.TemplateId == Guid.Parse(id)/' "$FILE"

# Fix line 1200: ActivateVersionAsync parameters (all need Guid.Parse)
sed -i '1200s/ActivateVersionAsync(id, versionId, session\.User\.Id, ct)/ActivateVersionAsync(Guid.Parse(id), Guid.Parse(versionId), Guid.Parse(session.User.Id), ct)/' "$FILE"

# Fix line 1653: GetConfigurationByIdAsync(id) - id is string
sed -i '1653s/GetConfigurationByIdAsync(id)/GetConfigurationByIdAsync(Guid.Parse(id))/' "$FILE"

# Fix line 1706: CreateConfigurationAsync(request, session.User.Id) - User.Id is Guid
sed -i '1706s/CreateConfigurationAsync(request, session\.User\.Id)/CreateConfigurationAsync(request, Guid.Parse(session.User.Id))/' "$FILE"

# Fix line 1742: UpdateConfigurationAsync(id, request, session.User.Id)
sed -i '1742s/UpdateConfigurationAsync(id, request, session\.User\.Id)/UpdateConfigurationAsync(Guid.Parse(id), request, Guid.Parse(session.User.Id))/' "$FILE"

# Fix line 1783: DeleteConfigurationAsync(id)
sed -i '1783s/DeleteConfigurationAsync(id)/DeleteConfigurationAsync(Guid.Parse(id))/' "$FILE"

# Fix line 1820: ToggleConfigurationAsync(id, isActive, session.User.Id)
sed -i '1820s/ToggleConfigurationAsync(id, isActive, session\.User\.Id)/ToggleConfigurationAsync(Guid.Parse(id), isActive, Guid.Parse(session.User.Id))/' "$FILE"

# Fix line 1858: BulkUpdateConfigurationsAsync(request, session.User.Id)
sed -i '1858s/BulkUpdateConfigurationsAsync(request, session\.User\.Id)/BulkUpdateConfigurationsAsync(request, Guid.Parse(session.User.Id))/' "$FILE"

# Fix line 1945: ImportConfigurationsAsync(request, session.User.Id)
sed -i '1945s/ImportConfigurationsAsync(request, session\.User\.Id)/ImportConfigurationsAsync(request, Guid.Parse(session.User.Id))/' "$FILE"

# Fix line 2072: GetConfigurationHistoryAsync(id, limit)
sed -i '2072s/GetConfigurationHistoryAsync(id, limit)/GetConfigurationHistoryAsync(Guid.Parse(id), limit)/' "$FILE"

# Fix line 2102: RollbackConfigurationAsync(id, version, session.User.Id)
sed -i '2102s/RollbackConfigurationAsync(id, version, session\.User\.Id)/RollbackConfigurationAsync(Guid.Parse(id), version, Guid.Parse(session.User.Id))/' "$FILE"

# Fix line 2235: g.Id == gameId (Guid == string)
sed -i '2235s/g\.Id == gameId/g.Id == Guid.Parse(gameId)/' "$FILE"

# Fix line 2335: CreatePromptTemplateAsync(request, session.User.Id, ct)
sed -i '2335s/CreatePromptTemplateAsync(request, session\.User\.Id, ct)/CreatePromptTemplateAsync(request, Guid.Parse(session.User.Id), ct)/' "$FILE"

# Fix line 2369: CreatePromptVersionAsync(templateId, request, session.User.Id, ct)
sed -i '2369s/CreatePromptVersionAsync(templateId, request, session\.User\.Id, ct)/CreatePromptVersionAsync(templateId, request, Guid.Parse(session.User.Id), ct)/' "$FILE"

# Fix line 2454: ActivateVersionAsync(templateId, versionId, session.User.Id, request.Reason, ct)
sed -i '2454s/ActivateVersionAsync(templateId, versionId, session\.User\.Id, request\.Reason, ct)/ActivateVersionAsync(templateId, versionId, Guid.Parse(session.User.Id), request.Reason, ct)/' "$FILE"

echo "Fixed remaining errors in $FILE"
