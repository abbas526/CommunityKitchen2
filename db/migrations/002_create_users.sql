USE CommKitchen;

-- Every person who logs in: Admins and Family Heads both live here,
-- distinguished by RoleId. Supports any number of Admins.
CREATE TABLE Users (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    RoleId TINYINT UNSIGNED NOT NULL,
    Email VARCHAR(255) NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    FullName VARCHAR(150) NOT NULL,
    Phone VARCHAR(20) NULL,
    IsActive TINYINT UNSIGNED NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
    LastLoginAt DATETIME NULL,
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT FK_Users_Role FOREIGN KEY (RoleId) REFERENCES Roles(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_Users_RoleId ON Users(RoleId);
