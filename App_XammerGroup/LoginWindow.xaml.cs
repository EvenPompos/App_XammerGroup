using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace App_XammerGroup
{
    public partial class LoginWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly List<string> _backgrounds = new List<string>
        {
            "Images/bg1.png",
            "Images/bg2.png",
            "Images/bg3.png"
        };

        private int _currentIndex;

        public LoginWindow()
        {
            InitializeComponent();

            Loaded += LoginWindow_Loaded;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetBackground();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_backgrounds.Count == 0)
            {
                return;
            }

            _currentIndex = (_currentIndex + 1) % _backgrounds.Count;
            SetBackground();
        }

        private void SetBackground()
        {
            try
            {
                BackgroundImage.Source = new BitmapImage(
                    new Uri(_backgrounds[_currentIndex], UriKind.Relative));
            }
            catch
            {
                BackgroundImage.Source = null;
            }
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Text = string.Empty;

            string email = LoginBox.Text.Trim();
            string password = PasswordBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ErrorText.Text = "\u0412\u0432\u0435\u0434\u0438\u0442\u0435 email \u0438 \u043f\u0430\u0440\u043e\u043b\u044c.";
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    var user = db.Users
                        .Include(u => u.Roles)
                        .FirstOrDefault(u => u.Email == email && u.Password == password);

                    if (user == null)
                    {
                        ErrorText.Text = "\u041d\u0435\u0432\u0435\u0440\u043d\u044b\u0439 email \u0438\u043b\u0438 \u043f\u0430\u0440\u043e\u043b\u044c.";
                        return;
                    }

                    var mainWindow = new MainWindow(user);
                    mainWindow.Show();
                    Close();
                }
            }
            catch (Exception ex)
            {
                ErrorText.Text = "\u041e\u0448\u0438\u0431\u043a\u0430 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0438\u044f \u043a \u0431\u0430\u0437\u0435.";
                MessageBox.Show(ex.Message, "\u041e\u0448\u0438\u0431\u043a\u0430", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            var registerWindow = new RegisterWindow
            {
                Owner = this
            };

            bool? result = registerWindow.ShowDialog();

            if (result == true && !string.IsNullOrWhiteSpace(registerWindow.RegisteredEmail))
            {
                LoginBox.Text = registerWindow.RegisteredEmail;
                PasswordBox.Password = string.Empty;
                ErrorText.Text = "\u0420\u0435\u0433\u0438\u0441\u0442\u0440\u0430\u0446\u0438\u044f \u0437\u0430\u0432\u0435\u0440\u0448\u0435\u043d\u0430. \u0422\u0435\u043f\u0435\u0440\u044c \u0432\u044b\u043f\u043e\u043b\u043d\u0438\u0442\u0435 \u0432\u0445\u043e\u0434.";
            }
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            var forgotPasswordWindow = new ForgotPasswordWindow(LoginBox.Text.Trim())
            {
                Owner = this
            };

            bool? result = forgotPasswordWindow.ShowDialog();

            if (result == true && !string.IsNullOrWhiteSpace(forgotPasswordWindow.UpdatedEmail))
            {
                LoginBox.Text = forgotPasswordWindow.UpdatedEmail;
                PasswordBox.Password = string.Empty;
                ErrorText.Text = "\u041f\u0430\u0440\u043e\u043b\u044c \u0438\u0437\u043c\u0435\u043d\u0435\u043d. \u0412\u044b\u043f\u043e\u043b\u043d\u0438\u0442\u0435 \u0432\u0445\u043e\u0434 \u0441 \u043d\u043e\u0432\u044b\u043c \u043f\u0430\u0440\u043e\u043b\u0435\u043c.";
            }
        }
    }
}
