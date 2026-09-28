USE CommKitchen;

-- The 3-4 tiffin sizes an Admin defines. A table rather than an enum so an
-- Admin can add/rename a size from the app without a deployment.
CREATE TABLE TiffinSizes (
    Id TINYINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(20) NOT NULL,
    SortOrder TINYINT UNSIGNED NOT NULL,
    CONSTRAINT UQ_TiffinSizes_Name UNIQUE (Name)
) ENGINE=InnoDB;
