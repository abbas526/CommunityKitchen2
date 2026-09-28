USE CommKitchen;

-- Extra no-service dates beyond the fixed weekly Sunday rule (which is
-- applied in application code, not stored here). "Is food served on date X"
-- = DAYOFWEEK(X) <> 1 (not Sunday) AND X NOT IN (SELECT TheDate FROM NonServingDays).
CREATE TABLE NonServingDays (
    Id INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    TheDate DATE NOT NULL,
    Reason VARCHAR(200) NULL,
    CreatedByUserId BIGINT UNSIGNED NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT UQ_NonServingDays_TheDate UNIQUE (TheDate),
    CONSTRAINT FK_NonServingDays_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;
