IF OBJECT_ID(N'dbo.InventoryItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryItems
    (
        InventoryItemId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_InventoryItems PRIMARY KEY,
        ItemName nvarchar(150) NOT NULL,
        UnitName nvarchar(30) NOT NULL,
        QuantityOnHand decimal(12, 3) NOT NULL CONSTRAINT DF_InventoryItems_QuantityOnHand DEFAULT 0,
        MinQuantity decimal(12, 3) NOT NULL CONSTRAINT DF_InventoryItems_MinQuantity DEFAULT 0,
        IsActive bit NOT NULL CONSTRAINT DF_InventoryItems_IsActive DEFAULT 1
    );
END;

IF COL_LENGTH(N'dbo.InventoryItems', N'IsActive') IS NULL
BEGIN
    ALTER TABLE dbo.InventoryItems
    ADD IsActive bit NOT NULL CONSTRAINT DF_InventoryItems_IsActive DEFAULT 1;
END;

IF OBJECT_ID(N'dbo.ProductMaterials', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductMaterials
    (
        ProductMaterialId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductMaterials PRIMARY KEY,
        ProductId int NOT NULL,
        InventoryItemId int NOT NULL,
        Quantity decimal(12, 3) NOT NULL,
        CONSTRAINT FK_ProductMaterials_Products FOREIGN KEY (ProductId)
            REFERENCES dbo.Products(ProductId) ON DELETE CASCADE,
        CONSTRAINT FK_ProductMaterials_InventoryItems FOREIGN KEY (InventoryItemId)
            REFERENCES dbo.InventoryItems(InventoryItemId)
    );
END;

IF OBJECT_ID(N'dbo.InventoryMovements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.InventoryMovements
    (
        MovementId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_InventoryMovements PRIMARY KEY,
        InventoryItemId int NOT NULL,
        OrderId int NULL,
        ProductId int NULL,
        QuantityChange decimal(12, 3) NOT NULL,
        MovementDate datetime NOT NULL CONSTRAINT DF_InventoryMovements_MovementDate DEFAULT GETDATE(),
        Comment nvarchar(255) NULL,
        CONSTRAINT FK_InventoryMovements_InventoryItems FOREIGN KEY (InventoryItemId)
            REFERENCES dbo.InventoryItems(InventoryItemId),
        CONSTRAINT FK_InventoryMovements_Orders FOREIGN KEY (OrderId)
            REFERENCES dbo.Orders(OrderId),
        CONSTRAINT FK_InventoryMovements_Products FOREIGN KEY (ProductId)
            REFERENCES dbo.Products(ProductId)
    );
END;
