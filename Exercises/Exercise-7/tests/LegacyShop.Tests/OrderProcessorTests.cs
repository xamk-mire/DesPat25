using LegacyShop;
using Xunit;

namespace LegacyShop.Tests;

public class OrderProcessorTests
{
    [Fact]
    public void BookDiscount_IsExclusive_AndBeatsThreshold()
    {
        var order = new Order
        {
            PaymentMethod = "CreditCard",
            ShippingMethod = "Standard",
            Items =
            {
                new OrderItem { Sku = "B-1", Category = "Book", UnitPrice = 30m, Quantity = 2 }, // 60
                new OrderItem { Sku = "T-9", Category = "Toy",  UnitPrice = 50m, Quantity = 1 }  // 50
            }
        };
        // Subtotal = 110
        // Book present ⇒ discount = 5% of subtotal = 5.50 (NOT +10)
        // Shipping(Standard) = 5
        // PaymentFee(CreditCard) = 2% of subtotal = 2.20 (fee is not taxed)
        // Tax base = (subtotal - discount + shipping) = (110 - 5.50 + 5) = 109.50 ⇒ tax = 10% = 10.95
        // Total = 110 - 5.50 + 5 + 2.20 + 10.95 = 122.65

        var sut = new OrderProcessor();
        sut.Process(order);

        Assert.Equal(110m, order.Subtotal);
        Assert.Equal(5.50m, order.Discount);            // should not stack the +10
        Assert.Equal(5m, order.Shipping);
        Assert.Equal(2.20m, order.PaymentFee);
        Assert.Equal(10.95m, order.Tax);                // fee excluded from tax base
        Assert.Equal(122.65m, order.Total);
    }

    [Fact]
    public void Tax_Should_Exclude_PaymentFees()
    {
        var order = new Order
        {
            PaymentMethod = "PayPal",
            ShippingMethod = "Express",
            Items =
            {
                new OrderItem { Sku = "X", Category = "Gadget", UnitPrice = 200m, Quantity = 1 }
            }
        };
        // Subtotal = 200
        // No books; subtotal > 100 ⇒ discount = 10 (flat)
        // Shipping(Express) = 15
        // PaymentFee(PayPal) = 3% of subtotal = 6.00 (NOT taxed)
        // Tax base = (200 - 10 + 15) = 205 ⇒ tax = 20.50
        // Total = 200 - 10 + 15 + 6 + 20.50 = 231.50

        var sut = new OrderProcessor();
        sut.Process(order);

        Assert.Equal(200m, order.Subtotal);
        Assert.Equal(10m, order.Discount);
        Assert.Equal(15m, order.Shipping);
        Assert.Equal(6m, order.PaymentFee);
        Assert.Equal(20.50m, order.Tax);               // excludes fee
        Assert.Equal(231.50m, order.Total);
    }

    [Fact]
    public void Adding_New_PaymentMethod_Should_Not_Change_Tax_Or_Shipping_Tests()
    {
        // This test exists to encourage decoupling of payment fee logic from tax calcs.
        var order = new Order
        {
            PaymentMethod = "Wire", // fee = 0
            ShippingMethod = "Standard",
            Items = { new OrderItem { Sku = "Z", Category = "Toy", UnitPrice = 50m, Quantity = 2 } }
        };
        // Subtotal = 100
        // No book; not > 100 ⇒ no discount
        // Shipping = 5; Fee = 0
        // Tax base = 100 + 5 = 105 ⇒ tax = 10.50
        // Total = 100 + 5 + 0 + 10.50 = 115.50

        var sut = new OrderProcessor();
        sut.Process(order);

        Assert.Equal(100m, order.Subtotal);
        Assert.Equal(0m, order.Discount);
        Assert.Equal(5m, order.Shipping);
        Assert.Equal(0m, order.PaymentFee);
        Assert.Equal(10.50m, order.Tax);
        Assert.Equal(115.50m, order.Total);
    }
}