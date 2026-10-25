# Validation, Normalization, and Display Conventions

This document describes the input rules, normalization behavior, database constraints, and display formatting used across CivicFlow Enterprise. It is the shared reference for backend, frontend, test, and review work.

## 1. Source of Truth

The API is authoritative. Every request is validated again on the server, regardless of what the browser already checked. Client-side validation exists only to give fast, accessible feedback and to avoid pointless round trips.

- Backend shared helpers: `PublicServiceCaseManagement.Api/Domain/Common/TextNormalization.cs`, `Domain/Common/PhoneNumbers.cs`, and `Infrastructure/Validation/ValidationAttributes.cs`.
- Backend request contracts use DataAnnotations and `[ApiController]` automatic model validation. Domain factories add invariants that must hold even when a request bypasses the controller (for example tests, jobs, or future endpoints).
- PostgreSQL enforces the same critical bounds with CHECK constraints added by the `AddValidationConstraints` migration.
- Frontend shared helpers: `civicflow-web/src/utils/validation.ts` and `civicflow-web/src/utils/format.ts`. Forms must not re-implement these rules locally.

## 2. Text Normalization

Normalization removes accidental noise. It never rewrites meaningful Unicode, casing of display text, or internal punctuation.

| Helper | Behavior |
|---|---|
| `CollapseWhitespace` | Trims and collapses every whitespace run to one ASCII space. Unicode letters and punctuation are preserved. |
| `NormalizeName` | Collapses whitespace and enforces 1 to 150 characters. Blank values are rejected. |
| `NormalizeOptional` | Trims an optional value and maps empty or whitespace-only input to `null`. Empty strings are never persisted for optional fields. |
| `NormalizeOptionalIdentifier` | Applies `NormalizeOptional` and upper-cases the result. Used for NID and citizenship numbers so uniqueness is stable. |
| `NormalizeEmail` | Trims and lower-cases the address. Email uniqueness and lookups use this canonical form. |
| `NormalizeMultiline` | Trims only the outer edges of free text and preserves internal line breaks and spacing. |
| `HasControlCharacters` | Rejects embedded control characters in single-line fields. Tab, carriage return, and line feed are allowed. |
| `PhoneNumbers.TryNormalize` | Trims and accepts digits, spaces, and `+ - ( ) . /`. Requires 6 to 30 characters and at least 6 digits. Blank input is valid for optional fields and becomes `null`. |

Client-side equivalents (`normalizeWhitespace`, `normalizeMultiline`, `normalizeOptional`, `normalizeEmail`, `normalizeIdentifier`, `sanitizePhoneInput`) follow the same rules so the submitted payload already matches the canonical shape.

## 3. Field Limits

| Field | Rule |
|---|---|
| Full name | Required; trimmed and whitespace-collapsed; 1 to 150 characters. |
| Email | Required where collected; trimmed and lower-cased; at most 254 characters; standard email shape. |
| Phone | Optional unless noted; 6 to 30 characters; at least 6 digits; limited character set. Required for citizen registration. |
| NID number | Up to 50 characters; trimmed and upper-cased; unique when present. |
| Citizenship number | Up to 50 characters; trimmed and upper-cased; unique when present. |
| Age | Optional integer from 0 to 150. |
| Gender | Up to 20 characters. |
| Address | Up to 500 characters per address field. |
| Case title | 3 to 200 characters after whitespace collapsing. |
| Case description | 10 to 5,000 characters after outer trimming. |
| Case update or citizen response | 1 to 4,000 characters after outer trimming. |
| Status explanation | Up to 2,000 characters. |
| Organization name | Required; trimmed and whitespace-collapsed; at most 200 characters. |
| Organization code | Required; trimmed and upper-cased; at most 50 characters; unique. |
| Password | 12 to 128 characters; upper case, lower case, digit, symbol; at least 6 distinct characters. |

## 4. Collections, Search, and Pagination

- Notifications accept `page` from 1 to 1,000,000 and `pageSize` from 1 to 100.
- Citizen case lists accept `search` up to 200 characters, a known status value, and one of `updated_desc`, `updated_asc`, `created_desc`, or `created_asc`.
- Staff case lists accept a bounded page and page size. Invalid or out-of-range values fail model validation with `400` instead of being silently clamped.
- Search terms are trimmed and internal whitespace is collapsed. SQL parameters are used throughout; no request value is concatenated into SQL.

## 5. File Uploads

- Maximum size: 10 MB (10,485,760 bytes). Empty files are rejected.
- The original file name must be non-empty, at most 255 characters, must not contain path traversal or NUL characters, and must not end with a dot or a space.
- Case documents accept arbitrary declared media types by contract. Declared `application/pdf`, `image/png`, `image/jpeg`, and `image/gif` uploads must match their magic signature; mismatches return `400`.
- MIME type alone is not proof of safety. Malware scanning and content sanitization are not provided; treat uploads as untrusted.
- File storage paths are generated by the server. Client-supplied paths are never used.

## 6. Database Enforcement

The `AddValidationConstraints` migration adds 27 CHECK constraints for the critical bounds that must survive direct database writes and future application paths. They cover citizen profile age, name, email, and identifiers; case document size; case title, description, status, priority, and assigned role; case update message and kind; status history values and explanation length; organization name and code; account, staff, and invitation name, email, phone, and role fields.

The migration has a full `Down` path that drops every constraint it adds. Constraint names use the `CK_<table>_<purpose>` convention. When a domain limit changes, update the DTO annotation, the domain helper, the frontend constant, this document, and add a new migration rather than editing an already-applied migration.

