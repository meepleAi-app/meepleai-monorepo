# Troubleshooting Guide

## PDF Processing Issues

### PDF File Too Large Error

**Symptom**: Error message `"PDF size (X MB) exceeds maximum allowed (Y MB)"`

**Cause**: The PDF file size exceeds the configured maximum file size limit. This is a defense-in-depth security measure implemented at the orchestrator level (BGAI-088).

**Default Limit**: 100 MB (104,857,600 bytes)

**Solution**:

1. **Reduce PDF file size**:
   - Compress images within the PDF
   - Remove unnecessary pages
   - Use PDF optimization tools

2. **Increase the limit** (if authorized):

   Update `appsettings.json` or environment configuration:

   ```json
   {
     "PdfProcessing": {
       "MaxFileSizeBytes": 209715200  // 200 MB (200 * 1024 * 1024)
     }
   }
   ```

3. **Split large PDFs**:
   - Process PDFs in smaller chunks
   - Split multi-rulebook PDFs into individual games

**Security Considerations**:
- Increasing the limit may expose the system to memory exhaustion attacks
- Monitor memory usage if increasing the limit above 100 MB
- Consider infrastructure capacity before changing limits

**Validation Layers**:
This error can occur at two levels:
1. **HTTP layer** (PdfEndpoints.cs) - First line of defense
2. **Orchestrator layer** (EnhancedPdfProcessingOrchestrator.cs) - Defense in depth (BGAI-088)

**Related Issues**: #1113 (BGAI-088)

---

## Related Documentation
- [PDF Processing Architecture](../01-architecture/adr/adr-003-pdf-processing.md)
- [Security Guidelines](../06-security/security-guidelines.md)
