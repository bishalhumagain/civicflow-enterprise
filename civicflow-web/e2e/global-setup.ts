import { randomUUID } from 'node:crypto'
import { Client, type ClientConfig } from 'pg'
import {
  E2E_CITIZEN_ONE,
  E2E_CITIZEN_TWO,
  E2E_ORGANIZATION_CODE,
  E2E_ORGANIZATION_NAME,
  E2E_STAFF_ADMIN,
} from './test-data'

/**
 * ASP.NET Core Identity v3 hash for the E2E password declared in
 * `test-data.ts`. The encoded payload carries its own salt and iteration
 * count, so verification remains stable when the framework default changes.
 * Only the disposable E2E database ever sees it.
 */
const passwordHash =
  'AQAAAAIAAYagAAAAEBrZt4TsINkfN8SpZmOjJh3AVX3x/bjg+lNXJM2kBJ1UXjS1gWRhearQPm/5zVBBrA=='

const defaultOrganizationId = '11111111-1111-1111-1111-111111111111'
const organizationTypeId = '00000000-0000-0000-0000-000000000001'

type SeedUser = {
  email: string
  fullName: string
  phoneNumber: string
  nidNumber: string
  citizenshipNumber: string
  role: 'citizen' | 'staff'
}

const users: SeedUser[] = [
  {
    email: E2E_CITIZEN_ONE,
    fullName: 'E2E Citizen One',
    phoneNumber: '9800000001',
    nidNumber: 'E2E-NID-1',
    citizenshipNumber: 'E2E-CIT-1',
    role: 'citizen',
  },
  {
    email: E2E_CITIZEN_TWO,
    fullName: 'E2E Citizen Two',
    phoneNumber: '9800000002',
    nidNumber: 'E2E-NID-2',
    citizenshipNumber: 'E2E-CIT-2',
    role: 'citizen',
  },
  {
    email: E2E_STAFF_ADMIN,
    fullName: 'E2E Staff Admin',
    phoneNumber: '9800000003',
    nidNumber: 'E2E-NID-3',
    citizenshipNumber: 'E2E-CIT-3',
    role: 'staff',
  },
]

function readConnection(): ClientConfig {
  const raw =
    process.env.CIVICFLOW_E2E_DATABASE_URL ??
    process.env.CIVICFLOW_TEST_POSTGRES_ADMIN

  if (!raw?.trim()) {
    throw new Error(
      'Set CIVICFLOW_TEST_POSTGRES_ADMIN (an ADO.NET connection string) or ' +
        'CIVICFLOW_E2E_DATABASE_URL (a PostgreSQL URL) to the disposable ' +
        'database used by the browser E2E suite.',
    )
  }

  const trimmed = raw.trim()

  if (trimmed.startsWith('postgres://') || trimmed.startsWith('postgresql://')) {
    return { connectionString: trimmed }
  }

  const values = new Map<string, string>()
  for (const part of trimmed.split(';')) {
    const separator = part.indexOf('=')
    if (separator <= 0) continue
    values.set(
      part.slice(0, separator).trim().toLowerCase(),
      part.slice(separator + 1).trim(),
    )
  }

  const host = values.get('host') ?? values.get('server')
  const database = values.get('database') ?? values.get('initial catalog')
  const user = values.get('username') ?? values.get('user id')
  const password = values.get('password')

  if (!host || !database || !user || !password) {
    throw new Error(
      'The E2E database connection string must include Host, Database, ' +
        'Username, and Password.',
    )
  }

  return {
    host,
    port: Number(values.get('port') ?? '5432'),
    database,
    user,
    password,
  }
}

