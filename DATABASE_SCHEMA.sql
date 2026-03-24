-- Inventory Table
CREATE TABLE Inventory (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(255),
    Quantity INT NOT NULL,
    Category NVARCHAR(100),
    CreatedAt DATETIME NOT NULL,
    UpdatedAt DATETIME NULL
);

-- Users Table
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL,
    IdentityNumber NVARCHAR(50),
    Email NVARCHAR(100),
    ContactNumber NVARCHAR(30),
    Address NVARCHAR(255),
    PasswordHash NVARCHAR(255),
    Role INT NOT NULL,
    IsApproved BIT NOT NULL,
    ApprovedByAdminId INT NULL,
    CreatedAt DATETIME NOT NULL,
    ApprovedAt DATETIME NULL
);

-- InventoryTransaction Table
CREATE TABLE InventoryTransaction (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    InventoryItemId INT NOT NULL,
    IssuedToUserId INT NULL,
    QuantityChanged INT NOT NULL,
    Comment NVARCHAR(255),
    Timestamp DATETIME NOT NULL,
    PerformedByAdminId INT NULL,
    FOREIGN KEY (InventoryItemId) REFERENCES Inventory(Id),
    FOREIGN KEY (IssuedToUserId) REFERENCES Users(Id),
    FOREIGN KEY (PerformedByAdminId) REFERENCES Users(Id)
);
