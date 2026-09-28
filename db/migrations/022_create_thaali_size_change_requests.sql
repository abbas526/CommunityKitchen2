USE CommKitchen;

-- A Family Head's request to change their Thaali size, together with a target
-- EffectiveFromDate. Unlike AddressChangeRequests' EffectiveDate (which is only
-- guidance for Admin), this EffectiveFromDate is used for real: approving a request
-- closes the family's currently-open FamilySizeHistory row, opens a new one dated
-- EffectiveFromDate, and updates Families.ThaaliSizeId, all in the same transaction
-- the request itself is marked Approved in -- exactly what the pre-existing direct
-- PUT /api/families/{id}/thaali-size endpoint already did, just now gated behind an
-- Admin decision instead of applying immediately when the family submits it. There's
-- still no deferred/scheduled application (same reasoning as AddressChangeRequests --
-- this app has no background job runner), so Admin is expected to review these daily
-- and simply wait to approve until the requested date is at hand if they want the
-- history dated later.
CREATE TABLE ThaaliSizeChangeRequests (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    FamilyId BIGINT UNSIGNED NOT NULL,
    NewThaaliSizeId TINYINT UNSIGNED NOT NULL,
    EffectiveFromDate DATE NOT NULL,
    Reason VARCHAR(500) NULL DEFAULT NULL,
    Status ENUM('Pending','Approved','Rejected') NOT NULL DEFAULT 'Pending',
    CreatedByUserId BIGINT UNSIGNED NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ReviewedByAdminUserId BIGINT UNSIGNED NULL DEFAULT NULL,
    ReviewedAt DATETIME NULL DEFAULT NULL,
    AdminNote VARCHAR(500) NULL DEFAULT NULL,
    FamilyNotifiedAt DATETIME NULL DEFAULT NULL,
    CONSTRAINT FK_ThaaliSizeChangeRequests_Family FOREIGN KEY (FamilyId) REFERENCES Families(Id),
    CONSTRAINT FK_ThaaliSizeChangeRequests_Size FOREIGN KEY (NewThaaliSizeId) REFERENCES ThaaliSizes(Id),
    CONSTRAINT FK_ThaaliSizeChangeRequests_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id),
    CONSTRAINT FK_ThaaliSizeChangeRequests_ReviewedBy FOREIGN KEY (ReviewedByAdminUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_ThaaliSizeChangeRequests_FamilyId ON ThaaliSizeChangeRequests(FamilyId);
CREATE INDEX IX_ThaaliSizeChangeRequests_Status ON ThaaliSizeChangeRequests(Status);
