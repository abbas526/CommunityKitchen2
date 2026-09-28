USE CommKitchen;

-- "Sabil Number" -- a unique identifier the Community Head assigns to every
-- family, captured on the Family Head's User row (this is the "Unique Key
-- for Users table" the requirement asked for -- not the primary key, but a
-- second unique identifier alongside Email). A plain VARCHAR since it's a
-- character-based code, not necessarily numeric.
--
-- NULLable at the DB level (MySQL allows any number of NULL values in a
-- UNIQUE index without conflict) because Admin accounts don't have one --
-- only Family Head registrations require it, and that's enforced in the
-- application layer (FamiliesController.Register), not here.
ALTER TABLE Users
    ADD COLUMN SabilNumber VARCHAR(50) NULL AFTER Email;

ALTER TABLE Users
    ADD CONSTRAINT UQ_Users_SabilNumber UNIQUE (SabilNumber);
