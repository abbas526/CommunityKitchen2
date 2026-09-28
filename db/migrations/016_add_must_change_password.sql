USE CommKitchen;

-- Supports Admin-initiated password resets: whenever an Admin sets/resets someone
-- else's password, this is set to 1 so the app can force a change at next login.
-- Defaults to 1 so every EXISTING row (whose PasswordHash is a meaningless
-- placeholder from before real authentication existed) is flagged too. New rows
-- created by a user choosing their own password (self-registration, the one-time
-- first-Admin bootstrap, or a self-service change-password) explicitly insert 0.
ALTER TABLE Users
    ADD COLUMN MustChangePassword TINYINT UNSIGNED NOT NULL DEFAULT 1 AFTER PasswordHash;
