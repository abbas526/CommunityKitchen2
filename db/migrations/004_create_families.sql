USE CommKitchen;

-- One row per home. FamilyHeadUserId is UNIQUE: exactly one Family per head.
-- TiffinSizeId holds the CURRENT size for fast day-to-day lookups; the full
-- history of size changes lives in FamilySizeHistory.
CREATE TABLE Families (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    FamilyHeadUserId BIGINT UNSIGNED NOT NULL,
    Address VARCHAR(300) NULL,
    NumberOfMembers TINYINT UNSIGNED NULL,
    TiffinSizeId TINYINT UNSIGNED NOT NULL,
    RegistrationStatus ENUM('Pending','Approved','Rejected') NOT NULL DEFAULT 'Pending',
    ApprovedByAdminUserId BIGINT UNSIGNED NULL,
    ApprovedAt DATETIME NULL,
    IsActive TINYINT UNSIGNED NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT UQ_Families_FamilyHeadUserId UNIQUE (FamilyHeadUserId),
    CONSTRAINT FK_Families_FamilyHeadUser FOREIGN KEY (FamilyHeadUserId) REFERENCES Users(Id),
    CONSTRAINT FK_Families_TiffinSize FOREIGN KEY (TiffinSizeId) REFERENCES TiffinSizes(Id),
    CONSTRAINT FK_Families_ApprovedBy FOREIGN KEY (ApprovedByAdminUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_Families_RegistrationStatus ON Families(RegistrationStatus);
