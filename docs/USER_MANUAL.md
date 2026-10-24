# CivicFlow Enterprise — User Manual

**Document Status:** Based on verified implementation. Some workflows may be limited or require proper configuration. See the [API authorization matrix](API-AUTHORIZATION.md) for exact server-enforced permissions.

**Audience:** Citizens, Government Officers (Staff), Organization Administrators, and Platform Super Administrators

**Last Updated:** 2026-10-23

---

## Table of Contents

- [1. About This Manual](#1-about-this-manual)
- [2. Accessing CivicFlow](#2-accessing-civicflow)
- [3. Understanding the Interface](#3-understanding-the-interface)
- [4. Role-Specific Instructions](#4-role-specific-instructions)
- [5. Core Workflows](#5-core-workflows)
- [6. Common Errors and Troubleshooting](#6-common-errors-and-troubleshooting)
- [7. Security and Privacy](#7-security-and-privacy)
- [8. Known Limitations](#8-known-limitations)
- [9. Glossary](#9-glossary)
- [Support](#support)

---

## 1. About This Manual

### Purpose

This manual describes how to use the currently implemented features of CivicFlow Enterprise. It focuses on verified functionality based on the actual application code and integration tests. For developer setup and architecture, see the [README](../README.md).

### Application Status

| Status | Features |
|---|---|
| **Implemented** | Staff workspace (cases, staff, organizations), first-party authentication with email/password and cookie sessions, portal-scoped sign-in (citizen, organization, and platform-admin portals), personal access tokens, case management operations, citizen self-service profile and dashboard, unified self-service profile for citizens and staff, persisted audit trail (platform-wide and organization-scoped), platform directory, organization management (including deletion), dashboard statistics (platform and organization), application error tracking, optional staff/invitation phone numbers, persisted in-app notifications, and a PostgreSQL-backed email job queue |
| **Implemented** | Authenticated citizen case submission with active-organization selection and optional document upload, plus citizen case tracking (each submitted request's status and a detail view with full status history) |
| **Not Fully Verified** | Live browser + database end-to-end runs (code paths are covered by automated integration tests) |
| **Limitations** | Some features require proper environment setup and role configuration; verification/reset/invitation emails require SMTP outside Development |

### Supported Environment

- Modern web browser (Chrome, Firefox, Edge, Safari — recent versions)
- JavaScript enabled
- Network access to the CivicFlow application

---

## 2. Accessing CivicFlow

### Launching the Application

In a local development environment:
1. Start the backend API and dependencies (see [README](../README.md) → Getting Started)
2. Start the frontend dev server
3. Navigate to the frontend URL (typically `http://localhost:5173` in development)

### Landing Page

When accessing CivicFlow without an active session, the public landing page (`/`) shows:
- **CivicFlow branding** and description
- **Citizen Portal** — For public service requests and citizen self-service profile, with two buttons: **Sign in** (existing citizen) and **Register** (new citizen account)
- **Organization Portal** — For staff and Organization Administrators

Platform administration is served at a **dedicated URL** (`/super-platform-admin`), intentionally not linked from the public landing page.

### Authentication

CivicFlow has three separate sign-in surfaces, and each one only accepts the account type that belongs to it:

| Portal | Where to sign in | Who can use it |
|---|---|---|
| **Citizen Portal** | `/citizen/login` (or the Citizen Portal on the landing page) | Citizen accounts |
| **Organization Portal** | `/admin` | Staff and Organization Administrators |
| **Platform Administration** | `/super-platform-admin` | Platform super administrators only |

If you use the wrong portal, the sign-in is refused with a message such as *"This account is not a citizen account…"* or *"This account is not an organization account…"* (a super administrator at `/admin` is told to use the platform portal). Nothing is signed in when this happens, so you can immediately try the correct portal.

#### For Government Officers (Staff and Organization Admin)

1. Navigate to the dedicated staff sign-in URL: `https://<your-civicflow-host>/admin`
2. You'll see the **staff sign-in form** with email and password fields
3. Enter your email and password and submit
4. After successful authentication, you'll be taken to the organization workspace
5. Authentication and authorization are enforced server-side by the API

If your email address has not been confirmed yet, open **Forgot password** and choose **Resend verification email** after entering your account email. Verification resend is available there rather than on the sign-in form. If the credentials are wrong, the form reports **Invalid email or password** without revealing whether the account exists.

#### For Platform Super Administrators

1. Navigate to `https://<your-civicflow-host>/super-platform-admin` (this page is intentionally not linked from the public site)
2. Sign in with your platform administrator email and password
3. You land in **Platform administration**, where you can manage organizations, memberships, invitations, view the directory, audit log, error tracker, dashboard, and your access tokens

A platform administrator cannot use `/admin` or the citizen portal with those same credentials; each portal is bound to its role.

#### Joining an Organization (Invited Staff)

Staff do not receive a temporary password. An administrator sends an invitation, and the invitee chooses their own password:

1. Open the **single-use link** in the invitation email. It looks like `/setup-password?token=…`
2. Review the inviting organization, your name, email, role, and expiry date
3. Choose a password (minimum 12 characters with upper case, lower case, a number, and a symbol) and confirm it
4. After the account is created, sign in at `/admin` with that password

Invitations expire (72 hours by default) and stop working after the first successful use. If the link is invalid, expired, revoked, or already used, the page says so and asks you to contact the administrator for a new invitation. Resending an invitation invalidates the previous link.

#### For Citizens

1. On the landing page (`/`), choose **Sign in** (existing account) or **Register** (new account) in the **Citizen Portal**
2. **Register**: provide your email and a password (minimum 12 characters with upper, lower, digit, and non-alphanumeric characters); a verification email is sent
3. Confirm your email using the verification link, then sign in. If you need another verification email, open **Forgot password**, enter the account email, and choose **Resend verification email**. In Development the one-time link appears in the API console; otherwise it is emailed.
4. After signing in, you land on your **citizen dashboard**, which shows your account status and the actions available to you
5. Open **My profile** to create or update your profile
6. Required fields: **Full Name** and **Email**. Optional fields include phone number, NID number, citizenship number, age, gender, and permanent/temporary addresses
7. The profile is stored against your authenticated identity; NID and citizenship numbers must be unique when provided

**Citizen route shortcuts:** `/citizen/login` and `/citizen/register` open the citizen flows directly.

#### Password Recovery

1. On the citizen or staff sign-in page, choose **Forgot password**
2. Enter your email and choose **Send recovery link**; a reset link is sent if the account exists
3. If the account needs email confirmation, choose **Resend verification email** on this same page
4. Open the recovery link and choose a new password

#### Logging Out

1. In the workspace, click **"Sign out"** in the top bar
2. You'll be returned to the landing page

**Note:** Authentication is server-authoritative using an HttpOnly session cookie with CSRF protection. A session expires after 30 minutes without activity; active use renews the cookie. No authentication tokens are stored in the browser's JavaScript-accessible storage.

Passwords for newly registered accounts and invited staff are stored as ASP.NET Core Identity password hashes, never as plaintext. Accounts migrated from the former external identity provider have no local password hash because CivicFlow cannot retrieve that provider's password; use **Forgot password** to set a new CivicFlow password before signing in.

---

## 3. Understanding the Interface

### Workspace Layout

The workspace consists of:
- **Navigation**: Role-scoped views. On smaller screens, navigation is a horizontally scrollable row above the workspace.
- **Top bar**: Current workspace and page breadcrumb, notifications, and sign-out action
- **Main content**: View-specific content with a keyboard-accessible "Skip to main content" link

### Navigation Menu

The navigation only shows the views your account is entitled to. Entries are determined by the roles the server reports for your session, so a citizen never sees organization surfaces and a staff member never sees the citizen dashboard.

Navigation and action icons use inline SVGs without a third-party icon service. Where an icon or control has a tooltip, it appears on hover and keyboard focus; visible text labels remain available for primary actions. Dropdowns retain native keyboard and screen-reader behavior with consistent application styling.

**Citizen accounts (citizen portal):**

| Navigation entry | Description |
|---|---|
| **Dashboard** | Citizen dashboard with account status and available actions (default view) |
| **Public intake** | Submit a public service request |
| **My profile** | Create or update your own citizen profile |
| **Access tokens** | Create, list, and revoke personal access tokens for API clients |
| **Profile** | Unified self-service profile (civil registry details for citizens, contact details for staff) |

**Staff accounts (organization portal):**

| Navigation entry | Description |
|---|---|
| **Cases** | Case management (default view) |
| **Team contacts** | Staff contact directory and staff phone numbers |
| **Organizations** | Organization administrators see the *Members & invitations* tab for organizations they administer (role changes, activation, invitations); the *Organization types* and *Organizations* tabs, along with organization CRUD, remain SuperAdmin-only |
| **Activity log** | Organization administrators see the organization's audit trail; staff see only their own activity |
| **Access tokens** | Create, list, and revoke personal access tokens for API clients |
| **Profile** | Unified self-service profile (contact details for staff) |

**Platform administrator accounts (platform administration):**

| Navigation entry | Description |
|---|---|
| **Organizations** | Three inner tabs — *Organization types* (shared catalogue CRUD, SuperAdmin only), *Organizations* (organization CRUD), and *Members & invitations* (with an organization-type + organization filter). Default view. |
| **Directory** | Platform overview counters plus Citizens, Staff, and All Users sections; account edits, platform-role changes, and links to organization membership management |
| **Audit log** | Platform-wide audit trail |
| **Errors** | Application error list with filter (all/unhandled/handled) and mark-as-handled |
| **Dashboard** | Platform account and organization totals plus case statistics, status, priority, and weekly/monthly activity charts |
| **Access tokens** | Create, list, and revoke personal access tokens for API clients |
| **Profile** | Unified self-service profile (contact details for staff) |

### Common Controls

- Buttons with clear labels
- Forms with validation
- Tables with sorting/filtering (where implemented)
- Toast notifications for feedback (success/error/info/warning)
- A persisted notification panel with unread count, paging, read state, and minute-level refresh
- Loading states

### Notifications

The Notifications button in the workspace header shows your unread count and a paginated list of your own notifications. The count refreshes approximately once per minute. Use “Mark read” for an individual item or “Mark all read.” Case submission, assignment, and status updates are currently supported; a status change notifies both the case submitter and the staff member assigned to the case (CaseWorker or Viewer). Staff with organization access can use “Open case” to switch to the normal case workspace; its organization and assignment checks remain authoritative. Citizen notifications do not link into the staff case workspace because citizens track their requests from their own dashboard instead. The notification list does not grant access to a case.

---

## 4. Role-Specific Instructions

### Government Officer / Staff User

**Access requirements:** Authenticated staff account with the `staff` role; must have appropriate organization membership/role as enforced by the API.

**Available actions:**
- View case details and history. Organization administrators see all cases in the selected organization; staff see only cases assigned to them.
- CaseWorker and OrganizationAdmin users can create cases. A CaseWorker-created case is assigned to its creator.
- Organization administrators assign cases to active staff in the same organization with a per-case CaseWorker or Viewer role. CaseWorker assignees may change status and upload case documents; Viewer assignees have read-only access. A citizen-submitted case is routed to an organization administrator automatically: assigned to an admin staff member when one exists (who receives an assignment notification), otherwise every active administrator is notified and assigns it. Both CaseWorker and Viewer assignees are notified when the case's status changes.
- Organization administrators can manage cases throughout their organization; staff cannot access other staff members' or unassigned cases.
- View the selected organization's staff directory and active staff assignment contacts.
- Organization administrators open **Organizations** to inspect and update memberships for the organizations they administer, invite staff (the invitee receives the Staff portal role and CaseWorker membership), deactivate/reactivate memberships, and manage invitations. They cannot grant OrganizationAdmin, create organizations, or edit/delete organization records; only platform administrators can.
- Organization administrators can collect and update staff phone numbers and read the organization activity log.
- View organization dashboard statistics
- Manage personal access tokens
- Update own profile

### Platform job monitoring

Only platform Super Administrators can see the Background jobs screen. Filter by status to inspect queue state, attempt counts, timestamps, and sanitized failure codes. The interface labels running jobs as **Processing** and scheduled retries as **Retrying**. Retry is offered for failed work; cancel is offered only for pending work, and both actions require confirmation and prevent duplicate submissions while processing. The API verifies the role and current job state for every request. Organization administrators and ordinary staff cannot inspect this global queue.

### Platform Administrator

**Access requirements:** Authenticated platform super administrator account, signed in at `/super-platform-admin`.

**Available actions:**
- Create, rename, deactivate, reactivate, and delete organizations
- Manage the shared organization-type catalogue (create, rename, re-code, deactivate, reactivate, delete) from the *Organization types* tab; `Uncategorized` is protected and types in use cannot be deleted
- Manage memberships and roles within any organization (add members with Viewer, CaseWorker, or OrganizationAdmin roles; update roles; activate/deactivate memberships)
- Invite staff to any organization and manage those invitations (send, resend, revoke)
- Browse the platform directory in three sections: Citizens with editable profiles, Staff with editable account contact details, and All Users with platform-role controls and links to organization membership management
- Read the platform-wide audit log
- View platform dashboard statistics (total cases, per-status, per-priority, weekly/monthly trends)
- Track and manage application errors (list, filter, view detail, mark as handled)
- Monitor background email jobs by status and attempt count; inspect sanitized failures and retry failed or cancel pending work. Job operations are audited and rate-limited.
- Manage personal access tokens
- Update own profile

**Organization Administration UI Details**

The Organizations view is split into three inner tabs for platform administrators, and a single *Members & invitations* tab for organization administrators:

1. **Organization types** *(SuperAdmin only)* — the shared `OrganizationType` catalogue. Every organization must be assigned one; the seeded **Uncategorized** type is used when a creator does not pick one and cannot be renamed away, deactivated, or deleted. The tab lists each type with its code, organization count, status pill, and created date, and offers *Create*, *Edit* (inline save), *Deactivate/Activate*, and *Delete*. A type in use by any organization cannot be deleted; the API returns `409 Conflict` and the UI surfaces the message.
2. **Organizations** *(SuperAdmin only)* — organization CRUD. Create/update forms include a *Type* dropdown populated from the catalogue; when no types are available the current type name is shown read-only. Actions: view members, rename, activate/deactivate, and delete. Deleting an organization that still holds cases is refused with a `409`.
3. **Members & invitations** — for platform administrators, the tab opens with a filter bar so they can narrow by organization type and then pick an organization; the filtered organization dropdown resets when the selected type would exclude the current pick. *Apply* loads the chosen organization's memberships and invitations; *Reset* clears the selection. Organization administrators land directly on this tab and only see the organizations they administer; the same membership role/activation controls and the `InvitationManager` (send, resend, revoke) appear below.

- **Memberships section**:
  - Membership list showing user ID, role, active status, and activation toggle; organization administrators can change roles and activation for administered organizations
  - Role options: Viewer, CaseWorker, OrganizationAdmin (only SuperAdmins can grant OrganizationAdmin)

- **InvitationManager section** (integrated component):
  - Form to send invitations: requires full name (1–150 characters), email (max 254 characters), optional phone number (max 30 characters), and role
  - Invitation list showing name, email, role, expiry date, phone number (if provided), and status, including a delivery-failed state with a Resend action
  - Actions: Resend (for pending invitations) and Revoke (for pending invitations)
  - No password is created or emailed; the invitee sets their own password from the single-use link

**Deleting an organization**
1. Open the **Organizations** view and select the organization
2. Choose **Delete organization**
3. Confirm the action in the confirmation prompt
4. The organization, its memberships, its staff records, and its pending invitations are removed
5. The action is refused while the organization still holds cases — resolve or move those cases first, then delete the organization

**Viewing the dashboard**
1. Open **Dashboard** in the platform administration navigation
2. Review the platform-wide case statistics:
   - Total cases
   - Cases per status (Submitted, UnderReview, Assigned, InProgress, AwaitingInformation, Resolved, Closed, Rejected)
   - Cases per priority (Low, Normal, High, Critical)
   - Weekly and monthly created/resolved case counts

**Tracking application errors**
1. Open **Errors** in the platform administration navigation
2. Review the list of unhandled application errors (stack traces, request paths, user context)
3. Filter by status: all, unhandled, or handled
4. Click an error to view full detail (message, stack trace, source, method, request path, user, organization, additional data)
5. Mark an error as handled once investigated and resolved

### Citizen User

**Status:** Citizens sign in through the **Citizen Portal** and stay inside the citizen workspace: dashboard, public case submission, their own profile, and access tokens. Case lists, staff administration, and organization administration are never part of a citizen session. Citizens can submit new cases to an active organization and optionally attach any file type up to 10 MB, and they can track each submitted request from the **My requests** list — opening its full detail with the current status and complete status history.

**Citizen Dashboard workflow:**
1. Sign in through the **Citizen Portal**
2. The dashboard opens and shows your account status, the portal you are signed in to, and the available actions
3. Use the action cards to open **Public intake**, **My profile**, or **Access tokens**
4. Review the **My requests** list, which shows each submitted request with its current status and reference
5. Select **View details** on a request to open its full description and status-history timeline, then **Back to my requests** to return

**Submit a case:** Open **Public intake**, select the responsible active organization, enter a title and description, and optionally attach a supporting file. The API validates the organization and records the citizen identity from the session. You receive a case reference after submission, the request is routed to an organization administrator for triage, and you can track it from **My requests**. Staff-created cases instead derive their organization from the signed-in staff membership.

**Citizen Profile workflow:**
1. Sign in through the **Citizen Portal**
2. Open **My profile** in the navigation (or the dashboard action card)
3. Provide **Full Name** and **Email** (required); optionally add phone number, NID number, citizenship number, age, gender, and permanent/temporary addresses
4. Submit to create the profile; submitting again updates the existing profile
5. NID number and citizenship number must be unique across profiles when provided; the server rejects duplicates

---

## 5. Core Workflows

### 5.1 Viewing and Managing Cases

1. Navigate to **Cases** in the sidebar (default view)
2. Use the search box to find cases by reference number or title
3. Use the status filter dropdown to filter by case status
4. Click on a case in the table to view details in the right panel
5. View case information, status history, assignment history, and documents

The workspace resolves the active organization from the signed-in account's active memberships. If the account belongs to multiple organizations, choose the organization in the workspace selector. The server validates every selection; changing the selector cannot grant access to a non-member organization.

### 5.2 Creating a Case

1. Go to **Cases** view
2. Click **"+ New case"** button
3. Fill in **Title** (required, 3–200 characters) and **Description** (required, 10–5000 characters)
4. Click **"Create case"**
5. You'll receive a toast notification on success/failure
6. The new case will appear in the list

The organization is fixed to the authenticated staff workspace; staff do not select an organization in the case form. Organization administrators can assign a case to an active staff member and choose the per-case role **CaseWorker** (can update details, status, and documents) or **Viewer** (read-only). Staff see only cases assigned to them.

Organization administrators can edit case title and description and can permanently delete a case. Deletion opens an explicit confirmation dialog and removes the case, its history, and attached documents. CaseWorkers can edit title and description only for cases assigned to them with the CaseWorker role; Viewers cannot make changes. No staff member can delete a case.

### 5.3 Updating Case Status

1. Select a case from the cases list
2. In the case details panel, use the status dropdown
3. Select the new status
4. The status will update according to the state machine:
   - Submitted → UnderReview → Assigned → InProgress → AwaitingInformation → Resolved → Closed/Rejected
5. Status history will be refreshed
6. Closed and rejected cases cannot be edited

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
3. Select a file to upload (max 10MB; allowed: .pdf, .jpg, .jpeg, .png, .doc, .docx)
4. Upload will proceed if permitted

**Downloading:**
1. Select a case with documents
2. Click on a document name/link to download
3. The file will download through your browser

### 5.6 Managing Staff

1. Navigate to **Team contacts** to view staff contact details
2. Organization administrators manage memberships and invitations from **Organizations** for organizations they administer
3. Invitees receive a single-use link and choose their own password
4. Organization administrators can toggle staff active/inactive status and correct phone numbers inline
5. Other staff can view the current organization's contact directory but do not receive membership-management or staff-administration controls

**Note on phone numbers:** A phone number is optional and must be 6–30 characters with at least 6 digits. When an invitation carries a number, it is stored on the new account and staff record; for an account that already exists the number is only filled in when the account has none of its own, so a number someone entered themselves is never overwritten.

### 5.7 Managing Organizations

1. Navigate to **Organizations** (platform administrators see all organizations; organization administrators see only organizations they administer)
2. Use the create button to add an organization by name and code; duplicate names or codes and invalid values are reported inline
3. **Rename an organization**: Click **Edit** on the organization row, update the name and code, then save
4. **Activate/Deactivate an organization**: Use the **Activate** or **Deactivate** button on the row
5. **Delete an organization**: Click **Delete** for the organization — deletion is refused while the organization still holds cases
6. **Manage memberships**:
   - Select **View members** to open the organization's membership grid
   - Update a member's role using the role dropdown
   - Activate or deactivate a membership using the **Activate** or **Deactivate** button
7. **Manage invitations**: Use the invitation panel below the membership grid to send, resend, or revoke invitations for the selected organization. A failed email delivery is shown on the invitation and can be retried with **Resend** without invalidating the link.

### 5.8 Reviewing the Platform Directory

1. Sign in as a platform administrator and open **Directory**
2. Read the overview counters (accounts, active accounts, staff, active staff, organizations, active organizations, invitations, pending invitations)
3. Open the **Citizens** tab to browse citizen accounts. Use **Edit** to correct name, phone, NID, citizenship number, age, gender, or addresses, and **Activate/Deactivate** to control account access
4. Open the **Organization Members** tab to browse every membership. Change the role from the row dropdown, activate/deactivate the membership, or use **Move to…** to transfer the member to another active organization; transfers require a platform super administrator and are audited
5. Use the search field, sort control, and pagination to narrow either grid
6. Use **Invitations** on a member row or the organization selector in the **Member invitations** panel to manage invitations for that organization

Citizens and organization members are distinct: an account appears in the Citizens grid only when it has a citizen profile, and a membership row never duplicates the account identity.

### 5.9 Reviewing the Audit Log

1. Platform administrators: open **Audit log** for the platform-wide trail. Organization administrators: open **Activity log** for their organization. CaseWorkers and Viewers: open **Activity log** to see only events they performed.
2. Filter by category (authentication, account, organization, membership, staff, case, access token), by action, by free text, or by date range
3. Page through the results with the pagination controls
4. Each row shows when the event happened, who did it, what it touched, and a readable detail

**Note:** The platform audit log is only available to platform super administrators. Organization administrators can see the full audit trail for their organization; staff can see only their own events. The audit trail is read-only, and the API enforces the actor and organization filters regardless of query parameters.

### 5.10 Reviewing the Dashboard

1. Platform administrators: open **Dashboard** for platform-wide case and platform-operation totals. Organization staff: open **Dashboard** for statistics scoped to the active organization.
2. Review the cards for total and active cases, monthly creation and resolution, and (for platform administrators) active organizations, accounts, staff memberships, and pending invitations.
3. Use **Cases by status** and **Priority mix** to compare current case totals. These charts include text labels and counts and do not rely on color alone.
4. Use **Recent case activity** to compare the weekly and monthly created/resolved counts returned by the API. The dashboard does not estimate daily trends.
5. Individual staff workload is not included in the current statistics API. Use **Cases** to review assignments.

Citizen accounts can submit service requests, but case tracking and status updates are not available in the citizen portal yet. The dashboard explains this limitation rather than showing invented case counts.

### 5.11 Tracking Application Errors

1. Sign in as a platform administrator and open **Errors**
2. Review the list of application errors, filtered by status:
   - **All** — all errors regardless of status
   - **Unhandled** — errors that have not been addressed yet (default)
   - **Handled** — errors that have been investigated and marked as resolved
3. Click an error to view full detail:
   - Error message and stack trace
   - Source and method name
   - Request path, method, and query string
   - User email and organization (if available)
   - Additional data (JSON)
   - Whether the error was handled by the application
   - When it occurred
4. Mark an error as **Handled** once investigated and resolved

### 5.12 Managing Your Citizen Profile

1. Sign in through the **Citizen Portal**
2. Navigate to **My profile** (from the dashboard action card or the sidebar)
3. If you are not signed in, you will be prompted to **Sign in**
4. Fill in the required fields (**Full Name**, **Email**) and any optional details
5. Submit to create or update your profile
6. A confirmation message appears on success; validation or duplicate errors are shown in red

**Note:** The server stores the profile against your authenticated identity. It does not accept a client-supplied user ID, and duplicate NID/citizenship numbers are rejected.

### 5.13 Updating Your Unified Profile

The **Profile** page is a unified self-service profile available to both citizens and staff:

1. Open **Profile** in the navigation
2. The page detects your account type (citizen or staff) from your roles
3. For citizens: edit civil registry details (full name, email, phone, NID, citizenship number, age, gender, addresses)
4. For staff: edit contact details (full name, phone number)
5. Submit to save changes
6. Changes are validated server-side and recorded in the audit trail

**Note:** Super administrators do not have access to the Profile page (they use the platform administration surfaces only).

### 5.14 Managing Personal Access Tokens

Personal access tokens let an automated client (a script, an integration, a mobile app) call the API without a browser session. A token acts on behalf of the user who created it and carries that user's permissions.

1. Sign in to CivicFlow (citizen, staff, or platform admin sign-in page)
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

---

## 6. Common Errors and Troubleshooting

| Error | Possible Cause | Recovery |
|---|---|---|
| Organization workspace reports no active membership | The account has no active membership, or its organization is inactive | Contact an organization administrator; the frontend resolves organization IDs from membership and does not configure them |
| "Invalid email or password" | Wrong credentials, wrong portal, inactive/unconfirmed account, or an account record requiring administrator review | Check the correct portal and try **Forgot password** for recovery. Accounts migrated from the previous external identity provider must set a new local password; their previous provider password is not available to CivicFlow. Verification resend is on the recovery page. If sign-in still fails, contact an administrator to verify account status; CivicFlow intentionally does not disclose whether an email is registered. |
| "This account is not a citizen account" | Signed in through the Organization Portal | Sign in through the **Citizen Portal** (`/citizen/login`) |
| "This account is not an organization account" | Signed in through the Citizen Portal | Sign in through the **Organization Portal** (`/admin`) |
| "This account is not a platform administrator" | Signed in at `/super-platform-admin` with a staff or citizen account | Staff use `/admin`; citizens use `/citizen/login` |
| "Platform administration unavailable" | A staff or citizen session opened `/super-platform-admin` | Use **Go to my workspace**, or sign in with a platform administrator account |
| "Organization workspace unavailable" | A citizen or platform-administrator session opened `/admin` | Use **Go to my dashboard** (citizens) or the platform portal (super administrators) |
| "This invitation link is invalid, expired, or has already been used" | Invitation was used, revoked, or passed its expiry | Ask an administrator to resend the invitation |
| "The account for this email address is inactive" | The invited address belongs to a deactivated account | Contact the platform administrator |
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
| "A phone number must be between 6 and 30 characters" | The number is too short or too long | Enter 6–30 characters with at least 6 digits; digits, spaces, and `+ - ( ) . /` are allowed |
| "This organization still holds cases" | Cases are still assigned to the organization | Resolve or move the cases, then delete the organization |
| "Organization not found" | The organization was already deleted | Refresh the list |
| "The audit log is not available for this account" | Audit access requires a platform administrator or an organization administrator | Sign in with the appropriate role |
| Case creation/update fails | Network/API issues or permissions | Check connection and permissions; retry |

**Tips:**
- Watch for toast notifications — they provide real-time feedback
- Check the browser console for additional error details in development
- Ensure all required environment variables are configured
- In Development, verification, password-reset, and invitation links are printed to the API console because email is not delivered. They contain sensitive, one-time tokens: use only in a private local run and never share or retain the logs. Outside Development, configure SMTP delivery as described in the [README](../README.md).
- A personal access token is displayed only once, at creation; if you lose it, revoke it and create a new one

---

## 7. Security and Privacy

### Safe Usage

- **Always sign out** when finished, especially on shared devices
- **Keep credentials secure** — never share login information
- **Verify URLs** — only access CivicFlow through trusted URLs
- **Be cautious with documents** — only upload/download appropriate files
- **Treat access tokens as passwords** — anyone holding one can act as you until revoked or expired

### Data Handling

- Cases and documents are organization-scoped — cross-organization access is prevented by server-side authorization and PostgreSQL row-level security
- Access is controlled by authentication and authorization
- Server-side validation enforces permissions
- Sessions use HttpOnly cookies with CSRF protection and are revoked on sign-out
- Personal access tokens are stored only as a SHA-256 hash; the plaintext exists only in the response that created it and in the caller's copy
- Every sign-in, account, invitation, membership, organization, staff, case, and access-token change is recorded in the audit trail with the actor and a timestamp
- Phone numbers are optional, are only visible to administrators of the relevant organization (and to platform administrators), and are never required to sign in
- Application errors are persisted with full context (stack trace, request path, user) and are accessible only to platform super administrators

**Note:** No absolute security guarantees can be made — follow your organization's security policies.

---

## 8. Known Limitations

### Current Limitations

| Area | Limitation |
|---|---|
| **Email delivery** | Account lifecycle and invitation email require TLS-enabled SMTP outside Development; case notification email uses durable retries. Development does not deliver email; it prints sensitive, one-time links to the API console automatically. Use only in a private local run and never share or retain the logs. |
| **Notifications** | Notification count is polled once per minute; push/WebSocket delivery and SMS are not implemented. Invitation lifecycle events use the existing invitation email flow, not the job queue. |
| **Citizen portal scope** | Citizens may submit cases to active organizations, attach an optional arbitrary file up to 10 MB, and track their own submitted cases (current status plus a detail view with full status history). Citizens have no access to the staff case workspace, staff administration, or other organizations' cases. |
| **Environment dependent** | Many features require proper backend/frontend configuration |
| **No offline support** | Requires network connectivity |
| **File restrictions** | Case attachments accept any file type up to 10 MB; malware scanning is not provided |
| **Role enforcement** | UI may show options that API will reject if permissions insufficient |
| **Audit trail** | Append-only and read-only; no retention, export, or alerting policies implemented |
| **Error tracking** | No automated alerting on new errors; manual review required |

### Not Implemented

- Advanced reporting and analytics (beyond the dashboard statistics)
- Bulk operations
- Mobile app
- Advanced search capabilities
- SMS notifications
- Workflow automation
- Per-token scopes for personal access tokens
- Per-organization sign-in selection (multi-org staff use `X-Organization-Id` header)

---

## 9. Glossary

| Term | Definition |
|---|---|
| **Case** | A service request or issue tracked through the system |
| **Case Status** | The current state of a case: Submitted, UnderReview, Assigned, InProgress, AwaitingInformation, Resolved, Closed, or Rejected |
| **Case Priority** | The urgency level of a case: Low, Normal, High, or Critical |
| **Organization** | A government department/agency using CivicFlow |
| **Membership** | Association between a user and an organization with a specific role (Viewer, CaseWorker, OrganizationAdmin) |
| **Application Error** | An unhandled exception persisted with full context (stack trace, request path, user, org) for review by super administrators |
| **Audit Event** | A structured record of an action (authentication, account, invitation, membership, organization, staff, case, or access-token event) with actor, organization, entity, and detail |
| **ASP.NET Core Identity** | CivicFlow uses first-party ASP.NET Core Identity with cookie sessions (no external identity provider) |
| **Session Cookie** | HttpOnly cookie identifying the signed-in user (not readable by JavaScript) |
| **CSRF** | Cross-Site Request Forgery protection using an anti-forgery token (`X-CSRF-TOKEN` header) |
| **Personal Access Token (PAT)** | A long-lived credential for API clients, sent as `Authorization: Bearer civicflow_pat_…`; it carries the creating user's permissions and can be revoked at any time |
| **Role** | Account permission level: `citizen`, `staff`, or `superadmin` |
| **Portal** | The sign-in surface an account is bound to — the Citizen Portal for citizen accounts, the Organization Portal for staff accounts, and Platform Administration for super administrators |
| **Invitation** | An administrator's request for someone to join an organization. It is delivered as a single-use, expiring link; the invitee chooses their own password |
| **Assignment** | Designating a staff member to handle a case |
| **Status History** | Audit trail of case status changes |
| **Assignment History** | Audit trail of case assignments |
| **Dashboard** | Platform-wide or organization-scoped case statistics (total, per-status, per-priority, weekly/monthly trends) |
| **Unified Profile** | A single self-service profile surface for both citizens and staff, replacing separate profile flows |
| **Row-Level Security (RLS)** | PostgreSQL feature enforcing organization-scoped data isolation at the database level |

---

## Support

For technical issues, check the [README](../README.md) for setup instructions or contact your system administrator. For questions about workflows, refer to the relevant sections of this manual. For implementation status and roadmap, see the [Delivery Roadmap](DELIVERY-ROADMAP.md).
