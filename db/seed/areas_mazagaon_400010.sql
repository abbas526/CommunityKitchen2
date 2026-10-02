USE CommKitchen;

-- 10 Areas in Mazagaon, Mumbai 400010.
-- INSERT IGNORE: re-running is safe; names already present (UQ_Areas_Name) are skipped.
-- SortOrder continues after the current highest value so new rows list after existing ones.
SET @base := (SELECT COALESCE(MAX(SortOrder), 0) FROM Areas);

INSERT IGNORE INTO Areas (Name, SortOrder) VALUES
('Nesbit Road, Mazagaon',               @base + 1),
('Dockyard Road, Mazagaon',             @base + 2),
('Mazagaon Road',                       @base + 3),
('Dr. Mascarenhas Road, Mazagaon',      @base + 4),
('Gunpowder Road, Mazagaon',            @base + 5),
('Tank Bunder Road, Mazagaon',          @base + 6),
('Hay Bunder Road, Mazagaon',           @base + 7),
('Barrister Nath Pai Marg, Mazagaon',   @base + 8),
('Frere Road, Mazagaon',                @base + 9),
('Mazagaon Hill Lane',                  @base + 10);

SELECT Id, Name, SortOrder FROM Areas ORDER BY SortOrder;
