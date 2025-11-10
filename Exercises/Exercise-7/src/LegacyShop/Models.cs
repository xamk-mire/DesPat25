namespace LegacyShop;

public class Order
{
    public List<OrderItem> Items { get; set; } = new();
    public string PaymentMethod { get; set; } = "CreditCard"; // magic string (intentional in legacy)
    public string ShippingMethod { get; set; } = "Standard";  // magic string (intentional in legacy)

    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Shipping { get; set; }
    public decimal PaymentFee { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
}

public class OrderItem
{
    public string Sku { get; set; } = "";
    public string Category { get; set; } = ""; // e.g., "Book", "Toy"
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}