## 7. Forms and Accessibility

- Forms use `noValidate` and run the shared validators on submit. Field errors stay hidden until the field is touched or the form is submitted, so users are not scolded while typing.
- Every input has a visible label and a stable `id`. Required inputs carry `required`. An invalid input gets `aria-invalid=true` and `aria-describedby` pointing at the error and, where present, the hint text.
- Field errors are rendered next to the field with `role=alert`; the form-level error is also announced. The first invalid field receives focus after a failed submit.
- Values are preserved after validation or API failures. Controls are disabled only while a request is in flight. Success is shown only after a successful response; optimistic false success is not used.
- API failures surface the server message where safe, with explicit handling for `400`, `401`, `403`, `429`, CSRF failures, and network errors. A permanent failure never leaves the form looking successful.
- Error toasts use `role=alert` and assertive live-region behavior; informational and success toasts are polite status messages.
- Controls are keyboard reachable, use native inputs and buttons, keep touch targets usable, and wrap long text without hiding the meaning.

## 8. Display Formatting

- Dates and times use the explicit display locale `en-GB` and the explicit display time zone `Asia/Kathmandu`. The API stores and transports UTC timestamps; only presentation converts them.
- `formatDate`, `formatDateTime`, `formatNumber`, `formatFileSize`, `formatReference`, `formatText`, `formatStatus`, and `truncate` live in `civicflow-web/src/utils/format.ts`.
- Missing, null, empty, or invalid values render as the em dash `—`, never as `Invalid Date`, `undefined`, `null`, or a misleading zero.
- Numbers use the explicit display locale. File sizes use binary units and are rounded up so a limit is never understated.
- Reference numbers are displayed upper-case. Status and role labels use the shared label mapping, not raw enum identifiers.
- Free text is rendered through React text nodes. HTML from user input is never injected. Long text wraps or truncates with a title or accessible label so the full value remains discoverable.
- Changing the display locale or time zone is a deliberate product decision. Update this document and the formatter tests when it changes.

## 9. Error and Response Conventions

| Status | Meaning |
|---|---|
| `400` | Model validation, malformed payload, invalid pagination, an invalid state transition, or a file validation failure. |
| `401` | No valid session or token, or an expired session. |
| `403` | Authenticated but not allowed, including cross-portal and read-only role attempts. |
| `404` | Unknown resource or a resource outside the caller scope, including cross-tenant and cross-citizen identifiers. |
| `409` | Duplicate or conflicting write. |
| `410` | Invitation no longer available. |
| `423` | Account locked after repeated failed sign-ins. |
| `429` | Rate limit exceeded. |
| `503` | Database readiness failure. |

Framework validation returns the MVC validation envelope. Feature failures commonly return `{ "error": "..." }`; authentication failures may also return a stable `code`. Global unhandled failures return a generic message without exception details.

## 10. Security Implications

- Client normalization and validation are user-experience features, not security controls. Every rule is re-applied server-side.
- Request DTOs expose only fields a caller may set. Identity, organization, role, submitter, and audit fields come from the validated session, never from the payload.
- Authorization is checked before reads and mutations. Organization IDs are validated against active membership. Citizen queries are owner-scoped. Unknown and unauthorized identifiers return `404` where disclosure matters.
- Database CHECK constraints are defense in depth. They are not a substitute for application authorization or for clear validation messages.
- Secrets, tokens, passwords, and full sensitive payloads are not written to logs. Uploaded file names are validated and storage paths are generated by the server.
- Registration and recovery responses remain enumeration-safe; validation errors must not reveal whether an account exists.

## 11. Verification

Backend:

`dotnet test PublicServiceCaseManagement.sln --configuration Release`

The suite includes `TextNormalizationTests`, `PhoneNumbersTests`, `PhoneNumbersTryNormalizeTests`, `ValidationAttributesTests`, and `InputValidationIntegrationTests`. The integration coverage asserts padded and whitespace-only values, over-limit names, invalid email and phone values, normalization before persistence, age bounds, case text normalization, notification pagination bounds, and PDF signature and empty-file rejection.

Frontend:

`cd civicflow-web`

`npm run lint`

`npm run build`

`npm run typecheck:e2e`

The Playwright suite includes `input-validation.spec.ts`, which covers registration field errors and normalization, password-recovery field errors and focus management, public-intake field errors, and normalized case text in the tracking view. Browser journeys require the disposable PostgreSQL fixture described in `INTEGRATION-VERIFICATION.md`.

Database:

`dotnet ef migrations script --project PublicServiceCaseManagement.Api --startup-project PublicServiceCaseManagement.Api`

Review the generated script to confirm every `ADD CONSTRAINT` has a matching `DROP CONSTRAINT` in `Down`. Apply migrations only to a disposable database during verification.

## 12. Change Checklist

When adding or changing a field:

1. Add or update the request DTO annotation and the domain invariant.
2. Add or update the shared backend helper when the rule is reused.
3. Add or update the matching frontend constant and validator.
4. Add an accessible error, hint, and focus behavior in the form.
5. Add unit and API integration tests for valid, invalid, missing, whitespace, and boundary values.
6. Add a migration for a critical database bound; never edit an applied migration.
7. Update this document, the API authorization notes, and the user manual.
8. Run the narrow tests, then the full backend suite, frontend lint/build/typecheck, and any affected browser journey.
