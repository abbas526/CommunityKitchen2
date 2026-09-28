USE CommKitchen;

-- Renames "Tiffin" -> "Thaali" throughout, per the updated terminology.
-- This is an in-place rename migration for a database that already ran
-- 000-012 with the old names. A brand-new install can just run 000-012
-- (still Tiffin-named) followed by this file -- there's no need to go back
-- and edit the historical 003/004/005/007 migration files themselves.
--
-- Only RENAME TABLE / CHANGE COLUMN / DROP+ADD FOREIGN KEY are used below --
-- all supported on MySQL 5.6.49, 5.7.38 and MariaDB 10.6 alike (GoDaddy's
-- hosting versions). RENAME INDEX (5.7+ only) and CHECK constraint renaming
-- (8.0.16+ only) are deliberately skipped for that reason -- the handful of
-- constraint/index names still containing "Tiffin" after this
-- (UQ_TiffinSizes_Name, CK_TiffinCancellations_DateRange, and similar) are
-- purely cosmetic leftovers and don't affect behavior at all.

RENAME TABLE TiffinSizes TO ThaaliSizes;
RENAME TABLE TiffinCancellations TO ThaaliCancellations;

-- Families.TiffinSizeId -> Families.ThaaliSizeId
ALTER TABLE Families
    DROP FOREIGN KEY FK_Families_TiffinSize;
ALTER TABLE Families
    CHANGE COLUMN TiffinSizeId ThaaliSizeId TINYINT UNSIGNED NOT NULL;
ALTER TABLE Families
    ADD CONSTRAINT FK_Families_ThaaliSize FOREIGN KEY (ThaaliSizeId) REFERENCES ThaaliSizes(Id);

-- FamilySizeHistory.TiffinSizeId -> FamilySizeHistory.ThaaliSizeId
ALTER TABLE FamilySizeHistory
    DROP FOREIGN KEY FK_FamilySizeHistory_TiffinSize;
ALTER TABLE FamilySizeHistory
    CHANGE COLUMN TiffinSizeId ThaaliSizeId TINYINT UNSIGNED NOT NULL;
ALTER TABLE FamilySizeHistory
    ADD CONSTRAINT FK_FamilySizeHistory_ThaaliSize FOREIGN KEY (ThaaliSizeId) REFERENCES ThaaliSizes(Id);

-- ThaaliCancellations' foreign keys still carry their old "TiffinCancellations"-era
-- names after the table rename (MySQL doesn't rename constraints automatically) --
-- refreshed here for clarity, though purely cosmetic.
ALTER TABLE ThaaliCancellations
    DROP FOREIGN KEY FK_TiffinCancellations_Family,
    DROP FOREIGN KEY FK_TiffinCancellations_CreatedBy,
    DROP FOREIGN KEY FK_TiffinCancellations_ReinstatedBy;
ALTER TABLE ThaaliCancellations
    ADD CONSTRAINT FK_ThaaliCancellations_Family FOREIGN KEY (FamilyId) REFERENCES Families(Id),
    ADD CONSTRAINT FK_ThaaliCancellations_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id),
    ADD CONSTRAINT FK_ThaaliCancellations_ReinstatedBy FOREIGN KEY (ReinstatedByUserId) REFERENCES Users(Id);
