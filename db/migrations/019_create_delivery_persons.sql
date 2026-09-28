USE CommKitchen;

-- The person who delivers/serves the Thaali to homes in a particular Area (as
-- opposed to a family that picks their thaali up from the kitchen themselves, for
-- whom no Delivery Person applies). An Area can have more than one Delivery Person
-- (e.g. a backup), though typically just one.
CREATE TABLE DeliveryPersons (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    AreaId TINYINT UNSIGNED NOT NULL,
    FullName VARCHAR(255) NOT NULL,
    MobileNumber VARCHAR(30) NOT NULL,
    IsActive TINYINT UNSIGNED NOT NULL DEFAULT 1,
    CreatedByUserId BIGINT UNSIGNED NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME NULL DEFAULT NULL,
    CONSTRAINT FK_DeliveryPersons_Area FOREIGN KEY (AreaId) REFERENCES Areas(Id),
    CONSTRAINT FK_DeliveryPersons_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_DeliveryPersons_AreaId ON DeliveryPersons(AreaId);
