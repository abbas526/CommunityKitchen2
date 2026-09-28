USE CommKitchen;

-- A Family Head's request to change their Address and/or Area (which Delivery Person
-- serves them -- see Areas/DeliveryPersons), together with a target EffectiveDate the
-- family gives as guidance for when they'd like it to take effect. Approving a request
-- writes NewAddress/NewAreaId straight into Families immediately -- there's no
-- deferred/scheduled application, since Admin is expected to review these daily and can
-- simply wait to approve until the date is at hand (this app has no background job
-- runner to apply a change automatically on a future date).
CREATE TABLE AddressChangeRequests (
    Id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    FamilyId BIGINT UNSIGNED NOT NULL,
    NewAddress VARCHAR(500) NOT NULL,
    NewAreaId TINYINT UNSIGNED NULL DEFAULT NULL,
    EffectiveDate DATE NOT NULL,
    Reason VARCHAR(500) NULL DEFAULT NULL,
    Status ENUM('Pending','Approved','Rejected') NOT NULL DEFAULT 'Pending',
    CreatedByUserId BIGINT UNSIGNED NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ReviewedByAdminUserId BIGINT UNSIGNED NULL DEFAULT NULL,
    ReviewedAt DATETIME NULL DEFAULT NULL,
    AdminNote VARCHAR(500) NULL DEFAULT NULL,
    FamilyNotifiedAt DATETIME NULL DEFAULT NULL,
    CONSTRAINT FK_AddressChangeRequests_Family FOREIGN KEY (FamilyId) REFERENCES Families(Id),
    CONSTRAINT FK_AddressChangeRequests_Area FOREIGN KEY (NewAreaId) REFERENCES Areas(Id),
    CONSTRAINT FK_AddressChangeRequests_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id),
    CONSTRAINT FK_AddressChangeRequests_ReviewedBy FOREIGN KEY (ReviewedByAdminUserId) REFERENCES Users(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_AddressChangeRequests_FamilyId ON AddressChangeRequests(FamilyId);
CREATE INDEX IX_AddressChangeRequests_Status ON AddressChangeRequests(Status);
