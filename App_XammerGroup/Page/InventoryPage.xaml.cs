using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace App_XammerGroup
{
    public partial class InventoryPage : Page
    {
        public InventoryPage()
        {
            InitializeComponent();
            Loaded += (sender, args) => LoadInventory();
        }

        private void LoadInventory()
        {
            InventoryGrid.ItemsSource = InventoryService.GetInventoryRows();
        }

        private void InventoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = InventoryGrid.SelectedItem as InventoryRow;
            if (selectedItem == null)
            {
                ClearSelectedItemForm();
                return;
            }

            ItemNameBox.Text = selectedItem.ItemName ?? string.Empty;
            UnitNameBox.Text = selectedItem.UnitName ?? string.Empty;
            QuantityOnHandBox.Text = selectedItem.QuantityOnHand.ToString(CultureInfo.CurrentCulture);
            MinQuantityBox.Text = selectedItem.MinQuantity.ToString(CultureInfo.CurrentCulture);
        }

        private void SaveInventoryItem_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Foreground = ErrorBrush();
            ErrorText.Text = string.Empty;

            var selectedItem = InventoryGrid.SelectedItem as InventoryRow;
            if (selectedItem == null)
            {
                ErrorText.Text = "Выберите материал для редактирования.";
                return;
            }

            string itemName = ItemNameBox.Text.Trim();
            string unitName = UnitNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(unitName))
            {
                ErrorText.Text = "Введите название материала и единицу измерения.";
                return;
            }

            if (!TryParseDecimal(QuantityOnHandBox.Text, out decimal quantityOnHand) || quantityOnHand < 0)
            {
                ErrorText.Text = "Введите корректный остаток.";
                return;
            }

            if (!TryParseDecimal(MinQuantityBox.Text, out decimal minQuantity) || minQuantity < 0)
            {
                ErrorText.Text = "Введите корректный минимальный остаток.";
                return;
            }

            try
            {
                InventoryService.UpdateInventoryItem(selectedItem.InventoryItemId, itemName, unitName, quantityOnHand, minQuantity);
                LoadInventory();
                ErrorText.Foreground = SuccessBrush();
                ErrorText.Text = "Материал сохранен.";
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Не удалось сохранить материал.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddStock_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Foreground = ErrorBrush();
            ErrorText.Text = string.Empty;

            var selectedItem = InventoryGrid.SelectedItem as InventoryRow;
            if (selectedItem == null)
            {
                ErrorText.Text = "Выберите материал для пополнения.";
                return;
            }

            if (!TryParseDecimal(AddQuantityBox.Text, out decimal quantity) || quantity <= 0)
            {
                ErrorText.Text = "Введите положительное количество.";
                return;
            }

            try
            {
                InventoryService.AddStock(selectedItem.InventoryItemId, quantity, NormalizeText(CommentBox.Text));
                AddQuantityBox.Text = string.Empty;
                CommentBox.Text = string.Empty;
                LoadInventory();
                ErrorText.Foreground = SuccessBrush();
                ErrorText.Text = "Склад пополнен.";
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Не удалось пополнить склад.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddInventoryItem_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Foreground = ErrorBrush();
            ErrorText.Text = string.Empty;

            string itemName = NewItemNameBox.Text.Trim();
            string unitName = NewUnitNameBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(unitName))
            {
                ErrorText.Text = "Введите название материала и единицу измерения.";
                return;
            }

            if (!TryParseDecimal(NewQuantityBox.Text, out decimal quantity) || quantity < 0)
            {
                ErrorText.Text = "Введите корректный начальный остаток.";
                return;
            }

            if (!TryParseDecimal(NewMinQuantityBox.Text, out decimal minQuantity) || minQuantity < 0)
            {
                ErrorText.Text = "Введите корректный минимальный остаток.";
                return;
            }

            try
            {
                InventoryService.AddInventoryItem(itemName, unitName, quantity, minQuantity);
                ClearNewItemForm();
                LoadInventory();
                ErrorText.Foreground = SuccessBrush();
                ErrorText.Text = "Материал добавлен.";
            }
            catch (Exception ex)
            {
                ErrorText.Text = "Не удалось добавить материал.";
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadInventory();
        }

        private void ClearSelectedItemForm()
        {
            ItemNameBox.Text = string.Empty;
            UnitNameBox.Text = string.Empty;
            QuantityOnHandBox.Text = string.Empty;
            MinQuantityBox.Text = string.Empty;
        }

        private void ClearNewItemForm()
        {
            NewItemNameBox.Text = string.Empty;
            NewUnitNameBox.Text = string.Empty;
            NewQuantityBox.Text = string.Empty;
            NewMinQuantityBox.Text = string.Empty;
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

        private static string NormalizeText(string value)
        {
            string trimmed = value?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
        }

        private static SolidColorBrush ErrorBrush()
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB00020"));
        }

        private static SolidColorBrush SuccessBrush()
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2E7D32"));
        }
    }
}
