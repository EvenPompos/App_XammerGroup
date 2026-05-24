using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace App_XammerGroup
{
    public partial class AdminPage : Page
    {
        private readonly int _currentUserId;
        private readonly bool _canManageEmployees;

        public AdminPage(int currentUserId, AdminSection initialSection, bool canManageEmployees = true)
        {
            InitializeComponent();

            _currentUserId = currentUserId;
            _canManageEmployees = canManageEmployees;

            Loaded += (sender, args) =>
            {
                if (!_canManageEmployees && AdminTabs.Items.Contains(EmployeesTab))
                {
                    AdminTabs.Items.Remove(EmployeesTab);
                }

                LoadRoles();
                LoadProducts();
                LoadMaterialItems();
                if (_canManageEmployees)
                {
                    LoadEmployees();
                }
                SelectSection(initialSection);
            };
        }

        private void SelectSection(AdminSection section)
        {
            AdminTabs.SelectedItem = section == AdminSection.Employees
                && _canManageEmployees
                ? EmployeesTab
                : ProductsTab;
        }

        private void LoadRoles()
        {
            using (var db = new DB_Xammer_groupEntities())
            {
                var roles = db.Roles
                    .ToList()
                    .Where(role => !IsClientRole(role.RoleName))
                    .OrderBy(role => role.RoleName)
                    .ToList();

                RoleBox.ItemsSource = roles;
                RoleBox.DisplayMemberPath = nameof(Roles.RoleName);
                RoleBox.SelectedValuePath = nameof(Roles.RoleId);
            }
        }

        private void LoadProducts()
        {
            using (var db = new DB_Xammer_groupEntities())
            {
                InventoryService.EnsureSchemaAndSeed(db);

                ProductsGrid.ItemsSource = db.Products
                    .OrderBy(product => product.ProductName)
                    .ToList()
                    .Select(product => new ProductRow
                    {
                        ProductId = product.ProductId,
                        ProductName = product.ProductName,
                        Description = product.Description,
                        Price = product.Price,
                        ImagePath = product.ImagePath,
                        IsActive = product.IsActive ?? false
                    })
                    .ToList();
            }
        }

        private void LoadMaterialItems()
        {
            MaterialItemBox.ItemsSource = InventoryService.GetInventoryRows();
            MaterialItemBox.DisplayMemberPath = nameof(InventoryRow.ItemName);
            MaterialItemBox.SelectedValuePath = nameof(InventoryRow.InventoryItemId);
        }

        private void LoadProductMaterials(int productId)
        {
            ProductMaterialsGrid.ItemsSource = InventoryService.GetProductMaterialRows(productId);
        }

        private void LoadEmployees()
        {
            using (var db = new DB_Xammer_groupEntities())
            {
                EmployeesGrid.ItemsSource = db.Users
                    .Include(user => user.Roles)
                    .ToList()
                    .Where(user => !IsClientRole(user.Roles?.RoleName))
                    .OrderBy(user => user.LastName)
                    .ThenBy(user => user.FirstName)
                    .Select(user => new EmployeeRow
                    {
                        UserId = user.UserId,
                        LastName = user.LastName,
                        FirstName = user.FirstName,
                        MiddleName = user.MiddleName,
                        Phone = user.Phone,
                        Email = user.Email,
                        Password = user.Password,
                        RoleId = user.RoleId,
                        RoleName = user.Roles?.RoleName,
                        FullName = BuildFullName(user)
                    })
                    .ToList();
            }
        }

        private void ProductsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var product = ProductsGrid.SelectedItem as ProductRow;
            if (product == null)
            {
                ClearProductForm();
                ProductMaterialsGrid.ItemsSource = null;
                return;
            }

            ProductNameBox.Text = product.ProductName ?? string.Empty;
            ProductDescriptionBox.Text = product.Description ?? string.Empty;
            ProductPriceBox.Text = product.Price.ToString(CultureInfo.CurrentCulture);
            ProductImagePathBox.Text = product.ImagePath ?? string.Empty;
            ProductIsActiveBox.IsChecked = product.IsActive;
            LoadProductMaterials(product.ProductId);
        }

        private void ProductMaterialsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var material = ProductMaterialsGrid.SelectedItem as ProductMaterialRow;
            if (material == null)
            {
                return;
            }

            MaterialItemBox.SelectedValue = material.InventoryItemId;
            MaterialQuantityBox.Text = material.Quantity.ToString(CultureInfo.CurrentCulture);
        }

        private void EmployeesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var employee = EmployeesGrid.SelectedItem as EmployeeRow;
            if (employee == null)
            {
                ClearEmployeeForm();
                return;
            }

            LastNameBox.Text = employee.LastName ?? string.Empty;
            FirstNameBox.Text = employee.FirstName ?? string.Empty;
            MiddleNameBox.Text = employee.MiddleName ?? string.Empty;
            PhoneBox.Text = employee.Phone ?? string.Empty;
            EmailBox.Text = employee.Email ?? string.Empty;
            PasswordBox.Password = employee.Password ?? string.Empty;
            RoleBox.SelectedValue = employee.RoleId;
        }

        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            ProductsErrorText.Foreground = ErrorBrush();
            ProductsErrorText.Text = string.Empty;

            if (!TryReadProductForm(out string name, out string description, out decimal price, out string imagePath, out bool isActive))
            {
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    db.Products.Add(new Products
                    {
                        ProductName = name,
                        Description = description,
                        Price = price,
                        ImagePath = imagePath,
                        IsActive = isActive
                    });

                    db.SaveChanges();
                }

                ClearProductForm();
                LoadProducts();
                ProductsErrorText.Foreground = SuccessBrush();
                ProductsErrorText.Text = "Товар добавлен.";
            }
            catch (Exception ex)
            {
                ProductsErrorText.Text = "Не удалось добавить товар.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateProduct_Click(object sender, RoutedEventArgs e)
        {
            ProductsErrorText.Foreground = ErrorBrush();
            ProductsErrorText.Text = string.Empty;

            var selectedProduct = ProductsGrid.SelectedItem as ProductRow;
            if (selectedProduct == null)
            {
                ProductsErrorText.Text = "Выберите товар для редактирования.";
                return;
            }

            if (!TryReadProductForm(out string name, out string description, out decimal price, out string imagePath, out bool isActive))
            {
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    var product = db.Products.FirstOrDefault(item => item.ProductId == selectedProduct.ProductId);
                    if (product == null)
                    {
                        ProductsErrorText.Text = "Товар не найден.";
                        return;
                    }

                    product.ProductName = name;
                    product.Description = description;
                    product.Price = price;
                    product.ImagePath = imagePath;
                    product.IsActive = isActive;
                    db.SaveChanges();
                }

                LoadProducts();
                ProductsErrorText.Foreground = SuccessBrush();
                ProductsErrorText.Text = "Изменения товара сохранены.";
            }
            catch (Exception ex)
            {
                ProductsErrorText.Text = "Не удалось сохранить товар.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            ProductsErrorText.Foreground = ErrorBrush();
            ProductsErrorText.Text = string.Empty;

            var product = ProductsGrid.SelectedItem as ProductRow;
            if (product == null)
            {
                ProductsErrorText.Text = "Выберите товар для удаления.";
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    var entity = db.Products.FirstOrDefault(item => item.ProductId == product.ProductId);
                    if (entity == null)
                    {
                        ProductsErrorText.Text = "Товар не найден.";
                        return;
                    }

                    bool hasItems = db.OrderItems.Any(item => item.ProductId == product.ProductId);
                    if (hasItems)
                    {
                        entity.IsActive = false;
                    }
                    else
                    {
                        db.Database.ExecuteSqlCommand(
                            "DELETE FROM dbo.ProductMaterials WHERE ProductId = @productId",
                            new SqlParameter("@productId", product.ProductId));
                        db.Products.Remove(entity);
                    }

                    db.SaveChanges();
                }

                ClearProductForm();
                ProductMaterialsGrid.ItemsSource = null;
                LoadProducts();
                ProductsErrorText.Foreground = SuccessBrush();
                ProductsErrorText.Text = "Товар удален или скрыт, если уже использовался в заказах.";
            }
            catch (Exception ex)
            {
                ProductsErrorText.Text = "Не удалось удалить товар.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveMaterial_Click(object sender, RoutedEventArgs e)
        {
            ProductsErrorText.Foreground = ErrorBrush();
            ProductsErrorText.Text = string.Empty;

            var product = ProductsGrid.SelectedItem as ProductRow;
            if (product == null)
            {
                ProductsErrorText.Text = "Выберите товар для настройки состава.";
                return;
            }

            if (!(MaterialItemBox.SelectedValue is int inventoryItemId))
            {
                ProductsErrorText.Text = "Выберите деталь со склада.";
                return;
            }

            if (!TryParseDecimal(MaterialQuantityBox.Text, out decimal quantity) || quantity <= 0)
            {
                ProductsErrorText.Text = "Введите положительное количество списания.";
                return;
            }

            try
            {
                InventoryService.SaveProductMaterial(product.ProductId, inventoryItemId, quantity);
                LoadProductMaterials(product.ProductId);
                ProductsErrorText.Foreground = SuccessBrush();
                ProductsErrorText.Text = "Состав товара сохранен.";
            }
            catch (Exception ex)
            {
                ProductsErrorText.Text = "Не удалось сохранить состав товара.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteMaterial_Click(object sender, RoutedEventArgs e)
        {
            ProductsErrorText.Foreground = ErrorBrush();
            ProductsErrorText.Text = string.Empty;

            var product = ProductsGrid.SelectedItem as ProductRow;
            var material = ProductMaterialsGrid.SelectedItem as ProductMaterialRow;
            if (product == null || material == null)
            {
                ProductsErrorText.Text = "Выберите строку состава для удаления.";
                return;
            }

            try
            {
                InventoryService.DeleteProductMaterial(material.ProductMaterialId);
                LoadProductMaterials(product.ProductId);
                MaterialQuantityBox.Text = string.Empty;
                ProductsErrorText.Foreground = SuccessBrush();
                ProductsErrorText.Text = "Деталь удалена из состава товара.";
            }
            catch (Exception ex)
            {
                ProductsErrorText.Text = "Не удалось удалить деталь из состава.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddEmployee_Click(object sender, RoutedEventArgs e)
        {
            EmployeesErrorText.Foreground = ErrorBrush();
            EmployeesErrorText.Text = string.Empty;

            if (!TryReadEmployeeForm(out string lastName, out string firstName, out string middleName, out string phone, out string email, out string password, out int roleId))
            {
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    if (db.Users.Any(user => user.Email == email))
                    {
                        EmployeesErrorText.Text = "Пользователь с таким email уже существует.";
                        return;
                    }

                    db.Users.Add(new Users
                    {
                        LastName = lastName,
                        FirstName = firstName,
                        MiddleName = middleName,
                        Phone = phone,
                        Email = email,
                        Password = password,
                        RoleId = roleId,
                        CreatedDate = DateTime.Now
                    });

                    db.SaveChanges();
                }

                ClearEmployeeForm();
                LoadEmployees();
                EmployeesErrorText.Foreground = SuccessBrush();
                EmployeesErrorText.Text = "Сотрудник добавлен.";
            }
            catch (Exception ex)
            {
                EmployeesErrorText.Text = "Не удалось добавить сотрудника.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateEmployee_Click(object sender, RoutedEventArgs e)
        {
            EmployeesErrorText.Foreground = ErrorBrush();
            EmployeesErrorText.Text = string.Empty;

            var selectedEmployee = EmployeesGrid.SelectedItem as EmployeeRow;
            if (selectedEmployee == null)
            {
                EmployeesErrorText.Text = "Выберите сотрудника для редактирования.";
                return;
            }

            if (!TryReadEmployeeForm(out string lastName, out string firstName, out string middleName, out string phone, out string email, out string password, out int roleId))
            {
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    var user = db.Users.FirstOrDefault(item => item.UserId == selectedEmployee.UserId);
                    if (user == null)
                    {
                        EmployeesErrorText.Text = "Сотрудник не найден.";
                        return;
                    }

                    if (db.Users.Any(item => item.UserId != selectedEmployee.UserId && item.Email == email))
                    {
                        EmployeesErrorText.Text = "Пользователь с таким email уже существует.";
                        return;
                    }

                    user.LastName = lastName;
                    user.FirstName = firstName;
                    user.MiddleName = middleName;
                    user.Phone = phone;
                    user.Email = email;
                    user.Password = password;
                    user.RoleId = roleId;
                    db.SaveChanges();
                }

                LoadEmployees();
                EmployeesErrorText.Foreground = SuccessBrush();
                EmployeesErrorText.Text = "Изменения сотрудника сохранены.";
            }
            catch (Exception ex)
            {
                EmployeesErrorText.Text = "Не удалось сохранить сотрудника.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteEmployee_Click(object sender, RoutedEventArgs e)
        {
            EmployeesErrorText.Foreground = ErrorBrush();
            EmployeesErrorText.Text = string.Empty;

            var employee = EmployeesGrid.SelectedItem as EmployeeRow;
            if (employee == null)
            {
                EmployeesErrorText.Text = "Выберите сотрудника для удаления.";
                return;
            }

            if (employee.UserId == _currentUserId)
            {
                EmployeesErrorText.Text = "Нельзя удалить текущего администратора.";
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    var entity = db.Users.FirstOrDefault(user => user.UserId == employee.UserId);
                    if (entity == null)
                    {
                        EmployeesErrorText.Text = "Сотрудник не найден.";
                        return;
                    }

                    bool hasLinkedOrders = db.Orders.Any(order => order.ManagerId == employee.UserId);
                    bool hasLinkedStages = db.ProductionStages.Any(stage => stage.AssignedWorkerId == employee.UserId);
                    if (hasLinkedOrders || hasLinkedStages)
                    {
                        EmployeesErrorText.Text = "Нельзя удалить сотрудника, который участвует в заказах или этапах производства.";
                        return;
                    }

                    db.Users.Remove(entity);
                    db.SaveChanges();
                }

                ClearEmployeeForm();
                LoadEmployees();
                EmployeesErrorText.Foreground = SuccessBrush();
                EmployeesErrorText.Text = "Сотрудник удален.";
            }
            catch (Exception ex)
            {
                EmployeesErrorText.Text = "Не удалось удалить сотрудника.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryReadProductForm(out string name, out string description, out decimal price, out string imagePath, out bool isActive)
        {
            name = ProductNameBox.Text.Trim();
            description = NormalizeText(ProductDescriptionBox.Text);
            imagePath = NormalizeText(ProductImagePathBox.Text);
            isActive = ProductIsActiveBox.IsChecked ?? false;

            if (string.IsNullOrWhiteSpace(name))
            {
                price = 0;
                ProductsErrorText.Text = "Введите название товара.";
                return false;
            }

            if (!TryParseDecimal(ProductPriceBox.Text, out price) || price < 0)
            {
                ProductsErrorText.Text = "Введите корректную цену.";
                return false;
            }

            return true;
        }

        private bool TryReadEmployeeForm(
            out string lastName,
            out string firstName,
            out string middleName,
            out string phone,
            out string email,
            out string password,
            out int roleId)
        {
            lastName = LastNameBox.Text.Trim();
            firstName = FirstNameBox.Text.Trim();
            middleName = NormalizeText(MiddleNameBox.Text);
            phone = PhoneBox.Text.Trim();
            email = EmailBox.Text.Trim();
            password = PasswordBox.Password.Trim();
            roleId = 0;

            if (string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                EmployeesErrorText.Text = "Заполните фамилию, имя, телефон, email и пароль.";
                return false;
            }

            if (!IsValidEmail(email))
            {
                EmployeesErrorText.Text = "Введите корректный email.";
                return false;
            }

            if (!(RoleBox.SelectedValue is int selectedRoleId))
            {
                EmployeesErrorText.Text = "Выберите роль сотрудника.";
                return false;
            }

            roleId = selectedRoleId;
            return true;
        }

        private void ClearProductForm()
        {
            ProductNameBox.Text = string.Empty;
            ProductDescriptionBox.Text = string.Empty;
            ProductPriceBox.Text = string.Empty;
            ProductImagePathBox.Text = string.Empty;
            ProductIsActiveBox.IsChecked = true;
            MaterialItemBox.SelectedIndex = -1;
            MaterialQuantityBox.Text = string.Empty;
        }

        private void ClearEmployeeForm()
        {
            LastNameBox.Text = string.Empty;
            FirstNameBox.Text = string.Empty;
            MiddleNameBox.Text = string.Empty;
            PhoneBox.Text = string.Empty;
            EmailBox.Text = string.Empty;
            PasswordBox.Password = string.Empty;
            RoleBox.SelectedIndex = -1;
        }

        private static bool TryParseDecimal(string value, out decimal result)
        {
            string normalizedValue = value?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedValue))
            {
                result = 0;
                return false;
            }

            return decimal.TryParse(normalizedValue, NumberStyles.Number, CultureInfo.CurrentCulture, out result)
                || decimal.TryParse(normalizedValue.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out result)
                || decimal.TryParse(normalizedValue.Replace('.', ','), NumberStyles.Number, new CultureInfo("ru-RU"), out result);
        }

        private static string NormalizeText(string value)
        {
            string trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static bool IsClientRole(string roleName)
        {
            string normalizedRole = roleName?.Trim().ToLowerInvariant() ?? string.Empty;
            return normalizedRole.Contains("client")
                || normalizedRole.Contains("user")
                || normalizedRole.Contains("клиент")
                || normalizedRole.Contains("пользователь");
        }

        private static string BuildFullName(Users user)
        {
            string fullName = string.Join(" ", new[]
            {
                user.LastName,
                user.FirstName,
                user.MiddleName
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var address = new MailAddress(email);
                return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static SolidColorBrush ErrorBrush()
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB00020"));
        }

        private static SolidColorBrush SuccessBrush()
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2E7D32"));
        }

        private sealed class ProductRow
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
            public string ImagePath { get; set; }
            public bool IsActive { get; set; }

            public string PriceText => $"{Price:N2} руб.";
            public string ActiveText => IsActive ? "Активен" : "Скрыт";
        }

        private sealed class EmployeeRow
        {
            public int UserId { get; set; }
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string MiddleName { get; set; }
            public string Phone { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
            public int RoleId { get; set; }
            public string FullName { get; set; }
            public string RoleName { get; set; }
        }
    }

    public enum AdminSection
    {
        Products,
        Employees
    }
}
