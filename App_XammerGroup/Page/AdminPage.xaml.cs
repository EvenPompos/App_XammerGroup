using System;
using System.Collections.Generic;
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

        public AdminPage(int currentUserId, AdminSection initialSection)
        {
            InitializeComponent();

            _currentUserId = currentUserId;

            Loaded += (sender, args) =>
            {
                LoadRoles();
                LoadProducts();
                LoadInventory();
                LoadEmployees();
                SelectSection(initialSection);
            };
        }

        private void SelectSection(AdminSection section)
        {
            switch (section)
            {
                case AdminSection.Employees:
                    AdminTabs.SelectedItem = EmployeesTab;
                    break;

                case AdminSection.Inventory:
                    AdminTabs.SelectedItem = InventoryTab;
                    break;

                default:
                    AdminTabs.SelectedItem = ProductsTab;
                    break;
            }
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
                        Price = product.Price,
                        IsActive = product.IsActive ?? false
                    })
                    .ToList();
            }
        }

        private void LoadInventory()
        {
            InventoryGrid.ItemsSource = InventoryService.GetInventoryRows();
        }

        private void LoadEmployees()
        {
            using (var db = new DB_Xammer_groupEntities())
            {
                EmployeesGrid.ItemsSource = db.Users
                    .ToList()
                    .Join(
                        db.Roles.ToList(),
                        user => user.RoleId,
                        role => role.RoleId,
                        (user, role) => new { user, role })
                    .Where(item => !IsClientRole(item.role.RoleName))
                    .OrderBy(item => item.user.LastName)
                    .ThenBy(item => item.user.FirstName)
                    .Select(item => new EmployeeRow
                    {
                        UserId = item.user.UserId,
                        FullName = BuildFullName(item.user),
                        Email = item.user.Email,
                        RoleName = item.role.RoleName
                    })
                    .ToList();
            }
        }

        private void AddProduct_Click(object sender, RoutedEventArgs e)
        {
            ProductsErrorText.Foreground = ErrorBrush();
            ProductsErrorText.Text = string.Empty;

            if (string.IsNullOrWhiteSpace(ProductNameBox.Text))
            {
                ProductsErrorText.Text = "Введите название товара.";
                return;
            }

            if (!TryParseDecimal(ProductPriceBox.Text, out decimal price) || price < 0)
            {
                ProductsErrorText.Text = "Введите корректную цену.";
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    db.Products.Add(new Products
                    {
                        ProductName = ProductNameBox.Text.Trim(),
                        Description = NormalizeText(ProductDescriptionBox.Text),
                        Price = price,
                        ImagePath = NormalizeText(ProductImagePathBox.Text),
                        IsActive = ProductIsActiveBox.IsChecked ?? false
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
                    bool hasRecipe = db.Database.SqlQuery<int>(
                        "SELECT COUNT(1) FROM dbo.ProductMaterials WHERE ProductId = @productId",
                        new SqlParameter("@productId", product.ProductId)).Single() > 0;

                    if (hasItems || hasRecipe)
                    {
                        ProductsErrorText.Text = "\u041d\u0435\u043b\u044c\u0437\u044f \u0443\u0434\u0430\u043b\u0438\u0442\u044c \u0442\u043e\u0432\u0430\u0440, \u043a\u043e\u0442\u043e\u0440\u044b\u0439 \u0443\u0436\u0435 \u0438\u0441\u043f\u043e\u043b\u044c\u0437\u0443\u0435\u0442\u0441\u044f \u0432 \u0437\u0430\u043a\u0430\u0437\u0430\u0445 \u0438\u043b\u0438 \u0441\u043f\u0435\u0446\u0438\u0444\u0438\u043a\u0430\u0446\u0438\u0438.";
                        return;
                    }

                    db.Products.Remove(entity);
                    db.SaveChanges();
                }

                LoadProducts();
                ProductsErrorText.Foreground = SuccessBrush();
                ProductsErrorText.Text = "Товар удален.";
            }
            catch (Exception ex)
            {
                ProductsErrorText.Text = "Не удалось удалить товар.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddInventory_Click(object sender, RoutedEventArgs e)
        {
            InventoryErrorText.Foreground = ErrorBrush();
            InventoryErrorText.Text = string.Empty;

            var selectedItem = InventoryGrid.SelectedItem as InventoryRow;
            if (selectedItem == null)
            {
                InventoryErrorText.Text = "\u0412\u044b\u0431\u0435\u0440\u0438\u0442\u0435 \u043c\u0430\u0442\u0435\u0440\u0438\u0430\u043b \u0434\u043b\u044f \u043f\u043e\u043f\u043e\u043b\u043d\u0435\u043d\u0438\u044f.";
                return;
            }

            if (!TryParseDecimal(InventoryQuantityBox.Text, out decimal quantity) || quantity <= 0)
            {
                InventoryErrorText.Text = "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u043f\u043e\u043b\u043e\u0436\u0438\u0442\u0435\u043b\u044c\u043d\u043e\u0435 \u043a\u043e\u043b\u0438\u0447\u0435\u0441\u0442\u0432\u043e.";
                return;
            }

            try
            {
                InventoryService.AddStock(selectedItem.InventoryItemId, quantity, NormalizeText(InventoryCommentBox.Text));
                InventoryQuantityBox.Text = string.Empty;
                InventoryCommentBox.Text = string.Empty;
                LoadInventory();
                LoadProducts();

                InventoryErrorText.Foreground = SuccessBrush();
                InventoryErrorText.Text = "\u0421\u043a\u043b\u0430\u0434 \u043f\u043e\u043f\u043e\u043b\u043d\u0435\u043d.";
            }
            catch (Exception ex)
            {
                InventoryErrorText.Text = "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u043f\u043e\u043f\u043e\u043b\u043d\u0438\u0442\u044c \u0441\u043a\u043b\u0430\u0434.";
                MessageBox.Show(ex.Message, "\u041e\u0448\u0438\u0431\u043a\u0430", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshInventory_Click(object sender, RoutedEventArgs e)
        {
            LoadInventory();
            LoadProducts();
        }

        private void AddInventoryItem_Click(object sender, RoutedEventArgs e)
        {
            InventoryErrorText.Foreground = ErrorBrush();
            InventoryErrorText.Text = string.Empty;

            string itemName = NewInventoryNameBox.Text.Trim();
            string unitName = NewInventoryUnitBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(unitName))
            {
                InventoryErrorText.Text = "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u043d\u0430\u0437\u0432\u0430\u043d\u0438\u0435 \u0437\u0430\u043f\u0447\u0430\u0441\u0442\u0438 \u0438 \u0435\u0434\u0438\u043d\u0438\u0446\u0443 \u0438\u0437\u043c\u0435\u0440\u0435\u043d\u0438\u044f.";
                return;
            }

            if (!TryParseDecimal(NewInventoryQuantityBox.Text, out decimal quantity) || quantity < 0)
            {
                InventoryErrorText.Text = "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u043a\u043e\u0440\u0440\u0435\u043a\u0442\u043d\u044b\u0439 \u043d\u0430\u0447\u0430\u043b\u044c\u043d\u044b\u0439 \u043e\u0441\u0442\u0430\u0442\u043e\u043a.";
                return;
            }

            if (!TryParseDecimal(NewInventoryMinQuantityBox.Text, out decimal minQuantity) || minQuantity < 0)
            {
                InventoryErrorText.Text = "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u043a\u043e\u0440\u0440\u0435\u043a\u0442\u043d\u044b\u0439 \u043c\u0438\u043d\u0438\u043c\u0430\u043b\u044c\u043d\u044b\u0439 \u043e\u0441\u0442\u0430\u0442\u043e\u043a.";
                return;
            }

            try
            {
                InventoryService.AddInventoryItem(itemName, unitName, quantity, minQuantity);
                ClearNewInventoryForm();
                LoadInventory();
                LoadProducts();

                InventoryErrorText.Foreground = SuccessBrush();
                InventoryErrorText.Text = "\u0417\u0430\u043f\u0447\u0430\u0441\u0442\u044c \u0434\u043e\u0431\u0430\u0432\u043b\u0435\u043d\u0430.";
            }
            catch (Exception ex)
            {
                InventoryErrorText.Text = "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0434\u043e\u0431\u0430\u0432\u0438\u0442\u044c \u0437\u0430\u043f\u0447\u0430\u0441\u0442\u044c.";
                MessageBox.Show(ex.Message, "\u041e\u0448\u0438\u0431\u043a\u0430", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddEmployee_Click(object sender, RoutedEventArgs e)
        {
            EmployeesErrorText.Foreground = ErrorBrush();
            EmployeesErrorText.Text = string.Empty;

            string lastName = LastNameBox.Text.Trim();
            string firstName = FirstNameBox.Text.Trim();
            string phone = PhoneBox.Text.Trim();
            string email = EmailBox.Text.Trim();
            string password = PasswordBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                EmployeesErrorText.Text = "Заполните фамилию, имя, телефон, email и пароль.";
                return;
            }

            if (!IsValidEmail(email))
            {
                EmployeesErrorText.Text = "Введите корректный email.";
                return;
            }

            if (!(RoleBox.SelectedValue is int roleId))
            {
                EmployeesErrorText.Text = "Выберите роль сотрудника.";
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    bool emailExists = db.Users.Any(user => user.Email == email);
                    if (emailExists)
                    {
                        EmployeesErrorText.Text = "Пользователь с таким email уже существует.";
                        return;
                    }

                    db.Users.Add(new Users
                    {
                        LastName = lastName,
                        FirstName = firstName,
                        MiddleName = NormalizeText(MiddleNameBox.Text),
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

        private void ClearProductForm()
        {
            ProductNameBox.Text = string.Empty;
            ProductDescriptionBox.Text = string.Empty;
            ProductPriceBox.Text = string.Empty;
            ProductImagePathBox.Text = string.Empty;
            ProductIsActiveBox.IsChecked = true;
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

        private void ClearNewInventoryForm()
        {
            NewInventoryNameBox.Text = string.Empty;
            NewInventoryUnitBox.Text = string.Empty;
            NewInventoryQuantityBox.Text = string.Empty;
            NewInventoryMinQuantityBox.Text = string.Empty;
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
                || normalizedRole.Contains("\u043a\u043b\u0438\u0435\u043d\u0442")
                || normalizedRole.Contains("\u043f\u043e\u043b\u044c\u0437\u043e\u0432\u0430\u0442\u0435\u043b");
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
            public decimal Price { get; set; }
            public bool IsActive { get; set; }

            public string PriceText => $"{Price:N2} руб.";
            public string ActiveText => IsActive ? "Активен" : "Скрыт";
        }

        private sealed class EmployeeRow
        {
            public int UserId { get; set; }
            public string FullName { get; set; }
            public string Email { get; set; }
            public string RoleName { get; set; }
        }
    }

    public enum AdminSection
    {
        Products,
        Inventory,
        Employees
    }
}
