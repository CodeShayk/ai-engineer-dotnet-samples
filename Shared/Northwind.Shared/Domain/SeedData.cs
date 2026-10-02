namespace Northwind.Shared.Domain;

/// <summary>
/// Sample data for Northwind Traders. The customer names are a nod to Microsoft's classic
/// Northwind sample database; email addresses use the reserved example.com domain.
/// </summary>
public static class SeedData
{
    public static IReadOnlyList<Customer> Customers() =>
    [
        new("C001", "Maria Anders", "maria.anders@example.com", "Berlin", "Germany", "Gold"),
        new("C002", "Ana Trujillo", "ana.trujillo@example.com", "Mexico City", "Mexico", "Silver"),
        new("C003", "Thomas Hardy", "thomas.hardy@example.com", "London", "United Kingdom", "Gold"),
        new("C004", "Christina Berglund", "christina.berglund@example.com", "Luleå", "Sweden", "Bronze"),
        new("C005", "Hanna Moos", "hanna.moos@example.com", "Mannheim", "Germany", "Silver"),
        new("C006", "Laurence Lebihan", "laurence.lebihan@example.com", "Marseille", "France", "Bronze"),
        new("C007", "Elizabeth Lincoln", "elizabeth.lincoln@example.com", "Tsawassen", "Canada", "Silver"),
        new("C008", "Yang Wang", "yang.wang@example.com", "Bern", "Switzerland", "Bronze"),
    ];

    public static IReadOnlyList<Product> Products() =>
    [
        new("P01", "Aurora Wireless Earbuds", ProductCategory.Electronics, 129.00m,
            "Noise-cancelling wireless earbuds with a sweat-resistant design, 8-hour battery life and a pocket-sized charging case. Ideal for running and commuting."),
        new("P02", "Nimbus Smart Speaker", ProductCategory.Electronics, 89.00m,
            "Compact smart speaker with room-filling sound, voice assistant support and Bluetooth streaming from iOS and Android devices."),
        new("P03", "Lumen Desk Lamp", ProductCategory.HomeAndGarden, 45.00m,
            "Dimmable LED desk lamp with adjustable colour temperature and a USB charging port in the base."),
        new("P04", "Kestrel 14-inch Laptop Sleeve", ProductCategory.Accessories, 19.99m,
            "Padded neoprene sleeve for 13- and 14-inch laptops with a zipped accessory pocket. Clearance item.", FinalSale: true),
        new("P05", "Harbor Rain Jacket", ProductCategory.Clothing, 95.00m,
            "Lightweight waterproof jacket with taped seams, an adjustable hood and a packable design."),
        new("P06", "Summit Hiking Boots", ProductCategory.Footwear, 140.00m,
            "Waterproof leather hiking boots with a cushioned midsole and grippy rubber outsole for rough trails."),
        new("P07", "Teak Garden Bench", ProductCategory.HomeAndGarden, 349.00m,
            "Solid teak three-seater garden bench, weather-resistant and suitable for year-round outdoor use."),
        new("P08", "Brewmaster Electric Kettle", ProductCategory.Kitchen, 59.00m,
            "1.7-litre stainless steel kettle with temperature control and a keep-warm function for tea and coffee."),
        new("P09", "Classic Chai Tea (500 g)", ProductCategory.Grocery, 12.50m,
            "Loose-leaf black tea blended with cardamom, cinnamon and ginger, from Northwind's original range."),
        new("P10", "Chang Ginger Beer (12-pack)", ProductCategory.Grocery, 18.00m,
            "Fiery brewed ginger beer in glass bottles, a Northwind classic since the early days."),
        new("P11", "Trailhead Daypack TD-35", ProductCategory.Accessories, 75.00m,
            "35-litre hiking daypack (model TD-35) with a water-resistant coating, rain cover, padded hip belt and hydration sleeve."),
        new("P12", "Orbit Fitness Watch", ProductCategory.Electronics, 199.00m,
            "GPS fitness watch with heart-rate monitoring, sleep tracking and seven-day battery life."),
        new("P13", "Summit Rain Shell", ProductCategory.Clothing, 120.00m,
            "Breathable waterproof shell jacket for mountain weather, with pit zips and a helmet-compatible hood."),
    ];

