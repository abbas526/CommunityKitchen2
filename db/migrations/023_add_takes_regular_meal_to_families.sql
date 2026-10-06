USE CommKitchen;

-- Some families are part of the system but only receive a meal on Special Days
-- (see 024_add_special_day_to_meal_plans.sql). TakesRegularMeal = 1 (the default, so every
-- existing family keeps today's behaviour) means they receive the regular daily meal;
-- 0 means they receive a meal only on a Special Day. Only an Admin can change it after
-- registration.
ALTER TABLE Families
    ADD COLUMN TakesRegularMeal TINYINT UNSIGNED NOT NULL DEFAULT 1 AFTER NumberOfMembers;
