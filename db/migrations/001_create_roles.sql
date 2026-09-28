USE CommKitchen;

-- Lookup table. Kept as a table (not a hardcoded enum) so a new role could be
-- added later without a code change, even though only two are seeded today.
CREATE TABLE Roles (
    Id TINYINT UNSIGNED NOT NULL PRIMARY KEY,
    Name VARCHAR(30) NOT NULL,
    CONSTRAINT UQ_Roles_Name UNIQUE (Name)
) ENGINE=InnoDB;

INSERT INTO Roles (Id, Name) VALUES
    (1, 'Admin'),
    (2, 'FamilyHead');
