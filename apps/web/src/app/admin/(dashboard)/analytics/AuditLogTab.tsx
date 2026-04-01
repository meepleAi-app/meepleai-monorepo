/**
 * AuditLogTab — re-exports the full-featured AuditTab from Operations Console.
 * The Operations AuditTab has filters, date range, pagination, and JSON/CSV export.
 * This avoids maintaining two implementations of the same feature.
 */
export { AuditTab as AuditLogTab } from '../monitor/operations/AuditTab';
