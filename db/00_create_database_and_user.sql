-- Run this ONCE as an administrator (e.g. root) using MySQL Workbench or the mysql CLI.
-- Creates the CommKitchen database and the application login used by the .NET API.

CREATE DATABASE IF NOT EXISTS CommKitchen
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_0900_ai_ci;

CREATE USER IF NOT EXISTS 'abbas'@'localhost' IDENTIFIED BY 'REPLACE_WITH_YOUR_OWN_PASSWORD'; -- placeholder: set a real password here before running, then match it in FaizMawaid/appsettings.json (never commit the real one)
GRANT ALL PRIVILEGES ON CommKitchen.* TO 'abbas'@'localhost';
FLUSH PRIVILEGES;
