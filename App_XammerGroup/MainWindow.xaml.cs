using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace App_XammerGroup
{
    public partial class MainWindow : Window
    {
        private Users _currentUser;
        private readonly AppRole _currentRole;

        public MainWindow(Users user)
        {
            InitializeComponent();

            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            _currentUser = LoadCurrentUser(user.UserId) ?? user;
            _currentRole = ResolveRole(_currentUser);

            UserNameText.Text = GetUserDisplayName(_currentUser);
            RoleText.Text = GetRoleCaption(_currentRole);

            ApplyRoleAccess();
            OpenDefaultSection();
        }

        private enum AppRole
        {
            User,
            Manager,
            Admin
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new ProfilePage(_currentUser.UserId, CanEditProfile(), RefreshCurrentUser));
        }

        private void Orders_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new OrdersPage(_currentUser.UserId, _currentRole != AppRole.User));
        }

        private void Products_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new ProductsPage(_currentUser.UserId, _currentRole == AppRole.User, OpenCartPage));
        }

        private void Cart_Click(object sender, RoutedEventArgs e)
        {
            OpenCartPage();
        }

        private void Employees_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new AdminPage(_currentUser.UserId, AdminSection.Employees));
        }

        private void Reports_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new ReportsPage());
        }

        private void Admin_Click(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new AdminPage(_currentUser.UserId, AdminSection.Products));
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            Close();
        }

        private void ApplyRoleAccess()
        {
            var hiddenButtons = new HashSet<Button>
            {
                ProfileButton,
                OrdersButton,
                ProductsButton,
                CartButton,
                EmployeesButton,
                ReportsButton,
                AdminButton
            };

            foreach (Button button in hiddenButtons)
            {
                button.Visibility = Visibility.Collapsed;
            }

            switch (_currentRole)
            {
                case AppRole.Admin:
                    ShowButtons(ProfileButton, OrdersButton, ProductsButton, EmployeesButton, ReportsButton, AdminButton);
                    break;

                case AppRole.Manager:
                    ShowButtons(ProfileButton, OrdersButton, ProductsButton);
                    break;

                default:
                    ShowButtons(ProfileButton, ProductsButton, CartButton);
                    break;
            }
        }

        private void OpenDefaultSection()
        {
            switch (_currentRole)
            {
                case AppRole.Admin:
                    Orders_Click(this, null);
                    break;

                case AppRole.Manager:
                    Orders_Click(this, null);
                    break;

                default:
                    Products_Click(this, null);
                    break;
            }
        }

        private static Users LoadCurrentUser(int userId)
        {
            using (var db = new DB_Xammer_groupEntities())
            {
                return db.Users
                    .Include(u => u.Roles)
                    .FirstOrDefault(u => u.UserId == userId);
            }
        }

        private static string GetUserDisplayName(Users user)
        {
            var fullName = string.Join(" ", new[]
            {
                user.LastName,
                user.FirstName,
                user.MiddleName
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName;
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                return user.Email;
            }

            return $"User #{user.UserId}";
        }

        private static AppRole ResolveRole(Users user)
        {
            string normalizedRole = NormalizeRoleName(user.Roles?.RoleName);

            if (normalizedRole.Contains("admin") || normalizedRole.Contains("\u0430\u0434\u043c\u0438\u043d"))
            {
                return AppRole.Admin;
            }

            if (normalizedRole.Contains("manager") ||
                normalizedRole.Contains("master") ||
                normalizedRole.Contains("\u043c\u0435\u043d\u0435\u0434\u0436\u0435\u0440") ||
                normalizedRole.Contains("\u043c\u0430\u0441\u0442\u0435\u0440"))
            {
                return AppRole.Manager;
            }

            if (user.RoleId == 1)
            {
                return AppRole.Admin;
            }

            return AppRole.User;
        }

        private static string NormalizeRoleName(string roleName)
        {
            return string.IsNullOrWhiteSpace(roleName)
                ? string.Empty
                : roleName.Trim().ToLowerInvariant();
        }

        private static string GetRoleCaption(AppRole role)
        {
            switch (role)
            {
                case AppRole.Admin:
                    return "\u0420\u043e\u043b\u044c: admin";

                case AppRole.Manager:
                    return "\u0420\u043e\u043b\u044c: manager/master";

                default:
                    return "\u0420\u043e\u043b\u044c: user";
            }
        }

        private void ShowButtons(params Button[] buttons)
        {
            foreach (Button button in buttons)
            {
                button.Visibility = Visibility.Visible;
            }
        }

        private void OpenCartPage()
        {
            MainFrame.Navigate(new CartPage(_currentUser.UserId));
        }

        private bool CanEditProfile()
        {
            return _currentRole == AppRole.Admin || _currentRole == AppRole.User;
        }

        private void RefreshCurrentUser(Users updatedUser)
        {
            _currentUser = updatedUser ?? LoadCurrentUser(_currentUser.UserId) ?? _currentUser;
            UserNameText.Text = GetUserDisplayName(_currentUser);
        }

        private void NavigateToSection(string title, string description)
        {
            MainFrame.Navigate(new PlaceholderPage(title, description));
        }
    }
}
