USE CommKitchen;

-- =====================================================================
-- ONE-TIME script: promotes ONE existing Admin account to SuperAdmin.
-- (SuperAdmin plan, 2026-10-06 -- see claude/SuperAdmin-Plan.md)
--
-- !! DO NOT RUN THIS UNTIL THE SUPERADMIN CODE IS BUILT AND DEPLOYED !!
-- Today's app only understands the roles Admin and FamilyHead. If this is
-- run first, the promoted account would be rejected by admin/login.html and
-- by every [Authorize(Roles = "Admin")] endpoint, locking that person out
-- of the admin area. (If that happens, use the ROLLBACK block at the bottom.)
--
-- Safe to re-run: it changes nothing if the account is already a SuperAdmin.
-- It will refuse (0 rows changed) when:
--   * no ACTIVE Admin has that email, or
--   * there are already 2 ACTIVE SuperAdmins (the maximum), or
--   * the Roles table isn't 1 = Admin and 3 = SuperAdmin (see STEP 3a).
-- =====================================================================

-- >>> STEP 1: put the login email of the Admin to promote between the quotes.
SET @superadmin_email = 'PUT-ADMIN-EMAIL-HERE';

-- STEP 2: make sure the SuperAdmin role exists (no-op if it already does).
INSERT INTO Roles (Id, Name)
SELECT 3, 'SuperAdmin' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 3);

-- STEP 3: look before you leap.
-- (a) Roles should read: 1 = Admin, 2 = FamilyHead, 3 = SuperAdmin. If not, STOP -- the
--     promotion in STEP 4 deliberately does nothing unless 1 = Admin and 3 = SuperAdmin.
SELECT Id, Name FROM Roles ORDER BY Id;
-- (b) This should show exactly one row for your account, with RoleId = 1 (Admin).
SELECT Id, Email, FullName, RoleId, IsActive
FROM Users
WHERE Email = @superadmin_email;

-- STEP 4: promote. Only an ACTIVE Admin (RoleId 1) is eligible, and only while
-- fewer than 2 ACTIVE SuperAdmins exist. (The inner SELECT is wrapped in a derived
-- table because MySQL won't read the table it is updating directly.)
UPDATE Users
SET RoleId = 3
WHERE Email = @superadmin_email
  AND RoleId = 1
  AND IsActive = 1
  AND EXISTS (SELECT 1 FROM Roles WHERE Id = 1 AND Name = 'Admin')
  AND EXISTS (SELECT 1 FROM Roles WHERE Id = 3 AND Name = 'SuperAdmin')
  AND (SELECT COUNT(*) FROM (SELECT Id FROM Users WHERE RoleId = 3 AND IsActive = 1) AS current_superadmins) < 2;

-- Expect rows_promoted = 1. If it is 0, re-read the STEP 3 output and the notes at the top.
SELECT ROW_COUNT() AS rows_promoted;

-- STEP 5: confirm. The account should now show RoleId = 3 / RoleName = SuperAdmin.
SELECT u.Id, u.Email, u.FullName, u.RoleId, r.Name AS RoleName, u.IsActive
FROM Users u
JOIN Roles r ON r.Id = u.RoleId
WHERE u.RoleId IN (1, 3)
ORDER BY u.RoleId DESC, u.Id;

-- ---------------------------------------------------------------------
-- ROLLBACK (leave commented; run only if you need to undo the promotion,
-- e.g. you ran it before the app code was deployed and are locked out):
-- ---------------------------------------------------------------------
-- UPDATE Users SET RoleId = 1 WHERE Email = @superadmin_email AND RoleId = 3;
