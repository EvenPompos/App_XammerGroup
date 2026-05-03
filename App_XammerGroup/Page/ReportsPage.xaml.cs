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
        private SalesReportData _currentReport;

        public ReportsPage()
        {
            InitializeComponent();

            Loaded += ReportsPage_Loaded;
        }

        private void ReportsPage_Loaded(object sender, RoutedEventArgs e)
        {
            EndDatePicker.SelectedDate = DateTime.Today;
            StartDatePicker.SelectedDate = DateTime.Today.AddMonths(-1);
            LoadReport();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadReport();
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Foreground = ErrorBrush();
            ErrorText.Text = string.Empty;

            if (_currentReport == null)
            {
                ErrorText.Text = "Сначала сформируйте отчет.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Сохранение отчета",
                Filter = "PDF files|*.pdf",
                FileName = $"sales_report_{_currentReport.StartDate:yyyyMMdd}_{_currentReport.EndDate:yyyyMMdd}.pdf"
            };

            bool? result = dialog.ShowDialog();
            if (result != true)
            {
                return;
            }

            try
            {
                SalesReportPdfExporter.Export(dialog.FileName, _currentReport);
                ErrorText.Foreground = SuccessBrush();
                ErrorText.Text = $"PDF-отчет сохранен: {dialog.FileName}";
            }
            catch (Exception ex)
            {
                ErrorText.Foreground = ErrorBrush();
                ErrorText.Text = "Не удалось сохранить PDF-отчет.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadReport()
        {
            ErrorText.Foreground = ErrorBrush();
            ErrorText.Text = string.Empty;

            if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
            {
                ErrorText.Text = "Выберите начальную и конечную дату.";
                return;
            }

            DateTime startDate = StartDatePicker.SelectedDate.Value.Date;
            DateTime endDate = EndDatePicker.SelectedDate.Value.Date;

            if (startDate > endDate)
            {
                ErrorText.Text = "Начальная дата не может быть больше конечной.";
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

                _currentReport = new SalesReportData
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

            OrdersCountText.Text = _currentReport.OrdersCount.ToString(CultureInfo.InvariantCulture);
            RevenueText.Text = $"{_currentReport.TotalRevenue:N2} руб.";
            ItemsCountText.Text = _currentReport.Products.Sum(item => item.QuantitySold).ToString(CultureInfo.InvariantCulture);

            ProductsReportGrid.ItemsSource = _currentReport.Products
                .Select(item => new ProductReportRow
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    QuantitySold = item.QuantitySold,
                    RevenueText = $"{item.Revenue:N2} руб."
                })
                .ToList();

            if (_currentReport.Products.Count == 0)
            {
                ErrorText.Text = "За выбранный период продаж не найдено.";
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
