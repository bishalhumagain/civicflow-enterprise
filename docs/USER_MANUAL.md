# CivicFlow Enterprise - User Manual

**Document Status:** Based on verified implementation. Some workflows may be limited or require proper configuration.

**Audience:** Citizens, Government Officers, and Administrative users  
**Last Updated:** 2026-10-21

## 1. About This Manual

### Purpose
This manual describes how to use the currently implemented features of CivicFlow Enterprise. It focuses on verified functionality based on the actual application code.

### Application Status
- **Implemented**: Core staff workspace (cases, staff, organizations), authentication framework, case management operations
- **Partially Implemented**: Public/citizen intake interface
- **Not Fully Verified**: End-to-end authentication flows depend on Keycloak configuration
- **Limitations**: Some features require proper environment setup and role configuration

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
When accessing CivicFlow without an active session, you'll see the landing page with:
- **CivicFlow branding** and description
- **"I'm a Citizen"** button - For public service requests
- **"I'm a Government Officer"** button - For staff access

### Authentication

#### For Government Officers (Staff)
1. Click **"I'm a Government Officer"** on the landing page
2. You'll see the **in-app login form** with username/email and password fields
3. Click **"Sign in"** - If OIDC is configured, you'll be redirected to Keycloak for secure authentication
4. After successful authentication, you'll be redirected back to the workspace
5. Authentication and authorization are enforced server-side by the API

#### For Citizens
1. Click **"I'm a Citizen"** on the landing page
2. This indicates your journey intent (authentication/authorization handled according to system configuration)
3. Public intake interface may be accessible depending on configuration

#### Logging Out
1. In the workspace, click **"Sign out"** in the top bar
2. You'll be returned to the landing page

**Note:** Authentication is server-authoritative via Keycloak OIDC. The in-app login form initiates the secure Authorization Code flow when OIDC is configured.

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

### Common Controls
- Buttons with clear labels
- Forms with validation
- Tables with sorting/filtering (where implemented)
- Toast notifications for feedback (success/error/info/warning)
- Loading states

## 4. Role-Specific Instructions

### Government Officer / Staff User
**Access requirements:** Authenticated via Keycloak with `azp` claim for staff client; must have appropriate organization membership/role as enforced by the API.

**Available actions:**
- View and manage cases
- Update case status
- Assign cases to staff members
- Upload and download case documents
- View case history and assignment history
- Manage staff directory (create/deactivate/reactivate)
- Manage organizations and memberships (if authorized)

### Citizen User
**Status:** Public intake interface exists but full end-to-end submission/tracking workflows depend on configuration and authentication setup. Check with your system administrator for current capabilities.

### Administrator Functions
Organization and membership administration are available in the "Organizations" view for authorized users. Specific permissions are enforced server-side by the API based on roles and claims.

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

## 6. Common Errors and Troubleshooting

| Error | Possible Cause | Recovery |
|---|---|---|
| "Configure an organization ID to load cases" | Missing `VITE_CIVICFLOW_ORGANIZATION_ID` | Configure environment variables (see README) |
| "Configure the OpenID Connect settings to sign in" | Missing Keycloak/OIDC config | Set `VITE_KEYCLOAK_*` environment variables |
| "Authentication not configured" | OIDC not set up | Contact administrator or configure Keycloak |
| "Sign in to load organization cases" | Not authenticated | Sign in via Government Officer flow |
| "Your role may not permit this action" | Insufficient permissions | Contact administrator for appropriate role |
| "Session expired" | Token expired | Sign out and sign in again |
| Case creation/update fails | Network/API issues or permissions | Check connection and permissions; retry |

**Tips:**
- Watch for toast notifications - they provide real-time feedback
- Check the browser console for additional error details in development
- Ensure all required environment variables are configured
- Verify Keycloak is running and properly configured if using auth

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
- Session management follows OIDC best practices

**Note:** No absolute security guarantees can be made - follow your organization's security policies.

## 8. Known Limitations

### Current Limitations
- **Authentication setup required**: Full OIDC/Keycloak configuration needed for production-like auth
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
- **OIDC**: OpenID Connect - authentication protocol used with Keycloak
- **Keycloak**: Identity and access management solution
- **JWT**: JSON Web Token - used for API authentication
- **azp**: Authorized party claim in JWT tokens
- **Assignment**: Designating a staff member to handle a case
- **Status History**: Audit trail of case status changes
- **Assignment History**: Audit trail of case assignments

## Support

For technical issues, check the [README](../README.md) for setup instructions or contact your system administrator. For questions about workflows, refer to the relevant sections of this manual.
