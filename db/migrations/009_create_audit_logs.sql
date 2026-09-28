USE CommKitchen;

-- Generic append-only trail: registration approvals, size changes,
-- cancellations entered by an Admin on someone's behalf, etc.
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
