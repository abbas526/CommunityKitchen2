USE CommKitchen;

-- JWT refresh-token storage. Only the hash is stored, never the raw token.
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
