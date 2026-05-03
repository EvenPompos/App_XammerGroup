using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace App_XammerGroup
{
    public partial class OrdersPage : Page
    {
        private readonly int _userId;
        private readonly bool _canManageOrders;

        private List<OrderListItem> _allOrders = new List<OrderListItem>();
        private List<StatusOption> _statuses = new List<StatusOption>();
        private List<UserOption> _managers = new List<UserOption>();
        private OrderListItem _selectedOrder;

        public OrdersPage(int userId, bool canManageOrders)
        {
            InitializeComponent();

            _userId = userId;
            _canManageOrders = canManageOrders;

            Loaded += OrdersPage_Loaded;
        }

        private void OrdersPage_Loaded(object sender, RoutedEventArgs e)
        {
            ConfigureSort();
            LoadReferenceData();
            LoadOrders();
            ApplyMode();
            ApplyFilters();
            SetEditorEnabled(false);
        }

        private void ConfigureSort()
        {
            SortBox.ItemsSource = new[]
            {
                new FilterOption("created_desc", "\u041d\u043e\u0432\u044b\u0435 \u0441\u043d\u0430\u0447\u0430\u043b\u0430"),
                new FilterOption("created_asc", "\u0421\u0442\u0430\u0440\u044b\u0435 \u0441\u043d\u0430\u0447\u0430\u043b\u0430"),
                new FilterOption("deadline_asc", "\u0411\u043b\u0438\u0436\u0430\u0439\u0448\u0438\u0439 \u0441\u0440\u043e\u043a"),
                new FilterOption("amount_desc", "\u0421\u0443\u043c\u043c\u0430 \u043f\u043e \u0443\u0431\u044b\u0432\u0430\u043d\u0438\u044e")
            };
            SortBox.SelectedIndex = 0;
        }

        private void LoadReferenceData()
        {
            using (var db = new DB_Xammer_groupEntities())
            {
                _statuses = db.OrderStatuses
                    .OrderBy(item => item.StatusName)
                    .Select(item => new StatusOption
                    {
                        StatusId = item.StatusId,
                        StatusName = item.StatusName
                    })
                    .ToList();

                _managers = db.Users
                    .Include(item => item.Roles)
                    .ToList()
                    .Where(IsManagementUser)
                    .OrderBy(item => item.LastName)
                    .ThenBy(item => item.FirstName)
                    .Select(item => new UserOption
                    {
                        UserId = item.UserId,
                        DisplayName = BuildFullName(item),
                        Email = item.Email
                    })
                    .ToList();
            }

            StatusFilterBox.ItemsSource = new[]
            {
                new FilterOption(string.Empty, "\u0412\u0441\u0435 \u0441\u0442\u0430\u0442\u0443\u0441\u044b")
            }.Concat(_statuses.Select(item => new FilterOption(item.StatusId.ToString(CultureInfo.InvariantCulture), item.StatusName)))
             .ToList();
            StatusFilterBox.SelectedIndex = 0;

            StatusBox.ItemsSource = _statuses;
            StatusBox.DisplayMemberPath = nameof(StatusOption.StatusName);
            StatusBox.SelectedValuePath = nameof(StatusOption.StatusId);

            var managerItems = new List<UserOption>
            {
                new UserOption
                {
                    UserId = null,
                    DisplayName = "\u041d\u0435 \u043d\u0430\u0437\u043d\u0430\u0447\u0435\u043d"
                }
            };
            managerItems.AddRange(_managers);

            ManagerBox.ItemsSource = managerItems;
            ManagerBox.DisplayMemberPath = nameof(UserOption.DisplayName);
            ManagerBox.SelectedValuePath = nameof(UserOption.UserId);
        }

        private void LoadOrders()
        {
            using (var db = new DB_Xammer_groupEntities())
            {
                IQueryable<Orders> query = db.Orders
                    .Include(item => item.Users)
                    .Include(item => item.Users1)
                    .Include(item => item.OrderStatuses);

                if (!_canManageOrders)
                {
                    query = query.Where(item => item.ClientId == _userId);
                }

                _allOrders = query
                    .ToList()
                    .Select(item => new OrderListItem
                    {
                        OrderId = item.OrderId,
                        ClientId = item.ClientId,
                        ClientName = BuildFullName(item.Users),
                        ManagerId = item.ManagerId,
                        ManagerName = BuildFullName(item.Users1, "\u041d\u0435 \u043d\u0430\u0437\u043d\u0430\u0447\u0435\u043d"),
                        StatusId = item.StatusId,
                        StatusName = item.OrderStatuses?.StatusName ?? "-",
                        CreatedDate = item.CreatedDate,
                        DeadlineDate = item.DeadlineDate,
                        TotalAmount = item.TotalAmount ?? 0,
                        Description = item.Description,
                        Comment = item.Comment
                    })
                    .ToList();
            }
        }

        private void ApplyMode()
        {
            ModeText.Text = _canManageOrders
                ? "\u0414\u043e\u0441\u0442\u0443\u043f\u043d\u043e \u0443\u043f\u0440\u0430\u0432\u043b\u0435\u043d\u0438\u0435 \u0437\u0430\u043a\u0430\u0437\u0430\u043c\u0438: \u0441\u0442\u0430\u0442\u0443\u0441, \u043c\u0435\u043d\u0435\u0434\u0436\u0435\u0440, \u0441\u0440\u043e\u043a, \u043e\u043f\u0438\u0441\u0430\u043d\u0438\u0435 \u0438 \u043a\u043e\u043c\u043c\u0435\u043d\u0442\u0430\u0440\u0438\u0439."
                : "\u041a\u043b\u0438\u0435\u043d\u0442 \u0432\u0438\u0434\u0438\u0442 \u0442\u043e\u043b\u044c\u043a\u043e \u0441\u0432\u043e\u0438 \u0437\u0430\u043a\u0430\u0437\u044b \u0431\u0435\u0437 \u0432\u043e\u0437\u043c\u043e\u0436\u043d\u043e\u0441\u0442\u0438 \u0440\u0435\u0434\u0430\u043a\u0442\u0438\u0440\u043e\u0432\u0430\u0442\u044c.";
        }

        private void Filters_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            IEnumerable<OrderListItem> query = _allOrders;

            string search = SearchBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(item =>
                    item.OrderId.ToString(CultureInfo.InvariantCulture).Contains(search) ||
                    ContainsText(item.ClientName, search) ||
                    ContainsText(item.ManagerName, search) ||
                    ContainsText(item.Description, search) ||
                    ContainsText(item.Comment, search));
            }

            string statusFilter = (StatusFilterBox.SelectedItem as FilterOption)?.Value;
            if (int.TryParse(statusFilter, out int statusId))
            {
                query = query.Where(item => item.StatusId == statusId);
            }

            string sort = (SortBox.SelectedItem as FilterOption)?.Value;
            switch (sort)
            {
                case "created_asc":
                    query = query.OrderBy(item => item.CreatedDate ?? DateTime.MinValue);
                    break;

                case "deadline_asc":
                    query = query.OrderBy(item => item.DeadlineDate ?? DateTime.MaxValue)
                        .ThenByDescending(item => item.CreatedDate ?? DateTime.MinValue);
                    break;

                case "amount_desc":
                    query = query.OrderByDescending(item => item.TotalAmount);
                    break;

                default:
                    query = query.OrderByDescending(item => item.CreatedDate ?? DateTime.MinValue);
                    break;
            }

            var items = query.ToList();
            OrdersGrid.ItemsSource = items;
            SummaryText.Text = $"\u041d\u0430\u0439\u0434\u0435\u043d\u043e \u0437\u0430\u043a\u0430\u0437\u043e\u0432: {items.Count}";

            if (_selectedOrder != null)
            {
                var refreshedSelection = items.FirstOrDefault(item => item.OrderId == _selectedOrder.OrderId);
                if (refreshedSelection != null)
                {
                    OrdersGrid.SelectedItem = refreshedSelection;
                }
                else
                {
                    ClearEditor();
                }
            }
        }

        private void OrdersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedOrder = OrdersGrid.SelectedItem as OrderListItem;

            if (_selectedOrder == null)
            {
                ClearEditor();
                return;
            }

            PopulateEditor(_selectedOrder);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!_canManageOrders || _selectedOrder == null)
            {
                return;
            }

            ErrorText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB00020"));
            ErrorText.Text = string.Empty;

            if (!(StatusBox.SelectedValue is int selectedStatusId))
            {
                ErrorText.Text = "\u0412\u044b\u0431\u0435\u0440\u0438\u0442\u0435 \u0441\u0442\u0430\u0442\u0443\u0441.";
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    var order = db.Orders.FirstOrDefault(item => item.OrderId == _selectedOrder.OrderId);
                    if (order == null)
                    {
                        ErrorText.Text = "\u0417\u0430\u043a\u0430\u0437 \u043d\u0435 \u043d\u0430\u0439\u0434\u0435\u043d.";
                        return;
                    }

                    order.ManagerId = ManagerBox.SelectedValue as int?;
                    order.StatusId = selectedStatusId;
                    order.DeadlineDate = DeadlinePicker.SelectedDate;
                    order.Description = NormalizeMultiline(DescriptionBox.Text);
                    order.Comment = NormalizeMultiline(CommentBox.Text);

                    db.SaveChanges();
                }

                LoadOrders();
                ApplyFilters();
                ErrorText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2E7D32"));
                ErrorText.Text = "\u0418\u0437\u043c\u0435\u043d\u0435\u043d\u0438\u044f \u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d\u044b.";
            }
            catch (Exception ex)
            {
                ErrorText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB00020"));
                ErrorText.Text = "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0441\u043e\u0445\u0440\u0430\u043d\u0438\u0442\u044c \u0437\u0430\u043a\u0430\u0437.";
                MessageBox.Show(ex.Message, "\u041e\u0448\u0438\u0431\u043a\u0430", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopulateEditor(OrderListItem order)
        {
            SelectionHintText.Text = _canManageOrders
                ? "\u0412\u044b\u0431\u0440\u0430\u043d\u043d\u044b\u0439 \u0437\u0430\u043a\u0430\u0437 \u0434\u043e\u0441\u0442\u0443\u043f\u0435\u043d \u0434\u043b\u044f \u0440\u0435\u0434\u0430\u043a\u0442\u0438\u0440\u043e\u0432\u0430\u043d\u0438\u044f."
                : "\u0414\u0430\u043d\u043d\u044b\u0435 \u0437\u0430\u043a\u0430\u0437\u0430 \u0434\u043e\u0441\u0442\u0443\u043f\u043d\u044b \u0442\u043e\u043b\u044c\u043a\u043e \u0434\u043b\u044f \u043f\u0440\u043e\u0441\u043c\u043e\u0442\u0440\u0430.";

            OrderNumberBox.Text = order.OrderId.ToString(CultureInfo.InvariantCulture);
            ClientBox.Text = order.ClientName;
            AmountBox.Text = order.TotalAmountText;
            ManagerBox.SelectedValue = order.ManagerId;
            StatusBox.SelectedValue = order.StatusId;
            DeadlinePicker.SelectedDate = order.DeadlineDate;
            DescriptionBox.Text = order.Description ?? string.Empty;
            CommentBox.Text = order.Comment ?? string.Empty;

            SetEditorEnabled(true);
        }

        private void ClearEditor()
        {
            _selectedOrder = null;
            SelectionHintText.Text = "\u0412\u044b\u0431\u0435\u0440\u0438\u0442\u0435 \u0437\u0430\u043a\u0430\u0437 \u0432 \u0442\u0430\u0431\u043b\u0438\u0446\u0435.";
            OrderNumberBox.Text = string.Empty;
            ClientBox.Text = string.Empty;
            AmountBox.Text = string.Empty;
            ManagerBox.SelectedIndex = -1;
            StatusBox.SelectedIndex = -1;
            DeadlinePicker.SelectedDate = null;
            DescriptionBox.Text = string.Empty;
            CommentBox.Text = string.Empty;
            ErrorText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB00020"));
            ErrorText.Text = string.Empty;
            SetEditorEnabled(false);
        }

        private void SetEditorEnabled(bool hasSelection)
        {
            bool editable = hasSelection && _canManageOrders;

            ManagerBox.IsEnabled = editable;
            StatusBox.IsEnabled = editable;
            DeadlinePicker.IsEnabled = editable;
            DescriptionBox.IsReadOnly = !editable;
            CommentBox.IsReadOnly = !editable;
            SaveButton.Visibility = _canManageOrders ? Visibility.Visible : Visibility.Collapsed;
            SaveButton.IsEnabled = editable;
        }

        private static bool ContainsText(string source, string value)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizeMultiline(string value)
        {
            string trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static bool IsManagementUser(Users user)
        {
            string roleName = user.Roles?.RoleName?.Trim().ToLowerInvariant() ?? string.Empty;

            return roleName.Contains("admin")
                || roleName.Contains("manager")
                || roleName.Contains("master")
                || roleName.Contains("\u0430\u0434\u043c\u0438\u043d")
                || roleName.Contains("\u043c\u0435\u043d\u0435\u0434\u0436\u0435\u0440")
                || roleName.Contains("\u043c\u0430\u0441\u0442\u0435\u0440")
                || user.RoleId == 1;
        }

        private static string BuildFullName(Users user, string fallback = null)
        {
            if (user == null)
            {
                return fallback ?? "-";
            }

            string fullName = string.Join(" ", new[]
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

            return fallback ?? $"User #{user.UserId}";
        }

        private sealed class OrderListItem
        {
            public int OrderId { get; set; }
            public int ClientId { get; set; }
            public string ClientName { get; set; }
            public int? ManagerId { get; set; }
            public string ManagerName { get; set; }
            public int StatusId { get; set; }
            public string StatusName { get; set; }
            public DateTime? CreatedDate { get; set; }
            public DateTime? DeadlineDate { get; set; }
            public decimal TotalAmount { get; set; }
            public string Description { get; set; }
            public string Comment { get; set; }

            public string CreatedDateText => CreatedDate?.ToString("dd.MM.yyyy") ?? "-";
            public string DeadlineDateText => DeadlineDate?.ToString("dd.MM.yyyy") ?? "-";
            public string TotalAmountText => $"{TotalAmount:N2} руб.";
        }

        private sealed class StatusOption
        {
            public int StatusId { get; set; }
            public string StatusName { get; set; }
        }

        private sealed class UserOption
        {
            public int? UserId { get; set; }
            public string DisplayName { get; set; }
            public string Email { get; set; }
        }

        private sealed class FilterOption
        {
            public FilterOption(string value, string title)
            {
                Value = value;
                Title = title;
            }

            public string Value { get; }
            public string Title { get; }

            public override string ToString()
            {
                return Title;
            }
        }
    }
}
