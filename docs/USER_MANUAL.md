# CivicFlow Enterprise - User Manual

**Document Status:** Based on verified implementation. Some workflows may be limited or require proper configuration.

**Audience:** Citizens, Government Officers, and Administrative users  
**Last Updated:** 2026-10-22

## 1. About This Manual

### Purpose
This manual describes how to use the currently implemented features of CivicFlow Enterprise. It focuses on verified functionality based on the actual application code.

### Application Status
- **Implemented**: Core staff workspace (cases, staff, organizations), first-party authentication with email/password and cookie sessions, portal-scoped sign-in (citizen and organization portals), personal access tokens for API clients, case management operations, citizen self-service profile and citizen dashboard
- **Partially Implemented**: Public/citizen intake interface (submission/tracking depend on configuration)
- **Not Fully Verified in this environment**: Live browser + database end-to-end runs (the code paths are covered by automated integration tests)
- **Limitations**: Some features require proper environment setup and role configuration; verification/reset emails require a configured email sender in production

### Supported Environment
- Modern web browser (Chrome, Firefox, Edge, Safari - recent versions)
- JavaScript enabled
- Network access to the CivicFlow application

## 2. Accessing CivicFlow

### Launching the Application
In a local development environment:
1. Start the backend API and dependencies (see [README.md](../README.md))
2. Start the frontend dev server
3. Navigate to the frontend URL (typically `http://localhost:5173` in development)

### Landing Page
When accessing CivicFlow without an active session, the public landing page (`/`) shows:
- **CivicFlow branding** and description
- **Citizen Portal** - For public service requests and citizen self-service profile, with two buttons: **Sign in** (existing citizen) and **Register** (new citizen account)
- **Organization Portal** - For staff, Organization Administrators, and Super Admins

Platform administration is served at a **dedicated URL** (`/admin`), not on the public landing page. Staff sign in there directly.

### Authentication

CivicFlow has two separate sign-in surfaces, and each one only accepts the account type that belongs to it:

| Portal | Where to sign in | Who can use it |
|---|---|---|
| **Citizen Portal** | `/citizen/login` (or the Citizen Portal on the landing page) | Citizen accounts |
| **Organization Portal** | `/admin` | Staff, Organization Administrators, and Super Admins |

If you use the wrong portal, the sign-in is refused with a message such as *"This account is not a citizen account. Sign in through the organization portal instead."* Nothing is signed in when this happens, so you can immediately try the correct portal.

#### For Government Officers (Staff, Organization Admin, Super Admin)
1. Navigate to the dedicated staff sign-in URL: `https://<your-civicflow-host>/admin`
2. You'll see the **staff sign-in form** with email and password fields
3. Enter your email and password and submit
4. After successful authentication, you'll be taken to the organization workspace
5. Authentication and authorization are enforced server-side by the API

If your email address has not been confirmed yet, sign-in is rejected with a message offering **Resend the verification email**. If the credentials are wrong, the form reports **Invalid email or password** without revealing whether the account exists.

#### For Citizens
1. On the landing page (`/`), choose **Sign in** (existing account) or **Register** (new account) in the **Citizen Portal**
2. **Register**: provide your email and a password (minimum 12 characters with upper, lower, digit, and non-alphanumeric characters); a verification email is sent
3. Confirm your email using the verification link, then sign in
4. After signing in, you land on your **citizen dashboard**, which shows your account status and the actions available to you
5. Open **My profile** to create or update your profile
6. Required fields: **Full Name** and **Email**. Optional fields include phone number, NID number, citizenship number, age, gender, and permanent/temporary addresses
7. The profile is stored against your authenticated identity; NID and citizenship numbers must be unique when provided

**Citizen route shortcuts:** `/citizen/login` and `/citizen/register` open the citizen flows directly.

#### Password Recovery
1. On the citizen or staff sign-in page, choose **Forgot password**
2. Enter your email; a reset link is sent if the account exists
3. Open the reset link and choose a new password

