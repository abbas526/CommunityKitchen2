USE CommKitchen;

-- Lookup: the Areas/Towers an Admin defines (e.g. "Tower A", "Sector 4"). Families
-- optionally pick one at registration, and each Area can have one or more Delivery
-- Persons assigned (see 019_create_delivery_persons.sql). Mirrors ThaaliSizes: no
-- seed data, the Admin defines these through the app.
CREATE TABLE Areas (
    Id TINYINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    SortOrder TINYINT UNSIGNED NOT NULL DEFAULT 0,
    CONSTRAINT UQ_Areas_Name UNIQUE (Name)
) ENGINE=InnoDB;
