USE CommKitchen;

-- One Delivery Person can now serve MORE THAN ONE Area (and an Area can still have more than
-- one Delivery Person). The single DeliveryPersons.AreaId column is replaced by a join table.
-- Existing assignments are copied across first, so nothing is lost.
CREATE TABLE DeliveryPersonAreas (
    DeliveryPersonId BIGINT UNSIGNED NOT NULL,
    AreaId TINYINT UNSIGNED NOT NULL,
    PRIMARY KEY (DeliveryPersonId, AreaId),
    CONSTRAINT FK_DeliveryPersonAreas_Person FOREIGN KEY (DeliveryPersonId) REFERENCES DeliveryPersons(Id) ON DELETE CASCADE,
    CONSTRAINT FK_DeliveryPersonAreas_Area FOREIGN KEY (AreaId) REFERENCES Areas(Id)
) ENGINE=InnoDB;

CREATE INDEX IX_DeliveryPersonAreas_AreaId ON DeliveryPersonAreas(AreaId);

INSERT INTO DeliveryPersonAreas (DeliveryPersonId, AreaId)
SELECT Id, AreaId FROM DeliveryPersons;

ALTER TABLE DeliveryPersons DROP FOREIGN KEY FK_DeliveryPersons_Area;
DROP INDEX IX_DeliveryPersons_AreaId ON DeliveryPersons;
ALTER TABLE DeliveryPersons DROP COLUMN AreaId;
