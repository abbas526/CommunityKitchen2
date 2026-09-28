USE CommKitchen;

-- The core "save food" record: a Family Head (or an Admin, on their behalf)
-- cancelling a day or a date range. A family is assumed to want their
-- tiffin on any serving day unless an Active row here covers that date.
-- NOTE: the CK_TiffinCancellations_DateRange CHECK constraint below is only
-- enforced on MySQL 8 / MariaDB 10.6+, not on MySQL 5.6/5.7 (GoDaddy's
-- shared-hosting versions parse but silently ignore CHECK) -- validate
-- EndDate >= StartDate in the application layer too, don't rely on this alone.
CREATE TABLE TiffinCancellations (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    FamilyId BIGINT UNSIGNED NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    Reason VARCHAR(300) NULL,
    Status ENUM('Active','Reinstated') NOT NULL DEFAULT 'Active',
    CreatedByUserId BIGINT UNSIGNED NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ReinstatedAt DATETIME NULL,
    ReinstatedByUserId BIGINT UNSIGNED NULL,
    CONSTRAINT FK_TiffinCancellations_Family FOREIGN KEY (FamilyId) REFERENCES Families(Id),
    CONSTRAINT FK_TiffinCancellations_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id),
    CONSTRAINT FK_TiffinCancellations_ReinstatedBy FOREIGN KEY (ReinstatedByUserId) REFERENCES Users(Id),
    CONSTRAINT CK_TiffinCancellations_DateRange CHECK (EndDate >= StartDate)
) ENGINE=InnoDB;

CREATE INDEX IX_TiffinCancellations_Family_Dates ON TiffinCancellations(FamilyId, StartDate, EndDate);
