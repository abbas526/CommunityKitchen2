# CommKitchen — Database Setup

These scripts build the CommKitchen MySQL database described in the project's
proposed architecture doc. Run them on the machine where MySQL Server is
installed (your Windows PC), using either MySQL Workbench or the `mysql`
command-line client — this can't be run remotely from Claude, since your
local MySQL server isn't reachable from here.

## Fastest path — one script

`RUN_ME_FIRST_full_setup.sql` does everything in one go: creates the
`CommKitchen` database, creates the `abbas` MySQL login used by the .NET API,
creates all 10 tables, and seeds the `Roles` and `PacketSizes` lookup data.

**MySQL Workbench:** connect as `root` (the account you set a password for
during install) → File → Open SQL Script → pick `RUN_ME_FIRST_full_setup.sql`
→ the lightning-bolt "Execute" button.

**Command line** (from a folder containing this file, in `cmd` or PowerShell):
```
mysql -u root -p < RUN_ME_FIRST_full_setup.sql
```
It will prompt for your root password.

## Proper path — versioned migrations (for when the .NET project exists)

- `00_create_database_and_user.sql` — run once, as root/admin.
- `migrations/001…010_*.sql` — run in numeric order (or point a migration
  runner such as DbUp at this folder) to create the tables.
- `seed/001…002_*.sql` — run after the migrations to seed `Roles` and
  `PacketSizes`.

## What got created

- Database: `CommKitchen` (utf8mb4)
- App login: `abbas`@`localhost`, granted full privileges on `CommKitchen.*`
  — this is what your appsettings.json connection string should use, e.g.
  `Server=localhost;Database=CommKitchen;User=abbas;Password=***;`
- Tables: `Roles`, `Users`, `Areas`, `PacketSizes`, `MemberProfiles`,
  `FoodMenus`, `FoodMenuItems`, `MemberOptOuts`, `RefreshTokens`, `AuditLogs`
  — matching the schema in the proposed architecture doc.
- Seed data: the 3 roles (SuperAdmin/Admin/Member) and 3 packet sizes
  (Small/Medium/Big). No SuperAdmin login is seeded yet — that needs a
  bcrypt password hash, which is easiest to generate once the .NET auth
  service exists (or ask me and I'll generate one separately).

## Security note

`00_create_database_and_user.sql` and `RUN_ME_FIRST_full_setup.sql` contain
the `abbas` password in plain text, which is normal for a local dev setup
script but shouldn't be committed to a public/shared git repo as-is. Add a
`.gitignore` entry for these two files, or move the password into a
`.env`/user-secrets file once the .NET project starts, and use a different,
stronger, unique password for the actual GoDaddy production database.
