using System;

namespace LegacyShop;

public class OrderProcessor
{
    // "Global" shared logger just writes to Console – hard to test & noisy.
    private static class Logger
    {
        public static void Info(string message) => Console.WriteLine($"[INFO] {DateTime.Now}: {message}");
    }

    public void Process(Order order)
    {
        // 1) Calculate subtotal
        decimal subtotal = 0m;
        foreach (var it in order.Items)
        {
            subtotal += it.UnitPrice * it.Quantity;
        }
        order.Subtotal = subtotal;

        // 2) Discounts (WRONG): book discount should be exclusive with threshold discount
        decimal discount = 0m;
        bool hasBook = order.Items.Exists(i => string.Equals(i.Category, "Book", StringComparison.OrdinalIgnoreCase));
        if (hasBook)
        {
            discount += subtotal * 0.05m; // 5%
        }
        if (subtotal > 100m)
        {
            discount += 10m; // should NOT stack if book discount is applied
        }
        order.Discount = discount;

        // 3) Shipping – magic strings & switches
        decimal shipping = 0m;
        switch (order.ShippingMethod)
        {
            case "Standard": shipping = 5m; break;
            case "Express": shipping = 15m; break;
            default: shipping = 5m; break; // silent fallback
        }
        order.Shipping = shipping;

        // 4) Payment fees – magic strings & switches
        decimal fee = 0m;
        switch (order.PaymentMethod)
        {
            case "CreditCard": fee = subtotal * 0.02m; break;  // 2%
            case "PayPal": fee = subtotal * 0.03m; break;      // 3%
            case "Wire": fee = 0m; break;
            default: fee = 0m; break; // silent fallback
        }
        order.PaymentFee = fee;

        // 5) Tax (WRONG): should not tax the payment fee
        var taxable = subtotal - discount + shipping + fee; // incorrect base
        var tax = Math.Round(taxable * 0.10m, 2, MidpointRounding.AwayFromZero); // 10%
        order.Tax = tax;

        order.Total = subtotal - discount + shipping + fee + tax;

        // Hidden side effect – writing to console in domain logic
        Logger.Info($"Processed order: total={order.Total}");
    }
}