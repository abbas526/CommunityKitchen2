USE CommKitchen;

-- Sub-families: an additional household (e.g. a married child staying in a nearby
-- flat/building) that an Admin links to an existing primary family so it can take
-- its own Thaali, while still being reportable alongside the primary family.
--
-- Modeled as another row in Families rather than a new table, via a nullable
-- self-referencing ParentFamilyId:
--   - A sub-family row has ParentFamilyId = the primary family's Id, and
--     FamilyHeadUserId = NULL (it has no login of its own -- the primary
--     family's head manages it). SubFamilyLabel identifies it since there's
--     no User row to derive a name from.
--   - A primary family always has ParentFamilyId = NULL.
-- This means ThaaliCancellations (keyed by FamilyId) and the daily Thaali-count
-- report (which selects directly from Families) support sub-families with no
-- further schema or query changes -- each sub-family is just another Families row.
--
-- FamilyHeadUserId must become nullable to allow this; the existing
-- UQ_Families_FamilyHeadUserId unique index already permits multiple NULLs under
-- MySQL, so many sub-families can coexist without conflict.

ALTER TABLE Families
    MODIFY COLUMN FamilyHeadUserId BIGINT UNSIGNED NULL;

ALTER TABLE Families
    ADD COLUMN ParentFamilyId BIGINT UNSIGNED NULL AFTER FamilyHeadUserId,
    ADD COLUMN SubFamilyLabel VARCHAR(150) NULL AFTER ParentFamilyId;

ALTER TABLE Families
    ADD CONSTRAINT FK_Families_ParentFamily FOREIGN KEY (ParentFamilyId) REFERENCES Families(Id);

CREATE INDEX IX_Families_ParentFamilyId ON Families(ParentFamilyId);
