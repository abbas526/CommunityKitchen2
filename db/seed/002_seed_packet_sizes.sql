USE CommKitchen;

INSERT INTO PacketSizes (Id, Name, SortOrder) VALUES
  (1, 'Small', 1),
  (2, 'Medium', 2),
  (3, 'Big', 3)
ON DUPLICATE KEY UPDATE Name = VALUES(Name), SortOrder = VALUES(SortOrder);
