USE CommKitchen;

-- The meal Admin enters ahead of time for a given serving date (4-5 lines
-- of free text, capped at 500 chars). One row per date. How many of these
-- are actually shown to Family Members is controlled separately by
-- AppSettings.MealVisibilityDays, not by how many rows exist here -- an
-- Admin can plan a month ahead while members only see the next week.
-- App layer should reject a MealDate that's a Sunday or in NonServingDays.
CREATE TABLE MealPlans (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    MealDate DATE NOT NULL,
    MealDescription VARCHAR(500) NOT NULL,
    CreatedByUserId BIGINT UNSIGNED NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedByUserId BIGINT UNSIGNED NULL,
    UpdatedAt DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT UQ_MealPlans_MealDate UNIQUE (MealDate),
    CONSTRAINT FK_MealPlans_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id),
    CONSTRAINT FK_MealPlans_UpdatedBy FOREIGN KEY (UpdatedByUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_MealPlans_MealDate ON MealPlans(MealDate);
