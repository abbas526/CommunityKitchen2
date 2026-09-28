# CommKitchen (SaveFood) — Project Notes

Community Kitchen web application. Purpose: let registered families cancel their daily
tiffin ahead of time so the kitchen doesn't over-prepare food, and let an Admin manage
family registrations and tiffin sizing.

Stack: ASP.NET Core (C#) + MySQL backend. Local dev DB target: MySQL 8 (Workbench,
"Local instance MySQL80"). Deployment target: GoDaddy shared hosting, which runs
MySQL 5.6.49 / 5.7.38 or MariaDB 10.6 — NOT MySQL 8. Implication: avoid MySQL-8-only
syntax (CTEs, window functions), and don't rely on CHECK constraints being enforced by
the database on 5.6/5.7 (they're parsed but silently ignored there; MariaDB 10.6 and
MySQL 8 do enforce them) — validate date ranges etc. in the application layer too.
`utf8mb4` charset is supported on all of the above.

Project folder: D:\CommKitchen (this file's location). Migration scripts live in
D:\CommKitchen\db\migrations.

---

## Status

An earlier, more complex design (SuperAdmin / Admin-per-Area / Member hierarchy) was
proposed first and even had migration scripts written (001–010, covering Roles, Users,
Areas, PacketSizes, MemberProfiles, FoodMenus, FoodMenuItems, MemberOptOuts,
RefreshTokens, AuditLogs). That version is **superseded** — the user found it
unconvincing and rewrote the requirements from a functional standpoint (see below).
The old migration files under db\migrations are still on disk as of this note (user
has a separate backup of the whole folder and authorized removing them); they should
be deleted/replaced before the new schema is created.

Current stage: the old v1 migration scripts have been deleted (user had a backup)
and replaced with fresh v2 migration scripts (000-011) in db\migrations, covering the
11-table schema below. Not yet run against MySQL -- that's the next step.

---

## Functional requirements (as given by the user)

1. Community kitchen serves food from the kitchen.
2. Food is served every day except Sundays and certain other days in the year —
   roughly 200 serving days/year.
3. Food is prepared in the kitchen.
4. Food is served once a day to all registered community members.
5. One tiffin box is served per home (family), not per individual.
6. A home can have multiple members, but that's not an important factor for the system.
7. Tiffin comes in 3–4 sizes, defined once by the Admin. Size is chosen when the family
   head first registers (based on family size) and can be changed later in the same
   year.
8. Today, a family member calls and cancels the tiffin by phone if they don't want it
   for a day or a range of days, to reduce food wastage. The new app should let this
   happen through the app instead.
9. Need a web app in C# / ASP.NET Core with MySQL backend. One Family Head registers
   per family; an Admin accepts/approves new family registrations.
10. Plan tables/fields first, then proceed to DB + table creation step by step.

Clarifications given after the first table proposal:
- The Family Head can cancel their own tiffin directly through the app (not only the
  Admin entering it on their behalf).
- The system should allow multiple Admins (not just one), even though there's
  currently only one.
- Historical reporting on family tiffin size is needed — i.e. the system must be able
  to answer "what size did this family have on date X", not just the current size.

---

## Finalized DB schema (v2 — current plan)

Creation order (respects FK dependencies):

1. **Roles** — lookup. Id, Name. Rows: Admin, FamilyHead.
2. **Users** — every login (Admins and Family Heads). Id, RoleId (FK), Email,
   PasswordHash, FullName, Phone, IsActive, CreatedAt, UpdatedAt, LastLoginAt.
3. **TiffinSizes** — lookup, the 3–4 sizes an Admin defines. Id, Name, SortOrder.
4. **Families** — one row per home/registration. Id, FamilyHeadUserId (FK → Users,
   unique), Address, NumberOfMembers (optional/informational), TiffinSizeId (FK,
   current size), RegistrationStatus (Pending/Approved/Rejected), ApprovedByAdminUserId
   (FK → Users, nullable), ApprovedAt, IsActive, CreatedAt, UpdatedAt.
5. **FamilySizeHistory** — one row per tiffin-size change, for historical reporting.
   Id, FamilyId (FK), TiffinSizeId (FK), EffectiveFromDate, EffectiveToDate (nullable —
   null = currently active), ChangedByUserId (FK → Users), ChangedAt. Whenever size
   changes: close the open history row (set EffectiveToDate), insert a new one, and
   update Families.TiffinSizeId to match — one transaction.
6. **NonServingDays** — the exception dates in the year when the kitchen doesn't serve
   (holidays/closures). Sundays are NOT stored here — that's a fixed weekly rule
   applied in code. Id, TheDate, Reason (optional), CreatedByUserId, CreatedAt.
   "Is food served on date X" = not a Sunday AND not present in this table.
7. **TiffinCancellations** — the core table: a family (via Family Head, or Admin on
   their behalf) cancelling a day or date range. Id, FamilyId (FK), StartDate,
   EndDate, Reason (optional), Status (Active/Reinstated), CreatedByUserId, CreatedAt,
   ReinstatedAt, ReinstatedByUserId. A family is assumed to want their tiffin on any
   serving day unless a row here covers that date.
8. **RefreshTokens** — JWT refresh-token storage for auth. Id, UserId (FK), TokenHash,
   ExpiresAt, CreatedAt, RevokedAt, ReplacedByTokenHash.
9. **AuditLogs** (optional, recommended) — trail of registration approvals, size
   changes, cancellations entered on someone's behalf, etc. Id, UserId, Action,
   EntityType, EntityId, Metadata (JSON), CreatedAt.
10. **MealPlans** — the meal Admin enters ahead of time for an upcoming serving date.
    Id (BIGINT UNSIGNED, PK), MealDate (DATE, UNIQUE — one entry per date),
    MealDescription (VARCHAR(500), NOT NULL — the 4-5 lines of text), CreatedByUserId
    (FK → Users), CreatedAt, UpdatedByUserId (FK → Users, nullable), UpdatedAt
    (nullable). App layer should reject a MealDate that's a Sunday or in
    NonServingDays, same validation as elsewhere. Admin can enter meals arbitrarily
    far ahead (a week, a month) — how many of those get shown to Family Members is a
    separate, configurable concern (see AppSettings below), not limited by this table.
11. **AppSettings** — single-row system configuration. Id (TINYINT UNSIGNED, PK,
    always 1), MealVisibilityDays (INT UNSIGNED, NOT NULL, DEFAULT 7 — how many
    upcoming days' meals are shown to Family Members; Admin-editable), UpdatedByUserId
    (FK → Users, nullable), UpdatedAt (nullable). Seeded with one row at migration
    time. Designed as a single-row table for now (simple, type-safe columns); if more
    settings accumulate later it can be widened with more columns rather than
    redesigned.

---

## Open items / not yet decided

- Migration scripts (000-011) exist on disk but have not yet been run against a real
  MySQL database. Next step: create the `CommKitchen` database (000) and run 001-011
  in order.
- No seed data for TiffinSizes yet -- the Admin is expected to define the 3-4 sizes
  through the app once it exists; nothing hardcoded in migrations.
- v1 had FoodMenus/FoodMenuItems for tracking what food is served each day; v2 now
  covers this via MealPlans (item 10) + AppSettings (item 11) instead, added per the
  user's request to let Admins enter upcoming meal text and control how many days
  ahead Family Members see.


---

## Backend implementation (FaizMawaid -- ASP.NET Core 8 Web API)

Solution: D:\CommKitchen\FaizMawaid (single Web API project, project/namespace name
`FaizMawaid`). Repository pattern throughout -- controllers depend only on repository
interfaces, never on Dapper/SQL directly. No authentication implemented yet (deferred
to a later phase); PasswordHash is currently stored as whatever the caller sends.

Structure:
- `Data/` -- `IDbConnectionFactory` + `MySqlConnectionFactory` (wraps MySqlConnector,
  reads the `CommKitchen` connection string from appsettings.json). Also
  `BooleanTypeHandler`, a Dapper type handler registered in Program.cs so IsActive-style
  TINYINT UNSIGNED columns map cleanly to C# `bool` (MySqlConnector only auto-maps to
  bool for the deprecated TINYINT(1) syntax, which this schema doesn't use).
- `Models/` -- one POCO per table (Role, User, TiffinSize, Family, FamilySizeHistory,
  NonServingDay, TiffinCancellation, AuditLog, MealPlan, AppSetting), plus
  `RoleIds` (Admin=1, FamilyHead=2 constants) and `Models/Dtos/` for request/response
  shapes so controllers never bind directly to entities.
- `Repositories/Interfaces/` + `Repositories/` -- one interface + Dapper implementation
  per entity/concern (11 total, including `IReportRepository` for the daily tiffin-count
  report). Interfaces exist specifically so they can be mocked in unit tests in a later
  phase, per your request.
- `Controllers/` -- 10 thin API controllers, one per concern (Roles, Users, TiffinSizes,
  Families, NonServingDays, TiffinCancellations, MealPlans, AppSettings, AuditLogs,
  Reports). Business-rule validation that lives here (not in the DB) because MySQL
  CHECK constraints aren't enforced on GoDaddy's MySQL 5.6/5.7: EndDate >= StartDate on
  cancellations, Sunday/NonServingDay rejection on meal plans, cancellation StartDate
  not in the past.

Key design decisions:
- **Type mapping**: BIGINT UNSIGNED -> `ulong`, INT UNSIGNED -> `uint`,
  TINYINT UNSIGNED -> `byte`, DATE -> `DateOnly`, DATETIME -> `DateTime`. `ulong` was
  chosen over the more common `long` specifically to avoid a known
  Dapper+MySqlConnector `InvalidCastException` (UInt64 -> Int64) on BIGINT UNSIGNED
  columns.
- **RegistrationStatus / TiffinCancellationStatus** are C# enums, but repositories
  always pass `.ToString()` when writing them as SQL parameters (Dapper sends an
  enum's numeric value by default, which would corrupt a MySQL ENUM column).
- **Family registration** (`FamilyRepository.RegisterAsync`) creates the User,
  Family, and the opening FamilySizeHistory row together in one DB transaction, so
  size history is complete from day one.
- **Tiffin size changes** (`FamilyRepository.ChangeTiffinSizeAsync`) close the open
  FamilySizeHistory row, insert the new one, and update Families.TiffinSizeId, all in
  one transaction.
- **AuditLogs** is wired into every mutating controller action (register/approve/
  reject a family, change tiffin size, create/reinstate a cancellation, create a
  non-serving day/meal plan, update settings) rather than left purely decorative.
- The 24-hour-ahead cancellation cutoff from the earlier (v1) design was **not**
  carried over, since the v2 functional rewrite didn't restate it -- cancellations
  currently only require StartDate in the future and EndDate >= StartDate. Add a
  cutoff check in `TiffinCancellationsController.Create` if you want one.

Not yet done:
- No unit test project yet (deliberately deferred, per your request) -- the interfaces
  above are the seam for that.
- No `dotnet build` was run from this environment (the .NET SDK isn't available in the
  sandbox that edits files on this computer) -- build in Visual Studio and report back
  any compile errors so they can be fixed immediately.
- `appsettings.json`'s `ConnectionStrings:CommKitchen` has a placeholder
  password (`CHANGE_ME`) -- update it to your actual local MySQL root password (or a
  dedicated app user) before running.
- Swashbuckle (Swagger) was added for manual endpoint testing via a browser UI at
  `/swagger` in Development -- remove it later if not wanted.


---

## Unit tests (FaizMawaid.Tests -- xUnit)

New project: D:\CommKitchen\FaizMawaid.Tests, added to FaizMawaid.sln so it opens
alongside the main project. 59 [Fact] tests total, all positive/happy-path (no
negative-case testing yet, per what was asked). Two different testing styles, because
repositories and controllers need genuinely different approaches:

- **ControllerTests/** (10 classes, ~35 tests) -- true isolated unit tests. Each
  repository interface is mocked with Moq; no database is touched. These assert the
  right ActionResult type comes back (Ok/CreatedAtAction/NoContent) and, for the
  actions that should audit-log, that IAuditLogRepository.AddAsync was called. Safe
  to run anytime, anywhere, no setup required.
- **RepositoryTests/** (11 classes, ~24 tests) -- these are integration tests against
  a real MySQL database, not isolated unit tests, since the logic under test IS the
  SQL. They point at the same local `CommKitchen` database the app uses by default
  (connection string hardcoded in `RepositoryTests/TestConnectionFactory.cs`,
  overridable via the `COMMKITCHEN_TEST_CONNECTION_STRING` environment variable).
  **Your local MySQL must be running with the schema already created** (migrations
  000-011) for these to pass. Each test creates its own throwaway data (unique GUID
  emails/names tagged "unittest.local"/"UT_", randomized far-future dates to dodge
  UNIQUE-constraint collisions with itself or real data) and cleans up after itself in
  a `finally` block -- via raw SQL for Users/Families/FamilySizeHistory specifically,
  since those repositories deliberately don't expose hard deletes (that's a business
  rule, not a test-plumbing gap). The one exception is `AppSettingsRepositoryTests`,
  which touches the single global settings row -- it saves the original value first
  and restores it afterward so it doesn't leave your real app configuration changed.

Not covered yet (call these out if you want them added next): negative/error-path
tests (bad input, not-found, conflict responses), and no test project references
Testcontainers -- if Docker becomes available later, the repository tests could be
swapped to spin up a throwaway MySQL instance instead of using your real local one.

## Bugs found from the first test run (2026-09-23)

The first real Test Explorer run (59 tests: 8 passed, 17 failed, 34 not run yet)
surfaced two independent bugs, both now fixed in source:

1. **`DateOnly` isn't a Dapper-bindable parameter type out of the box.** With this
   pinned Dapper/MySqlConnector combo, any repository method taking a `DateOnly` or
   `DateOnly?` parameter (Family.EffectiveFromDate, FamilySizeHistory's date param,
   NonServingDay.TheDate, TiffinCancellation.StartDate/EndDate, MealPlan.MealDate,
   Report's date param, AuditLog.GetAsync's from/to) threw
   `System.NotSupportedException: The member X of type System.DateOnly cannot be used
   as a parameter value` -- this is what caused all 17 failures in the screenshot.
   **Fix:** added `FaizMawaid/Data/DateOnlyTypeHandler.cs`
   (`SqlMapper.TypeHandler<DateOnly>`, converting to/from `DateTime` since the
   underlying column is `DATE`). Registered via `SqlMapper.AddTypeHandler(...)` in
   two places, both required:
   - `Program.cs`, alongside the existing `BooleanTypeHandler` registration -- covers
     the real running app.
   - `FaizMawaid.Tests/RepositoryTests/TestConnectionFactory.cs`, in a static
     constructor -- the test project never runs `Program.cs`, so without this the
     repository tests would still fail even after rebuilding. The static constructor
     runs once, automatically, the first time any test calls `TestConnectionFactory.Create()`.

2. **`AppSettingsRepositoryTests` had a latent FK-violation bug in its own cleanup,**
   unrelated to DateOnly. The test creates a throwaway user, updates the global
   AppSettings row (stamping `UpdatedByUserId` with that user's id), then in its
   `finally` block tried to restore the *original* `UpdatedByUserId` -- but fell back
   to the throwaway user's id (`original.UpdatedByUserId ?? userId`) whenever the
   original value was `NULL` (true on a freshly-seeded database, since migration 011's
   seed row has no updater). That left `AppSettings.UpdatedByUserId` pointing at the
   throwaway user right before the same `finally` block deleted that user, tripping
   the `FK_AppSettings_UpdatedBy` foreign key and throwing instead of cleanly passing.
   **Fix:**
   - `UpdateAppSettingsRequest.UpdatedByUserId` changed from `ulong` to `ulong?` (this
     also matches the DB column, which is nullable) so the test can restore a true
     `NULL` instead of being forced to substitute some user id.
   - `AppSettingsRepositoryTests`'s `finally` block now restores
     `original.UpdatedByUserId` directly (no `?? userId` fallback).
   - No controller change needed: `AuditLog.UserId` was already `ulong?`, so
     `AppSettingsController.Update` assigning `request.UpdatedByUserId` into it still
     compiles as-is.

Both fixes are applied but **not yet confirmed against a real test run** -- next step
is rebuilding and re-running all 59 tests in Visual Studio.

## Front-end UI (wwwroot -- HTML + Bootstrap 5 + jQuery)

A full set of screens now lives under `D:\CommKitchen\FaizMawaid\wwwroot`, served
directly by Kestrel/IIS Express as static files (`app.UseDefaultFiles()` +
`app.UseStaticFiles()` were already wired up in Program.cs). No build step, no
npm/webpack -- plain HTML files, Bootstrap 5.3 + Bootstrap Icons + Google Fonts
(Poppins/Inter) from CDN, jQuery for all DOM/AJAX work, per the user's request. One
shared stylesheet and one shared script are reused by every page:

- `wwwroot/css/site.css` -- theme (warm teal/orange "community kitchen" palette),
  larger-than-default base font size and button/input heights (for older users),
  responsive breakpoints, reusable component classes (`.ck-stat-card`, `.ck-tile`,
  `.ck-meal-card`, `.badge-status-*`, `.ck-bar-*` for a plain-CSS bar chart, etc).
- `wwwroot/js/site.js` -- the `CK` namespace: `CK.api` (jQuery AJAX wrapper +
  `errorMessage()` that reads both the plain-string bodies this API returns from
  `BadRequest("...")`/`Conflict("...")` and ASP.NET Core's automatic
  ProblemDetails validation errors), `CK.session` (see below), `CK.nav.render()`
  (injects the shared top navbar into a page's `#ckNavbar` div), `CK.toast()`,
  `CK.fmt` / `CK.esc` / `CK.today()` / `CK.addDays()` helpers.

**Pages built** (all under `wwwroot/`):
- `index.html` -- landing page, choose Family Head or Admin.
- `family/register.html`, `family/login.html`, `family/dashboard.html` -- register,
  "log in" by email, and a single dashboard covering family info, current tiffin
  size + change-size modal + size history, upcoming meals (from
  `/api/mealplans/upcoming`), the cancel-tiffin form, and the cancellations list
  with reinstate.
- `admin/login.html`, `admin/dashboard.html`, `admin/registrations.html`,
  `admin/families.html`, `admin/tiffin-sizes.html`, `admin/non-serving-days.html`,
  `admin/meal-plans.html`, `admin/settings.html`, `admin/reports.html`,
  `admin/audit-log.html` -- one screen per admin capability the API already
  exposes. `admin/families.html` covers edit/change-size/history/activate for all
  families (not just pending ones, which get their own approve/reject screen at
  `admin/registrations.html`).

**No login/auth yet, by design (see "Don't do Authentication code right now"
above) -- so the UI fakes a session client-side only:** `family/login.html` and
`admin/login.html` just look up a user by email (`GET /api/users?roleId=...`,
matched client-side -- there's no `GetByEmail` endpoint) with **no password
prompt at all**, since a password field would be misleading when nothing checks
it yet. `CK.session` stores `{userId, familyId/…, fullName, email, phone}` in
`localStorage` (keys `ck_family_session` / `ck_admin_session`) purely so the UI
can behave like a logged-in app and pages can send the right `*UserId` on writes
(`CreatedByUserId`, `ApprovedByAdminUserId`, etc). **This is not a security
boundary** -- anyone can edit localStorage or call the API directly. Real auth
replacing this is still a later phase. Registration (`family/register.html`) and
first-Admin creation (`admin/login.html`'s collapsible form) both silently send a
random placeholder string as `PasswordHash` since the field is `NOT NULL` in the
DB but nothing reads it yet.

**Small backend changes made alongside the UI** (both low-risk, covered by
reasoning not by a fresh test run):
- `Program.cs`'s `AddJsonOptions` now also registers a `JsonStringEnumConverter`,
  so `RegistrationStatus`, `TiffinCancellationStatus` etc. serialize over HTTP as
  their string names (`"Pending"`, `"Active"`, ...) instead of raw numbers. This
  only affects JSON payloads going out over HTTP -- it doesn't touch Dapper's
  enum handling (which already works off MySQL ENUM string values) or any
  existing xUnit test, so it carries no risk to the 59 tests already written; it
  just makes the API (and the UI, and Swagger) far less confusing.
- Deleted the leftover placeholder `wwwroot/Index.html` ("Hello World 123") and
  changed `Properties/launchSettings.json`'s `launchUrl` from `weatherforecast`
  (a controller that no longer exists) to `index.html`, so pressing F5 in Visual
  Studio opens the new landing page directly.

**Known gaps / good follow-ups, not fixed here (out of scope for a UI-only ask):**
- `FamiliesController.Register` still doesn't pre-check for a duplicate email
  (unlike `UsersController.Create`, which does) -- a repeat registration attempt
  will hit a raw MySQL unique-constraint error and surface as a generic 500. The
  UI shows a best-guess message on any 500 from that endpoint. Fixing this
  properly means adding `IUserRepository` to `FamiliesController`'s constructor.
- There's no dedicated "find user/family by email" endpoint, so the two login
  pages fetch the full list for that role (`GET /api/users?roleId=...`) and
  filter client-side. Fine at community-kitchen scale; would need a real
  endpoint if the member list grows very large.
- The cancel-tiffin "StartDate cannot be in the past" check compares against the
  server's UTC date, while the UI's date picker defaults to the browser's local
  date. They can disagree for users well behind UTC (harmless for an IST-based
  deployment, since IST is ahead of UTC).
- No automated tests cover the wwwroot pages -- they haven't been run in a
  browser yet either (this was built without dotnet/a browser available in the
  build environment). Next step is to run the app and click through each screen.

---

## Sabil Number, "Thaali" rebrand, sub-families & Delete Thaali Size (2026-09-24)

Six related changes requested together. **Migrations to run, in order, against the
dev DB (and later production) before the app will work against this code:**
`db/migrations/012_add_sabil_number_to_users.sql`,
`db/migrations/013_rename_tiffin_to_thaali.sql`,
`db/migrations/014_add_sub_families.sql`. As with 000-011, these are
forward-only -- 000-011 (and the original design doc) still say "Tiffin"
throughout and are deliberately left unedited; a brand-new install runs
000-011 followed by 012-014.

**1. Sabil Number.** Every family now carries a `Users.SabilNumber VARCHAR(50)
NULL UNIQUE` (migration 012) -- the identifier the Community Head assigns to a
family, entered at registration. Nullable (an Admin account typically has none;
MySQL's UNIQUE index allows multiple NULLs) but unique whenever set, enforced
three ways: the `UQ_Users_SabilNumber` DB constraint, an explicit pre-check in
both `FamiliesController.Register` and `UsersController.Create`/`Update` (via
new `IUserRepository.GetBySabilNumberAsync`), and a `MySqlException`(1062)
catch as a race-condition backstop, matching the existing duplicate-email
pattern. `RegisterFamilyRequest.SabilNumber` is required (non-nullable,
`BadRequest` if blank). Wired into `family/register.html` (new required
field), `family/dashboard.html` (info card, via `CK.session`), and the
`admin/registrations.html` / `admin/families.html` list views (via the
existing `usersById` map). There's no admin "edit an existing user's Sabil
Number" screen yet -- `UsersController.Update` supports it server-side, but no
page calls it; a dedicated admin Users page would be the natural place if
that's needed later.

**2. Tiffin -> Thaali rename.** Renamed throughout: tables (`TiffinSizes` ->
`ThaaliSizes`, `TiffinCancellations` -> `ThaaliCancellations`,
`Families.TiffinSizeId`/`FamilySizeHistory.TiffinSizeId` -> `ThaaliSizeId`),
all C# (`Controllers/ThaaliSizesController.cs`,
`Controllers/ThaaliCancellationsController.cs`, matching Models/Dtos/
Repositories/Interfaces, and all their tests), and all `wwwroot` HTML/JS
(including the renamed `admin/tiffin-sizes.html` -> `admin/thaali-sizes.html`,
with every internal link -- dashboard tile, nav dropdown -- updated to match).
Migration 013 handles the DB side via `RENAME TABLE`/`CHANGE COLUMN`/FK
drop+recreate only (no `RENAME INDEX` or CHECK-constraint renaming, since
those need MySQL 5.7+/8.0.16+ respectively and this has to run on 5.6 too) --
a few constraint/index names (`UQ_TiffinSizes_Name` etc.) still say "Tiffin"
internally; purely cosmetic, doesn't affect behavior. `grep -rli tiffin` over
all source (excluding `bin`/`obj`/`.vs`) comes back empty.

**3. Delete a Thaali Size.** `DELETE /api/thaalisizes/{id}` (new
`ThaaliSizesController.Delete`) returns `204` if the size is unused, `409
Conflict` with a friendly message if any family (current, via `Families`, or
historical, via `FamilySizeHistory`) ever referenced it, `404` if the id
doesn't exist. Backed by `IThaaliSizeRepository.IsInUseAsync`/`DeleteAsync`.
`admin/thaali-sizes.html` got a Delete button per row and a Bootstrap
confirmation modal (not a native `confirm()`) that surfaces the 409 message
inline. This also gives a path to clean up the leftover `UT_...` test rows
visible in the admin screenshot that prompted this work, once the app is
running.

**4. Sub-families.** For the "one family sometimes needs more than one
thaali" case (e.g. a married child in a nearby flat/building). Modeled as
*another row in `Families`* rather than a new table (migration 014):
`Families.ParentFamilyId` (nullable, self-referencing FK) plus
`Families.SubFamilyLabel` (nullable, since a sub-family has no `Users` row to
name it from); `Families.FamilyHeadUserId` had to become nullable to allow
this. A sub-family row has `ParentFamilyId` set and `FamilyHeadUserId = NULL`
(no login of its own -- managed by the primary family's head/an Admin); a
primary family always has `ParentFamilyId = NULL`. Because
`ThaaliCancellations` is keyed by `FamilyId` and `ReportRepository`'s daily
count query selects directly from `Families`, sub-families flow through
cancellations and the daily thaali-count report with **zero further code
changes** -- this was the deciding factor over a separate table.
`IFamilyRepository.GetAllAsync` now filters `WHERE ParentFamilyId IS NULL` so
sub-families don't show up as extra top-level registrations; new
`GetSubFamiliesAsync`/`CreateSubFamilyAsync` plus
`GET`/`POST /api/families/{id}/sub-families` expose them. Admin-only by
design (per the request) -- `CreateSubFamily` checks `CreatedByAdminUserId`
resolves to a user with `RoleId == RoleIds.Admin`; there's still no real
auth, so this is an application-layer check like everything else, not a
security boundary. Created already `Approved`/active (an Admin is vouching
for it directly, so no Pending review queue). UI: `admin/families.html` got a
"Sub-Families" button per approved family opening a modal that lists linked
sub-families and a form to add one. **Not yet built:** anything on the
Family Head's own dashboard for sub-families (viewing them, cancelling a
thaali on their behalf) -- the request scoped this as Admin-only, but if
Family Heads should eventually see/manage their linked sub-families too,
that's a `family/dashboard.html` addition, not an API one (the endpoints
already support reading them).

**5. Rebrand.** "FaizMawaid" (the one-word display string) replaced with
"Faiz al-Mawaid al-Burhaniyah" in every user-facing spot: all `<title>` tags,
the navbar brand (`site.js`'s `CK.nav.render`, one shared function for every
page), and every page footer -- the footer instance is an actual hyperlink to
`https://www.thedawoodibohras.com/faiz-al-mawaid-al-burhaniyah/` (new
`.ck-footer-link` style in `site.css`, opens in a new tab). Deliberately
**not** renamed: the .NET solution/project/namespace (`FaizMawaid` stays as
the C# identifier and folder/file name throughout) -- that's internal
plumbing, not user-facing text, and renaming it would be a much bigger,
riskier change for no user-visible benefit. Added a small `@media
(max-width: 767.98px)` rule shrinking the navbar brand font, since the new
name is considerably longer than the old one-word brand and needs to fit on
phone-width screens.

**6. Tests.** All of the above is covered by new/updated xUnit tests:
`FamiliesControllerTests` (SabilNumber validation + duplicate rejection,
sub-family create/list including the not-an-Admin and
sub-family-of-a-sub-family rejection cases; its `CreateController` helper
now also takes a `Mock<IUserRepository>`, a compile-breaking change from the
new constructor parameter), `ThaaliSizesControllerTests`/
`ThaaliSizeRepositoryTests` (delete success/conflict/not-found,
`IsInUseAsync` true/false), `FamilyRepositoryTests` (sub-family creation
end-to-end against the real DB, `GetAllAsync` excluding sub-families). Also
fixed two now-latent test bugs while doing this: `TestDataHelper
.CreateFamilyAsync` was leaving `SabilNumber` at its DTO default of `""`,
which would've collided across tests under the new UNIQUE constraint --
it now generates a unique `UT_...` value per call (new
`TestDataHelper.RandomSabilNumber()` helper); and
`TestDataHelper.DeleteFamilyAsync`'s `familyHeadUserId` parameter became
nullable to support deleting sub-family test rows (which have none).

**Everything above was written and reasoned through without a compiler or
browser available in the build environment (same constraint as the rest of
this project's AI-assisted work) -- run `dotnet build`/`dotnet test` in
Visual Studio to confirm before relying on it, especially the constructor
signature changes.**

## Meal Plan Templates & Token-Based Authentication (2026-09-24)

**1. Meal Plan Templates.** A reusable "canned" meal-description library so
Admin doesn't retype the same recurring meal (e.g. "Khichdi & Kadhi") over
and over. New `MealPlanTemplates` table (migration `015`: `Id`, `Name`
UNIQUE, `MealDescription`, `SortOrder`, `CreatedByUserId`, timestamps) with
the standard Model/DTO/Repository/Controller layers
(`MealPlanTemplatesController`, Admin-only) and full CRUD tests
(`MealPlanTemplatesControllerTests`, `MealPlanTemplateRepositoryTests`). A
template's text is *copied* into `MealPlans.MealDescription` as free text --
there's no FK from `MealPlans` to `MealPlanTemplates` -- so deleting a
template never affects any meal plan already built from it, and template
deletion is unconditional (unlike Thaali Sizes, which has an in-use guard).
New `admin/meal-plan-templates.html` management page (same
list/add/edit/delete pattern as `admin/thaali-sizes.html`). On
`admin/meal-plans.html`, templates render as small draggable "chip" buttons
above the meal-description textarea (both the Add form and the Edit modal):
drag-and-drop (HTML5 `draggable`/`dragstart`/`dragover`/`drop`) inserts a
template's text into the textarea, appended with `", "` if there's already
text there; clicking a chip does the same thing as a fallback for anyone who
doesn't want to drag. Capped at the textarea's existing 500-char limit.

**2. Token-based authentication -- full rollout.** Every existing user had a
meaningless placeholder password (no real auth existed before today); this
replaces that with real password hashing and JWT bearer tokens, and locks
down **every** existing API endpoint in the same pass (the user explicitly
chose "full rollout now" over a staged fast-follow when asked).

- **Password hashing**: `Services/PasswordHasherService.cs` wraps ASP.NET
  Core Identity's `PasswordHasher<T>` (PBKDF2) standalone -- no full
  Identity system, no `IdentityUser`, no EF store; just the hashing
  algorithm. `Users.PasswordHash` now holds a real hash, never plaintext.
- **Tokens**: `Services/TokenService.cs` issues short-lived JWT access
  tokens (HMAC-SHA256, `Jwt:AccessTokenMinutes` = 30 by default) carrying
  `NameIdentifier`/`Email`/`Name`/`Role` claims, plus long-lived opaque
  refresh tokens (`Jwt:RefreshTokenDays` = 14 by default) -- only the
  refresh token's SHA-256 hash is ever persisted (`RefreshTokens` table,
  already anticipated by migration `008`), the raw value is a bearer secret
  and is never stored. `Program.cs` wires up `AddAuthentication().AddJwtBearer(...)`
  reading `Jwt:Key`/`Issuer`/`Audience` from config, plus
  `app.UseAuthentication()` before the existing `app.UseAuthorization()`.
- **`Users.MustChangePassword`** (migration `016`, defaults to 1 so every
  pre-existing placeholder-password row is flagged): 0 when someone chose
  their own password (self-registration, the one-time bootstrap-admin, a
  self-service change), 1 when an Admin set/reset it on their behalf -- the
  UI forces a change at next login while this is true.
- **`AuthController`** (`/api/auth/*`, all new): `POST login`, `POST
  refresh` (rotates the refresh token -- the old one is revoked and
  `ReplacedByTokenHash` points at the new one), `POST logout`, `POST
  change-password` (`[Authorize]`, reads the caller's id from the JWT's
  `NameIdentifier` claim -- never trusts a client-supplied user id), `POST
  admin-reset-password` (`[Authorize(Roles=Admin)]`), and `POST
  bootstrap-admin` (`[AllowAnonymous]` but self-limiting: refuses with 409
  once *any* Admin already exists, solving the chicken-and-egg problem of
  the very first Admin account needing to be created before anyone can log
  in to create it the normal way).
- **Password reset approach**: Admin-initiated only (the user's explicit
  choice) -- an Admin sets/resets any user's password from
  `admin/users.html`; there's no email sender configured, so no
  "forgot password" self-service flow exists. `MustChangePassword` is forced
  true on an admin reset so the real owner picks their own at next login.
- **Authorization on every existing controller**: `RoleNames` (string
  constants `"Admin"`/`"FamilyHead"`, for `[Authorize(Roles=...)]`, which
  matches by claim string, not the numeric `RoleIds`) is used throughout.
  Final map: `AppSettingsController`, `AuditLogsController`,
  `ReportsController`, `UsersController` -- Admin-only, no per-action
  overrides. `ThaaliSizesController` -- Admin-only, **except** `GetAll`/
  `GetById` stay `[AllowAnonymous]` (the anonymous `family/register.html`
  page needs the size dropdown). `FamiliesController` -- Admin-only, except
  `Register` (`[AllowAnonymous]`) and `GetById`/`ChangeThaaliSize`/
  `GetSizeHistory` (`[Authorize]`, any signed-in user -- a Family Head needs
  these for their own dashboard). `MealPlansController`/
  `NonServingDaysController` -- `[Authorize]` by default (both areas read
  these), `Create`/`Update`/`Delete` (`MealPlans`) and `Create`/`Delete`
  (`NonServingDays`) overridden to Admin-only. `RolesController`,
  `ThaaliCancellationsController` -- `[Authorize]`, any signed-in user (no
  admin-only actions in either). `MealPlanTemplatesController` -- Admin-only
  (set when the controller was built, same pass).
- **Why this was safe for the 70+ pre-existing Moq-based controller unit
  tests**: `[Authorize]` attributes are pure metadata -- the MVC
  authorization filter pipeline only runs for real HTTP requests, never when
  a test instantiates a controller and calls an action method directly. None
  of the pre-existing controllers read `HttpContext.User`/claims in their
  action bodies (that would've broken under direct instantiation, since
  `ControllerContext.HttpContext` is null by default there); claims-reading
  is confined to the brand-new `AuthController.ChangePassword`, which has
  its own tests that set up `ControllerContext`/`HttpContext` properly.
- **Client-side (`wwwroot/js/site.js`)**: `CK.session` now stores the full
  JWT/refresh-token pair plus profile fields per area (`ck_family_session`/
  `ck_admin_session` in `localStorage`) -- this is the real security
  boundary now, not just a UI convenience. `CK.api.call` attaches
  `Authorization: Bearer <token>` automatically (based on whether the
  current page is under `/family/` or `/admin/`), and on a 401 transparently
  refreshes the access token once via `/api/auth/refresh` and retries the
  original request before giving up and redirecting to that area's login
  page; concurrent 401s share a single in-flight refresh call. New
  `CK.auth` namespace wraps the `/api/auth/*` endpoints. `family/login.html`,
  `admin/login.html` (including its "create first Admin" form, now posting
  to `bootstrap-admin`), and `family/register.html` all rewritten to collect
  a real password and use these instead of the old client-side
  email-lookup hack (`GET /api/users?roleId=...` + linear search), which no
  longer works now that `UsersController` is Admin-only.
- **Change Password UI**: a shared modal (`CK.changePassword`, injected into
  the page by `site.js`, reachable from a "Change Password" item in both
  navbars' user dropdown) posts to `/api/auth/change-password`. When the
  signed-in session has `mustChangePassword: true`, the modal opens
  automatically in a non-dismissable "forced" mode (no close/cancel, static
  backdrop) with an explanatory notice, right after the navbar renders.
- **New `admin/users.html`**: lists every Admin + Family Head account
  (filterable by role) with Activate/Deactivate and a "Reset Password"
  action per row (`POST /api/auth/admin-reset-password`), plus badges
  showing whether a password is Admin-set (pending change) or self-chosen.
  Linked from the admin navbar's Families dropdown as "Users & Passwords".
- **Tests**: `PasswordHasherServiceTests` (hash/verify roundtrip, wrong
  password fails, same password salts differently), `TokenServiceTests`
  (JWT claims/issuer/expiry, missing-key throws, refresh token hash
  matches, tokens are unique per call), `AuthControllerTests` (login
  success/unknown-email/wrong-password/deactivated, refresh success (and
  that it rotates)/expired/revoked/unknown, change-password
  success/wrong-current-password/no-identity, bootstrap-admin
  first-allowed/conflict-if-one-exists/short-password, admin-reset-password
  success/not-found/short-password).

**Known follow-ups / deliberate scope decisions -- read before relying on
this in production:**

- **No per-family resource-ownership (row-level) checks yet.** Endpoints
  like `GET /api/families/{id}` or `PUT /api/thaalicancellations/...` are
  `[Authorize]` (any signed-in user), not scoped to "only the Family Head
  who owns this record" -- a signed-in Family Head could technically call
  another family's endpoints if they guessed/enumerated an id. Closing this
  needs claims-based resource checks inside each action (comparing the
  caller's id/familyId from their JWT against the record being accessed),
  which was deliberately out of scope for this pass (see the "why this was
  safe for existing tests" note above -- adding that now would need real
  `HttpContext`/`ControllerContext` setup added to every existing
  controller test too). Flagging this explicitly so it isn't mistaken for
  an oversight.
- **`Jwt:Key` in `appsettings.json` is a placeholder and is NOT safe to
  deploy as-is** -- it says so in the value itself. Replace it with a real,
  long random secret (an environment variable or `dotnet user-secrets`,
  never committed to source control) before deploying anywhere real users
  can reach.
- **Two new NuGet package versions are unverified**
  (`Microsoft.AspNetCore.Authentication.JwtBearer` and
  `Microsoft.Extensions.Identity.Core`, both pinned to `8.0.8` in
  `FaizMawaid.csproj`) -- this build environment has no NuGet/internet
  access to confirm they resolve; if Visual Studio's NuGet manager suggests
  a different compatible 8.0.x patch, that's expected and fine.
- **`GET /api/users` and `GET /api/users/{id}` still return `PasswordHash`**
  in the JSON body (a pre-existing `User` model/DTO choice, not introduced
  today) -- harmless in that only Admins can call these endpoints now, but
  worth trimming to a response DTO that omits it in a future pass.
- **Nothing in this pass was compiled** -- same constraint as every other
  change in this project's AI-assisted history (no `dotnet` CLI or NuGet
  access in the build environment). `node --check` was used to syntax-check
  every touched inline `<script>` block, and every touched `.cs` file was
  read back and brace/paren-balance-checked, but **run `dotnet build` and
  `dotnet test` in Visual Studio before relying on any of this**,
  especially the `Program.cs` DI wiring, the JWT middleware configuration,
  and the two new package versions.

## Family login stale-session bug + Add User feature (2026-09-25)

- **Fixed: Family Head login silently bounced to a broken dashboard.**
  `family/login.html` had a "skip to dashboard if already signed in"
  shortcut that only checked whether *some* `familyId`/`accessToken` was
  cached in `localStorage` -- it never validated that data was still real.
  A browser holding a stale cached session (e.g. from a test account that
  was later deleted, such as the userid=43 cleanup done this same day)
  would get redirected straight past the login form to `dashboard.html`,
  which then 404'd fetching a family that no longer existed and showed a
  blank page with a "Could not load your family details." toast -- with no
  way back to a real login. Fixed in two places: `dashboard.html` now
  detects a 404/401 on the initial family fetch, clears the stale session,
  and redirects to `family/login.html?expired=1`; `login.html` shows a
  "Your session has expired, please sign in again" message for that case.
  This self-heals for anyone currently stuck -- no DB changes needed.
- **Confirmed, not a bug**: the app has no per-family-member roster table
  and never did -- the only member-related field is `Families.NumberOfMembers`
  (a headcount used for Thaali sizing), shown on the Family Details card
  alongside the Family Head's own profile fields. The user confirmed no
  change is needed here after this was pointed out.
- **`admin/users.html`** now has an "Add User" button/modal (role, full
  name, email, phone, Sabil Number, password) so an existing Admin can
  create additional Admin or Family Head login accounts -- previously the
  only way to get a 2nd Admin account was the one-time
  `bootstrap-admin` endpoint, which is deliberately self-limiting and
  refuses once any Admin exists (that refusal is correct behavior, not a
  bug -- it's what stops an anonymous visitor from self-signing-up as an
  extra Admin).
- Also fixed two of my own earlier mistakes reported via build/load errors:
  an illegal `--` inside an XML `<!-- -->` comment in `FaizMawaid.csproj`
  (Visual Studio refused to load the project), and a missing
  `using FaizMawaid.Models;` in `ReportsController.cs` (`CS0103: RoleNames
  does not exist`). Swept the whole solution afterwards for the same class
  of missing-using bug; no other files were affected.

## Family-login authorization bug (real root cause) + Bulk Upload features (2026-09-26)

- **Fixed the real cause of the Family Head dashboard failure** (the 2026-09-25 fix
  only papered over a symptom). Reproduced live via a browser: login succeeded, but
  `GET /api/families/{id}` returned **403 Forbidden**, not 404/401. Root cause: ASP.NET
  Core combines a class-level `[Authorize]` with a method-level one using **AND, not
  override**. `FamiliesController` had `[Authorize(Roles = RoleNames.Admin)]` at the
  class level, and `GetById`/`ChangeThaaliSize`/`GetSizeHistory` each additionally
  carried a bare `[Authorize]` intending to open just those three to any signed-in
  user -- but that never worked; the class's Roles=Admin requirement still applied on
  top, so every Family Head hitting their own dashboard has been getting a 403 since
  the auth rollout went in. **Fix**: the class-level attribute is now a bare
  `[Authorize]` (any signed-in user), and every action that must stay Admin-only
  (`GetAll`, `Approve`, `Reject`, `Update`, `Deactivate`, `Activate`, `GetSubFamilies`,
  `CreateSubFamily`, and the new `BulkImport` below) now carries its own explicit
  `[Authorize(Roles = RoleNames.Admin)]`. Checked every other controller in the
  solution for the same class+method `[Authorize]` combination pattern --
  `FamiliesController` was the only one affected (`MealPlansController`/
  `NonServingDaysController` use the *opposite*, correct direction: a bare class-level
  `[Authorize]` with `[Authorize(Roles=Admin)]` added on top of specific mutating
  actions, which combines correctly).
- **Bulk Upload Meals** (`admin/meal-plans.html`): a "Bulk Upload" button opens a modal
  with a "Download Template" button (generates a 2-column .xlsx --Date, Meal
  Description -- entirely client-side via SheetJS, with an Instructions sheet) and a
  file upload that parses client-side, shows a preview with per-row validation
  problems flagged before anything is submitted, then posts the valid rows to new
  `POST /api/mealplans/bulk-import` (Admin-only). Each row is validated and saved
  independently -- a Sunday/non-serving-day/duplicate-in-file date is skipped with a
  reason rather than failing the whole batch; a date that already has a meal planned
  is overwritten (Status "Updated") rather than rejected, since re-uploading a
  corrected sheet is an expected use case. Results (created/updated/skipped counts +
  per-row reasons) are shown in the same modal.
- **Bulk Import Families** (`admin/families.html`): same pattern -- a "Bulk Import"
  button, a generated .xlsx template (Full Name, Email, Phone, Sabil Number, Address,
  Number of Members, Thaali Size [matched by name, not id], Password), client-side
  preview/validation, then `POST /api/families/bulk-import` (Admin-only). New
  `IFamilyRepository.ImportApprovedFamilyAsync` mirrors `RegisterAsync`'s
  User+Family+FamilySizeHistory transaction, but creates the Family **already Approved
  and active** (skipping the Pending queue) and forces `MustChangePassword = 1` --
  the user explicitly chose this over a Pending-queue-per-import when asked, matching
  how Sub-Families already work ("the Admin is vouching for every row by uploading
  it"). Duplicate email/Sabil Number (against the DB, or against another row in the
  same file) is skipped with a reason, not a batch failure.
- **Password-generation tip for bulk-imported members** (also given to the user
  directly in chat): since every imported account has `MustChangePassword = 1` and is
  forced to change it at first login, the initial password only needs to be
  communicable, not memorable long-term or individually strong. The template's
  Instructions sheet suggests: first 4 letters of the person's name + last 4 digits of
  their Sabil Number + "!" (e.g. "Fatema Rangwala", Sabil ...4567 -> "Fate4567!") --
  easy to write on a printed handout, and importantly *different per person* (never
  reuse one password across the whole batch, since anyone could sign in as anyone else
  before the real owner changes it).
- **New shared client helper**: `CK.excel` in `site.js` (`downloadTemplate`,
  `readRows`, `parseDateCell`) -- wraps SheetJS (loaded via CDN only on the two pages
  that need it, `https://cdn.jsdelivr.net/npm/xlsx@0.18.5/dist/xlsx.full.min.js`, not
  globally) so both bulk-upload features share the same "generate a .xlsx template"/
  "parse an uploaded sheet by header name, not column position" logic.
- Nothing in this pass was compiled (same constraint as always) -- `node --check` was
  used on every touched inline `<script>` block and every touched `.cs` file was
  brace/paren-balance-checked, but **rebuild and test in Visual Studio**, especially
  the authorization fix (confirm a Family Head dashboard actually loads now) and both
  new bulk-import flows end to end (including a deliberately bad row in each template
  to confirm the per-row skip-with-reason behavior).

## Meal Calendar screen (2026-09-26, later same day)

- New `admin/meal-calendar.html`: a month-grid view (Sun-Sat columns, one row per
  week, previous/next/Today navigation) built entirely from existing endpoints --
  `GET /api/mealplans?from=&to=` for the visible date range and
  `GET /api/nonservingdays` (no year filter -- the table is small, so it's simplest to
  fetch everything once and filter client-side across a month boundary that might
  span two years) -- no backend changes needed. Each day cell shows: a meal's
  description + an "Edit" link (`meal-plans.html?edit=<id>`) if one is planned; a red
  "X" with the reason (Sunday, or a NonServingDays row's Reason) if the kitchen is
  closed that day; or a muted "Not planned -- Add" link (`meal-plans.html?date=<iso>`)
  for a serving day with nothing entered yet. Today's cell is outlined; padding days
  from the adjacent month are dimmed.
- `admin/meal-plans.html` gained deep-link handling for those two links: `?edit=<id>`
  fetches that meal and opens the existing Edit modal directly (reusing the same
  modal/template-chips code, no duplication); `?date=<iso>` prefills the "Plan a Meal"
  date field and scrolls to it. The query string is cleared via
  `history.replaceState` right after reading it, so a page refresh doesn't reopen the
  modal.
- Linked from the "Kitchen Setup" nav dropdown (first item, above "Meal Plans") and
  from a new dashboard tile.
- New calendar-grid CSS added to `site.css` (`.ck-cal-*`), including a mobile
  fallback (`@media (max-width: 700px)`) that collapses the 7-column grid into a
  single-column vertical agenda list, since a 7-column mini-calendar doesn't work well
  on a phone.
- Purely additive and front-end-only (no controller/repository/DTO changes) --
  syntax-checked with `node --check`, div-balance-checked, not run in a real browser.

## Stale-browser-cache bug + Family Feedback feature + dashboard reorder (2026-09-26, later same day)

- **Root cause of "I can't see the new bulk-upload templates" report**: not a code bug --
  `js/site.js` and `css/site.css` were referenced by every page with no cache-busting
  query string, so a browser that had loaded site.js before the Excel bulk-upload code
  was added kept serving that stale cached copy indefinitely (confirmed by diffing the
  live browser pane's cached copy against a `?cachebust=` fetch of the same URL --
  the server was always serving the current file; only the browser cache was stale).
  **Fix**: every `<script src=".../site.js">` and `<link href=".../site.css">` tag
  across all 18 HTML pages now carries a version query string (`?v=20260926b`, bumped
  again this same session after the Feedback JS changes below) so a browser is forced
  to refetch whenever that shared file actually changes. If a future change to
  site.js/site.css doesn't seem to show up for the user, bump this version string
  again rather than assuming the code itself is wrong.
- **New Family Feedback feature**: a private message thread between one family and
  the Admin team. New `Feedback` table (migration `017`: FamilyId, CreatedByUserId,
  Message, Status ENUM('Open','Responded'), ResponseText, RespondedByAdminUserId,
  RespondedAt, CreatedAt) with the standard Model/DTO/Repository/Controller layers
  (`FeedbackController` -- bare class-level `[Authorize]` with explicit
  `[Authorize(Roles = Admin)]` on `GetAll`/`GetOpenCount`/`Respond`, following the
  corrected pattern from the earlier authorization-bug fix). A Family Head posts
  `POST /api/feedback`; sees their own history via `GET /api/feedback/family/{id}`;
  an Admin sees everyone's via `GET /api/feedback` (optional `?status=Open`) and
  responds via `PUT /api/feedback/{id}/respond` (can also revise a response later).
  `GET /api/feedback/open-count` backs both the admin dashboard tile and a small red
  badge next to "Family Feedback" in the admin nav dropdown. New
  `admin/feedback.html` page (card list, filter by status, a Respond/Edit-Response
  modal). `family/dashboard.html` gained a Feedback section (submit form + own
  history with any Admin response shown inline) and a "Feedback" nav link.
- **Family dashboard reordered**: Upcoming Meals and the Cancel-a-Thaali/My
  Cancellations row are now at the top; Family Details (head info, thaali size,
  size-history) moved to the very bottom, per request. A small top-right card now
  appears -- only when relevant -- listing any usual serving weekday (Mon-Sat) in the
  next 14 days that's a declared Non-Serving Day, so a family can see at a glance
  that a meal isn't coming and why, without having to notice a gap in the meal list.
  Built entirely from the existing `GET /api/nonservingdays` endpoint, filtered
  client-side -- no new backend endpoint needed for this part.
- Nothing in this pass was compiled (same constraint as always) -- `node --check` on
  every touched inline `<script>`, div-balance and brace/paren-balance checks on every
  touched file. None of it has been exercised in a real browser yet this round.
