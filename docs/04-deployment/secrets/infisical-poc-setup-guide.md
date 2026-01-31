# Infisical POC Setup Guide - Issue #936

**Date**: 2025-12-12
**Status**: POC In Progress
**Stack**: Infisical latest-postgres, PostgreSQL 16, Redis 7

---

## Prerequisites Completed ✅

1. **Encryption keys generated**:
   - `INFISICAL_ENCRYPTION_KEY`: 76b40ae6035f049537efb133f807e292
   - `INFISICAL_AUTH_SECRET`: rR+7R8tjf8DaXbHkgCN+camR70Wuh+L1UlAkAyHdOdk=
   - `INFISICAL_DB_PASSWORD`: LP+zCl5ou8ZF84z+KZyAGg==

2. **Configuration file**: `infra/experimental/infisical.env`

3. **Stack running**: http://localhost:8081 (port 8081 to avoid conflict with MeepleAI API on 8080)

---

## Manual UI Configuration Steps

### Step 1: Create Admin Account (First Time)

1. Navigate to http://localhost:8081
2. Click "Create Account" (first-time setup)
3. Fill in:
   - **Name**: Admin User
   - **Email**: admin@meepleai.local
   - **Password**: Strong password (min 8 chars)
4. Verify email (skip if email not configured)
5. Login with credentials

### Step 2: Create Project

1. After login, click "Create Project" or "+" button
2. Fill in:
   - **Project Name**: meepleai-poc
   - **Description**: POC for secret rotation testing (Issue #936)
3. Click "Create"
4. **Save Project ID** (visible in project settings or URL)

### Step 3: Create Machine Identity (for API access)

1. Navigate to **Project Settings** → **Machine Identities**
2. Click "Create Identity"
3. Fill in:
   - **Name**: meepleai-api-client
   - **Description**: C# API client for POC testing
4. Click "Create"
5. Choose **Universal Auth** method
