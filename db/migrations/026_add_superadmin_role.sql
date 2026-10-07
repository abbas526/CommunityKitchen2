USE CommKitchen;

-- SuperAdmin role (2026-10-07; see claude/SuperAdmin-Plan.md).
-- A SuperAdmin manages everything an Admin can, plus every Admin/SuperAdmin account,
-- App Settings and the Audit Log. At most 2 ACTIVE SuperAdmins may exist -- that limit is
-- enforced in code (UserRepository, inside a transaction), not by this table.
-- Re-runnable: does nothing if Roles row 3 already exists.
INSERT INTO Roles (Id, Name)
SELECT 3, 'SuperAdmin' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM Roles WHERE Id = 3);
