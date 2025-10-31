using MongoDB.Driver;
using ProductService.Models;
using ProductService.Services;

namespace ProductService.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IMongoDatabase database, IOpenSearchIndexer? searchIndexer = null)
    {
        var categoriesCollection = database.GetCollection<Category>("categories");
        var productsCollection = database.GetCollection<Product>("products");

        if (await categoriesCollection.CountDocumentsAsync(FilterDefinition<Category>.Empty) > 0)
        {
            return;
        }
        var elektronik = new Category
        {
            Name = "Elektronik",
            Slug = "elektronik",
            Description = "Elektronik ürünler",
            ImageUrl = "https://via.placeholder.com/300x200",
            ParentId = null,
            Children = new List<string>(),
            Path = new List<string>(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var giyim = new Category
        {
            Name = "Giyim",
            Slug = "giyim",
            Description = "Erkek ve kadın giyim",
            ImageUrl = "https://via.placeholder.com/300x200",
            ParentId = null,
            Children = new List<string>(),
            Path = new List<string>(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var evYasam = new Category
        {
            Name = "Ev & Yaşam",
            Slug = "ev-yasam",
            Description = "Ev dekorasyonu ve yaşam ürünleri",
            ImageUrl = "https://via.placeholder.com/300x200",
            ParentId = null,
            Children = new List<string>(),
            Path = new List<string>(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var kitaplar = new Category
        {
            Name = "Kitaplar",
            Slug = "kitaplar",
            Description = "Roman, bilim, tarih ve daha fazlası",
            ImageUrl = "https://via.placeholder.com/300x200",
            ParentId = null,
            Children = new List<string>(),
            Path = new List<string>(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var categories = new List<Category> { elektronik, giyim, evYasam, kitaplar };

        await categoriesCollection.InsertManyAsync(categories);

        var products = new List<Product>
        {
            new Product
            {
                Name = "MacBook Pro 14 M3",
                Description = "Apple MacBook Pro 14-inch with M3 chip, 16GB RAM, 512GB SSD",
                Slug = "macbook-pro-14-m3",
                Price = 54999.99m,
                Stock = 25,
                Category = new CategoryInfo
                {
                    Id = elektronik.Id,
                    Name = elektronik.Name,
                    Path = elektronik.Path
                },
                Brand = "Apple",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "laptop", "apple", "premium" },
                IsActive = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "iPhone 15 Pro",
                Description = "Apple iPhone 15 Pro 256GB - Titanium",
                Slug = "iphone-15-pro",
                Price = 49999.99m,
                Stock = 50,
                Category = new CategoryInfo
                {
                    Id = elektronik.Id,
                    Name = elektronik.Name,
                    Path = elektronik.Path
                },
                Brand = "Apple",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "phone", "apple", "5g" },
                IsActive = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Samsung Galaxy S24 Ultra",
                Description = "Samsung Galaxy S24 Ultra 512GB - Phantom Black",
                Slug = "samsung-galaxy-s24-ultra",
                Price = 45999.99m,
                Stock = 30,
                Category = new CategoryInfo
                {
                    Id = elektronik.Id,
                    Name = elektronik.Name,
                    Path = elektronik.Path
                },
                Brand = "Samsung",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "phone", "samsung", "android" },
                IsActive = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Klasik Pamuklu Tişört",
                Description = "%100 pamuk, rahat kesim",
                Slug = "klasik-pamuklu-tisort",
                Price = 299.99m,
                Stock = 200,
                Category = new CategoryInfo
                {
                    Id = giyim.Id,
                    Name = giyim.Name,
                    Path = giyim.Path
                },
                Brand = "Basic",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "t-shirt", "cotton", "casual" },
                IsActive = true,
                IsFeatured = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Premium Polo Tişört",
                Description = "Yaka detaylı, %100 pamuk polo tişört",
                Slug = "premium-polo-tisort",
                Price = 499.99m,
                Stock = 150,
                Category = new CategoryInfo
                {
                    Id = giyim.Id,
                    Name = giyim.Name,
                    Path = giyim.Path
                },
                Brand = "Premium",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "polo", "cotton", "premium" },
                IsActive = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Denim Kot Pantolon",
                Description = "Slim fit, yüksek kaliteli denim kumaş",
                Slug = "denim-kot-pantolon",
                Price = 899.99m,
                Stock = 120,
                Category = new CategoryInfo
                {
                    Id = giyim.Id,
                    Name = giyim.Name,
                    Path = giyim.Path
                },
                Brand = "Denim Co",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "jeans", "denim", "casual" },
                IsActive = true,
                IsFeatured = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Kapüşonlu Sweatshirt",
                Description = "Yumuşak kumaş, kanguru cepli",
                Slug = "kapusonlu-sweatshirt",
                Price = 799.99m,
                Stock = 180,
                Category = new CategoryInfo
                {
                    Id = giyim.Id,
                    Name = giyim.Name,
                    Path = giyim.Path
                },
                Brand = "Urban",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "hoodie", "sweatshirt", "winter" },
                IsActive = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Spor Ayakkabı",
                Description = "Hafif, rahat, günlük kullanım için ideal",
                Slug = "spor-ayakkabi",
                Price = 1299.99m,
                Stock = 90,
                Category = new CategoryInfo
                {
                    Id = giyim.Id,
                    Name = giyim.Name,
                    Path = giyim.Path
                },
                Brand = "SportMax",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "shoes", "sneakers", "sport" },
                IsActive = true,
                IsFeatured = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Temiz Kod",
                Description = "Çevik Yazılım Geliştirme El Kitabı - Robert C. Martin",
                Slug = "temiz-kod",
                Price = 459.99m,
                Stock = 75,
                Category = new CategoryInfo
                {
                    Id = kitaplar.Id,
                    Name = kitaplar.Name,
                    Path = kitaplar.Path
                },
                Brand = "Kodlab",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "programming", "software", "bestseller" },
                IsActive = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Sony WH-1000XM5 Kulaklık",
                Description = "Aktif gürültü önleme özellikli premium kulaklık",
                Slug = "sony-wh-1000xm5-kulaklik",
                Price = 12999.99m,
                Stock = 35,
                Category = new CategoryInfo
                {
                    Id = elektronik.Id,
                    Name = elektronik.Name,
                    Path = elektronik.Path
                },
                Brand = "Sony",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "headphones", "audio", "noise-cancelling" },
                IsActive = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "iPad Air M2",
                Description = "Apple iPad Air 11-inch with M2 chip, 256GB",
                Slug = "ipad-air-m2",
                Price = 28999.99m,
                Stock = 45,
                Category = new CategoryInfo
                {
                    Id = elektronik.Id,
                    Name = elektronik.Name,
                    Path = elektronik.Path
                },
                Brand = "Apple",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "tablet", "apple", "productivity" },
                IsActive = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Logitech MX Master 3S Mouse",
                Description = "Kablosuz ergonomik profesyonel mouse",
                Slug = "logitech-mx-master-3s",
                Price = 3499.99m,
                Stock = 60,
                Category = new CategoryInfo
                {
                    Id = elektronik.Id,
                    Name = elektronik.Name,
                    Path = elektronik.Path
                },
                Brand = "Logitech",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "mouse", "wireless", "ergonomic" },
                IsActive = true,
                IsFeatured = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Dell UltraSharp 27 4K Monitor",
                Description = "27-inch 4K UHD IPS monitör, USB-C hub",
                Slug = "dell-ultrasharp-27-4k",
                Price = 18999.99m,
                Stock = 20,
                Category = new CategoryInfo
                {
                    Id = elektronik.Id,
                    Name = elektronik.Name,
                    Path = elektronik.Path
                },
                Brand = "Dell",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "monitor", "4k", "professional" },
                IsActive = true,
                IsFeatured = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Ergonomik Ofis Sandalyesi",
                Description = "Bel desteği ile rahat ofis sandalyesi",
                Slug = "ergonomik-ofis-sandalyesi",
                Price = 2999.99m,
                Stock = 40,
                Category = new CategoryInfo
                {
                    Id = evYasam.Id,
                    Name = evYasam.Name,
                    Path = evYasam.Path
                },
                Brand = "ErgoMax",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "furniture", "office", "ergonomic" },
                IsActive = true,
                IsFeatured = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Bambu Kahve Sehpası",
                Description = "Modern tasarım, doğal bambu malzeme",
                Slug = "bambu-kahve-sehpasi",
                Price = 1499.99m,
                Stock = 25,
                Category = new CategoryInfo
                {
                    Id = evYasam.Id,
                    Name = evYasam.Name,
                    Path = evYasam.Path
                },
                Brand = "NatureHome",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "furniture", "table", "bamboo" },
                IsActive = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Dekoratif LED Masa Lambası",
                Description = "Dimlenebilir, RGB renk seçenekli akıllı lamba",
                Slug = "dekoratif-led-masa-lambasi",
                Price = 899.99m,
                Stock = 55,
                Category = new CategoryInfo
                {
                    Id = evYasam.Id,
                    Name = evYasam.Name,
                    Path = evYasam.Path
                },
                Brand = "LightHome",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "lighting", "smart", "decoration" },
                IsActive = true,
                IsFeatured = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Yumuşak Peluş Halı 160x230",
                Description = "Kaymaz tabanlı, makinede yıkanabilir halı",
                Slug = "yumusak-pelus-hali",
                Price = 2199.99m,
                Stock = 30,
                Category = new CategoryInfo
                {
                    Id = evYasam.Id,
                    Name = evYasam.Name,
                    Path = evYasam.Path
                },
                Brand = "ComfortFloor",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "carpet", "home", "soft" },
                IsActive = true,
                IsFeatured = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Seramik Vazo Seti 3'lü",
                Description = "El yapımı seramik vazolar, farklı boyutlarda",
                Slug = "seramik-vazo-seti",
                Price = 649.99m,
                Stock = 40,
                Category = new CategoryInfo
                {
                    Id = evYasam.Id,
                    Name = evYasam.Name,
                    Path = evYasam.Path
                },
                Brand = "ArtDecor",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "vase", "ceramic", "decoration" },
                IsActive = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Algoritma Tasarımı",
                Description = "Jon Kleinberg ve Éva Tardos - Algoritma ve veri yapıları",
                Slug = "algoritma-tasarimi",
                Price = 589.99m,
                Stock = 50,
                Category = new CategoryInfo
                {
                    Id = kitaplar.Id,
                    Name = kitaplar.Name,
                    Path = kitaplar.Path
                },
                Brand = "Pearson",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "programming", "algorithms", "computer-science" },
                IsActive = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Suç ve Ceza",
                Description = "Fyodor Dostoyevski - Klasik Rus edebiyatı",
                Slug = "suc-ve-ceza",
                Price = 349.99m,
                Stock = 80,
                Category = new CategoryInfo
                {
                    Id = kitaplar.Id,
                    Name = kitaplar.Name,
                    Path = kitaplar.Path
                },
                Brand = "İş Bankası Kültür Yayınları",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "novel", "classic", "russian-literature" },
                IsActive = true,
                IsFeatured = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Sapiens",
                Description = "Yuval Noah Harari - İnsan Türünün Kısa Bir Tarihi",
                Slug = "sapiens",
                Price = 429.99m,
                Stock = 70,
                Category = new CategoryInfo
                {
                    Id = kitaplar.Id,
                    Name = kitaplar.Name,
                    Path = kitaplar.Path
                },
                Brand = "Kolektif Kitap",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "history", "anthropology", "bestseller" },
                IsActive = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "Atomic Habits",
                Description = "James Clear - Kötü Alışkanlıklardan Kurtulmanın ve İyi Alışkanlıklar Edinmenin En Kolay Yolu",
                Slug = "atomic-habits",
                Price = 399.99m,
                Stock = 90,
                Category = new CategoryInfo
                {
                    Id = kitaplar.Id,
                    Name = kitaplar.Name,
                    Path = kitaplar.Path
                },
                Brand = "Pegasus Yayınları",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "self-help", "productivity", "habits" },
                IsActive = true,
                IsFeatured = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Product
            {
                Name = "1984",
                Description = "George Orwell - Distopik klasik roman",
                Slug = "1984",
                Price = 299.99m,
                Stock = 65,
                Category = new CategoryInfo
                {
                    Id = kitaplar.Id,
                    Name = kitaplar.Name,
                    Path = kitaplar.Path
                },
                Brand = "Can Yayınları",
                Images = new List<string>
                {
                    "https://via.placeholder.com/400x300",
                    "https://via.placeholder.com/400x300",
                },
                Tags = new List<string> { "novel", "dystopia", "classic" },
                IsActive = true,
                IsFeatured = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        await productsCollection.InsertManyAsync(products);

        if (searchIndexer != null)
        {
            foreach (var product in products)
            {
                await searchIndexer.IndexProductAsync(product);
            }
        }
    }
}
