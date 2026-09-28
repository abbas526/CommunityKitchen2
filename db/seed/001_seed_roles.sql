USE CommKitchen;

INSERT INTO Roles (Id, Name) VALUES
  (1, 'SuperAdmin'),
  (2, 'Admin'),
  (3, 'Member')
ON DUPLICATE KEY UPDATE Name = VALUES(Name);
