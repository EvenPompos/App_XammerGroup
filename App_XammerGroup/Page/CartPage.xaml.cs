using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace App_XammerGroup
{
    public partial class CartPage : Page
    {
        private readonly int _userId;
        private readonly Action _orderPlacedAction;

        private List<CartRow> _rows = new List<CartRow>();

        public CartPage(int userId, Action orderPlacedAction = null)
        {
            InitializeComponent();

            _userId = userId;
            _orderPlacedAction = orderPlacedAction;

            Loaded += CartPage_Loaded;
        }

        private void CartPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCart();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            SaveQuantitiesToCart();
            LoadCart();
        }

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var row = (sender as Button)?.Tag as CartRow;
            if (row == null)
            {
                return;
            }

            CartService.RemoveProduct(_userId, row.ProductId);
            LoadCart();
        }

        private void Checkout_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB00020"));
            ErrorText.Text = string.Empty;

            SaveQuantitiesToCart();

            var items = CartService.GetCartItems(_userId);
            if (items.Count == 0)
            {
                ErrorText.Text = "\u041a\u043e\u0440\u0437\u0438\u043d\u0430 \u043f\u0443\u0441\u0442\u0430.";
                return;
            }

            try
            {
                using (var db = new DB_Xammer_groupEntities())
                {
                    InventoryService.EnsureSchemaAndSeed(db);
                    var shortages = InventoryService.ValidateAvailability(db, items);
                    if (shortages.Count > 0)
                    {
                        ErrorText.Text = InventoryService.BuildShortageMessage(shortages);
                        return;
                    }

                    using (var transaction = db.Database.BeginTransaction())
                    {
                        int statusId = ResolveInitialStatusId(db);

                        decimal total = items.Sum(item => item.TotalPrice);
                        string description = string.Join(", ", items.Select(item => item.ProductName));

                        var order = new Orders
                        {
                            ClientId = _userId,
                            ManagerId = null,
                            StatusId = statusId,
                            CreatedDate = DateTime.Now,
                            DeadlineDate = null,
                            TotalAmount = total,
                            Description = description,
                            Comment = NormalizeComment(CommentBox.Text)
                        };

                        db.Orders.Add(order);
                        db.SaveChanges();

                        foreach (var item in items)
                        {
                            db.OrderItems.Add(new OrderItems
                            {
                                OrderId = order.OrderId,
                                ProductId = item.ProductId,
                                Quantity = item.Quantity,
                                Price = item.Price
                            });
                        }

                        db.SaveChanges();
                        InventoryService.WriteOffMaterials(db, order.OrderId, items);
                        transaction.Commit();

                        CartService.Clear(_userId);

                        ErrorText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2E7D32"));
                        ErrorText.Text = $"\u0417\u0430\u043a\u0430\u0437 \u2116{order.OrderId} \u0443\u0441\u043f\u0435\u0448\u043d\u043e \u043e\u0444\u043e\u0440\u043c\u043b\u0435\u043d. \u041c\u0430\u0442\u0435\u0440\u0438\u0430\u043b\u044b \u0441\u043f\u0438\u0441\u0430\u043d\u044b \u0441\u043e \u0441\u043a\u043b\u0430\u0434\u0430.";
                        CommentBox.Text = string.Empty;

                        LoadCart();
                        _orderPlacedAction?.Invoke();
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                ErrorText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB00020"));
                ErrorText.Text = ex.Message;
            }
            catch (Exception ex)
            {
                ErrorText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB00020"));
                ErrorText.Text = "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u043e\u0444\u043e\u0440\u043c\u0438\u0442\u044c \u0437\u0430\u043a\u0430\u0437.";
                MessageBox.Show(ex.Message, "\u041e\u0448\u0438\u0431\u043a\u0430", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCart()
        {
            _rows = CartService.GetCartItems(_userId)
                .Select(item =>
                {
                    var row = new CartRow
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Price = item.Price,
                        Quantity = item.Quantity
                    };

                    row.PropertyChanged += CartRow_PropertyChanged;
                    return row;
                })
                .ToList();

            CartGrid.ItemsSource = _rows;
            UpdateSummary();
        }

        private void CartRow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CartRow.Quantity))
            {
                UpdateSummary();
            }
        }

        private void SaveQuantitiesToCart()
        {
            CartGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            CartGrid.CommitEdit(DataGridEditingUnit.Row, true);

            foreach (CartRow row in _rows)
            {
                CartService.UpdateQuantity(_userId, row.ProductId, row.Quantity);
            }
        }

        private void UpdateSummary()
        {
            int totalItems = _rows.Sum(row => row.Quantity);
            decimal totalAmount = _rows.Sum(row => row.TotalPrice);
            SummaryText.Text = $"\u041f\u043e\u0437\u0438\u0446\u0438\u0439: {totalItems}\n\u0421\u0443\u043c\u043c\u0430: {totalAmount:N2} руб.";
        }

        private static int ResolveInitialStatusId(DB_Xammer_groupEntities db)
        {
            var status = db.OrderStatuses.FirstOrDefault(item =>
                item.StatusName == "\u041d\u043e\u0432\u044b\u0439" ||
                item.StatusName == "\u0421\u043e\u0437\u0434\u0430\u043d" ||
                item.StatusName == "\u0421\u043e\u0437\u0434\u0430\u043d\u0430" ||
                item.StatusName == "New");

            if (status != null)
            {
                return status.StatusId;
            }

            status = db.OrderStatuses.OrderBy(item => item.StatusId).FirstOrDefault();
            if (status == null)
            {
                throw new InvalidOperationException("\u0412 \u0442\u0430\u0431\u043b\u0438\u0446\u0435 OrderStatuses \u043e\u0442\u0441\u0443\u0442\u0441\u0442\u0432\u0443\u044e\u0442 \u0441\u0442\u0430\u0442\u0443\u0441\u044b \u0437\u0430\u043a\u0430\u0437\u043e\u0432.");
            }

            return status.StatusId;
        }

        private static string NormalizeComment(string comment)
        {
            string trimmed = comment?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private sealed class CartRow : INotifyPropertyChanged
        {
            private int _quantity;

            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public decimal Price { get; set; }

            public int Quantity
            {
                get => _quantity;
                set
                {
                    int normalizedValue = value < 0 ? 0 : value;
                    if (_quantity == normalizedValue)
                    {
                        return;
                    }

                    _quantity = normalizedValue;
                    OnPropertyChanged(nameof(Quantity));
                    OnPropertyChanged(nameof(TotalText));
                }
            }

            public decimal TotalPrice => Price * Quantity;
            public string PriceText => $"{Price:N2} руб.";
            public string TotalText => $"{TotalPrice:N2} руб.";

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
