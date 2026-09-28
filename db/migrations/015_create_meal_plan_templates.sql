USE CommKitchen;

-- Reusable "canned" meal descriptions an Admin defines once (e.g. "Khichdi & Kadhi",
-- "Biryani & Raita") so entering a repeat meal is a click/drag instead of retyping the
-- same text. Purely a convenience library for the Meal Plans screen -- MealPlans.MealDescription
-- stays free text (a template's text is copied in, not referenced), so deleting a
-- template never affects a meal plan that was built from it.
CREATE TABLE MealPlanTemplates (
    Id SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    MealDescription VARCHAR(500) NOT NULL,
    SortOrder TINYINT UNSIGNED NOT NULL DEFAULT 0,
    CreatedByUserId BIGINT UNSIGNED NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT UQ_MealPlanTemplates_Name UNIQUE (Name),
    CONSTRAINT FK_MealPlanTemplates_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_MealPlanTemplates_SortOrder ON MealPlanTemplates(SortOrder);
