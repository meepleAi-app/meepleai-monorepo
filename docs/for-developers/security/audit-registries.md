# Two audit registries: `security_audit_logs` and `audit_logs`

> #3838. The backend keeps **two** audit tables, with different jobs. Someone looking for a security event in the wrong one does not find it, so this page says which is which.

## `security_audit_logs`: the identity and access register

**What goes there:** events that change who can get in, or with which privileges:

- login, both successful and failed (a wrong password and a wrong second factor alike);
- account lockout;
- logout;
- password change and reset, by the user or by an admin;
- 2FA turned on or off, and the admin override that removes it;
- OAuth links and unlinks;
- role changes;
- revocation of several sessions at once;
- creation of the bootstrap admin.

It also holds a few admin actions on the catalog that need evidence (categories, manual covers).

**How an event gets there:** the command handler calls `IAuditLogger.LogAsync` (`BoundedContexts/SecurityAudit/Application/Services`) with a constant of `AuditEventType`. The constant's XML comment says who the actor and the target are and what the metadata contains. Writing the event type inline at the call site is not allowed.

**The source of truth for the vocabulary** is `AuditEventType.cs`. `SecurityAuditEventTypesAreEmittedArchitectureTests` fails when a declared constant has no caller. That is the defect #3838 found: for a long time this table recorded only logins, while the vocabulary already declared password changes, resets, OAuth links, role changes, the 2FA override and bulk revocation.

**What a failure to write means:** `LogAsync` never throws. A failed audit write must not roll back the action or reach the user, so it is logged at `Error`.

**What it does not carry:** an entry has no IP or user agent when the command does not carry them. Login carries them; the other handlers, for now, do not.

## `audit_logs`: the register of administrative actions

**What goes there:** the administrative operations performed through commands decorated with `[AuditableAction]`, such as a role change, an impersonation or an admin configuration. A few writers (`AuditService`, `DatabaseSyncAuditWriter`, the outbox `AuditOutboxProcessor`) write to it directly. This table also has the impersonation columns (`impersonated_user_id`, `step_up_token_id`).

**How an event gets there:** `AuditLoggingBehavior` (MediatR pipeline, `BoundedContexts/Administration/Application/Behaviors`) reads the attribute from the command. The handler does not have to do anything.

## When an action belongs in both

A role change is an administrative operation, and it is also an identity event. It is in **both** tables, and that is on purpose:

- in `audit_logs` because `ChangeUserRoleCommand` carries `[AuditableAction]`;
- in `security_audit_logs` because the handler logs `auth.role.changed`, with the admin as actor and the old and new role in the metadata.

To reconstruct a user's access history, read `security_audit_logs`. To reconstruct what an admin did, read `audit_logs`.
