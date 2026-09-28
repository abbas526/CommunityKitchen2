USE CommKitchen;

-- A private message thread between one family and the Admin team -- a Family Head
-- can send a piece of feedback/question, and any Admin can add a single response to
-- it (visible only to that family and Admins, never to other families). Modeled as
-- one row per feedback item rather than a full multi-message thread table, since the
-- request was for "feedback + a response", not an open-ended chat.
CREATE TABLE Feedback (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    FamilyId BIGINT UNSIGNED NOT NULL,
    CreatedByUserId BIGINT UNSIGNED NOT NULL,
    Message VARCHAR(1000) NOT NULL,
    Status ENUM('Open','Responded') NOT NULL DEFAULT 'Open',
    ResponseText VARCHAR(1000) NULL DEFAULT NULL,
    RespondedByAdminUserId BIGINT UNSIGNED NULL DEFAULT NULL,
    RespondedAt DATETIME NULL DEFAULT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT FK_Feedback_Family FOREIGN KEY (FamilyId) REFERENCES Families(Id),
    CONSTRAINT FK_Feedback_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id),
    CONSTRAINT FK_Feedback_RespondedBy FOREIGN KEY (RespondedByAdminUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_Feedback_FamilyId ON Feedback(FamilyId);
CREATE INDEX IX_Feedback_Status ON Feedback(Status);