#### Logging Out
1. In the workspace, click **"Sign out"** in the top bar
2. You'll be returned to the landing page

**Note:** Authentication is server-authoritative using an HttpOnly session cookie with CSRF protection. No authentication tokens are stored in the browser's JavaScript-accessible storage.

## 3. Understanding the Interface

### Workspace Layout (Authenticated Staff)
The staff workspace consists of:
- **Sidebar** (left): Branding, workspace info, navigation menu, footer with user info and "Back to landing" option
- **Top bar**: Breadcrumb navigation, auth status, sign in/out button
- **Main content**: View-specific content

### Navigation Menu
The navigation only shows the views your account is entitled to. Entries are determined by the roles the server reports for your session, so a citizen never sees organization surfaces and a staff member never sees the citizen dashboard.

**Citizen accounts (citizen portal):**
- **Dashboard** - Citizen dashboard with your account status and available actions (default view)
- **Public intake** - Submit a public service request
- **My profile** - Create or update your own citizen profile
- **Access tokens** - Create, list, and revoke personal access tokens for API clients

**Staff and Super Admin accounts (organization portal):**
- **Cases** - Case management (default view)
- **Staff & team** - Staff directory management
- **Organizations** - Organization and membership administration (Super Admin only)
- **Access tokens** - Create, list, and revoke personal access tokens for API clients

### Common Controls
- Buttons with clear labels
- Forms with validation
- Tables with sorting/filtering (where implemented)
- Toast notifications for feedback (success/error/info/warning)
- Loading states

## 4. Role-Specific Instructions

### Government Officer / Staff User
**Access requirements:** Authenticated staff account with the `staff` (or `superadmin`) role; must have appropriate organization membership/role as enforced by the API.

**Available actions:**
- View and manage cases
- Update case status
- Assign cases to staff members
- Upload and download case documents
- View case history and assignment history
- Manage staff directory (create/deactivate/reactivate)
- Manage organizations and memberships (if authorized)

### Citizen User
**Status:** Citizens sign in through the **Citizen Portal** and stay inside the citizen workspace: a citizen dashboard, the public intake interface, their own profile, and access tokens for their own account. Case management, staff administration, and organization administration are never part of a citizen session. Public submission/tracking workflows still depend on configuration — check with your system administrator for current capabilities.

**Citizen Dashboard workflow:**
1. Sign in through the **Citizen Portal**
2. The dashboard opens and shows your account status, the portal you are signed in to, and the available actions
3. Use the action cards to open **Public intake**, **My profile**, or **Access tokens**

**Citizen Profile workflow:**
1. Sign in through the **Citizen Portal**
2. Open **My profile** in the navigation (or the dashboard action card)
3. Provide **Full Name** and **Email** (required); optionally add phone number, NID number, citizenship number, age, gender, and permanent/temporary addresses
4. Submit to create the profile; submitting again updates the existing profile
5. NID number and citizenship number must be unique across profiles when provided; the server rejects duplicates

### Administrator Functions
Organization and membership administration are available in the "Organizations" view for authorized users. Specific permissions are enforced server-side by the API based on roles and organization memberships.

## 5. Core Workflows

### 5.1 Viewing and Managing Cases
1. Navigate to **Cases** in the sidebar (default view)
2. Use the search box to find cases by reference number or title
3. Use the status filter dropdown to filter by case status
4. Click on a case in the table to view details in the right panel
5. View case information, status history, assignment history, and documents

### 5.2 Creating a Case
1. Go to **Cases** view
2. Click **"+ New case"** button
3. Fill in **Title** (required, 3-200 characters) and **Description** (required, 10-5000 characters)
4. Click **"Create case"**
5. You'll receive a toast notification on success/failure
6. The new case will appear in the list

### 5.3 Updating Case Status
1. Select a case from the cases list
2. In the case details panel, use the status dropdown
3. Select the new status
4. The status will update (demo cases may have restrictions)
5. Status history will be refreshed

