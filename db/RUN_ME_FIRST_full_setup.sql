-- Run this ONCE as an administrator (e.g. root) using MySQL Workbench or the mysql CLI.
-- Creates the CommKitchen database and the application login used by the .NET API.

CREATE DATABASE IF NOT EXISTS CommKitchen
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;

CREATE USER IF NOT EXISTS 'abbas'@'localhost' IDENTIFIED BY 'REPLACE_WITH_YOUR_OWN_PASSWORD'; -- placeholder: set a real password here before running, then match it in FaizMawaid/appsettings.json (never commit the real one)
GRANT ALL PRIVILEGES ON CommKitchen.* TO 'abbas'@'localhost';
FLUSH PRIVILEGES;

USE CommKitchen;

CREATE TABLE Roles (
    Id TINYINT UNSIGNED NOT NULL PRIMARY KEY,
    Name VARCHAR(30) NOT NULL,
    CONSTRAINT UQ_Roles_Name UNIQUE (Name)
) ENGINE=InnoDB;

USE CommKitchen;

CREATE TABLE Users (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    RoleId TINYINT UNSIGNED NOT NULL,
    Email VARCHAR(255) NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL,
    FullName VARCHAR(150) NOT NULL,
    Phone VARCHAR(20) NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CreatedByUserId BIGINT UNSIGNED NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
    LastLoginAt DATETIME NULL,
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT FK_Users_Role FOREIGN KEY (RoleId) REFERENCES Roles(Id),
    CONSTRAINT FK_Users_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_Users_RoleId ON Users(RoleId);

USE CommKitchen;

CREATE TABLE Areas (
    Id INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(150) NOT NULL,
    Description VARCHAR(500) NULL,
    AdminUserId BIGINT UNSIGNED NULL,
    CreatedByUserId BIGINT UNSIGNED NOT NULL,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT UQ_Areas_Name UNIQUE (Name),
    CONSTRAINT UQ_Areas_AdminUserId UNIQUE (AdminUserId),
    CONSTRAINT FK_Areas_AdminUser FOREIGN KEY (AdminUserId) REFERENCES Users(Id),
    CONSTRAINT FK_Areas_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

USE CommKitchen;

CREATE TABLE PacketSizes (
    Id TINYINT UNSIGNED NOT NULL PRIMARY KEY,
    Name VARCHAR(20) NOT NULL,
    SortOrder TINYINT UNSIGNED NOT NULL,
    CONSTRAINT UQ_PacketSizes_Name UNIQUE (Name)
) ENGINE=InnoDB;

USE CommKitchen;

CREATE TABLE MemberProfiles (
    UserId BIGINT UNSIGNED NOT NULL PRIMARY KEY,
    AreaId INT UNSIGNED NOT NULL,
    PacketSizeId TINYINT UNSIGNED NOT NULL,
    Address VARCHAR(300) NULL,
    RegisteredByAdminUserId BIGINT UNSIGNED NOT NULL,
    RegisteredAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    IsActive TINYINT(1) NOT NULL DEFAULT 1,
    CONSTRAINT FK_MemberProfiles_User FOREIGN KEY (UserId) REFERENCES Users(Id),
    CONSTRAINT FK_MemberProfiles_Area FOREIGN KEY (AreaId) REFERENCES Areas(Id),
    CONSTRAINT FK_MemberProfiles_PacketSize FOREIGN KEY (PacketSizeId) REFERENCES PacketSizes(Id),
    CONSTRAINT FK_MemberProfiles_RegisteredBy FOREIGN KEY (RegisteredByAdminUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_MemberProfiles_Area_Active ON MemberProfiles(AreaId, IsActive);

USE CommKitchen;

CREATE TABLE FoodMenus (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    AreaId INT UNSIGNED NOT NULL,
    MenuDate DATE NOT NULL,
    Description VARCHAR(1000) NULL,
    CreatedByUserId BIGINT UNSIGNED NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT UQ_FoodMenus_Area_Date UNIQUE (AreaId, MenuDate),
    CONSTRAINT FK_FoodMenus_Area FOREIGN KEY (AreaId) REFERENCES Areas(Id),
    CONSTRAINT FK_FoodMenus_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

USE CommKitchen;

-- Optional table: only needed if a day should list multiple distinct dishes
-- rather than a single free-text Description on FoodMenus. Safe to keep either way.
CREATE TABLE FoodMenuItems (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    FoodMenuId BIGINT UNSIGNED NOT NULL,
    ItemName VARCHAR(150) NOT NULL,
    SortOrder TINYINT UNSIGNED NOT NULL DEFAULT 0,
    CONSTRAINT FK_FoodMenuItems_FoodMenu FOREIGN KEY (FoodMenuId) REFERENCES FoodMenus(Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE INDEX IX_FoodMenuItems_FoodMenuId ON FoodMenuItems(FoodMenuId);

USE CommKitchen;

CREATE TABLE MemberOptOuts (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    MemberUserId BIGINT UNSIGNED NOT NULL,
    AreaId INT UNSIGNED NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    Reason VARCHAR(300) NULL,
    Status ENUM('Active','Cancelled') NOT NULL DEFAULT 'Active',
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedByUserId BIGINT UNSIGNED NOT NULL,
    CancelledAt DATETIME NULL,
    CancelledByUserId BIGINT UNSIGNED NULL,
    CONSTRAINT FK_MemberOptOuts_Member FOREIGN KEY (MemberUserId) REFERENCES MemberProfiles(UserId),
    CONSTRAINT FK_MemberOptOuts_Area FOREIGN KEY (AreaId) REFERENCES Areas(Id),
    CONSTRAINT FK_MemberOptOuts_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id),
    CONSTRAINT FK_MemberOptOuts_CancelledBy FOREIGN KEY (CancelledByUserId) REFERENCES Users(Id),
    CONSTRAINT CK_MemberOptOuts_DateRange CHECK (EndDate >= StartDate)
) ENGINE=InnoDB;

CREATE INDEX IX_MemberOptOuts_Area_Dates ON MemberOptOuts(AreaId, StartDate, EndDate);
CREATE INDEX IX_MemberOptOuts_Member_Dates ON MemberOptOuts(MemberUserId, StartDate, EndDate);

USE CommKitchen;

CREATE TABLE RefreshTokens (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    UserId BIGINT UNSIGNED NOT NULL,
    TokenHash VARCHAR(255) NOT NULL,
    ExpiresAt DATETIME NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CreatedByIp VARCHAR(45) NULL,
    RevokedAt DATETIME NULL,
    ReplacedByTokenHash VARCHAR(255) NULL,
    CONSTRAINT FK_RefreshTokens_User FOREIGN KEY (UserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens(UserId);
CREATE INDEX IX_RefreshTokens_TokenHash ON RefreshTokens(TokenHash);

USE CommKitchen;

CREATE TABLE AuditLogs (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    UserId BIGINT UNSIGNED NULL,
    Action VARCHAR(100) NOT NULL,
    EntityType VARCHAR(50) NOT NULL,
    EntityId BIGINT UNSIGNED NULL,
    Metadata JSON NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_AuditLogs_User FOREIGN KEY (UserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_AuditLogs_CreatedAt ON AuditLogs(CreatedAt);

USE CommKitchen;

INSERT INTO Roles (Id, Name) VALUES
  (1, 'SuperAdmin'),
  (2, 'Admin'),
  (3, 'Member')
ON DUPLICATE KEY UPDATE Name = VALUES(Name);

USE CommKitchen;

INSERT INTO PacketSizes (Id, Name, SortOrder) VALUES
  (1, 'Small', 1),
  (2, 'Medium', 2),
  (3, 'Big', 3)
ON DUPLICATE KEY UPDATE Name = VALUES(Name), SortOrder = VALUES(SortOrder);

