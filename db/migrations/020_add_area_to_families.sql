USE CommKitchen;

-- Optional -- a family that picks its thaali up from the kitchen itself has no Area.
ALTER TABLE Families
    ADD COLUMN AreaId TINYINT UNSIGNED NULL DEFAULT NULL AFTER Address,
    ADD CONSTRAINT FK_Families_Area FOREIGN KEY (AreaId) REFERENCES Areas(Id);

CREATE INDEX IX_Families_AreaId ON Families(AreaId);
