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
                      WHERE IsActive = 1
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
                    @"INSERT INTO dbo.InventoryItems (ItemName, UnitName, QuantityOnHand, MinQuantity, IsActive)
                      VALUES (@itemName, @unitName, @quantityOnHand, @minQuantity, 1)",
                    new SqlParameter("@itemName", normalizedItemName),
                    new SqlParameter("@unitName", normalizedUnitName),
                    new SqlParameter("@quantityOnHand", quantityOnHand),
                    new SqlParameter("@minQuantity", minQuantity));
            }
        }

        public static void UpdateInventoryItem(int inventoryItemId, string itemName, string unitName, decimal quantityOnHand, decimal minQuantity)
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

                bool duplicateExists = db.Database.SqlQuery<int>(
                    @"SELECT COUNT(1)
                      FROM dbo.InventoryItems
                      WHERE InventoryItemId <> @inventoryItemId AND ItemName = @itemName",
                    new SqlParameter("@inventoryItemId", inventoryItemId),
                    new SqlParameter("@itemName", normalizedItemName)).Single() > 0;

                if (duplicateExists)
                {
                    throw new InvalidOperationException("Такая запчасть уже есть на складе.");
                }

                int affectedRows = db.Database.ExecuteSqlCommand(
                    @"UPDATE dbo.InventoryItems
                      SET ItemName = @itemName,
                          UnitName = @unitName,
                          QuantityOnHand = @quantityOnHand,
                          MinQuantity = @minQuantity,
                          IsActive = 1
                      WHERE InventoryItemId = @inventoryItemId",
                    new SqlParameter("@inventoryItemId", inventoryItemId),
                    new SqlParameter("@itemName", normalizedItemName),
                    new SqlParameter("@unitName", normalizedUnitName),
                    new SqlParameter("@quantityOnHand", quantityOnHand),
                    new SqlParameter("@minQuantity", minQuantity));

                if (affectedRows == 0)
                {
                    throw new InvalidOperationException("Материал не найден.");
                }
            }
        }

        public static List<ProductMaterialRow> GetProductMaterialRows(int productId)
        {
            using (var db = new DB_Xammer_groupEntities())
            {
                EnsureSchemaAndSeed(db);

                return db.Database.SqlQuery<ProductMaterialRow>(
                    @"SELECT
                          pm.ProductMaterialId,
                          pm.ProductId,
                          ii.InventoryItemId,
                          ii.ItemName,
                          ii.UnitName,
                          pm.Quantity
                      FROM dbo.ProductMaterials pm
                      INNER JOIN dbo.InventoryItems ii ON ii.InventoryItemId = pm.InventoryItemId
                      WHERE pm.ProductId = @productId
                      ORDER BY ii.ItemName",
                    new SqlParameter("@productId", productId)).ToList();
            }
        }

        public static void SaveProductMaterial(int productId, int inventoryItemId, decimal quantity)
        {
            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            using (var db = new DB_Xammer_groupEntities())
            {
                EnsureSchemaAndSeed(db);

                db.Database.ExecuteSqlCommand(
                    @"IF EXISTS (SELECT 1 FROM dbo.ProductMaterials WHERE ProductId = @productId AND InventoryItemId = @inventoryItemId)
                      BEGIN
                          UPDATE dbo.ProductMaterials
                          SET Quantity = @quantity
                          WHERE ProductId = @productId AND InventoryItemId = @inventoryItemId;
                      END
                      ELSE
                      BEGIN
                          INSERT INTO dbo.ProductMaterials (ProductId, InventoryItemId, Quantity)
                          VALUES (@productId, @inventoryItemId, @quantity);
                      END",
                    new SqlParameter("@productId", productId),
                    new SqlParameter("@inventoryItemId", inventoryItemId),
                    new SqlParameter("@quantity", quantity));
            }
        }

        public static void DeleteProductMaterial(int productMaterialId)
        {
            using (var db = new DB_Xammer_groupEntities())
            {
                EnsureSchemaAndSeed(db);
                db.Database.ExecuteSqlCommand(
                    "DELETE FROM dbo.ProductMaterials WHERE ProductMaterialId = @productMaterialId",
                    new SqlParameter("@productMaterialId", productMaterialId));
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
                              ELSE CAST(1 AS bit)
                          END AS HasMaterials,
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

            var productsWithoutMaterials = GetProductsWithoutMaterials(db, cartItems);
            if (productsWithoutMaterials.Count > 0)
            {
                throw new InvalidOperationException(BuildMissingMaterialsMessage(productsWithoutMaterials));
            }

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
                    int affectedRows = db.Database.ExecuteSqlCommand(
                        @"UPDATE dbo.InventoryItems
                          SET QuantityOnHand = QuantityOnHand - @quantity
                          WHERE InventoryItemId = @inventoryItemId
                            AND QuantityOnHand >= @quantity",
                        new SqlParameter("@quantity", material.RequiredQuantity),
                        new SqlParameter("@inventoryItemId", material.InventoryItemId));

                    if (affectedRows == 0)
                    {
                        throw new InvalidOperationException(
                            $"Не удалось списать материал \"{material.ItemName}\" со склада. Проверьте остаток.");
                    }

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

        public static string BuildMissingMaterialsMessage(IEnumerable<string> productNames)
        {
            return "Для этих изделий не задан состав:\n" + string.Join("\n", productNames);
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
                          MinQuantity decimal(12, 3) NOT NULL CONSTRAINT DF_InventoryItems_MinQuantity DEFAULT 0,
                          IsActive bit NOT NULL CONSTRAINT DF_InventoryItems_IsActive DEFAULT 1
                      );
                  END");

            db.Database.ExecuteSqlCommand(
                @"IF COL_LENGTH(N'dbo.InventoryItems', N'IsActive') IS NULL
                  BEGIN
                      ALTER TABLE dbo.InventoryItems
                      ADD IsActive bit NOT NULL CONSTRAINT DF_InventoryItems_IsActive DEFAULT 1;
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
            EnsureInventoryItem(db, "Металлическое изделие для люка", "шт.", 500, 40);
            EnsureInventoryItem(db, "Металлическое изделие для дверей", "шт.", 500, 40);
            EnsureInventoryItem(db, "Наполнитель EIS 30", "шт.", 500, 40);
            EnsureInventoryItem(db, "Наполнитель EIS 60", "шт.", 500, 40);
            EnsureInventoryItem(db, "Метизы для люка", "шт.", 2000, 240);
            EnsureInventoryItem(db, "Метизы для двери", "шт.", 2000, 280);
            EnsureInventoryItem(db, "Краска", "кг", 250, 25);
            EnsureInventoryItem(db, "Плитка", "шт.", 250, 25);
            EnsureInventoryItem(db, "Плитка Керлит", "шт.", 250, 25);
            EnsureInventoryItem(db, "Упаковка для люка", "шт.", 300, 30);
            EnsureInventoryItem(db, "Упаковка для двери", "шт.", 300, 30);
            EnsureInventoryItem(db, "Паспорт", "шт.", 300, 30);
            EnsureInventoryItem(db, "Шильд люк", "шт.", 150, 15);
            EnsureInventoryItem(db, "Шильд дверь", "шт.", 150, 15);
            EnsureInventoryItem(db, "Замок", "шт.", 200, 20);
            EnsureInventoryItem(db, "Ручка огнестойкая", "шт.", 200, 20);
            EnsureInventoryItem(db, "Ручка", "шт.", 200, 20);
            EnsureInventoryItem(db, "Фурнитура", "шт.", 500, 100);
            EnsureInventoryItem(db, "Стекло", "шт.", 100, 10);
            EnsureInventoryItem(db, "Петли люк 90\"", "шт.", 300, 30);
            EnsureInventoryItem(db, "Петли люк 180\"", "шт.", 300, 30);
            EnsureInventoryItem(db, "Петли дверь 90\"", "шт.", 300, 30);
            EnsureInventoryItem(db, "Петли дверь 180\"", "шт.", 300, 30);

            DeactivateLegacyInventoryItems(db);
        }

        private static void SeedProductsAndRecipes(DB_Xammer_groupEntities db)
        {
            foreach (var product in GetSeedProducts())
            {
                int productId = EnsureProduct(db, product.Name, product.Description, product.Price);
                SetRecipe(db, productId, BuildRecipe(product));
            }

            DeactivateLegacyProducts(db);
        }

        private static IEnumerable<ProductDefinition> GetSeedProducts()
        {
            return new[]
            {
                Product("Люк Прометей одностворчатый EIS-30", ProductKind.Hatch, 5000, "Одностворчатый противопожарный люк серии Прометей с пределом огнестойкости EIS-30, металлическим корпусом, замком, шильдом и петлями открывания до 90 градусов.", eisRating: 30),
                Product("Люк Прометей одностворчатый EIS-60", ProductKind.Hatch, 6000, "Одностворчатый люк Прометей с усиленным заполнением EIS-60 для помещений с повышенными требованиями к защите от огня и дыма.", eisRating: 60),
                Product("Люк Прометей двустворчатый EIS-30", ProductKind.Hatch, 7000, "Двустворчатый противопожарный люк Прометей EIS-30 для широких проемов; комплектуется двумя замками, огнестойкими ручками и увеличенным набором метизов.", isDouble: true, eisRating: 30),
                Product("Люк Прометей двустворчатый EIS-60", ProductKind.Hatch, 8000, "Двустворчатый люк Прометей EIS-60 с усиленным наполнителем и расширенной фурнитурой для широких технических проемов.", isDouble: true, eisRating: 60),
                Product("Люк Пионер одностворчатый", ProductKind.Hatch, 4500, "Одностворчатый технический люк Пионер без огнестойкого наполнителя для стандартного доступа к инженерным коммуникациям."),
                Product("Люк Пионер двустворчатый", ProductKind.Hatch, 5000, "Двустворчатый технический люк Пионер для широких проемов без требований по огнестойкости; рассчитан на удобное обслуживание коммуникаций.", isDouble: true),
                Product("Люк Пионер одностворчатый пожарный", ProductKind.Hatch, 6000, "Одностворчатый пожарный люк Пионер с огнестойкой ручкой, наполнителем EIS-30 и комплектом обязательной маркировки.", isFire: true),
                Product("Люк Пионер двустворчатый пожарный", ProductKind.Hatch, 6500, "Двустворчатый пожарный люк Пионер для широких проемов, с двумя замками, огнестойкими ручками и усиленным комплектом крепежа.", isDouble: true, isFire: true),
                Product("Люк Хаммер одностворчатый", ProductKind.Hatch, 4000, "Одностворчатый технический люк Хаммер базовой комплектации для помещений без требований по защите от огня и дыма."),
                Product("Люк Хаммер двустворчатый", ProductKind.Hatch, 4500, "Двустворчатый технический люк Хаммер для широких проемов; комплектуется двойным набором замков, ручек и фурнитуры.", isDouble: true),
                Product("Люк Хаммер одностворчатый пожарный", ProductKind.Hatch, 5000, "Одностворчатый пожарный люк Хаммер с огнестойкой ручкой, противопожарным наполнителем и шильдом люка.", isFire: true),
                Product("Люк Хаммер двустворчатый пожарный", ProductKind.Hatch, 5500, "Двустворчатый пожарный люк Хаммер с увеличенной комплектацией фурнитуры и противопожарным наполнителем.", isDouble: true, isFire: true),
                Product("Люк Хаммер одностворчатый 180\"", ProductKind.Hatch, 4250, "Одностворчатый люк Хаммер с петлями открывания до 180 градусов для удобного доступа к обслуживаемому проему.", hasHinge180: true),
                Product("Люк Хаммер двустворчатый 180\"", ProductKind.Hatch, 4750, "Двустворчатый люк Хаммер с петлями 180 градусов, рассчитанный на широкие проемы и частое обслуживание.", isDouble: true, hasHinge180: true),
                Product("Люк Хаммер одностворчатый пожарный 180\"", ProductKind.Hatch, 5250, "Одностворчатый пожарный люк Хаммер с петлями 180 градусов, огнестойкой ручкой и противопожарным наполнителем.", isFire: true, hasHinge180: true),
                Product("Люк Хаммер двустворчатый пожарный 180\"", ProductKind.Hatch, 5750, "Двустворчатый пожарный люк Хаммер с петлями 180 градусов, расширенной фурнитурой и комплектом противопожарной оснастки.", isDouble: true, isFire: true, hasHinge180: true),
                Product("Люк Гиппократ под краску", ProductKind.Hatch, 8000, "Скрытый люк Гиппократ с подготовкой поверхности под окраску, металлической основой и комплектом фурнитуры.", paint: true),
                Product("Люк Гиппократ под плитку", ProductKind.Hatch, 10000, "Скрытый люк Гиппократ под облицовку плиткой, с усиленной фурнитурой и комплектом для монтажа в отделку.", tile: true),
                Product("Люк Стикер", ProductKind.Hatch, 1500, "Компактный ревизионный люк Стикер для легкого доступа к коммуникациям без декоративной отделки и огнестойкого наполнителя.", skipDefaultPaint: true),
                Product("Люк Ветерок под краску", ProductKind.Hatch, 3000, "Люк Ветерок с подготовкой под окраску для аккуратного доступа к вентиляционным и инженерным узлам.", paint: true),
                Product("Люк Ветерок под плитку", ProductKind.Hatch, 3750, "Люк Ветерок под плиточную отделку с комплектом крепежа и фурнитуры для скрытого монтажа.", tile: true),
                Product("Дверь одностворчатая EIS-30", ProductKind.Door, 12000, "Одностворчатая противопожарная дверь EIS-30 с металлическим полотном, наполнителем, огнестойкой ручкой и маркировочным шильдом.", eisRating: 30),
                Product("Дверь одностворчатая EIS-60", ProductKind.Door, 13000, "Одностворчатая дверь EIS-60 с усиленным противопожарным наполнителем для объектов с повышенными требованиями безопасности.", eisRating: 60),
                Product("Дверь двустворчатая EIS-30", ProductKind.Door, 14000, "Двустворчатая противопожарная дверь EIS-30 для широких проемов, с двойным комплектом замков, ручек и фурнитуры.", isDouble: true, eisRating: 30),
                Product("Дверь двустворчатая EIS-60", ProductKind.Door, 15000, "Двустворчатая дверь EIS-60 с усиленным наполнением, комплектом метизов и фурнитурой для широких проемов.", isDouble: true, eisRating: 60),
                Product("Дверь техническая", ProductKind.Door, 10000, "Техническая металлическая дверь без противопожарного наполнителя для служебных и инженерных помещений."),
                Product("Дверь остекленная", ProductKind.Door, 10000, "Металлическая остекленная дверь без противопожарного наполнителя, укомплектованная стеклом, замком и стандартной фурнитурой.", glass: true),
                Product("Дверь одностворчатая Керлит EIS-30", ProductKind.Door, 15000, "Одностворчатая дверь Керлит EIS-30 с петлями 180 градусов и подготовкой под отделку плиткой Керлит.", eisRating: 30, kerlit: true, hasHinge180: true),
                Product("Дверь одностворчатая Керлит EIS-60", ProductKind.Door, 17000, "Одностворчатая дверь Керлит EIS-60 с усиленным наполнителем, петлями 180 градусов и отделкой под плитку Керлит.", eisRating: 60, kerlit: true, hasHinge180: true),
                Product("Дверь двустворчатая Керлит EIS-30", ProductKind.Door, 18000, "Двустворчатая дверь Керлит EIS-30 для широких проемов, с петлями 180 градусов и комплектом плитки Керлит.", isDouble: true, eisRating: 30, kerlit: true, hasHinge180: true),
                Product("Дверь двустворчатая Керлит EIS-60", ProductKind.Door, 20000, "Двустворчатая дверь Керлит EIS-60 с усиленным противопожарным наполнителем, петлями 180 градусов и облицовкой Керлит.", isDouble: true, eisRating: 60, kerlit: true, hasHinge180: true)
            };
        }

        private static ProductDefinition Product(
            string name,
            ProductKind kind,
            decimal price,
            string description,
            bool isDouble = false,
            int? eisRating = null,
            bool isFire = false,
            bool hasHinge180 = false,
            bool paint = false,
            bool tile = false,
            bool kerlit = false,
            bool glass = false,
            bool skipDefaultPaint = false)
        {
            return new ProductDefinition
            {
                Name = name,
                Description = description,
                Price = price,
                Kind = kind,
                IsDouble = isDouble,
                EisRating = eisRating,
                IsFire = isFire,
                HasHinge180 = hasHinge180,
                Paint = paint,
                Tile = tile,
                Kerlit = kerlit,
                Glass = glass,
                SkipDefaultPaint = skipDefaultPaint
            };
        }

        private static IEnumerable<RecipeItem> BuildRecipe(ProductDefinition product)
        {
            int leafCount = product.IsDouble ? 2 : 1;
            decimal bodyQuantity = product.IsDouble ? 8 : 6;
            bool isHatch = product.Kind == ProductKind.Hatch;
            bool isFireRated = product.EisRating.HasValue || product.IsFire;
            var recipe = new List<RecipeItem>
            {
                Recipe(isHatch ? "Металлическое изделие для люка" : "Металлическое изделие для дверей", bodyQuantity),
                Recipe(isHatch ? "Метизы для люка" : "Метизы для двери", isHatch ? (product.IsDouble ? 24 : 12) : (product.IsDouble ? 28 : 14)),
                Recipe(isHatch ? "Упаковка для люка" : "Упаковка для двери", 1),
                Recipe("Паспорт", 1),
                Recipe(isHatch ? "Шильд люк" : "Шильд дверь", 1),
                Recipe("Замок", leafCount),
                Recipe(isFireRated ? "Ручка огнестойкая" : "Ручка", leafCount),
                Recipe("Фурнитура", product.IsDouble ? 10 : 5),
                Recipe(GetHingeItemName(product), leafCount)
            };

            if (isFireRated)
            {
                recipe.Add(Recipe(product.EisRating == 60 ? "Наполнитель EIS 60" : "Наполнитель EIS 30", bodyQuantity));
            }

            if (product.Kerlit)
            {
                recipe.Add(Recipe("Плитка Керлит", leafCount));
            }
            else if (product.Tile)
            {
                recipe.Add(Recipe("Плитка", leafCount));
            }
            else if (product.Paint || !product.SkipDefaultPaint)
            {
                recipe.Add(Recipe("Краска", leafCount));
            }

            if (product.Glass)
            {
                recipe.Add(Recipe("Стекло", leafCount));
            }

            return recipe;
        }

        private static string GetHingeItemName(ProductDefinition product)
        {
            if (product.Kind == ProductKind.Hatch)
            {
                return product.HasHinge180 ? "Петли люк 180\"" : "Петли люк 90\"";
            }

            return product.HasHinge180 ? "Петли дверь 180\"" : "Петли дверь 90\"";
        }

        private static void DeactivateLegacyInventoryItems(DB_Xammer_groupEntities db)
        {
            db.Database.ExecuteSqlCommand(
                @"UPDATE dbo.InventoryItems
                  SET IsActive = 0
                  WHERE ItemName IN (
                      N'Металлическое изделие',
                      N'Уплотнитель',
                      N'Метизы',
                      N'Упаковка',
                      N'Шильд универсальный'
                  )");
        }

        private static void DeactivateLegacyProducts(DB_Xammer_groupEntities db)
        {
            var legacyProducts = db.Products
                .Where(item => item.ProductName == "Люк" ||
                               item.ProductName == "Дверь" ||
                               item.ProductName == "Другое" ||
                               item.ProductName == "Люк Пионер одностворчатый (без защиты от дыма и огня)" ||
                               item.ProductName == "Люк Пионер двустворчатый (без защиты от дыма и огня)" ||
                               item.ProductName == "Люк Хаммер одностворчатый (без защиты от дыма и огня)" ||
                               item.ProductName == "Люк Хаммер двустворчатый (без защиты от дыма и огня)" ||
                               item.ProductName == "Люк Хаммер одностворчатый 180\" (без защиты от дыма и огня)" ||
                               item.ProductName == "Люк Хаммер двустворчатый 180\" (без защиты от дыма и огня)" ||
                               item.ProductName == "Дверь техническая (без защиты от огня и дыма)" ||
                               item.ProductName == "Дверь остекленная (без защиты от огня и дыма)")
                .ToList();

            foreach (var product in legacyProducts)
            {
                product.IsActive = false;
            }

            if (legacyProducts.Count > 0)
            {
                db.SaveChanges();
            }
        }

        private static void EnsureInventoryItem(DB_Xammer_groupEntities db, string itemName, string unitName, decimal quantity, decimal minQuantity)
        {
            db.Database.ExecuteSqlCommand(
                @"IF NOT EXISTS (SELECT 1 FROM dbo.InventoryItems WHERE ItemName = @itemName)
                  BEGIN
                      INSERT INTO dbo.InventoryItems (ItemName, UnitName, QuantityOnHand, MinQuantity, IsActive)
                      VALUES (@itemName, @unitName, @quantity, @minQuantity, 1);
                  END
                  ELSE
                  BEGIN
                      UPDATE dbo.InventoryItems
                      SET UnitName = @unitName,
                          MinQuantity = @minQuantity,
                          IsActive = 1
                      WHERE ItemName = @itemName;
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
                product.Description = description;
                product.Price = price;
                product.IsActive = true;
                db.SaveChanges();

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
            db.Database.ExecuteSqlCommand(
                "DELETE FROM dbo.ProductMaterials WHERE ProductId = @productId",
                new SqlParameter("@productId", productId));

            foreach (var item in recipe)
            {
                db.Database.ExecuteSqlCommand(
                    @"DECLARE @inventoryItemId int;
                      SELECT @inventoryItemId = InventoryItemId
                      FROM dbo.InventoryItems
                      WHERE ItemName = @itemName;

                      IF @inventoryItemId IS NOT NULL
                      BEGIN
                          INSERT INTO dbo.ProductMaterials (ProductId, InventoryItemId, Quantity)
                          VALUES (@productId, @inventoryItemId, @quantity);
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

        private static List<string> GetProductsWithoutMaterials(DB_Xammer_groupEntities db, IReadOnlyList<CartItem> cartItems)
        {
            var productIds = cartItems
                .Where(item => item.Quantity > 0)
                .Select(item => item.ProductId)
                .Distinct()
                .ToList();

            if (productIds.Count == 0)
            {
                return new List<string>();
            }

            return db.Products
                .Where(product => productIds.Contains(product.ProductId) && !product.ProductMaterials.Any())
                .OrderBy(product => product.ProductName)
                .Select(product => product.ProductName)
                .ToList();
        }

        private static string NormalizeSqlText(string value)
        {
            string trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private enum ProductKind
        {
            Hatch,
            Door
        }

        private sealed class ProductDefinition
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
            public ProductKind Kind { get; set; }
            public bool IsDouble { get; set; }
            public int? EisRating { get; set; }
            public bool IsFire { get; set; }
            public bool HasHinge180 { get; set; }
            public bool Paint { get; set; }
            public bool Tile { get; set; }
            public bool Kerlit { get; set; }
            public bool Glass { get; set; }
            public bool SkipDefaultPaint { get; set; }
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
        public bool HasMaterials { get; set; }
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

    public sealed class ProductMaterialRow
    {
        public int ProductMaterialId { get; set; }
        public int ProductId { get; set; }
        public int InventoryItemId { get; set; }
        public string ItemName { get; set; }
        public string UnitName { get; set; }
        public decimal Quantity { get; set; }

        public string QuantityText => $"{Quantity:N3} {UnitName}";
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
