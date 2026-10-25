/**
 * Deterministic identities used by the browser end-to-end suite. The
 * accounts live only in the disposable PostgreSQL database that backs the
 * test run; the seeder in `global-setup.ts` creates or repairs them before
 * each suite so no manual database work is required.
 */
export const E2E_PASSWORD = 'CivicFlow-E2E!2026'

export const E2E_CITIZEN_ONE = 'e2e.citizen.one@example.test'
export const E2E_CITIZEN_TWO = 'e2e.citizen.two@example.test'
export const E2E_STAFF_ADMIN = 'e2e.staff.admin@example.test'

export const E2E_ORGANIZATION_NAME = 'CivicFlow Development Organization'
export const E2E_ORGANIZATION_CODE = 'CIVICFLOW-DEV'
