using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace App_XammerGroup
{
    public static class InventoryService
    {
        public static void EnsureSchemaAndSeed(DB_Xammer_groupEntities db)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }

            EnsureSchema(db);
            SeedInventoryItems(db);
            SeedProductsAndRecipes(db);
        }

        public static List<InventoryRow> GetInventoryRows()
        {
            using (var db = new DB_Xammer_groupEntities())
            {
                EnsureSchemaAndSeed(db);

                return db.Database.SqlQuery<InventoryRow>(
                    @"SELECT
                          InventoryItemId,
                          ItemName,
                          UnitName,
                          QuantityOnHand,
                          MinQuantity
                      FROM dbo.InventoryItems
                      ORDER BY ItemName").ToList();
            }
        }

        public static void AddStock(int inventoryItemId, decimal quantity, string comment)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            using (var db = new DB_Xammer_groupEntities())
            {
                EnsureSchemaAndSeed(db);

                using (var transaction = db.Database.BeginTransaction())
                {
                    db.Database.ExecuteSqlCommand(
                        @"UPDATE dbo.InventoryItems
                          SET QuantityOnHand = QuantityOnHand + @quantity
                          WHERE InventoryItemId = @inventoryItemId",
                        new SqlParameter("@quantity", quantity),
                        new SqlParameter("@inventoryItemId", inventoryItemId));

                    db.Database.ExecuteSqlCommand(
                        @"INSERT INTO dbo.InventoryMovements
                              (InventoryItemId, OrderId, ProductId, QuantityChange, MovementDate, Comment)
                          VALUES
                              (@inventoryItemId, NULL, NULL, @quantity, GETDATE(), @comment)",
                        new SqlParameter("@inventoryItemId", inventoryItemId),
                        new SqlParameter("@quantity", quantity),
                        new SqlParameter("@comment", NormalizeSqlText(comment) ?? "Stock refill"));

                    transaction.Commit();
                }
            }
        }

        public static void AddInventoryItem(string itemName, string unitName, decimal quantityOnHand, decimal minQuantity)
        {
            string normalizedItemName = NormalizeSqlText(itemName);
            string normalizedUnitName = NormalizeSqlText(unitName);

            if (normalizedItemName == null)
            {
                throw new ArgumentException("Item name is required.", nameof(itemName));
            }

            if (normalizedUnitName == null)
            {
                throw new ArgumentException("Unit name is required.", nameof(unitName));
            }

            if (quantityOnHand < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantityOnHand));
            }

            if (minQuantity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minQuantity));
            }

            using (var db = new DB_Xammer_groupEntities())
            {
                EnsureSchemaAndSeed(db);

                bool exists = db.Database.SqlQuery<int>(
                    "SELECT COUNT(1) FROM dbo.InventoryItems WHERE ItemName = @itemName",
                    new SqlParameter("@itemName", normalizedItemName)).Single() > 0;

                if (exists)
                {
                    throw new InvalidOperationException("Такая запчасть уже есть на складе.");
                }

                db.Database.ExecuteSqlCommand(
                    @"INSERT INTO dbo.InventoryItems (ItemName, UnitName, QuantityOnHand, MinQuantity)
                      VALUES (@itemName, @unitName, @quantityOnHand, @minQuantity)",
                    new SqlParameter("@itemName", normalizedItemName),
                    new SqlParameter("@unitName", normalizedUnitName),
                    new SqlParameter("@quantityOnHand", quantityOnHand),
                    new SqlParameter("@minQuantity", minQuantity));
            }
        }

        public static List<ProductAvailabilityInfo> GetProductAvailability()
        {
            using (var db = new DB_Xammer_groupEntities())
            {
                EnsureSchemaAndSeed(db);

                return db.Database.SqlQuery<ProductAvailabilityInfo>(
                    @"SELECT
                          p.ProductId,
                          CASE
                              WHEN COUNT(pm.ProductMaterialId) = 0 THEN CAST(0 AS bit)
                              WHEN SUM(CASE WHEN ii.QuantityOnHand < pm.Quantity THEN 1 ELSE 0 END) > 0 THEN CAST(0 AS bit)
                              ELSE CAST(1 AS bit)
                          END AS IsAvailable,
                          CASE
                              WHEN COUNT(pm.ProductMaterialId) = 0 THEN N'Нет состава'
                              WHEN SUM(CASE WHEN ii.QuantityOnHand < pm.Quantity THEN 1 ELSE 0 END) > 0 THEN N'Не хватает материалов'
                              ELSE N'Материалы есть'
                          END AS AvailabilityText
                      FROM dbo.Products p
                      LEFT JOIN dbo.ProductMaterials pm ON pm.ProductId = p.ProductId
                      LEFT JOIN dbo.InventoryItems ii ON ii.InventoryItemId = pm.InventoryItemId
                      GROUP BY p.ProductId").ToList();
            }
        }

        public static List<InventoryShortage> ValidateAvailability(DB_Xammer_groupEntities db, IReadOnlyList<CartItem> cartItems)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }

            EnsureSchemaAndSeed(db);

            var requiredByItem = BuildRequiredMaterials(db, cartItems);
            return requiredByItem
                .Where(item => item.RequiredQuantity > item.QuantityOnHand)
                .Select(item => new InventoryShortage
                {
                    InventoryItemId = item.InventoryItemId,
                    ItemName = item.ItemName,
                    UnitName = item.UnitName,
                    RequiredQuantity = item.RequiredQuantity,
                    QuantityOnHand = item.QuantityOnHand
                })
                .OrderBy(item => item.ItemName)
                .ToList();
        }

        public static void WriteOffMaterials(DB_Xammer_groupEntities db, int orderId, IReadOnlyList<CartItem> cartItems)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }

            EnsureSchemaAndSeed(db);

            var shortages = ValidateAvailability(db, cartItems);
            if (shortages.Count > 0)
            {
                throw new InvalidOperationException(BuildShortageMessage(shortages));
            }

            foreach (var product in cartItems.Where(item => item.Quantity > 0))
            {
                var materials = db.Database.SqlQuery<ProductMaterialRequirement>(
                    @"SELECT
                          ii.InventoryItemId,
                          ii.ItemName,
                          ii.UnitName,
                          CAST(pm.Quantity * @quantity AS decimal(12, 3)) AS RequiredQuantity,
                          ii.QuantityOnHand
                      FROM dbo.ProductMaterials pm
                      INNER JOIN dbo.InventoryItems ii ON ii.InventoryItemId = pm.InventoryItemId
                      WHERE pm.ProductId = @productId",
                    new SqlParameter("@productId", product.ProductId),
                    new SqlParameter("@quantity", product.Quantity)).ToList();

                foreach (var material in materials)
                {
                    db.Database.ExecuteSqlCommand(
                        @"UPDATE dbo.InventoryItems
                          SET QuantityOnHand = QuantityOnHand - @quantity
                          WHERE InventoryItemId = @inventoryItemId",
                        new SqlParameter("@quantity", material.RequiredQuantity),
                        new SqlParameter("@inventoryItemId", material.InventoryItemId));

                    db.Database.ExecuteSqlCommand(
                        @"INSERT INTO dbo.InventoryMovements
                              (InventoryItemId, OrderId, ProductId, QuantityChange, MovementDate, Comment)
                          VALUES
                              (@inventoryItemId, @orderId, @productId, -@quantity, GETDATE(), @comment)",
                        new SqlParameter("@inventoryItemId", material.InventoryItemId),
                        new SqlParameter("@orderId", orderId),
                        new SqlParameter("@productId", product.ProductId),
                        new SqlParameter("@quantity", material.RequiredQuantity),
                        new SqlParameter("@comment", "Order write-off"));
                }
            }
        }

        public static string BuildShortageMessage(IEnumerable<InventoryShortage> shortages)
        {
            var lines = shortages.Select(item =>
                $"{item.ItemName}: нужно {item.RequiredQuantity:N3} {item.UnitName}, на складе {item.QuantityOnHand:N3} {item.UnitName}");

            return "Недостаточно материалов на складе:\n" + string.Join("\n", lines);
        }

        private static void EnsureSchema(DB_Xammer_groupEntities db)
        {
            db.Database.ExecuteSqlCommand(
                @"IF OBJECT_ID(N'dbo.InventoryItems', N'U') IS NULL
                  BEGIN
                      CREATE TABLE dbo.InventoryItems
                      (
                          InventoryItemId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_InventoryItems PRIMARY KEY,
                          ItemName nvarchar(150) NOT NULL,
                          UnitName nvarchar(30) NOT NULL,
                          QuantityOnHand decimal(12, 3) NOT NULL CONSTRAINT DF_InventoryItems_QuantityOnHand DEFAULT 0,
                          MinQuantity decimal(12, 3) NOT NULL CONSTRAINT DF_InventoryItems_MinQuantity DEFAULT 0
                      );
                  END");

            db.Database.ExecuteSqlCommand(
                @"IF OBJECT_ID(N'dbo.ProductMaterials', N'U') IS NULL
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
                  END");

            db.Database.ExecuteSqlCommand(
                @"IF OBJECT_ID(N'dbo.InventoryMovements', N'U') IS NULL
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
                  END");

            db.Database.ExecuteSqlCommand(
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_InventoryItems_ItemName' AND object_id = OBJECT_ID(N'dbo.InventoryItems'))
                  BEGIN
                      CREATE UNIQUE INDEX UX_InventoryItems_ItemName ON dbo.InventoryItems(ItemName);
                  END");

            db.Database.ExecuteSqlCommand(
                @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ProductMaterials_Product_Item' AND object_id = OBJECT_ID(N'dbo.ProductMaterials'))
                  BEGIN
                      CREATE UNIQUE INDEX UX_ProductMaterials_Product_Item ON dbo.ProductMaterials(ProductId, InventoryItemId);
                  END");
        }

        private static void SeedInventoryItems(DB_Xammer_groupEntities db)
        {
            EnsureInventoryItem(db, "Металлическое изделие", "шт.", 500, 40);
            EnsureInventoryItem(db, "Уплотнитель", "шт.", 300, 30);
            EnsureInventoryItem(db, "Метизы", "шт.", 2000, 200);
            EnsureInventoryItem(db, "Краска", "кг", 250, 25);
            EnsureInventoryItem(db, "Упаковка", "шт.", 300, 30);
            EnsureInventoryItem(db, "Паспорт", "шт.", 300, 30);
            EnsureInventoryItem(db, "Шильд люк", "шт.", 150, 15);
            EnsureInventoryItem(db, "Шильд дверь", "шт.", 150, 15);
            EnsureInventoryItem(db, "Шильд универсальный", "шт.", 150, 15);
            EnsureInventoryItem(db, "Замок", "шт.", 200, 20);
            EnsureInventoryItem(db, "Ручка", "шт.", 200, 20);
            EnsureInventoryItem(db, "Фурнитура", "компл.", 100, 10);
        }

        private static void SeedProductsAndRecipes(DB_Xammer_groupEntities db)
        {
            int hatchId = EnsureProduct(db, "Люк", "Металлический люк со складским списанием по спецификации.", 4500);
            int doorId = EnsureProduct(db, "Дверь", "Металлическая дверь со складским списанием по спецификации.", 9500);
            int otherId = EnsureProduct(db, "Другое", "Универсальный малый комплект для нестандартных изделий.", 2500);

            SetRecipe(db, hatchId, new[]
            {
                Recipe("Металлическое изделие", 4),
                Recipe("Уплотнитель", 1),
                Recipe("Метизы", 8),
                Recipe("Краска", 1),
                Recipe("Упаковка", 1),
                Recipe("Паспорт", 1),
                Recipe("Шильд люк", 1),
                Recipe("Замок", 1),
                Recipe("Ручка", 1)
            });

            SetRecipe(db, doorId, new[]
            {
                Recipe("Металлическое изделие", 8),
                Recipe("Уплотнитель", 2),
                Recipe("Метизы", 16),
                Recipe("Краска", 2),
                Recipe("Упаковка", 1),
                Recipe("Паспорт", 1),
                Recipe("Шильд дверь", 1),
                Recipe("Замок", 2),
                Recipe("Ручка", 1)
            });

            SetRecipe(db, otherId, new[]
            {
                Recipe("Металлическое изделие", 2),
                Recipe("Уплотнитель", 1),
                Recipe("Метизы", 4),
                Recipe("Краска", 0.5m),
                Recipe("Упаковка", 1),
                Recipe("Паспорт", 1),
                Recipe("Шильд универсальный", 1),
                Recipe("Фурнитура", 1)
            });
        }

        private static void EnsureInventoryItem(DB_Xammer_groupEntities db, string itemName, string unitName, decimal quantity, decimal minQuantity)
        {
            db.Database.ExecuteSqlCommand(
                @"IF NOT EXISTS (SELECT 1 FROM dbo.InventoryItems WHERE ItemName = @itemName)
                  BEGIN
                      INSERT INTO dbo.InventoryItems (ItemName, UnitName, QuantityOnHand, MinQuantity)
                      VALUES (@itemName, @unitName, @quantity, @minQuantity);
                  END",
                new SqlParameter("@itemName", itemName),
                new SqlParameter("@unitName", unitName),
                new SqlParameter("@quantity", quantity),
                new SqlParameter("@minQuantity", minQuantity));
        }

        private static int EnsureProduct(DB_Xammer_groupEntities db, string productName, string description, decimal price)
        {
            var product = db.Products.FirstOrDefault(item => item.ProductName == productName);
            if (product != null)
            {
                if (!(product.IsActive ?? false))
                {
                    product.IsActive = true;
                    db.SaveChanges();
                }

                return product.ProductId;
            }

            product = new Products
            {
                ProductName = productName,
                Description = description,
                Price = price,
                ImagePath = null,
                IsActive = true
            };

            db.Products.Add(product);
            db.SaveChanges();

            return product.ProductId;
        }

        private static RecipeItem Recipe(string itemName, decimal quantity)
        {
            return new RecipeItem { ItemName = itemName, Quantity = quantity };
        }

        private static void SetRecipe(DB_Xammer_groupEntities db, int productId, IEnumerable<RecipeItem> recipe)
        {
            foreach (var item in recipe)
            {
                db.Database.ExecuteSqlCommand(
                    @"DECLARE @inventoryItemId int;
                      SELECT @inventoryItemId = InventoryItemId
                      FROM dbo.InventoryItems
                      WHERE ItemName = @itemName;

                      IF @inventoryItemId IS NOT NULL
                      BEGIN
                          IF EXISTS (SELECT 1 FROM dbo.ProductMaterials WHERE ProductId = @productId AND InventoryItemId = @inventoryItemId)
                          BEGIN
                              UPDATE dbo.ProductMaterials
                              SET Quantity = @quantity
                              WHERE ProductId = @productId AND InventoryItemId = @inventoryItemId;
                          END
                          ELSE
                          BEGIN
                              INSERT INTO dbo.ProductMaterials (ProductId, InventoryItemId, Quantity)
                              VALUES (@productId, @inventoryItemId, @quantity);
                          END
                      END",
                    new SqlParameter("@productId", productId),
                    new SqlParameter("@itemName", item.ItemName),
                    new SqlParameter("@quantity", item.Quantity));
            }
        }

        private static List<ProductMaterialRequirement> BuildRequiredMaterials(DB_Xammer_groupEntities db, IReadOnlyList<CartItem> cartItems)
        {
            var result = new List<ProductMaterialRequirement>();

            foreach (var productGroup in cartItems.Where(item => item.Quantity > 0).GroupBy(item => item.ProductId))
            {
                int totalQuantity = productGroup.Sum(item => item.Quantity);
                var materials = db.Database.SqlQuery<ProductMaterialRequirement>(
                    @"SELECT
                          ii.InventoryItemId,
                          ii.ItemName,
                          ii.UnitName,
                          CAST(pm.Quantity * @quantity AS decimal(12, 3)) AS RequiredQuantity,
                          ii.QuantityOnHand
                      FROM dbo.ProductMaterials pm
                      INNER JOIN dbo.InventoryItems ii ON ii.InventoryItemId = pm.InventoryItemId
                      WHERE pm.ProductId = @productId",
                    new SqlParameter("@productId", productGroup.Key),
                    new SqlParameter("@quantity", totalQuantity)).ToList();

                result.AddRange(materials);
            }

            return result
                .GroupBy(item => item.InventoryItemId)
                .Select(group =>
                {
                    var first = group.First();
                    return new ProductMaterialRequirement
                    {
                        InventoryItemId = first.InventoryItemId,
                        ItemName = first.ItemName,
                        UnitName = first.UnitName,
                        RequiredQuantity = group.Sum(item => item.RequiredQuantity),
                        QuantityOnHand = first.QuantityOnHand
                    };
                })
                .ToList();
        }

        private static string NormalizeSqlText(string value)
        {
            string trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private sealed class RecipeItem
        {
            public string ItemName { get; set; }
            public decimal Quantity { get; set; }
        }

        private sealed class ProductMaterialRequirement
        {
            public int InventoryItemId { get; set; }
            public string ItemName { get; set; }
            public string UnitName { get; set; }
            public decimal RequiredQuantity { get; set; }
            public decimal QuantityOnHand { get; set; }
        }
    }

    public sealed class ProductAvailabilityInfo
    {
        public int ProductId { get; set; }
        public bool IsAvailable { get; set; }
        public string AvailabilityText { get; set; }
    }

    public sealed class InventoryRow
    {
        public int InventoryItemId { get; set; }
        public string ItemName { get; set; }
        public string UnitName { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal MinQuantity { get; set; }

        public string QuantityText => $"{QuantityOnHand:N3} {UnitName}";
        public string MinQuantityText => $"{MinQuantity:N3} {UnitName}";
        public string StatusText => QuantityOnHand <= MinQuantity ? "Нужно пополнить" : "В норме";
    }

    public sealed class InventoryShortage
    {
        public int InventoryItemId { get; set; }
        public string ItemName { get; set; }
        public string UnitName { get; set; }
        public decimal RequiredQuantity { get; set; }
        public decimal QuantityOnHand { get; set; }
    }
}
