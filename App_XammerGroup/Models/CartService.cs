using System;
using System.Collections.Generic;
using System.Linq;

namespace App_XammerGroup
{
    public static class CartService
    {
        private static readonly Dictionary<int, List<CartItem>> CartsByUserId = new Dictionary<int, List<CartItem>>();

        public static IReadOnlyList<CartItem> GetCartItems(int userId)
        {
            return GetOrCreateCart(userId)
                .Select(item => item.Clone())
                .ToList();
        }

        public static int GetItemCount(int userId)
        {
            return GetOrCreateCart(userId).Sum(item => item.Quantity);
        }

        public static void AddProduct(int userId, Products product, int quantity)
        {
            if (product == null)
            {
                throw new ArgumentNullException(nameof(product));
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity));
            }

            var cart = GetOrCreateCart(userId);
            var existingItem = cart.FirstOrDefault(item => item.ProductId == product.ProductId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
                existingItem.ProductName = product.ProductName;
                existingItem.Price = product.Price;
                return;
            }

            cart.Add(new CartItem
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Price = product.Price,
                Quantity = quantity
            });
        }

        public static void UpdateQuantity(int userId, int productId, int quantity)
        {
            var cart = GetOrCreateCart(userId);
            var item = cart.FirstOrDefault(entry => entry.ProductId == productId);
            if (item == null)
            {
                return;
            }

            if (quantity <= 0)
            {
                cart.Remove(item);
                return;
            }

            item.Quantity = quantity;
        }

        public static void RemoveProduct(int userId, int productId)
        {
            var cart = GetOrCreateCart(userId);
            var item = cart.FirstOrDefault(entry => entry.ProductId == productId);
            if (item != null)
            {
                cart.Remove(item);
            }
        }

        public static void Clear(int userId)
        {
            GetOrCreateCart(userId).Clear();
        }

        public static decimal GetTotal(int userId)
        {
            return GetOrCreateCart(userId).Sum(item => item.TotalPrice);
        }

        private static List<CartItem> GetOrCreateCart(int userId)
        {
            if (!CartsByUserId.TryGetValue(userId, out List<CartItem> cart))
            {
                cart = new List<CartItem>();
                CartsByUserId[userId] = cart;
            }

            return cart;
        }
    }

    public sealed class CartItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice => Price * Quantity;

        public CartItem Clone()
        {
            return new CartItem
            {
                ProductId = ProductId,
                ProductName = ProductName,
                Price = Price,
                Quantity = Quantity
            };
        }
    }
}
