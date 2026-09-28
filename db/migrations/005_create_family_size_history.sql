USE CommKitchen;

-- One row per tiffin-size change for a family, so past sizes can be
-- reported on, not just the current one. EffectiveToDate IS NULL means
-- this row is the currently active size (kept in sync with
-- Families.TiffinSizeId whenever a change happens).
CREATE TABLE FamilySizeHistory (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    FamilyId BIGINT UNSIGNED NOT NULL,
    TiffinSizeId TINYINT UNSIGNED NOT NULL,
    EffectiveFromDate DATE NOT NULL,
    EffectiveToDate DATE NULL,
    ChangedByUserId BIGINT UNSIGNED NOT NULL,
    ChangedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_FamilySizeHistory_Family FOREIGN KEY (FamilyId) REFERENCES Families(Id),
    CONSTRAINT FK_FamilySizeHistory_TiffinSize FOREIGN KEY (TiffinSizeId) REFERENCES TiffinSizes(Id),
    CONSTRAINT FK_FamilySizeHistory_ChangedBy FOREIGN KEY (ChangedByUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_FamilySizeHistory_Family_Dates ON FamilySizeHistory(FamilyId, EffectiveFromDate, EffectiveToDate);
