USE CommKitchen;

-- A Special Day is a meal-plan date on which EVERY approved, active family receives the
-- meal, including families that don't take the regular meal (Families.TakesRegularMeal = 0).
-- Admin marks it while planning the month's meals. A Special Day may fall on a Sunday, in
-- Ramadan, or on a declared non-serving day -- being special overrides all three.
ALTER TABLE MealPlans
    ADD COLUMN IsSpecialDay TINYINT UNSIGNED NOT NULL DEFAULT 0 AFTER MealDescription,
    ADD COLUMN SpecialDayName VARCHAR(100) NULL AFTER IsSpecialDay;
