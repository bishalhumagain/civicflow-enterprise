# CivicFlow Enterprise - User Manual

**Document Status:** Based on verified implementation. Some workflows may be limited or require proper configuration.

**Audience:** Citizens, Government Officers, and Administrative users  
**Last Updated:** 2026-10-21

## 1. About This Manual

### Purpose
This manual describes how to use the currently implemented features of CivicFlow Enterprise. It focuses on verified functionality based on the actual application code.

### Application Status
- **Implemented**: Core staff workspace (cases, staff, organizations), first-party authentication with email/password and cookie sessions, case management operations, citizen self-service profile
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

#### For Government Officers (Staff, Organization Admin, Super Admin)
1. Navigate to the dedicated staff sign-in URL: `https://<your-civicflow-host>/admin`
2. You'll see the **staff sign-in form** with email and password fields
3. Enter your email and password and submit
4. After successful authentication, you'll be taken to the workspace
5. Authentication and authorization are enforced server-side by the API

#### For Citizens
1. On the landing page (`/`), choose **Sign in** (existing account) or **Register** (new account) in the **Citizen Portal**
2. **Register**: provide your email and a password (minimum 12 characters with upper, lower, digit, and non-alphanumeric characters); a verification email is sent
3. Confirm your email using the verification link, then sign in
4. After signing in, open **Citizen profile** in the navigation to create or update your profile
5. Required fields: **Full Name** and **Email**. Optional fields include phone number, NID number, citizenship number, age, gender, and permanent/temporary addresses
6. The profile is stored against your authenticated identity; NID and citizenship numbers must be unique when provided

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
Available views (as implemented):
- **Cases** - Case management (default view)
- **Staff & team** - Staff directory management
- **Organizations** - Organization and membership administration
- **Public intake** - Citizen intake interface
- **Citizen profile** - Create/update your own citizen profile (requires citizen authentication)

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
**Status:** Citizens can sign in through the **Citizen Portal** and maintain a self-service profile. Public intake interface exists, but full end-to-end submission/tracking workflows depend on configuration and authentication setup. Check with your system administrator for current capabilities.

**Citizen Profile workflow:**
1. Sign in through the **Citizen Portal**
2. Open **Citizen profile** in the navigation
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
2. Navigate to **Citizen profile**
3. If you are not signed in, you will be prompted to **Sign in**
4. Fill in the required fields (**Full Name**, **Email**) and any optional details
5. Submit to create or update your profile
6. A confirmation message appears on success; validation or duplicate errors are shown in red

**Note:** The server stores the profile against your authenticated identity. It does not accept a client-supplied user ID, and duplicate NID/citizenship numbers are rejected.

## 6. Common Errors and Troubleshooting

| Error | Possible Cause | Recovery |
|---|---|---|
| "Configure an organization ID to load cases" | Missing `VITE_CIVICFLOW_ORGANIZATION_ID` | Configure environment variables (see README) |
| "Invalid email or password" | Wrong credentials | Retry; use **Forgot password** if needed |
| "Please verify your email to sign in" | Email not confirmed | Open the verification link from your email (or resend it) |
| "Too many attempts. Try again later." | Account temporarily locked | Wait ~15 minutes and retry |
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

**Note:** No absolute security guarantees can be made - follow your organization's security policies.

## 8. Known Limitations

### Current Limitations
- **Email delivery**: Verification and password-reset emails require a configured email sender in production (local development logs them)
- **Citizen portal incomplete**: Public submission/tracking workflows may be limited
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
- **Role**: Account permission level (`citizen`, `staff`, or `superadmin`)
- **Assignment**: Designating a staff member to handle a case
- **Status History**: Audit trail of case status changes
- **Assignment History**: Audit trail of case assignments

## Support

For technical issues, check the [README](../README.md) for setup instructions or contact your system administrator. For questions about workflows, refer to the relevant sections of this manual.