### 5.4 Assigning a Case
1. Select a case from the cases list
2. In the case details panel, use the assignment controls
3. Select a staff member from the dropdown
4. Assignment will be updated with audit tracking
5. Assignment history will reflect the change

### 5.5 Managing Documents
**Uploading:**
1. Select a case
2. In the documents section, use the file upload control
3. Select a file to upload
4. Upload will proceed if permitted (demo cases may be restricted)

**Downloading:**
1. Select a case with documents
2. Click on a document name/link to download
3. The file will download through your browser

### 5.6 Managing Staff
1. Navigate to **Staff & team**
2. Use the form to add new staff members (full name and email)
3. Toggle staff member active/inactive status using the action buttons
4. Changes require appropriate permissions

### 5.7 Managing Organizations
1. Navigate to **Organizations**
2. Create organizations or select an existing organization
3. Manage memberships for the selected organization
4. Add members with specific roles
5. Update membership roles or activate/deactivate memberships

### 5.8 Managing Your Citizen Profile
1. Sign in through the **Citizen Portal**
2. Navigate to **My profile** (from the dashboard action card or the sidebar)
3. If you are not signed in, you will be prompted to **Sign in**
4. Fill in the required fields (**Full Name**, **Email**) and any optional details
5. Submit to create or update your profile
6. A confirmation message appears on success; validation or duplicate errors are shown in red

**Note:** The server stores the profile against your authenticated identity. It does not accept a client-supplied user ID, and duplicate NID/citizenship numbers are rejected.

### 5.9 Managing Personal Access Tokens
Personal access tokens let an automated client (a script, an integration, a mobile app) call the API without a browser session. A token acts on behalf of the user who created it and carries that user's permissions.

1. Sign in to CivicFlow (citizen or staff sign-in page)
2. Open **Access tokens** in the navigation
3. Enter a **name** that identifies the client or purpose (for example `nightly-sync`)
4. Optionally choose an **expiry** (30 days, 90 days, 1 year, or never)
5. Select **Create token**
6. **Copy the token immediately** — it is shown exactly once and cannot be retrieved later
7. Store it in the client's secret store; the server keeps only a hash of it

**Using the token**
Send it as an HTTP bearer credential on every request:

```
Authorization: Bearer civicflow_pat_<publicId>_<secret>
```

**Revoking a token**
1. Open **Access tokens**
2. Find the token in the list and select **Revoke**
3. The token stops working immediately — requests using it receive `401`

**Notes**
- A token is limited to the permissions of the account that created it; a citizen token cannot reach staff endpoints.
- At most 25 active tokens can exist per account. Revoke tokens you no longer use.
- Revoking a token does not end your browser session and does not affect other tokens.
- Tokens must be treated as passwords: anyone holding one can act as you until it is revoked or expires.

## 6. Common Errors and Troubleshooting

| Error | Possible Cause | Recovery |
|---|---|---|
| "Configure an organization ID to load cases" | Missing `VITE_CIVICFLOW_ORGANIZATION_ID` | Configure environment variables (see README) |
| "Invalid email or password" | Wrong credentials | Retry; use **Forgot password** if needed |
| "This account is not a citizen account" | Signed in through the Organization Portal | Sign in through the **Citizen Portal** (`/citizen/login`) |
| "This account is not an organization account" | Signed in through the Citizen Portal | Sign in through the **Organization Portal** (`/admin`) |
| "Organization workspace unavailable" | A citizen session opened `/admin` | Use **Go to my dashboard**, or sign in through `/admin` with a staff account |
| "Please verify your email to sign in" | Email not confirmed | Open the verification link from your email (or resend it) |
| "Too many attempts. Try again later." | Account temporarily locked | Wait ~15 minutes and retry |
| "You have reached the maximum number of active access tokens" | 25 active tokens already exist | Revoke an unused token, then create a new one |
| "Access token name is required" | Blank token name | Enter a name identifying the client or purpose |
| API returns `401` for a token that used to work | Token was revoked, expired, or the account was deactivated | Create a new token, or ask the administrator to check the account |
| API returns `403` for a token | The token's owner lacks the required role | Use an account with the required role |
| "Sign in to load organization cases" | Not authenticated | Sign in via the staff or citizen sign-in page |
| "Your role may not permit this action" | Insufficient permissions | Contact administrator for appropriate role |
| Session expired / unexpected sign-out | Cookie expired or revoked | Sign in again |
| "Your current session is not authorized for citizen profile access" | Signed in as a staff account | Sign in through the Citizen Portal |
| "A citizen profile with this NID number already exists" | Duplicate NID/citizenship/email | Use your own details or contact your administrator |
| CSRF error on save | Anti-forgery token missing/expired | Refresh the page and retry |
| Case creation/update fails | Network/API issues or permissions | Check connection and permissions; retry |

