using Microsoft.Win32;
using System;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace App_XammerGroup
{
    public partial class ProfilePage : Page
    {
        private readonly int _userId;
        private readonly bool _canEdit;
        private readonly Action<Users> _onProfileUpdated;

        private Users _user;
        private string _selectedAvatarSourcePath;

        public ProfilePage(int userId, bool canEdit, Action<Users> onProfileUpdated = null)
        {
            InitializeComponent();

            _userId = userId;
            _canEdit = canEdit;
            _onProfileUpdated = onProfileUpdated;

            Loaded += ProfilePage_Loaded;
        }

        private void ProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUser();
            ApplyAccessMode();
        }

        private void ChangeAvatar_Click(object sender, RoutedEventArgs e)
        {
            if (!_canEdit)
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Выбор аватара",
                Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp"
            };

            bool? result = dialog.ShowDialog();
            if (result != true)
            {
                return;
            }

            _selectedAvatarSourcePath = dialog.FileName;
            SetAvatarPreview(_selectedAvatarSourcePath);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!_canEdit)
            {
                return;
            }

            ErrorText.Text = string.Empty;

            string validationError = ValidateInput(
                LastNameBox.Text.Trim(),
                FirstNameBox.Text.Trim(),
                PhoneBox.Text.Trim(),
                EmailBox.Text.Trim(),
                PasswordBox.Password.Trim());

            if (!string.IsNullOrWhiteSpace(validationError))
            {
                ErrorText.Text = validationError;
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    var user = db.Users
                        .Include(u => u.Roles)
                        .FirstOrDefault(u => u.UserId == _userId);

                    if (user == null)
                    {
                        ErrorText.Text = "Пользователь не найден.";
                        return;
                    }

                    string email = EmailBox.Text.Trim();
                    bool emailTaken = db.Users.Any(u => u.UserId != _userId && u.Email == email);
                    if (emailTaken)
                    {
                        ErrorText.Text = "Этот email уже используется другим пользователем.";
                        return;
                    }

                    user.LastName = LastNameBox.Text.Trim();
                    user.FirstName = FirstNameBox.Text.Trim();
                    user.MiddleName = NormalizeOptional(MiddleNameBox.Text);
                    user.Phone = PhoneBox.Text.Trim();
                    user.Email = email;
                    user.Password = PasswordBox.Password.Trim();

                    db.SaveChanges();

                    if (!string.IsNullOrWhiteSpace(_selectedAvatarSourcePath))
                    {
                        SaveAvatar(_selectedAvatarSourcePath, _userId);
                    }

                    _user = user;
                    PopulateFields();
                    _selectedAvatarSourcePath = null;
                    _onProfileUpdated?.Invoke(user);

                    ErrorText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2E7D32"));
                    ErrorText.Text = "Изменения сохранены.";
                }
            }
            catch (Exception ex)
            {
                ErrorText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB00020"));
                ErrorText.Text = "Не удалось сохранить профиль.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadUser()
        {
            using (var db = new DB_Xammer_groupEntities())
            {
                _user = db.Users
                    .Include(u => u.Roles)
                    .FirstOrDefault(u => u.UserId == _userId);
            }

            if (_user == null)
            {
                ErrorText.Text = "Не удалось загрузить профиль.";
                return;
            }

            PopulateFields();
        }

        private void PopulateFields()
        {
            LastNameBox.Text = _user.LastName ?? string.Empty;
            FirstNameBox.Text = _user.FirstName ?? string.Empty;
            MiddleNameBox.Text = _user.MiddleName ?? string.Empty;
            PhoneBox.Text = _user.Phone ?? string.Empty;
            EmailBox.Text = _user.Email ?? string.Empty;
            PasswordBox.Password = _user.Password ?? string.Empty;

            string roleName = _user.Roles?.RoleName ?? $"RoleId: {_user.RoleId}";
            RoleBox.Text = roleName;
            RoleValueText.Text = roleName;
            EmailValueText.Text = _user.Email ?? string.Empty;

            CreatedDateBox.Text = _user.CreatedDate.HasValue
                ? _user.CreatedDate.Value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)
                : "-";

            AvatarInitialsText.Text = BuildInitials(_user);
            SetAvatarPreview(GetStoredAvatarPath(_userId));

            ErrorText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB00020"));
            ErrorText.Text = string.Empty;
        }

        private void ApplyAccessMode()
        {
            AccessModeText.Text = _canEdit
                ? "Вы можете редактировать свои данные и установить аватар."
                : "Доступ только на просмотр. Изменение данных и аватара недоступно.";

            LastNameBox.IsReadOnly = !_canEdit;
            FirstNameBox.IsReadOnly = !_canEdit;
            MiddleNameBox.IsReadOnly = !_canEdit;
            PhoneBox.IsReadOnly = !_canEdit;
            EmailBox.IsReadOnly = !_canEdit;
            PasswordBox.IsEnabled = _canEdit;
            ChangeAvatarButton.Visibility = _canEdit ? Visibility.Visible : Visibility.Collapsed;
            SaveButton.Visibility = _canEdit ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetAvatarPreview(string imagePath)
        {
            if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.EndInit();

                AvatarEllipse.Fill = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                AvatarInitialsText.Visibility = Visibility.Collapsed;
                return;
            }

            AvatarEllipse.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD9D9D9"));
            AvatarInitialsText.Visibility = Visibility.Visible;
        }

        private static string ValidateInput(
            string lastName,
            string firstName,
            string phone,
            string email,
            string password)
        {
            if (string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                return "Заполните фамилию, имя, телефон, email и пароль.";
            }

            if (!IsValidEmail(email))
            {
                return "Введите корректный email.";
            }

            if (password.Length < 6)
            {
                return "Пароль должен содержать не меньше 6 символов.";
            }

            return null;
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

        private static string NormalizeOptional(string value)
        {
            string trimmedValue = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmedValue) ? null : trimmedValue;
        }

        private static string BuildInitials(Users user)
        {
            string initials =
                GetInitial(user.FirstName) +
                GetInitial(user.LastName);

            return string.IsNullOrWhiteSpace(initials)
                ? "?"
                : initials.ToUpperInvariant();
        }

        private static string GetInitial(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Substring(0, 1);
        }

        private static void SaveAvatar(string sourcePath, int userId)
        {
            string avatarDirectory = GetAvatarDirectory();
            Directory.CreateDirectory(avatarDirectory);

            foreach (string existingFile in Directory.GetFiles(avatarDirectory, $"user_{userId}.*"))
            {
                File.Delete(existingFile);
            }

            string extension = Path.GetExtension(sourcePath);
            string destinationPath = Path.Combine(avatarDirectory, $"user_{userId}{extension}");
            File.Copy(sourcePath, destinationPath, true);
        }

        private static string GetStoredAvatarPath(int userId)
        {
            string avatarDirectory = GetAvatarDirectory();
            if (!Directory.Exists(avatarDirectory))
            {
                return null;
            }

            return Directory.GetFiles(avatarDirectory, $"user_{userId}.*")
                .OrderBy(path => path)
                .FirstOrDefault();
        }

        private static string GetAvatarDirectory()
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "App_XammerGroup", "Avatars");
        }
    }
}
