using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace App_XammerGroup
{
    public partial class ProductsPage : Page
    {
        private readonly int _userId;
        private readonly bool _canAddToCart;
        private readonly Action _openCartAction;
        private readonly bool _onlyActiveProducts;

        private List<ProductListItem> _allProducts = new List<ProductListItem>();

        public ProductsPage(int userId, bool canAddToCart, Action openCartAction = null, bool onlyActiveProducts = false)
        {
            InitializeComponent();

            _userId = userId;
            _canAddToCart = canAddToCart;
            _openCartAction = openCartAction;
            _onlyActiveProducts = onlyActiveProducts;

            Loaded += ProductsPage_Loaded;
        }

        private void ProductsPage_Loaded(object sender, RoutedEventArgs e)
        {
            ConfigureFilters();
            LoadProducts();
            ApplyFilters();
            ApplyMode();
        }

        private void ConfigureFilters()
        {
            ActiveFilterBox.ItemsSource = new[]
            {
                new FilterOption(string.Empty, "\u0412\u0441\u0435"),
                new FilterOption("active", "\u0422\u043e\u043b\u044c\u043a\u043e \u0430\u043a\u0442\u0438\u0432\u043d\u044b\u0435"),
                new FilterOption("inactive", "\u0422\u043e\u043b\u044c\u043a\u043e \u0441\u043a\u0440\u044b\u0442\u044b\u0435")
            };
            ActiveFilterBox.SelectedIndex = _canAddToCart || _onlyActiveProducts ? 1 : 0;
            ActiveFilterBox.IsEnabled = !_onlyActiveProducts;

            SortBox.ItemsSource = new[]
            {
                new FilterOption("name_asc", "\u041f\u043e \u043d\u0430\u0437\u0432\u0430\u043d\u0438\u044e A-Z"),
                new FilterOption("name_desc", "\u041f\u043e \u043d\u0430\u0437\u0432\u0430\u043d\u0438\u044e Z-A"),
                new FilterOption("price_asc", "\u0426\u0435\u043d\u0430 \u043f\u043e \u0432\u043e\u0437\u0440\u0430\u0441\u0442\u0430\u043d\u0438\u044e"),
                new FilterOption("price_desc", "\u0426\u0435\u043d\u0430 \u043f\u043e \u0443\u0431\u044b\u0432\u0430\u043d\u0438\u044e")
            };
            SortBox.SelectedIndex = 0;
        }

        private void LoadProducts()
        {
            using (var db = new DB_Xammer_groupEntities())
            {
                InventoryService.EnsureSchemaAndSeed(db);
                var availabilityByProductId = InventoryService.GetProductAvailability()
                    .ToDictionary(item => item.ProductId, item => item);

                _allProducts = db.Products
                    .ToList()
                    .Select(product => new ProductListItem
                    {
                        ProductId = product.ProductId,
                        ProductName = product.ProductName,
                        Description = product.Description,
                        Price = product.Price,
                        IsActive = product.IsActive ?? false,
                        IsAvailable = availabilityByProductId.TryGetValue(product.ProductId, out ProductAvailabilityInfo availability) &&
                            availability.IsAvailable,
                        AvailabilityText = availabilityByProductId.TryGetValue(product.ProductId, out availability)
                            ? availability.AvailabilityText
                            : "\u041d\u0435\u0442 \u0441\u043e\u0441\u0442\u0430\u0432\u0430"
                    })
                    .ToList();
            }
        }

        private void ApplyFilters()
        {
            IEnumerable<ProductListItem> query = _allProducts;

            string search = SearchBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(item =>
                    (!string.IsNullOrWhiteSpace(item.ProductName) &&
                     item.ProductName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (!string.IsNullOrWhiteSpace(item.Description) &&
                     item.Description.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            if (_onlyActiveProducts)
            {
                query = query.Where(item => item.IsActive);
            }
            else
            {
                string activeFilter = (ActiveFilterBox.SelectedItem as FilterOption)?.Value;
                if (activeFilter == "active")
                {
                    query = query.Where(item => item.IsActive);
                }
                else if (activeFilter == "inactive")
                {
                    query = query.Where(item => !item.IsActive);
                }
            }

            if (TryParseDecimal(MinPriceBox.Text, out decimal minPrice))
            {
                query = query.Where(item => item.Price >= minPrice);
            }

            if (TryParseDecimal(MaxPriceBox.Text, out decimal maxPrice))
            {
                query = query.Where(item => item.Price <= maxPrice);
            }

            string sort = (SortBox.SelectedItem as FilterOption)?.Value;
            switch (sort)
            {
                case "name_desc":
                    query = query.OrderByDescending(item => item.ProductName);
                    break;

                case "price_asc":
                    query = query.OrderBy(item => item.Price);
                    break;

                case "price_desc":
                    query = query.OrderByDescending(item => item.Price);
                    break;

                default:
                    query = query.OrderBy(item => item.ProductName);
                    break;
            }

            var items = query.ToList();
            ProductsGrid.ItemsSource = items;
            SummaryText.Text = $"\u041d\u0430\u0439\u0434\u0435\u043d\u043e \u043f\u043e\u0437\u0438\u0446\u0438\u0439: {items.Count}";
        }

        private void ApplyMode()
        {
            ModeText.Text = _canAddToCart
                ? "\u041a\u0430\u0442\u0430\u043b\u043e\u0433 \u0434\u043e\u0441\u0442\u0443\u043f\u0435\u043d \u0434\u043b\u044f \u0432\u044b\u0431\u043e\u0440\u0430 \u0442\u043e\u0432\u0430\u0440\u043e\u0432. \u041c\u043e\u0436\u043d\u043e \u0434\u043e\u0431\u0430\u0432\u043b\u044f\u0442\u044c \u043f\u0440\u043e\u0434\u0443\u043a\u0446\u0438\u044e \u0432 \u043a\u043e\u0440\u0437\u0438\u043d\u0443."
                : "\u041a\u0430\u0442\u0430\u043b\u043e\u0433 \u0434\u043e\u0441\u0442\u0443\u043f\u0435\u043d \u0442\u043e\u043b\u044c\u043a\u043e \u0434\u043b\u044f \u043f\u0440\u043e\u0441\u043c\u043e\u0442\u0440\u0430 \u0438 \u0444\u0438\u043b\u044c\u0442\u0440\u0430\u0446\u0438\u0438.";

            CartColumn.Visibility = _canAddToCart ? Visibility.Visible : Visibility.Collapsed;
            OpenCartButton.Visibility = _canAddToCart ? Visibility.Visible : Visibility.Collapsed;

            if (_canAddToCart)
            {
                UpdateCartButtonText();
            }
        }

        private void UpdateCartButtonText()
        {
            int itemsCount = CartService.GetItemCount(_userId);
            OpenCartButton.Content = itemsCount > 0
                ? $"\u041e\u0442\u043a\u0440\u044b\u0442\u044c \u043a\u043e\u0440\u0437\u0438\u043d\u0443 ({itemsCount})"
                : "\u041e\u0442\u043a\u0440\u044b\u0442\u044c \u043a\u043e\u0440\u0437\u0438\u043d\u0443";
        }

        private void Filters_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            ApplyFilters();
        }

        private void AddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (!_canAddToCart)
            {
                return;
            }

            var button = sender as Button;
            var item = button?.Tag as ProductListItem;
            if (item == null)
            {
                return;
            }

            using (var db = new DB_Xammer_groupEntities())
            {
                var product = db.Products.FirstOrDefault(p => p.ProductId == item.ProductId);
                if (product == null)
                {
                    MessageBox.Show("\u041f\u0440\u043e\u0434\u0443\u043a\u0442 \u043d\u0435 \u043d\u0430\u0439\u0434\u0435\u043d.", "\u041e\u0448\u0438\u0431\u043a\u0430", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!(product.IsActive ?? false))
                {
                    MessageBox.Show("\u0412 \u043a\u043e\u0440\u0437\u0438\u043d\u0443 \u043c\u043e\u0436\u043d\u043e \u0434\u043e\u0431\u0430\u0432\u043b\u044f\u0442\u044c \u0442\u043e\u043b\u044c\u043a\u043e \u0430\u043a\u0442\u0438\u0432\u043d\u0443\u044e \u043f\u0440\u043e\u0434\u0443\u043a\u0446\u0438\u044e.", "\u041e\u0433\u0440\u0430\u043d\u0438\u0447\u0435\u043d\u0438\u0435", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var shortages = InventoryService.ValidateAvailability(db, new[]
                {
                    new CartItem
                    {
                        ProductId = product.ProductId,
                        ProductName = product.ProductName,
                        Price = product.Price,
                        Quantity = 1
                    }
                });

                if (shortages.Count > 0)
                {
                    MessageBox.Show(InventoryService.BuildShortageMessage(shortages), "\u0421\u043a\u043b\u0430\u0434", MessageBoxButton.OK, MessageBoxImage.Warning);
                    LoadProducts();
                    ApplyFilters();
                    return;
                }

                CartService.AddProduct(_userId, product, 1);
            }

            UpdateCartButtonText();
            SummaryText.Text = $"\u041d\u0430\u0439\u0434\u0435\u043d\u043e \u043f\u043e\u0437\u0438\u0446\u0438\u0439: {((IEnumerable<ProductListItem>)ProductsGrid.ItemsSource)?.Count() ?? 0}. \u0422\u043e\u0432\u0430\u0440 \u0434\u043e\u0431\u0430\u0432\u043b\u0435\u043d \u0432 \u043a\u043e\u0440\u0437\u0438\u043d\u0443.";
        }

        private void OpenCart_Click(object sender, RoutedEventArgs e)
        {
            _openCartAction?.Invoke();
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

        private sealed class ProductListItem
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
            public bool IsActive { get; set; }
            public bool IsAvailable { get; set; }
            public string AvailabilityText { get; set; }

            public string ShortDescription
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(Description))
                    {
                        return "-";
                    }

                    return Description.Length <= 90
                        ? Description
                        : Description.Substring(0, 87) + "...";
                }
            }

            public string PriceText => $"{Price:N2} руб.";
            public string ActiveText => IsActive ? "\u0410\u043a\u0442\u0438\u0432\u0435\u043d" : "\u0421\u043a\u0440\u044b\u0442";
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
