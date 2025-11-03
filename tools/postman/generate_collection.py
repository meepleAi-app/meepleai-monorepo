#!/usr/bin/env python3
"""Generate Postman collection and environment files for MeepleAI API."""
from __future__ import annotations

import json
import uuid
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional, Sequence, Tuple

ROOT = Path(__file__).resolve().parents[2]
OUTPUT_DIR = ROOT / "docs" / "postman"
COLLECTION_FILE = OUTPUT_DIR / "meepleai-api.postman_collection.json"
ENVIRONMENT_FILE = OUTPUT_DIR / "meepleai-api.postman_environment.json"

@dataclass
class QueryParam:
    key: str
    value: str
    description: Optional[str] = None
    disabled: bool = False

    def as_postman(self) -> Dict[str, Any]:
        data: Dict[str, Any] = {"key": self.key, "value": self.value}
        if self.description:
            data["description"] = self.description
        if self.disabled:
            data["disabled"] = True
        return data

@dataclass
class RequestBody:
    mode: str
    raw: Optional[Any] = None
    formdata: Optional[List[Dict[str, Any]]] = None

    def as_postman(self) -> Dict[str, Any]:
        body: Dict[str, Any] = {"mode": self.mode}
        if self.mode == "raw" and self.raw is not None:
            body["raw"] = json.dumps(self.raw, indent=2)
            body.setdefault("options", {"raw": {"language": "json"}})
        if self.mode == "formdata" and self.formdata is not None:
            body["formdata"] = self.formdata
        return body

@dataclass
class Endpoint:
    name: str
    method: str
    url: str
    description: str
    auth: Optional[str] = None
    body: Optional[RequestBody] = None
    params: Sequence[QueryParam] = field(default_factory=list)

    headers: List[Dict[str, Any]] = field(default_factory=list)

    def build_headers(self) -> List[Dict[str, Any]]:
        headers = list(self.headers)
        if self.body and self.body.mode == "raw":
            headers.append({"key": "Content-Type", "value": "application/json"})
        if self.body and self.body.mode == "formdata":
            headers.append({"key": "Content-Type", "value": "multipart/form-data"})
        if self.auth in {"session", "sessionOrApiKey", "sessionAdmin"}:
            headers.append({
                "key": "Cookie",
                "value": "{{sessionCookieName}}={{sessionToken}}",
                "description": "Session cookie issued by login endpoint."
            })
        if self.auth in {"apiKey", "sessionOrApiKey"}:
            headers.append({
                "key": "X-API-Key",
                "value": "{{apiKey}}",
                "description": "API key for header-based authentication."
            })
        # Remove duplicates while preserving order
        seen = set()
        unique: List[Dict[str, Any]] = []
        for header in headers:
            key = header.get("key"), header.get("value")
            if key not in seen:
                seen.add(key)
                unique.append(header)
        return unique

    def as_postman(self) -> Dict[str, Any]:
        url_path = self.url.lstrip("/")
        raw_url = "{{baseUrl}}" + ("/" + url_path if url_path else "")
        url = {"raw": raw_url}
        if url_path:
            url["path"] = [segment for segment in url_path.split("/") if segment]
        query = [p.as_postman() for p in self.params]
        if query:
            url["query"] = query
        request: Dict[str, Any] = {
            "method": self.method,
            "header": self.build_headers(),
            "url": url,
            "description": self.description,
        }
        if self.body:
            request["body"] = self.body.as_postman()
        return {"name": self.name, "request": request, "response": []}

@dataclass
class Folder:
    name: str
    description: str
    requests: Sequence[Endpoint] = field(default_factory=list)
    children: Sequence['Folder'] = field(default_factory=list)

    def as_postman(self) -> Dict[str, Any]:
        data: Dict[str, Any] = {"name": self.name, "description": self.description}
        items: List[Any] = []
        for child in self.children:
            items.append(child.as_postman())
        if self.requests:
            items.extend(endpoint.as_postman() for endpoint in self.requests)
        data["item"] = items
        return data


def folder(name: str, description: str, requests: Sequence[Endpoint] = (), children: Sequence[Folder] = ()) -> Folder:
    return Folder(name=name, description=description, requests=list(requests), children=list(children))


def endpoint(
    name: str,
    method: str,
    url: str,
    description: str,
    *,
    auth: Optional[str] = None,
    body: Optional[RequestBody] = None,
    params: Sequence[QueryParam] = (),
    headers: Sequence[Dict[str, Any]] = (),
) -> Endpoint:
    return Endpoint(
        name=name,
        method=method,
        url=url,
        description=description,
        auth=auth,
        body=body,
        params=list(params),
        headers=list(headers),
    )


def qp(key: str, value: str, description: Optional[str] = None, *, disabled: bool = False) -> QueryParam:
    return QueryParam(key=key, value=value, description=description, disabled=disabled)


def raw_body(payload: Any) -> RequestBody:
    return RequestBody(mode="raw", raw=payload)


def form_body(fields: List[Dict[str, Any]]) -> RequestBody:
    return RequestBody(mode="formdata", formdata=fields)


