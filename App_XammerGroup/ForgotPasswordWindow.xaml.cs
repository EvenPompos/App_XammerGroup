using System;
using System.Linq;
using System.Net.Mail;
using System.Windows;

namespace App_XammerGroup
{
    public partial class ForgotPasswordWindow : Window
    {
        public string UpdatedEmail { get; private set; }

        public ForgotPasswordWindow(string email = null)
        {
            InitializeComponent();

            EmailBox.Text = email ?? string.Empty;
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            string email = EmailBox.Text.Trim();
            string phone = PhoneBox.Text.Trim();
            string newPassword = NewPasswordBox.Password.Trim();
            string confirmPassword = ConfirmPasswordBox.Password.Trim();

            string validationError = ValidateInput(email, phone, newPassword, confirmPassword);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                ErrorText.Text = validationError;
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    var user = db.Users.FirstOrDefault(u => u.Email == email && u.Phone == phone);
                    if (user == null)
                    {
                        ErrorText.Text = "\u041f\u043e\u043b\u044c\u0437\u043e\u0432\u0430\u0442\u0435\u043b\u044c \u0441 \u0443\u043a\u0430\u0437\u0430\u043d\u043d\u044b\u043c email \u0438 \u0442\u0435\u043b\u0435\u0444\u043e\u043d\u043e\u043c \u043d\u0435 \u043d\u0430\u0439\u0434\u0435\u043d.";
                        return;
                    }

                    user.Password = newPassword;
                    db.SaveChanges();

                    UpdatedEmail = email;

                    MessageBox.Show(
                        "\u041f\u0430\u0440\u043e\u043b\u044c \u0443\u0441\u043f\u0435\u0448\u043d\u043e \u0438\u0437\u043c\u0435\u043d\u0435\u043d.",
                        "\u0423\u0441\u043f\u0435\u0448\u043d\u043e",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text = "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0438\u0437\u043c\u0435\u043d\u0438\u0442\u044c \u043f\u0430\u0440\u043e\u043b\u044c.";
                MessageBox.Show(ex.Message, "\u041e\u0448\u0438\u0431\u043a\u0430", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static string ValidateInput(
            string email,
            string phone,
            string newPassword,
            string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(newPassword) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                return "\u0417\u0430\u043f\u043e\u043b\u043d\u0438\u0442\u0435 email, \u0442\u0435\u043b\u0435\u0444\u043e\u043d \u0438 \u043e\u0431\u0430 \u043f\u043e\u043b\u044f \u043f\u0430\u0440\u043e\u043b\u044f.";
            }

            if (!IsValidEmail(email))
            {
                return "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u043a\u043e\u0440\u0440\u0435\u043a\u0442\u043d\u044b\u0439 email.";
            }

            if (newPassword.Length < 6)
            {
                return "\u041d\u043e\u0432\u044b\u0439 \u043f\u0430\u0440\u043e\u043b\u044c \u0434\u043e\u043b\u0436\u0435\u043d \u0441\u043e\u0434\u0435\u0440\u0436\u0430\u0442\u044c \u043d\u0435 \u043c\u0435\u043d\u044c\u0448\u0435 6 \u0441\u0438\u043c\u0432\u043e\u043b\u043e\u0432.";
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                return "\u041f\u0430\u0440\u043e\u043b\u0438 \u043d\u0435 \u0441\u043e\u0432\u043f\u0430\u0434\u0430\u044e\u0442.";
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
    }
}
