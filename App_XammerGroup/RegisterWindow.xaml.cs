using System;
using System.Linq;
using System.Net.Mail;
using System.Windows;

namespace App_XammerGroup
{
    public partial class RegisterWindow : Window
    {
        private const string ClientRoleName = "\u041a\u043b\u0438\u0435\u043d\u0442";

        public string RegisteredEmail { get; private set; }

        public RegisterWindow()
        {
            InitializeComponent();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            string lastName = LastNameBox.Text.Trim();
            string firstName = FirstNameBox.Text.Trim();
            string middleName = MiddleNameBox.Text.Trim();
            string phone = PhoneBox.Text.Trim();
            string email = EmailBox.Text.Trim();
            string password = PasswordBox.Password.Trim();
            string confirmPassword = ConfirmPasswordBox.Password.Trim();

            string validationError = ValidateInput(lastName, firstName, phone, email, password, confirmPassword);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                ErrorText.Text = validationError;
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    bool userExists = db.Users.Any(u => u.Email == email);
                    if (userExists)
                    {
                        ErrorText.Text = "\u041f\u043e\u043b\u044c\u0437\u043e\u0432\u0430\u0442\u0435\u043b\u044c \u0441 \u0442\u0430\u043a\u0438\u043c email \u0443\u0436\u0435 \u0441\u0443\u0449\u0435\u0441\u0442\u0432\u0443\u0435\u0442.";
                        return;
                    }

                    int? clientRoleId = GetClientRoleId(db);
                    if (!clientRoleId.HasValue)
                    {
                        ErrorText.Text = "\u0412 \u0431\u0430\u0437\u0435 \u043d\u0435 \u043d\u0430\u0439\u0434\u0435\u043d\u0430 \u0440\u043e\u043b\u044c \u043a\u043b\u0438\u0435\u043d\u0442\u0430. \u0414\u043e\u0431\u0430\u0432\u044c\u0442\u0435 \u0440\u043e\u043b\u044c '\u041a\u043b\u0438\u0435\u043d\u0442' \u0432 \u0442\u0430\u0431\u043b\u0438\u0446\u0443 Roles.";
                        return;
                    }

                    var user = new Users
                    {
                        LastName = lastName,
                        FirstName = firstName,
                        MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName,
                        Phone = phone,
                        Email = email,
                        Password = password,
                        RoleId = clientRoleId.Value,
                        CreatedDate = DateTime.Now
                    };

                    db.Users.Add(user);
                    db.SaveChanges();

                    RegisteredEmail = email;
                    MessageBox.Show(
                        "\u0420\u0435\u0433\u0438\u0441\u0442\u0440\u0430\u0446\u0438\u044f \u0432\u044b\u043f\u043e\u043b\u043d\u0435\u043d\u0430. \u0422\u0435\u043f\u0435\u0440\u044c \u0432\u044b \u043c\u043e\u0436\u0435\u0442\u0435 \u0432\u043e\u0439\u0442\u0438 \u0432 \u0441\u0438\u0441\u0442\u0435\u043c\u0443.",
                        "\u0423\u0441\u043f\u0435\u0448\u043d\u043e",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text = "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0437\u0430\u0440\u0435\u0433\u0438\u0441\u0442\u0440\u0438\u0440\u043e\u0432\u0430\u0442\u044c \u043f\u043e\u043b\u044c\u0437\u043e\u0432\u0430\u0442\u0435\u043b\u044f.";
                MessageBox.Show(ex.Message, "\u041e\u0448\u0438\u0431\u043a\u0430", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static string ValidateInput(
            string lastName,
            string firstName,
            string phone,
            string email,
            string password,
            string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                return "\u0417\u0430\u043f\u043e\u043b\u043d\u0438\u0442\u0435 \u043e\u0431\u044f\u0437\u0430\u0442\u0435\u043b\u044c\u043d\u044b\u0435 \u043f\u043e\u043b\u044f: \u0444\u0430\u043c\u0438\u043b\u0438\u044e, \u0438\u043c\u044f, \u0442\u0435\u043b\u0435\u0444\u043e\u043d, email \u0438 \u043f\u0430\u0440\u043e\u043b\u044c.";
            }

            if (!IsValidEmail(email))
            {
                return "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 \u043a\u043e\u0440\u0440\u0435\u043a\u0442\u043d\u044b\u0439 email.";
            }

            if (password.Length < 6)
            {
                return "\u041f\u0430\u0440\u043e\u043b\u044c \u0434\u043e\u043b\u0436\u0435\u043d \u0441\u043e\u0434\u0435\u0440\u0436\u0430\u0442\u044c \u043d\u0435 \u043c\u0435\u043d\u044c\u0448\u0435 6 \u0441\u0438\u043c\u0432\u043e\u043b\u043e\u0432.";
            }

            if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
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

        private static int? GetClientRoleId(DB_Xammer_groupEntities db)
        {
            var clientRole = db.Roles.FirstOrDefault(r =>
                r.RoleName == ClientRoleName ||
                r.RoleName == "Client");

            if (clientRole != null)
            {
                return clientRole.RoleId;
            }

            clientRole = db.Roles.FirstOrDefault(r => r.RoleName.Contains(ClientRoleName));
            return clientRole?.RoleId;
        }
    }
}