FOLDERS: List[Folder] = [
    folder(
        "Health",
        "Infrastructure readiness and liveness endpoints.",
        requests=[
            endpoint(
                "Root Ping",
                "GET",
                "/",
                "Returns API name and ok=true for basic availability.",
            ),
            endpoint(
                "Health Check",
                "GET",
                "/health",
                "Aggregated health check across subsystems.",
            ),
            endpoint(
                "Readiness",
                "GET",
                "/health/ready",
                "Readiness probe used by orchestrators to gate traffic.",
            ),
            endpoint(
                "Liveness",
                "GET",
                "/health/live",
                "Liveness probe ensuring process is responsive.",
            ),
        ],
    ),
    folder(
        "Authentication",
        "User registration, session lifecycle, OAuth, and 2FA flows.",
        requests=[
            endpoint(
                "Register",
                "POST",
                "/api/v1/auth/register",
                "Create a new account and receive session cookie upon success.",
                body=raw_body({
                    "email": "{{email}}",
                    "password": "{{password}}",
                    "displayName": "{{displayName}}",
                    "role": "User",
                }),
            ),
            endpoint(
                "Login",
                "POST",
                "/api/v1/auth/login",
                "Authenticate with email and password to establish a session.",
                body=raw_body({
                    "email": "{{email}}",
                    "password": "{{password}}",
                }),
            ),
            endpoint(
                "Logout",
                "POST",
                "/api/v1/auth/logout",
                "Invalidate the active session cookie.",
                auth="session",
            ),
            endpoint(
                "Current User",
                "GET",
                "/api/v1/auth/me",
                "Retrieve authenticated user details (cookie or API key).",
                auth="sessionOrApiKey",
            ),
            endpoint(
                "Setup 2FA",
                "POST",
                "/api/v1/auth/2fa/setup",
                "Generate QR code and secret to enable authenticator apps.",
                auth="session",
            ),
            endpoint(
                "Enable 2FA",
                "POST",
                "/api/v1/auth/2fa/enable",
                "Confirm setup with current OTP code.",
                auth="session",
                body=raw_body({"code": "{{twoFactorCode}}"}),
            ),
            endpoint(
                "Verify 2FA Challenge",
                "POST",
                "/api/v1/auth/2fa/verify",
                "Complete login after providing valid OTP or backup code.",
                body=raw_body({
                    "sessionToken": "{{tempSessionToken}}",
                    "code": "{{twoFactorCode}}",
                }),
            ),
            endpoint(
                "Disable 2FA",
                "POST",
                "/api/v1/auth/2fa/disable",
                "Disable two-factor authentication for current user.",
                auth="session",
                body=raw_body({
                    "password": "{{password}}",
                    "code": "{{twoFactorCode}}",
                }),
            ),
            endpoint(
                "2FA Status",
                "GET",
                "/api/v1/users/me/2fa/status",
                "Inspect 2FA enrollment and recovery status.",
                auth="session",
            ),
            endpoint(
                "OAuth Login Redirect",
                "GET",
                "/api/v1/auth/oauth/{{oauthProvider}}/login",
                "Start OAuth2 login with provider (google, discord, github).",
                params=[qp("redirect", "{{frontendRedirect}}", "Optional redirect override", disabled=True)],
            ),
            endpoint(
                "OAuth Callback",
                "GET",
                "/api/v1/auth/oauth/{{oauthProvider}}/callback",
                "Handle OAuth2 callback by exchanging authorization code.",
                params=[
                    qp("code", "{{oauthCode}}"),
                    qp("state", "{{oauthState}}"),
                ],
            ),
            endpoint(
                "Unlink OAuth Provider",
                "DELETE",
                "/api/v1/auth/oauth/{{oauthProvider}}/unlink",
                "Detach linked OAuth provider from current account.",
                auth="session",
            ),
            endpoint(
                "Linked OAuth Accounts",
                "GET",
                "/api/v1/users/me/oauth-accounts",
                "List OAuth providers associated with the current account.",
                auth="session",
            ),
            endpoint(
                "Session Status",
                "GET",
                "/api/v1/auth/session/status",
                "Report absolute and inactivity-based expiry for active session.",
                auth="session",
            ),
            endpoint(
                "Extend Session",
                "POST",
                "/api/v1/auth/session/extend",
                "Refresh inactivity timeout for active session.",
                auth="session",
            ),
            endpoint(
                "My Sessions",
                "GET",
                "/api/v1/users/me/sessions",
                "List active sessions owned by the authenticated user.",
                auth="session",
            ),
            endpoint(
                "Request Password Reset",
                "POST",
                "/api/v1/auth/password-reset/request",
                "Send password reset email if account exists.",
                body=raw_body({"email": "{{email}}"}),
            ),
            endpoint(
                "Verify Reset Token",
                "GET",
                "/api/v1/auth/password-reset/verify",
                "Validate reset token before allowing password change.",
                params=[qp("token", "{{passwordResetToken}}")],
            ),
            endpoint(
                "Confirm Password Reset",
                "PUT",
                "/api/v1/auth/password-reset/confirm",
                "Update password and receive a fresh session cookie.",
                body=raw_body({
                    "token": "{{passwordResetToken}}",
                    "newPassword": "{{newPassword}}",
                }),
            ),
        ],
    ),
    folder(
        "Games",
        "Game catalog operations and agent discovery.",
        requests=[
            endpoint(
                "List Games",
                "GET",
                "/api/v1/games",
                "Fetch games visible to the current session.",
                auth="session",
            ),
            endpoint(
                "Create Game",
                "POST",
                "/api/v1/games",
                "Create a new game (Admin or Editor role required).",
                auth="session",
                body=raw_body({
                    "name": "{{gameName}}",
                    "gameId": "{{gameId}}",
                }),
            ),
            endpoint(
                "Agents for Game",
                "GET",
                "/api/v1/games/{{gameId}}/agents",
                "List conversational agents registered for a game.",
                auth="session",
            ),
        ],
    ),
    folder(
        "Chat",
        "Chat threads, message lifecycle, and export flows.",
        requests=[
            endpoint(
                "List Chats",
                "GET",
                "/api/v1/chats",
                "Return most recent chats for the authenticated user.",
                auth="session",
                params=[qp("gameId", "{{gameId}}", "Optional filter", disabled=True)],
            ),
            endpoint(
                "Get Chat",
                "GET",
                "/api/v1/chats/{{chatId}}",
                "Retrieve chat metadata and message history.",
                auth="session",
            ),
            endpoint(
                "Create Chat",
                "POST",
                "/api/v1/chats",
                "Start a new conversation for a game and agent.",
                auth="session",
                body=raw_body({
                    "gameId": "{{gameId}}",
                    "agentId": "{{agentId}}",
                }),
            ),
            endpoint(
                "Delete Chat",
                "DELETE",
                "/api/v1/chats/{{chatId}}",
                "Soft-delete chat history (owner only).",
                auth="session",
            ),
            endpoint(
                "Update Message",
                "PUT",
                "/api/v1/chats/{{chatId}}/messages/{{messageId}}",
                "Edit a user-authored message when feature flag permits.",
                auth="session",
                body=raw_body({"content": "Updated message content"}),
            ),
            endpoint(
                "Delete Message",
                "DELETE",
                "/api/v1/chats/{{chatId}}/messages/{{messageId}}",
                "Soft-delete a specific message (self or admin override).",
                auth="session",
            ),
            endpoint(
                "Export Chat",
                "POST",
                "/api/v1/chats/{{chatId}}/export",
                "Export chat logs as PDF or JSON when feature flag enabled.",
                auth="session",
                body=raw_body({
                    "format": "pdf",
                    "dateFrom": "{{exportFrom}}",
                    "dateTo": "{{exportTo}}",
                }),
            ),
        ],
    ),
    folder(
        "AI Agents",
        "Retrieval augmented agents, streaming flows, and feedback.",
        requests=[
            endpoint(
                "QA",
                "POST",
                "/api/v1/agents/qa",
                "Ask a rules question using hybrid RAG search.",
                auth="session",
                body=raw_body({
                    "gameId": "{{gameId}}",
                    "query": "Explain combat resolution",
                    "chatId": "{{chatId}}",
                    "searchMode": "Hybrid",
                }),
            ),
            endpoint(
                "QA Stream (SSE)",
                "POST",
                "/api/v1/agents/qa/stream",
                "Server-sent events streaming QA responses token-by-token.",
                auth="session",
                body=raw_body({
                    "gameId": "{{gameId}}",
                    "query": "What happens when a player cannot draw cards?",
                    "chatId": "{{chatId}}",
                }),
            ),
            endpoint(
                "Explain",
                "POST",
                "/api/v1/agents/explain",
                "Generate narrative explanation with citations for a topic.",
                auth="session",
                body=raw_body({
                    "gameId": "{{gameId}}",
                    "topic": "Setup overview",
                    "chatId": "{{chatId}}",
                }),
            ),
            endpoint(
                "Explain Stream (SSE)",
                "POST",
                "/api/v1/agents/explain/stream",
                "Stream explain script and outline over SSE.",
                auth="session",
                body=raw_body({
                    "gameId": "{{gameId}}",
                    "topic": "Victory conditions",
                }),
            ),
            endpoint(
                "Setup Guide",
                "POST",
                "/api/v1/agents/setup",
                "Produce game setup checklist (feature flag guarded).",
                auth="session",
                body=raw_body({
                    "gameId": "{{gameId}}",
                    "chatId": "{{chatId}}",
                }),
            ),
            endpoint(
                "Agent Feedback",
                "POST",
                "/api/v1/agents/feedback",
                "Record thumbs-up / thumbs-down feedback for agent responses.",
                auth="session",
                body=raw_body({
                    "messageId": "{{messageId}}",
                    "endpoint": "qa",
                    "outcome": "positive",
                    "userId": "{{userId}}",
                    "gameId": "{{gameId}}",
                }),
            ),
            endpoint(
                "Chess Agent",
                "POST",
                "/api/v1/agents/chess",
                "Ask the chess strategist agent for move suggestions.",
                auth="session",
                body=raw_body({
                    "question": "What is the best plan here?",
                    "fenPosition": "{{fen}}",
                    "chatId": "{{chatId}}",
                }),
            ),
            endpoint(
                "BGG Search",
                "GET",
                "/api/v1/bgg/search",
                "Proxy search against BoardGameGeek API (authenticated).",
                auth="session",
                params=[
                    qp("q", "{{bggQuery}}"),
                    qp("exact", "false"),
                ],
            ),
            endpoint(
                "BGG Game Details",
                "GET",
                "/api/v1/bgg/games/{{bggId}}",
                "Fetch BoardGameGeek metadata for a specific game id.",
                auth="session",
            ),
            endpoint(
                "Index Chess Knowledge",
                "POST",
                "/api/v1/chess/index",
                "Rebuild chess knowledge embeddings (Admin only).",
                auth="session",
            ),
            endpoint(
                "Search Chess Knowledge",
                "GET",
                "/api/v1/chess/search",
                "Semantic search over indexed chess explanations.",
                auth="session",
                params=[
                    qp("q", "{{chessQuery}}"),
                    qp("limit", "5", "Max results", disabled=True),
                ],
            ),
            endpoint(
                "Delete Chess Knowledge",
                "DELETE",
                "/api/v1/chess/index",
                "Remove all chess knowledge entries (Admin only).",
                auth="session",
            ),
        ],
    ),
    folder(
        "PDF Ingestion",
        "Upload, monitor, and index PDF rulebooks.",
        requests=[
            endpoint(
                "Upload PDF",
                "POST",
                "/api/v1/ingest/pdf",
                "Upload rulebook PDF for processing (Admin/Editor).",
                auth="session",
                body=form_body([
                    {"key": "file", "type": "file", "src": ""},
                    {"key": "gameId", "type": "text", "value": "{{gameId}}"},
                ]),
            ),
            endpoint(
                "PDFs for Game",
                "GET",
                "/api/v1/games/{{gameId}}/pdfs",
                "List uploaded PDFs for a game.",
                auth="session",
            ),
            endpoint(
                "PDF Extracted Text",
                "GET",
                "/api/v1/pdfs/{{pdfId}}/text",
                "Retrieve extracted text and metadata for a PDF.",
                auth="session",
            ),
            endpoint(
                "Delete PDF",
                "DELETE",
                "/api/v1/pdf/{{pdfId}}",
                "Delete PDF (owner or admin) with audit logging.",
                auth="session",
            ),
            endpoint(
                "Processing Progress",
                "GET",
                "/api/v1/pdfs/{{pdfId}}/progress",
                "Check background processing progress for a PDF.",
                auth="session",
            ),
            endpoint(
                "Cancel Processing",
                "DELETE",
                "/api/v1/pdfs/{{pdfId}}/processing",
                "Cancel ongoing processing if still active.",
                auth="session",
            ),
            endpoint(
                "Generate RuleSpec from PDF",
                "POST",
                "/api/v1/ingest/pdf/{{pdfId}}/rulespec",
                "Generate structured rule specification draft (Admin/Editor).",
                auth="session",
            ),
            endpoint(
                "Index PDF",
                "POST",
                "/api/v1/ingest/pdf/{{pdfId}}/index",
                "Push PDF chunks to vector store for retrieval (Admin/Editor).",
                auth="session",
            ),
        ],
    ),
    folder(
        "Rule Specifications",
        "CRUD, versioning, commenting, and diff endpoints for RuleSpec.",
        requests=[
            endpoint(
                "Get RuleSpec",
                "GET",
                "/api/v1/games/{{gameId}}/rulespec",
                "Fetch the active rule specification for a game.",
                auth="session",
            ),
            endpoint(
                "Update RuleSpec",
                "PUT",
                "/api/v1/games/{{gameId}}/rulespec",
                "Replace rule specification (Admin/Editor).",
                auth="session",
                body=raw_body({
                    "gameId": "{{gameId}}",
                    "version": "v2",
                    "title": "Updated RuleSpec",
                    "sections": [],
                }),
            ),
            endpoint(
                "Version History",
                "GET",
                "/api/v1/games/{{gameId}}/rulespec/history",
                "List past versions (Admin/Editor).",
                auth="session",
            ),
            endpoint(
                "Version Timeline",
                "GET",
                "/api/v1/games/{{gameId}}/rulespec/versions/timeline",
                "Timeline filtered by date, author, or search (Admin/Editor).",
                auth="session",
                params=[
                    qp("startDate", "{{startDate}}", disabled=True),
                    qp("endDate", "{{endDate}}", disabled=True),
                    qp("author", "{{authorEmail}}", disabled=True),
                    qp("searchQuery", "setup", disabled=True),
                ],
            ),
            endpoint(
                "Get Version",
                "GET",
                "/api/v1/games/{{gameId}}/rulespec/versions/{{ruleSpecVersion}}",
                "Fetch a specific rule spec version (Admin/Editor).",
                auth="session",
            ),
            endpoint(
                "Diff Versions",
                "GET",
                "/api/v1/games/{{gameId}}/rulespec/diff",
                "Compute diff between two versions (Admin/Editor).",
                auth="session",
                params=[
                    qp("from", "v1"),
                    qp("to", "{{ruleSpecVersion}}"),
                ],
            ),
            endpoint(
                "Add Legacy Comment",
                "POST",
                "/api/v1/games/{{gameId}}/rulespec/versions/{{ruleSpecVersion}}/comments",
                "Add comment using legacy comment service (Admin/Editor).",
                auth="session",
                body=raw_body({
                    "atomId": "{{ruleAtomId}}",
                    "commentText": "Needs clarification",
                }),
            ),
            endpoint(
                "List Legacy Comments",
                "GET",
                "/api/v1/games/{{gameId}}/rulespec/versions/{{ruleSpecVersion}}/comments",
                "List comments created via legacy API (Admin/Editor).",
                auth="session",
            ),
            endpoint(
                "Update Legacy Comment",
                "PUT",
                "/api/v1/games/{{gameId}}/rulespec/comments/{{ruleSpecCommentId}}",
                "Update legacy comment content (author only).",
                auth="session",
                body=raw_body({"commentText": "Updated comment"}),
            ),
            endpoint(
                "Delete Legacy Comment",
                "DELETE",
                "/api/v1/games/{{gameId}}/rulespec/comments/{{ruleSpecCommentId}}",
                "Delete legacy comment (author or admin).",
                auth="session",
            ),
            endpoint(
                "Create Comment",
                "POST",
                "/api/v1/rulespecs/{{gameId}}/{{ruleSpecVersion}}/comments",
                "Create hierarchical comment on a rule spec (Admin/Editor).",
                auth="session",
                body=raw_body({
                    "lineNumber": 12,
                    "commentText": "Consider adding an example.",
                }),
            ),
            endpoint(
                "Reply to Comment",
                "POST",
                "/api/v1/comments/{{ruleCommentId}}/replies",
                "Reply to existing comment thread (Admin/Editor).",
                auth="session",
                body=raw_body({"commentText": "Agree with the suggestion"}),
            ),
            endpoint(
                "List Comments",
                "GET",
                "/api/v1/rulespecs/{{gameId}}/{{ruleSpecVersion}}/comments",
                "Fetch hierarchical comments (Admin/Editor).",
                auth="session",
                params=[qp("includeResolved", "false")],
            ),
            endpoint(
                "Comments for Line",
                "GET",
                "/api/v1/rulespecs/{{gameId}}/{{ruleSpecVersion}}/lines/{{lineNumber}}/comments",
                "Get comments for a specific rule line (Admin/Editor).",
                auth="session",
            ),
            endpoint(
                "Resolve Comment",
                "POST",
                "/api/v1/comments/{{ruleCommentId}}/resolve",
                "Mark comment as resolved (Admin/Editor).",
                auth="session",
                params=[qp("resolveReplies", "true", "Also resolve replies")],
            ),
            endpoint(
                "Unresolve Comment",
                "POST",
                "/api/v1/comments/{{ruleCommentId}}/unresolve",
                "Re-open a resolved comment (Admin/Editor).",
                auth="session",
                params=[qp("unresolveParent", "false", "Propagate to parent", disabled=True)],
            ),
            endpoint(
                "Bulk Export RuleSpecs",
                "POST",
                "/api/v1/rulespecs/bulk/export",
                "Export multiple rule specs as ZIP (Admin/Editor).",
                auth="session",
                body=raw_body({"ruleSpecIds": ["{{gameId}}"]}),
            ),
        ],
    ),
    folder(
        "Users",
        "User search endpoint accessible beyond authentication core.",
        requests=[
            endpoint(
                "Search Users",
                "GET",
                "/api/v1/users/search",
                "Search by display name or email for mentions.",
                auth="session",
                params=[qp("query", "{{userSearch}}")],
            ),
        ],
    ),
    folder(
        "API Keys",
        "Self-service API key lifecycle endpoints for authenticated users.",
        requests=[
            endpoint(
                "Create API Key",
                "POST",
                "/api/v1/api-keys",
                "Generate a new API key and return plaintext once.",
                auth="session",
                body=raw_body({
                    "keyName": "Local dev key",
                    "description": "CLI access",
                    "expiresAt": "",
                    "metadata": {},
                }),
            ),
            endpoint(
                "List API Keys",
                "GET",
                "/api/v1/api-keys",
                "List API keys owned by the current user.",
                auth="session",
                params=[
                    qp("includeRevoked", "false"),
                    qp("page", "1"),
                    qp("pageSize", "20"),
                ],
            ),
            endpoint(
                "Get API Key",
                "GET",
                "/api/v1/api-keys/{{apiKeyId}}",
                "Retrieve metadata for a specific key.",
                auth="session",
            ),
            endpoint(
                "Update API Key",
                "PUT",
                "/api/v1/api-keys/{{apiKeyId}}",
                "Update display name, metadata, or expiration.",
                auth="session",
                body=raw_body({
                    "keyName": "Updated key name",
                    "description": "Updated description",
                    "expiresAt": "",
                    "metadata": {},
                }),
            ),
            endpoint(
                "Revoke API Key",
                "DELETE",
                "/api/v1/api-keys/{{apiKeyId}}",
                "Soft-revoke API key for the current user.",
                auth="session",
            ),
            endpoint(
                "Rotate API Key",
                "POST",
                "/api/v1/api-keys/{{apiKeyId}}/rotate",
                "Rotate API key and receive new plaintext secret.",
                auth="session",
                body=raw_body({"reason": "Suspected compromise"}),
            ),
            endpoint(
                "API Key Usage",
                "GET",
                "/api/v1/api-keys/{{apiKeyId}}/usage",
                "Retrieve hourly/daily usage statistics for a key.",
                auth="session",
            ),
        ],
    ),
    folder(
        "n8n Templates",
        "Workflow templates catalogue and import flows.",
        requests=[
            endpoint(
                "List Templates",
                "GET",
                "/api/v1/n8n/templates",
                "List workflow templates (authentication required).",
                auth="session",
                params=[qp("category", "{{templateCategory}}", disabled=True)],
            ),
            endpoint(
                "Template Details",
                "GET",
                "/api/v1/n8n/templates/{{n8nTemplateId}}",
                "Get full template metadata and workflow definition.",
                auth="session",
            ),
            endpoint(
                "Import Template",
                "POST",
                "/api/v1/n8n/templates/{{n8nTemplateId}}/import",
                "Import template with parameter substitution.",
                auth="session",
                body=raw_body({"parameters": {"API_KEY": "{{apiKey}}"}}),
            ),
            endpoint(
                "Validate Template",
                "POST",
                "/api/v1/n8n/templates/validate",
                "Validate template JSON structure (Admin only).",
                auth="session",
                body=raw_body({"templateJson": "{}"}),
            ),
        ],
    ),
    folder(
        "Admin",
        "Privileged administration endpoints grouped by domain.",
        children=[
            folder(
                "Logs & Analytics",
                "AI request telemetry, seeding, and analytics dashboards.",
                requests=[
                    endpoint(
                        "Recent AI Logs",
                        "GET",
                        "/api/v1/logs",
                        "List recent AI request logs (Admin only).",
                        auth="session",
                    ),
                    endpoint(
                        "Seed Demo Data",
                        "POST",
                        "/api/v1/admin/seed",
                        "Seed demo RuleSpec for a game (Admin).",
                        auth="session",
                        body=raw_body({"gameId": "{{gameId}}"}),
                    ),
                    endpoint(
                        "AI Requests Dashboard",
                        "GET",
                        "/api/v1/admin/requests",
                        "Paginated AI request analytics (Admin).",
                        auth="session",
                        params=[
                            qp("limit", "100"),
                            qp("offset", "0"),
                            qp("endpoint", "", disabled=True),
                            qp("userId", "", disabled=True),
                            qp("gameId", "", disabled=True),
                            qp("startDate", "", disabled=True),
                            qp("endDate", "", disabled=True),
                        ],
                    ),
                    endpoint(
                        "AI Stats",
                        "GET",
                        "/api/v1/admin/stats",
                        "Aggregated metrics plus feedback stats (Admin).",
                        auth="session",
                        params=[
                            qp("startDate", "", disabled=True),
                            qp("endDate", "", disabled=True),
                            qp("userId", "", disabled=True),
                            qp("gameId", "", disabled=True),
                        ],
                    ),
                    endpoint(
                        "Low Quality Responses",
                        "GET",
                        "/api/v1/admin/quality/low-responses",
                        "Inspect flagged low quality answers (Admin).",
                        auth="session",
                        params=[
                            qp("limit", "50"),
                            qp("offset", "0"),
                            qp("startDate", "", disabled=True),
                            qp("endDate", "", disabled=True),
                        ],
                    ),
                    endpoint(
                        "Quality Report",
                        "GET",
                        "/api/v1/admin/quality/report",
                        "Generate rolling quality score report (Admin).",
                        auth="session",
                        params=[qp("days", "7")],
                    ),
                    endpoint(
                        "Analytics Overview",
                        "GET",
                        "/api/v1/admin/analytics",
                        "Dashboard metrics with time series data (Admin).",
                        auth="session",
                        params=[
                            qp("fromDate", "", disabled=True),
                            qp("toDate", "", disabled=True),
                            qp("days", "30"),
                            qp("gameId", "", disabled=True),
                            qp("roleFilter", "", disabled=True),
                        ],
                    ),
                    endpoint(
                        "Export Analytics",
                        "POST",
                        "/api/v1/admin/analytics/export",
                        "Export analytics dataset to CSV/JSON (Admin).",
                        auth="session",
                        body=raw_body({
                            "format": "csv",
                            "days": 30,
                            "gameId": "",
                            "roleFilter": "",
                        }),
                    ),
                ],
            ),
            folder(
                "n8n Configuration",
                "Manage n8n connection metadata and health checks.",
                requests=[
                    endpoint(
                        "List n8n Configs",
                        "GET",
                        "/api/v1/admin/n8n",
                        "List stored n8n configurations (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Get n8n Config",
                        "GET",
                        "/api/v1/admin/n8n/{{n8nConfigId}}",
                        "Retrieve a specific n8n configuration (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Create n8n Config",
                        "POST",
                        "/api/v1/admin/n8n",
                        "Create new n8n connection settings (Admin).",
                        auth="session",
                        body=raw_body({
                            "name": "Local",
                            "baseUrl": "http://localhost:5678",
                            "apiKey": "{{n8nApiKey}}",
                            "webhookUrl": "",
                        }),
                    ),
                    endpoint(
                        "Update n8n Config",
                        "PUT",
                        "/api/v1/admin/n8n/{{n8nConfigId}}",
                        "Update n8n configuration fields (Admin).",
                        auth="session",
                        body=raw_body({
                            "name": "Updated name",
                            "baseUrl": "",
                            "apiKey": "",
                            "webhookUrl": "",
                            "isActive": True,
                        }),
                    ),
                    endpoint(
                        "Delete n8n Config",
                        "DELETE",
                        "/api/v1/admin/n8n/{{n8nConfigId}}",
                        "Delete n8n config (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Test n8n Config",
                        "POST",
                        "/api/v1/admin/n8n/{{n8nConfigId}}/test",
                        "Trigger connectivity test against n8n instance (Admin).",
                        auth="session",
                    ),
                ],
            ),
            folder(
                "Session Management",
                "Administrative control of user sessions.",
                requests=[
                    endpoint(
                        "List Active Sessions",
                        "GET",
                        "/api/v1/admin/sessions",
                        "List active sessions optionally filtered by user (Admin).",
                        auth="session",
                        params=[
                            qp("limit", "100"),
                            qp("userId", "", disabled=True),
                        ],
                    ),
                    endpoint(
                        "Revoke Session",
                        "DELETE",
                        "/api/v1/admin/sessions/{{sessionId}}",
                        "Revoke session by id (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Revoke Sessions for User",
                        "DELETE",
                        "/api/v1/admin/users/{{userId}}/sessions",
                        "Revoke all sessions for a user (Admin).",
                        auth="session",
                    ),
                ],
            ),
            folder(
                "Workflow & Alerts",
                "Workflow error ingestion and alert lifecycle management.",
                requests=[
                    endpoint(
                        "Log Workflow Error",
                        "POST",
                        "/api/v1/logs/workflow-error",
                        "Webhook for logging workflow errors (no auth).",
                        body=raw_body({
                            "workflowId": "{{workflowId}}",
                            "executionId": "{{executionId}}",
                            "message": "Failed to deliver payload",
                            "payload": {},
                        }),
                    ),
                    endpoint(
                        "List Workflow Errors",
                        "GET",
                        "/api/v1/admin/workflows/errors",
                        "List workflow errors with filters (Admin).",
                        auth="session",
                        params=[
                            qp("workflowId", "", disabled=True),
                            qp("fromDate", "", disabled=True),
                            qp("toDate", "", disabled=True),
                            qp("page", "1"),
                            qp("limit", "20"),
                        ],
                    ),
                    endpoint(
                        "Workflow Error Details",
                        "GET",
                        "/api/v1/admin/workflows/errors/{{workflowErrorId}}",
                        "Fetch details for a specific workflow error (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Prometheus Alert Webhook",
                        "POST",
                        "/api/v1/alerts/prometheus",
                        "Webhook endpoint receiving Prometheus alerts (no auth).",
                        body=raw_body({
                            "status": "firing",
                            "receiver": "default",
                            "groupLabels": {"alertname": "MeepleAIHighLatency"},
                            "commonLabels": {"severity": "warning"},
                            "alerts": [],
                        }),
                    ),
                    endpoint(
                        "List Alerts",
                        "GET",
                        "/api/v1/admin/alerts",
                        "List active or historical alerts (Admin).",
                        auth="session",
                        params=[qp("activeOnly", "true")],
                    ),
                    endpoint(
                        "Resolve Alert",
                        "POST",
                        "/api/v1/admin/alerts/{{alertType}}/resolve",
                        "Manually resolve alert by type (Admin).",
                        auth="session",
                    ),
                ],
            ),
            folder(
                "Prompt Management",
                "Prompt template lifecycle, evaluation, and activation workflows.",
                requests=[
                    endpoint(
                        "List Prompt Templates",
                        "GET",
                        "/api/v1/admin/prompts",
                        "Paginated list of prompt templates (Admin).",
                        auth="session",
                        params=[
                            qp("page", "1"),
                            qp("limit", "50"),
                            qp("category", "", disabled=True),
                        ],
                    ),
                    endpoint(
                        "Create Prompt Template",
                        "POST",
                        "/api/v1/admin/prompts",
                        "Create a new prompt template (Admin).",
                        auth="session",
                        body=raw_body({
                            "name": "New template",
                            "description": "",
                            "category": "General",
                        }),
                    ),
                    endpoint(
                        "Prompt Template Details",
                        "GET",
                        "/api/v1/admin/prompts/{{promptTemplateId}}",
                        "Get template metadata including active version (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Create Prompt Version",
                        "POST",
                        "/api/v1/admin/prompts/{{promptTemplateId}}/versions",
                        "Create a new prompt version (Admin).",
                        auth="session",
                        body=raw_body({
                            "content": "You are MeepleAI...",
                            "metadata": {},
                        }),
                    ),
                    endpoint(
                        "List Prompt Versions",
                        "GET",
                        "/api/v1/admin/prompts/{{promptTemplateId}}/versions",
                        "Version history for template (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Activate Prompt Version",
                        "POST",
                        "/api/v1/admin/prompts/{{promptTemplateId}}/versions/{{promptVersionId}}/activate",
                        "Activate prompt version with transaction safety (Admin).",
                        auth="session",
                        body=raw_body({"reason": "Promoting after evaluation"}),
                    ),
                    endpoint(
                        "Evaluate Prompt Version",
                        "POST",
                        "/api/v1/admin/prompts/{{promptTemplateId}}/versions/{{promptVersionId}}/evaluate",
                        "Run automated evaluation dataset (Admin).",
                        auth="session",
                        body=raw_body({
                            "datasetPath": "datasets/rules_eval.jsonl",
                            "storeResults": True,
                        }),
                    ),
                    endpoint(
                        "Compare Prompt Versions",
                        "POST",
                        "/api/v1/admin/prompts/{{promptTemplateId}}/compare",
                        "A/B compare baseline vs candidate prompts (Admin).",
                        auth="session",
                        body=raw_body({
                            "baselineVersionId": "{{baselineVersionId}}",
                            "candidateVersionId": "{{candidateVersionId}}",
                            "datasetPath": "datasets/rules_eval.jsonl",
                        }),
                    ),
                    endpoint(
                        "Evaluation History",
                        "GET",
                        "/api/v1/admin/prompts/{{promptTemplateId}}/evaluations",
                        "List stored evaluation runs (Admin).",
                        auth="session",
                        params=[qp("limit", "20")],
                    ),
                    endpoint(
                        "Evaluation Report",
                        "GET",
                        "/api/v1/admin/prompts/evaluations/{{evaluationId}}/report",
                        "Download evaluation report as Markdown or JSON (Admin).",
                        auth="session",
                        params=[qp("format", "markdown")],
                    ),
                    endpoint(
                        "Prompt Audit Log",
                        "GET",
                        "/api/v1/admin/prompts/{{promptTemplateId}}/audit-log",
                        "Retrieve audit log entries for template (Admin).",
                        auth="session",
                        params=[qp("limit", "20")],
                    ),
                ],
            ),
            folder(
                "Prompt API",
                "Prompt management service endpoints including active version lookup.",
                requests=[
                    endpoint(
                        "List Templates (Admin API)",
                        "GET",
                        "/api/v1/prompts",
                        "List templates using admin management service (Admin).",
                        auth="session",
                        params=[qp("category", "", disabled=True)],
                    ),
                    endpoint(
                        "Get Template (Admin API)",
                        "GET",
                        "/api/v1/prompts/{{promptTemplateId}}",
                        "Get template with versions using prompt management service (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Create Template (Admin API)",
                        "POST",
                        "/api/v1/prompts",
                        "Create prompt template via prompt management service (Admin).",
                        auth="session",
                        body=raw_body({
                            "name": "Another Template",
                            "description": "",
                            "category": "General",
                        }),
                    ),
                    endpoint(
                        "Create Version (Admin API)",
                        "POST",
                        "/api/v1/prompts/{{promptTemplateId}}/versions",
                        "Create version via prompt management service (Admin).",
                        auth="session",
                        body=raw_body({"content": "Prompt content", "metadata": {}}),
                    ),
                    endpoint(
                        "Prompt Version History (Admin API)",
                        "GET",
                        "/api/v1/prompts/{{promptTemplateId}}/versions",
                        "Get version history via prompt management service (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Active Prompt Version",
                        "GET",
                        "/api/v1/prompts/{{promptTemplateId}}/versions/active",
                        "Retrieve active prompt version (Authenticated users).",
                        auth="session",
                    ),
                    endpoint(
                        "Activate Prompt Version (Admin API)",
                        "PUT",
                        "/api/v1/prompts/{{promptTemplateId}}/versions/{{promptVersionId}}/activate",
                        "Activate version via prompt management service (Admin).",
                        auth="session",
                        body=raw_body({"reason": "Rollback request"}),
                    ),
                ],
            ),
            folder(
                "User Management",
                "Admin CRUD operations for platform users and API keys.",
                requests=[
                    endpoint(
                        "List Users",
                        "GET",
                        "/api/v1/admin/users",
                        "List users with pagination and filters (Admin).",
                        auth="session",
                        params=[
                            qp("search", "", disabled=True),
                            qp("role", "", disabled=True),
                            qp("sortBy", "createdAt", disabled=True),
                            qp("sortOrder", "desc"),
                            qp("page", "1"),
                            qp("limit", "20"),
                        ],
                    ),
                    endpoint(
                        "Create User",
                        "POST",
                        "/api/v1/admin/users",
                        "Create user manually (Admin).",
                        auth="session",
                        body=raw_body({
                            "email": "new.user@example.com",
                            "password": "ChangeMe!1",
                            "displayName": "New User",
                            "role": "User",
                        }),
                    ),
                    endpoint(
                        "Update User",
                        "PUT",
                        "/api/v1/admin/users/{{userId}}",
                        "Update user role or metadata (Admin).",
                        auth="session",
                        body=raw_body({
                            "displayName": "Updated Name",
                            "role": "Editor",
                        }),
                    ),
                    endpoint(
                        "Delete User",
                        "DELETE",
                        "/api/v1/admin/users/{{userId}}",
                        "Delete user (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Delete API Key (Admin)",
                        "DELETE",
                        "/api/v1/admin/api-keys/{{apiKeyId}}",
                        "Hard delete API key across all users (Admin).",
                        auth="session",
                    ),
                ],
            ),
            folder(
                "Configuration",
                "System configuration CRUD, validation, and history endpoints.",
                requests=[
                    endpoint(
                        "List Configurations",
                        "GET",
                        "/api/v1/admin/configurations",
                        "List system configuration entries (Admin).",
                        auth="session",
                        params=[
                            qp("category", "", disabled=True),
                            qp("environment", "", disabled=True),
                            qp("activeOnly", "true"),
                            qp("page", "1"),
                            qp("pageSize", "50"),
                        ],
                    ),
                    endpoint(
                        "Get Configuration",
                        "GET",
                        "/api/v1/admin/configurations/{{configId}}",
                        "Fetch configuration by id (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Get Configuration by Key",
                        "GET",
                        "/api/v1/admin/configurations/key/{{configKey}}",
                        "Lookup configuration by key (Admin).",
                        auth="session",
                        params=[qp("environment", "", disabled=True)],
                    ),
                    endpoint(
                        "Create Configuration",
                        "POST",
                        "/api/v1/admin/configurations",
                        "Create configuration value (Admin).",
                        auth="session",
                        body=raw_body({
                            "key": "Features.NewExperiment",
                            "value": "true",
                            "valueType": "bool",
                            "description": "Toggle new experiment",
                            "category": "Features",
                            "isActive": True,
                            "requiresRestart": False,
                            "environment": "All",
                        }),
                    ),
                    endpoint(
                        "Update Configuration",
                        "PUT",
                        "/api/v1/admin/configurations/{{configId}}",
                        "Update configuration entry (Admin).",
                        auth="session",
                        body=raw_body({
                            "value": "false",
                            "valueType": "bool",
                            "description": "Updated toggle",
                        }),
                    ),
                    endpoint(
                        "Delete Configuration",
                        "DELETE",
                        "/api/v1/admin/configurations/{{configId}}",
                        "Delete configuration entry (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Toggle Configuration",
                        "PATCH",
                        "/api/v1/admin/configurations/{{configId}}/toggle",
                        "Toggle active state (Admin).",
                        auth="session",
                        params=[qp("isActive", "true")],
                    ),
                    endpoint(
                        "Bulk Update Configurations",
                        "POST",
                        "/api/v1/admin/configurations/bulk-update",
                        "Update multiple configurations in one call (Admin).",
                        auth="session",
                        body=raw_body({"updates": [{"id": "{{configId}}", "value": "true"}]}),
                    ),
                    endpoint(
                        "Validate Configuration Value",
                        "POST",
                        "/api/v1/admin/configurations/validate",
                        "Validate configuration value prior to saving (Admin).",
                        auth="session",
                        body=raw_body({
                            "key": "Features.NewExperiment",
                            "value": "true",
                            "valueType": "bool",
                        }),
                    ),
                    endpoint(
                        "Export Configurations",
                        "GET",
                        "/api/v1/admin/configurations/export",
                        "Export configuration snapshot (Admin).",
                        auth="session",
                        params=[
                            qp("environment", "All"),
                            qp("activeOnly", "true"),
                        ],
                    ),
                    endpoint(
                        "Import Configurations",
                        "POST",
                        "/api/v1/admin/configurations/import",
                        "Bulk import configuration entries (Admin).",
                        auth="session",
                        body=raw_body({
                            "configurations": [
                                {
                                    "key": "Features.NewExperiment",
                                    "value": "false",
                                    "valueType": "bool",
                                    "description": "",
                                    "category": "Features",
                                    "isActive": True,
                                    "requiresRestart": False,
                                    "environment": "All",
                                }
                            ],
                            "overwriteExisting": True,
                        }),
                    ),
                    endpoint(
                        "Configuration History",
                        "GET",
                        "/api/v1/admin/configurations/{{configId}}/history",
                        "History of configuration changes (Admin).",
                        auth="session",
                        params=[qp("limit", "20")],
                    ),
                    endpoint(
                        "Rollback Configuration",
                        "POST",
                        "/api/v1/admin/configurations/{{configId}}/rollback/{{configVersion}}",
                        "Rollback configuration to previous version (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Configuration Categories",
                        "GET",
                        "/api/v1/admin/configurations/categories",
                        "List configuration categories (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Invalidate Config Cache",
                        "POST",
                        "/api/v1/admin/configurations/cache/invalidate",
                        "Invalidate configuration cache or specific key (Admin).",
                        auth="session",
                        params=[qp("key", "", disabled=True)],
                    ),
                    endpoint(
                        "Feature Flags",
                        "GET",
                        "/api/v1/admin/features",
                        "List all feature flags and roles (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Update Feature Flag",
                        "PUT",
                        "/api/v1/admin/features/{{featureName}}",
                        "Enable/disable feature flag optionally per role (Admin).",
                        auth="session",
                        body=raw_body({"enabled": True, "role": ""}),
                    ),
                ],
            ),
            folder(
                "Cache",
                "Invalidate cached AI responses and view cache statistics.",
                requests=[
                    endpoint(
                        "Cache Stats",
                        "GET",
                        "/api/v1/admin/cache/stats",
                        "Retrieve hybrid cache statistics (Admin).",
                        auth="session",
                        params=[qp("gameId", "", disabled=True)],
                    ),
                    endpoint(
                        "Invalidate Game Cache",
                        "DELETE",
                        "/api/v1/admin/cache/games/{{gameId}}",
                        "Invalidate cached responses for a game (Admin).",
                        auth="session",
                    ),
                    endpoint(
                        "Invalidate Cache by Tag",
                        "DELETE",
                        "/api/v1/admin/cache/tags/{{cacheTag}}",
                        "Invalidate cache entries by tag (Admin).",
                        auth="session",
                    ),
                ],
            ),
        ],
    ),
]