**Tips:**
- Watch for toast notifications - they provide real-time feedback
- Check the browser console for additional error details in development
- Ensure all required environment variables are configured
- Verification and password-reset emails are logged by the API in local development (see README)
- A personal access token is displayed only once, at creation; if you lose it, revoke it and create a new one

## 7. Security and Privacy

### Safe Usage
- **Always sign out** when finished, especially on shared devices
- **Keep credentials secure** - never share login information
- **Verify URLs** - only access CivicFlow through trusted URLs
- **Be cautious with documents** - only upload/download appropriate files

### Data Handling
- Cases and documents are organization-scoped
- Access is controlled by authentication and authorization
- Server-side validation enforces permissions
- Sessions use HttpOnly cookies with CSRF protection and are revoked on sign-out
- Personal access tokens are stored only as a SHA-256 hash; the plaintext exists only in the response that created it and in the caller's copy

**Note:** No absolute security guarantees can be made - follow your organization's security policies.

## 8. Known Limitations

### Current Limitations
- **Email delivery**: Verification and password-reset emails require a configured email sender in production (local development logs them)
- **Citizen portal scope**: The citizen portal is self-service only (dashboard, public intake, own profile, access tokens); public submission/tracking workflows may be limited
- **Environment dependent**: Many features require proper backend/frontend configuration
- **No offline support**: Requires network connectivity
- **File restrictions**: Document upload/download may have type/size limitations based on API configuration
- **Role enforcement**: UI may show options that API will reject if permissions insufficient

### Not Implemented
- Advanced reporting and analytics
- Real-time notifications (beyond toasts)
- Bulk operations
- Mobile app
- Advanced search capabilities
- Email/SMS notifications
- Workflow automation

## 9. Glossary

- **Case**: A service request or issue tracked through the system
- **Organization**: A government department/agency using CivicFlow
- **Membership**: Association between a user and an organization with a specific role
- **OIDC/Identity**: CivicFlow uses first-party ASP.NET Core Identity with cookie sessions
- **Session Cookie**: HttpOnly cookie identifying the signed-in user (not readable by JavaScript)
- **CSRF**: Cross-Site Request Forgery protection using an anti-forgery token (`X-CSRF-TOKEN` header)
- **Personal Access Token (PAT)**: A long-lived credential for API clients, sent as `Authorization: Bearer civicflow_pat_…`; it carries the creating user's permissions and can be revoked at any time
- **Role**: Account permission level (`citizen`, `staff`, or `superadmin`)
- **Portal**: The sign-in surface an account is bound to — the Citizen Portal for citizen accounts, the Organization Portal for staff and super administrators. An account can only sign in through the portal its roles belong to.
- **Assignment**: Designating a staff member to handle a case
- **Status History**: Audit trail of case status changes
- **Assignment History**: Audit trail of case assignments

## Support

For technical issues, check the [README](../README.md) for setup instructions or contact your system administrator. For questions about workflows, refer to the relevant sections of this manual.
