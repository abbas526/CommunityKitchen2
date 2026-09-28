USE CommKitchen;

-- Single-row system configuration table. Id is always 1 -- the app should
-- only ever read/update the row where Id = 1 (the CHECK below is a
-- MySQL-8/MariaDB-10.6+ backstop, not enforced on 5.6/5.7; enforce
-- single-row-ness in the application layer too).
CREATE TABLE AppSettings (
    Id TINYINT UNSIGNED NOT NULL PRIMARY KEY,
    MealVisibilityDays INT UNSIGNED NOT NULL DEFAULT 7,
    UpdatedByUserId BIGINT UNSIGNED NULL,
    UpdatedAt DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT CK_AppSettings_SingleRow CHECK (Id = 1),
    CONSTRAINT FK_AppSettings_UpdatedBy FOREIGN KEY (UpdatedByUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

INSERT INTO AppSettings (Id, MealVisibilityDays) VALUES (1, 7);