ENVIRONMENT_VALUES = [
    {"key": "baseUrl", "value": "http://localhost:5000", "type": "default"},
    {"key": "sessionCookieName", "value": "meeple_session", "type": "default"},
    {"key": "sessionToken", "value": "", "type": "default"},
    {"key": "apiKey", "value": "", "type": "default"},
    {"key": "email", "value": "user@example.com", "type": "default"},
    {"key": "password", "value": "ChangeMe!1", "type": "default"},
    {"key": "newPassword", "value": "ChangeMe!2", "type": "default"},
    {"key": "displayName", "value": "Meeple User", "type": "default"},
    {"key": "tempSessionToken", "value": "", "type": "default"},
    {"key": "twoFactorCode", "value": "000000", "type": "default"},
    {"key": "passwordResetToken", "value": "", "type": "default"},
    {"key": "oauthProvider", "value": "google", "type": "default"},
    {"key": "oauthCode", "value": "", "type": "default"},
    {"key": "oauthState", "value": "", "type": "default"},
    {"key": "frontendRedirect", "value": "http://localhost:3000/auth/callback", "type": "default"},
    {"key": "gameId", "value": "game-default", "type": "default"},
    {"key": "gameName", "value": "Sample Game", "type": "default"},
    {"key": "agentId", "value": "agent-default", "type": "default"},
    {"key": "chatId", "value": "00000000-0000-0000-0000-000000000000", "type": "default"},
    {"key": "messageId", "value": "00000000-0000-0000-0000-000000000000", "type": "default"},
    {"key": "userId", "value": "00000000-0000-0000-0000-000000000000", "type": "default"},
    {"key": "userSearch", "value": "meeple", "type": "default"},
    {"key": "exportFrom", "value": "", "type": "default"},
    {"key": "exportTo", "value": "", "type": "default"},
    {"key": "bggQuery", "value": "Meeple", "type": "default"},
    {"key": "bggId", "value": "174430", "type": "default"},
    {"key": "chessQuery", "value": "King safety", "type": "default"},
    {"key": "fen", "value": "", "type": "default"},
    {"key": "pdfId", "value": "pdf-default", "type": "default"},
    {"key": "ruleSpecVersion", "value": "v1", "type": "default"},
    {"key": "ruleSpecCommentId", "value": "00000000-0000-0000-0000-000000000000", "type": "default"},
    {"key": "ruleCommentId", "value": "00000000-0000-0000-0000-000000000000", "type": "default"},
    {"key": "ruleAtomId", "value": "atom-1", "type": "default"},
    {"key": "lineNumber", "value": "1", "type": "default"},
    {"key": "authorEmail", "value": "", "type": "default"},
    {"key": "startDate", "value": "", "type": "default"},
    {"key": "endDate", "value": "", "type": "default"},
    {"key": "templateCategory", "value": "", "type": "default"},
    {"key": "n8nTemplateId", "value": "welcome-email", "type": "default"},
    {"key": "n8nApiKey", "value": "", "type": "default"},
    {"key": "n8nConfigId", "value": "config-default", "type": "default"},
    {"key": "sessionId", "value": "session-default", "type": "default"},
    {"key": "workflowId", "value": "workflow-1", "type": "default"},
    {"key": "executionId", "value": "execution-1", "type": "default"},
    {"key": "workflowErrorId", "value": "00000000-0000-0000-0000-000000000000", "type": "default"},
    {"key": "alertType", "value": "MeepleAIHighLatency", "type": "default"},
    {"key": "promptTemplateId", "value": "template-default", "type": "default"},
    {"key": "promptVersionId", "value": "version-default", "type": "default"},
    {"key": "baselineVersionId", "value": "baseline-version", "type": "default"},
    {"key": "candidateVersionId", "value": "candidate-version", "type": "default"},
    {"key": "evaluationId", "value": "evaluation-default", "type": "default"},
    {"key": "configId", "value": "config-default", "type": "default"},
    {"key": "configKey", "value": "Features.NewExperiment", "type": "default"},
    {"key": "configVersion", "value": "1", "type": "default"},
    {"key": "cacheTag", "value": "game:{{gameId}}", "type": "default"},
    {"key": "featureName", "value": "Features.SetupGuideGeneration", "type": "default"},
]