async function seedUser(client: Client, seed: SeedUser): Promise<string> {
  const result = await client.query<{ Id: string }>(
    `
    insert into application_users (
      "Id", "FullName", "Email", "NormalizedEmail", "PlatformRole",
      "CreatedAt", "UpdatedAt", "IsActive", "AccessFailedCount",
      "ConcurrencyStamp", "EmailConfirmed", "LockoutEnabled", "LockoutEnd",
      "NormalizedUserName", "PasswordHash", "PhoneNumber",
      "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UserName")
    values (
      $1, $2, $3, $4, 'None', now(), now(), true, 0, $5, true, false, null,
      $4, $6, $7, false, $8, false, $3)
    on conflict ("NormalizedEmail") do update set
      "FullName" = excluded."FullName",
      "Email" = excluded."Email",
      "IsActive" = true,
      "AccessFailedCount" = 0,
      "ConcurrencyStamp" = excluded."ConcurrencyStamp",
      "EmailConfirmed" = true,
      "LockoutEnd" = null,
      "NormalizedUserName" = excluded."NormalizedUserName",
      "PasswordHash" = excluded."PasswordHash",
      "PhoneNumber" = excluded."PhoneNumber",
      "SecurityStamp" = excluded."SecurityStamp",
      "UpdatedAt" = now(),
      "UserName" = excluded."UserName"
    returning "Id"
    `,
    [
      randomUUID(),
      seed.fullName,
      seed.email,
      seed.email.toUpperCase(),
      randomUUID(),
      passwordHash,
      seed.phoneNumber,
      randomUUID(),
    ],
  )

  const userId = result.rows[0].Id

  await client.query(
    `
    insert into citizen_profiles (
      "Id", "ApplicationUserId", "FullName", "Email", "PhoneNumber",
      "NIDNumber", "CitizenshipNumber", "CreatedAt", "UpdatedAt")
    values (
      gen_random_uuid(), $1, $2, $3, $4, $5, $6, now(), now())
    on conflict ("ApplicationUserId") do update set
      "FullName" = excluded."FullName",
      "Email" = excluded."Email",
      "PhoneNumber" = excluded."PhoneNumber",
      "NIDNumber" = excluded."NIDNumber",
      "CitizenshipNumber" = excluded."CitizenshipNumber",
      "UpdatedAt" = now()
    `,
    [
      userId,
      seed.fullName,
      seed.email,
      seed.phoneNumber,
      seed.nidNumber,
      seed.citizenshipNumber,
    ],
  )

  const roleResult = await client.query<{ Id: string }>(
    `
    insert into identity_roles ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
    values (gen_random_uuid(), $1, $2, $3)
    on conflict ("NormalizedName") do update set "Name" = excluded."Name"
    returning "Id"
    `,
    [seed.role, seed.role.toUpperCase(), randomUUID()],
  )

  await client.query(
    `
    insert into identity_user_roles ("UserId", "RoleId")
    values ($1, $2)
    on conflict ("UserId", "RoleId") do nothing
    `,
    [userId, roleResult.rows[0].Id],
  )

  return userId
}

export default async function globalSetup(): Promise<void> {
  const client = new Client(readConnection())
  await client.connect()

  try {
    await client.query('begin')

    await client.query(
      `
      insert into organization_types (
        "Id", "Name", "Code", "IsActive", "CreatedAt", "UpdatedAt")
      values ($1, 'Uncategorized', 'UNCATEGORIZED', true, now(), now())
      on conflict ("Code") do nothing
      `,
      [organizationTypeId],
    )

    const organization = await client.query<{ Id: string }>(
      `
      insert into organizations (
        "Id", "Name", "Code", "IsActive", "CreatedAt", "UpdatedAt",
        "OrganizationTypeId")
      values ($1, $2, $3, true, now(), now(), $4)
      on conflict ("Code") do update set
        "Name" = excluded."Name",
        "IsActive" = true,
        "UpdatedAt" = now()
      returning "Id"
      `,
      [
        defaultOrganizationId,
        E2E_ORGANIZATION_NAME,
        E2E_ORGANIZATION_CODE,
        organizationTypeId,
      ],
    )

    const organizationId = organization.rows[0].Id
    const userIds = new Map<string, string>()

    for (const user of users) {
      userIds.set(user.email, await seedUser(client, user))
    }

    const staffUserId = userIds.get(E2E_STAFF_ADMIN)!

    await client.query(
      `
      insert into organization_memberships (
        "Id", "UserId", "OrganizationId", "Role", "IsActive",
        "CreatedAt", "UpdatedAt")
      values (gen_random_uuid(), $1, $2, 'OrganizationAdmin', true, now(), now())
      on conflict ("UserId", "OrganizationId") do update set
        "Role" = 'OrganizationAdmin',
        "IsActive" = true,
        "UpdatedAt" = now()
      `,
      [staffUserId, organizationId],
    )

    await client.query(
      `
      insert into staff_members (
        "Id", "OrganizationId", "FullName", "Email", "IsActive",
        "CreatedAt", "UpdatedAt", "PhoneNumber")
      values (gen_random_uuid(), $1, 'E2E Staff Admin', $2, true, now(), now(),
              '9800000003')
      on conflict ("OrganizationId", "Email") do update set
        "FullName" = excluded."FullName",
        "IsActive" = true,
        "PhoneNumber" = excluded."PhoneNumber",
        "UpdatedAt" = now()
      `,
      [organizationId, E2E_STAFF_ADMIN],
    )

    await client.query('commit')

    console.log(
      `E2E data ready: organization "${E2E_ORGANIZATION_NAME}" and ` +
        `${users.length} confirmed accounts.`,
    )
  } catch (error) {
    await client.query('rollback').catch(() => undefined)
    throw error
  } finally {
    await client.end()
  }
}