    public static IReadOnlyList<Order> Orders(DateOnly today)
    {
        DateOnly Days(int offset) => today.AddDays(offset);

        return
        [
            new("NW-10248", "C003", Days(-10), OrderStatus.Delivered,
                [new("P05", "Harbor Rain Jacket", 1, 95.00m)], "Standard", "ZX99812031", DeliveredOn: Days(-6)),
            new("NW-10249", "C003", Days(-4), OrderStatus.Shipped,
                [new("P01", "Aurora Wireless Earbuds", 1, 129.00m)], "Standard", "ZX99812044", EstimatedDelivery: Days(1)),
            new("NW-10250", "C001", Days(-1), OrderStatus.Processing,
                [new("P07", "Teak Garden Bench", 1, 349.00m)], "Standard", EstimatedDelivery: Days(6)),
            new("NW-10251", "C001", Days(-45), OrderStatus.Delivered,
                [new("P08", "Brewmaster Electric Kettle", 1, 59.00m)], "Express", "ZX99811207", DeliveredOn: Days(-40)),
            new("NW-10252", "C002", Days(-13), OrderStatus.Delivered,
                [new("P02", "Nimbus Smart Speaker", 1, 89.00m)], "Standard", "ZX99811876", DeliveredOn: Days(-10)),
            new("NW-10253", "C002", Days(-6), OrderStatus.Delivered,
                [new("P09", "Classic Chai Tea (500 g)", 3, 12.50m)], "Standard", "ZX99812011", DeliveredOn: Days(-3)),
            new("NW-10254", "C004", Days(-24), OrderStatus.Delivered,
                [new("P06", "Summit Hiking Boots", 1, 140.00m)], "Standard", "ZX99811650", DeliveredOn: Days(-20)),
            new("NW-10255", "C005", Days(-8), OrderStatus.Shipped,
                [new("P03", "Lumen Desk Lamp", 2, 45.00m)], "Standard", "ZX99812102", EstimatedDelivery: Days(-2)),
            new("NW-10256", "C006", Days(-7), OrderStatus.Delivered,
                [new("P04", "Kestrel 14-inch Laptop Sleeve", 1, 19.99m)], "Standard", "ZX99812076", DeliveredOn: Days(-4)),
            new("NW-10257", "C007", Days(-12), OrderStatus.Cancelled,
                [new("P01", "Aurora Wireless Earbuds", 1, 129.00m), new("P11", "Trailhead Daypack TD-35", 1, 75.00m)], "Express"),
            new("NW-10258", "C008", Days(0), OrderStatus.Pending,
                [new("P10", "Chang Ginger Beer (12-pack)", 2, 18.00m), new("P08", "Brewmaster Electric Kettle", 1, 59.00m)], "Standard"),
            new("NW-10259", "C004", Days(-21), OrderStatus.Delivered,
                [new("P02", "Nimbus Smart Speaker", 1, 89.00m)], "Standard", "ZX99811702", DeliveredOn: Days(-18)),
            new("NW-10260", "C005", Days(-15), OrderStatus.ReturnRequested,
                [new("P05", "Harbor Rain Jacket", 1, 95.00m)], "Standard", "ZX99811733", DeliveredOn: Days(-12)),
            new("NW-10261", "C006", Days(-34), OrderStatus.Refunded,
                [new("P01", "Aurora Wireless Earbuds", 1, 129.00m)], "Express", "ZX99811402", DeliveredOn: Days(-30)),
            new("NW-10262", "C003", Days(-18), OrderStatus.Delivered,
                [new("P11", "Trailhead Daypack TD-35", 1, 75.00m)], "Standard", "ZX99811690", DeliveredOn: Days(-15)),
        ];
    }
}