def build_collection() -> Dict[str, Any]:
    collection: Dict[str, Any] = {
        "info": {
            "name": "MeepleAI API",
            "_postman_id": str(uuid.uuid4()),
            "description": (
                "Generated collection covering MeepleAI API endpoints.\n"
                "Update tools/postman/generate_collection.py to keep definitions in sync."
            ),
            "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
        },
        "item": [folder.as_postman() for folder in FOLDERS],
        "event": [
            {
                "listen": "test",
                "script": {
                    "type": "text/javascript",
                    "exec": [
                        "pm.test('Status code is 2xx', function () {",
                        "    pm.expect(pm.response.code).to.be.within(200, 299);",
                        "});",
                    ],
                },
            }
        ],
    }
    return collection


def build_environment() -> Dict[str, Any]:
    return {
        "id": str(uuid.uuid4()),
        "name": "MeepleAI Local",
        "values": [
            {"key": entry["key"], "value": entry["value"], "type": entry.get("type", "default"), "enabled": True}
            for entry in ENVIRONMENT_VALUES
        ],
        "_postman_variable_scope": "environment",
        "_postman_exported_at": json.dumps({"generatedAt": uuid.uuid4().hex})
    }


def main() -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    collection = build_collection()
    environment = build_environment()
    COLLECTION_FILE.write_text(json.dumps(collection, indent=2) + "\n", encoding="utf-8")
    ENVIRONMENT_FILE.write_text(json.dumps(environment, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {COLLECTION_FILE.relative_to(ROOT)}")
    print(f"Wrote {ENVIRONMENT_FILE.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
