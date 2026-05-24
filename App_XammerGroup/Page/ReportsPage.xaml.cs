using Microsoft.Win32;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace App_XammerGroup
{
    public partial class ReportsPage : Page
    {
        private SalesReportData _currentSalesReport;
        private InventoryReportData _currentInventoryReport;

        public ReportsPage()
        {
            InitializeComponent();
            Loaded += ReportsPage_Loaded;
        }

        private void ReportsPage_Loaded(object sender, RoutedEventArgs e)
        {
            EndDatePicker.SelectedDate = DateTime.Today;
            StartDatePicker.SelectedDate = DateTime.Today.AddMonths(-1);
            LoadSalesReport();
            LoadInventoryReport();
        }

        private void RefreshSales_Click(object sender, RoutedEventArgs e)
        {
            LoadSalesReport();
        }

        private void RefreshInventory_Click(object sender, RoutedEventArgs e)
        {
            LoadInventoryReport();
        }

        private void ExportSalesPdf_Click(object sender, RoutedEventArgs e)
        {
            SalesErrorText.Foreground = ErrorBrush();
            SalesErrorText.Text = string.Empty;

            if (_currentSalesReport == null)
            {
                SalesErrorText.Text = "Сначала сформируйте отчет по продажам.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Сохранение отчета по продажам",
                Filter = "PDF files|*.pdf",
                FileName = $"Отчет_по_продажам_{_currentSalesReport.StartDate:yyyyMMdd}_{_currentSalesReport.EndDate:yyyyMMdd}.pdf"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                SalesReportPdfExporter.Export(dialog.FileName, _currentSalesReport);
                SalesErrorText.Foreground = SuccessBrush();
                SalesErrorText.Text = $"PDF-отчет сохранен: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                SalesErrorText.Text = "Не удалось сохранить PDF-отчет.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportInventoryPdf_Click(object sender, RoutedEventArgs e)
        {
            InventoryErrorText.Foreground = ErrorBrush();
            InventoryErrorText.Text = string.Empty;

            if (_currentInventoryReport == null)
            {
                InventoryErrorText.Text = "Сначала сформируйте отчет по складу.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Сохранение отчета по складу",
                Filter = "PDF files|*.pdf",
                FileName = $"Отчет_по_складу_{DateTime.Today:yyyyMMdd}.pdf"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                SalesReportPdfExporter.ExportInventoryPdf(dialog.FileName, _currentInventoryReport);
                InventoryErrorText.Foreground = SuccessBrush();
                InventoryErrorText.Text = $"PDF-отчет сохранен: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                InventoryErrorText.Text = "Не удалось сохранить PDF-отчет по складу.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportInventoryExcel_Click(object sender, RoutedEventArgs e)
        {
            InventoryErrorText.Foreground = ErrorBrush();
            InventoryErrorText.Text = string.Empty;

            if (_currentInventoryReport == null)
            {
                InventoryErrorText.Text = "Сначала сформируйте отчет по складу.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Сохранение отчета по складу",
                Filter = "Excel 2003 XML|*.xls",
                FileName = $"Отчет_по_складу_{DateTime.Today:yyyyMMdd}.xls"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                SalesReportPdfExporter.ExportInventoryExcel(dialog.FileName, _currentInventoryReport);
                InventoryErrorText.Foreground = SuccessBrush();
                InventoryErrorText.Text = $"Excel-отчет сохранен: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                InventoryErrorText.Text = "Не удалось сохранить Excel-отчет по складу.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSalesReport()
        {
            SalesErrorText.Foreground = ErrorBrush();
            SalesErrorText.Text = string.Empty;

            if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
            {
                SalesErrorText.Text = "Выберите начальную и конечную дату.";
                return;
            }

            DateTime startDate = StartDatePicker.SelectedDate.Value.Date;
            DateTime endDate = EndDatePicker.SelectedDate.Value.Date;

            if (startDate > endDate)
            {
                SalesErrorText.Text = "Начальная дата не может быть больше конечной.";
                return;
            }

            using (var db = new DB_Xammer_groupEntities())
            {
                DateTime exclusiveEndDate = endDate.AddDays(1);

                var orders = db.Orders
                    .Where(order => order.CreatedDate >= startDate && order.CreatedDate < exclusiveEndDate)
                    .ToList();

                var orderIds = orders.Select(order => order.OrderId).ToList();
                var orderItems = db.OrderItems
                    .Where(item => orderIds.Contains(item.OrderId))
                    .ToList();

                var products = db.Products.ToList();

                _currentSalesReport = new SalesReportData
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    OrdersCount = orders.Count,
                    TotalRevenue = orders.Sum(order => order.TotalAmount ?? 0),
                    Products = orderItems
                        .Join(
                            products,
                            item => item.ProductId,
                            product => product.ProductId,
                            (item, product) => new { item, product })
                        .GroupBy(entry => new { entry.product.ProductId, entry.product.ProductName })
                        .Select(group => new SalesReportProductLine
                        {
                            ProductId = group.Key.ProductId,
                            ProductName = group.Key.ProductName,
                            QuantitySold = group.Sum(entry => entry.item.Quantity),
                            Revenue = group.Sum(entry => (entry.item.Price ?? 0) * entry.item.Quantity)
                        })
                        .OrderByDescending(item => item.Revenue)
                        .ThenBy(item => item.ProductName)
                        .ToList()
                };
            }

            OrdersCountText.Text = _currentSalesReport.OrdersCount.ToString(CultureInfo.InvariantCulture);
            RevenueText.Text = $"{_currentSalesReport.TotalRevenue:N2} руб.";
            ItemsCountText.Text = _currentSalesReport.Products.Sum(item => item.QuantitySold).ToString(CultureInfo.InvariantCulture);

            ProductsReportGrid.ItemsSource = _currentSalesReport.Products
                .Select(item => new ProductReportRow
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    QuantitySold = item.QuantitySold,
                    RevenueText = $"{item.Revenue:N2} руб."
                })
                .ToList();

            if (_currentSalesReport.Products.Count == 0)
            {
                SalesErrorText.Text = "За выбранный период продаж не найдено.";
            }
        }

        private void LoadInventoryReport()
        {
            InventoryErrorText.Foreground = ErrorBrush();
            InventoryErrorText.Text = string.Empty;

            var items = InventoryService.GetInventoryRows();
            _currentInventoryReport = new InventoryReportData
            {
                CreatedAt = DateTime.Now,
                Items = items
                    .Select(item => new InventoryReportLine
                    {
                        InventoryItemId = item.InventoryItemId,
                        ItemName = item.ItemName,
                        UnitName = item.UnitName,
                        QuantityOnHand = item.QuantityOnHand,
                        MinQuantity = item.MinQuantity,
                        StatusText = item.StatusText
                    })
                    .ToList()
            };

            InventoryReportGrid.ItemsSource = items;
            if (items.Count == 0)
            {
                InventoryErrorText.Text = "Складские позиции не найдены.";
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

        private sealed class ProductReportRow
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; }
            public int QuantitySold { get; set; }
            public string RevenueText { get; set; }
        }
    }
}
