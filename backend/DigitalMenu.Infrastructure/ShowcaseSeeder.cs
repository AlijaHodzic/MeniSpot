using DigitalMenu.Application;
using DigitalMenu.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DigitalMenu.Infrastructure;

internal static class ShowcaseSeeder
{
    private const string DelRioSlug = "del-rio-mostar";
    private const string DelRioOwnerEmail = "delrio@menispot.demo";
    private const string ShowcaseBasePath = "/showcase/del-rio";

    public static async Task SeedDelRioAsync(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var restaurant = await db.Restaurants
            .Include(x => x.Subscription)
            .Include(x => x.Theme)
            .Include(x => x.Categories).ThenInclude(x => x.Items)
            .Include(x => x.SpecialOffers)
            .Include(x => x.BusinessHours)
            .FirstOrDefaultAsync(x => x.Slug == DelRioSlug);

        if (restaurant is null)
        {
            restaurant = new Restaurant
            {
                Name = "Del Rio Restaurant",
                Slug = DelRioSlug,
                Type = EstablishmentType.Restaurant,
                Status = RestaurantStatus.Active
            };
            db.Restaurants.Add(restaurant);
        }

        restaurant.Name = "Del Rio Restaurant";
        restaurant.Slug = DelRioSlug;
        restaurant.Description = "Premium demo meni za restoran u Mostaru, sa fokusom na steak, riblje specijalitete, sezonske ponude i elegantan vizuelni identitet.";
        restaurant.LogoUrl = Asset("logo.jpg");
        restaurant.CoverImageUrl = Asset("cover-chef-flame.jpg");
        restaurant.Address = "Kardinala Stepinca 12, Mostar 88000";
        restaurant.Phone = "+387 36 000 000";
        restaurant.Email = "info@menispot.com";
        restaurant.WebsiteUrl = "https://menispot.com/menu/del-rio-mostar";
        restaurant.InstagramUrl = "https://www.instagram.com/delriomostar";
        restaurant.Currency = "BAM";
        restaurant.DefaultLanguage = "bs";
        restaurant.EnabledLanguages = "bs,en,de";
        restaurant.Type = EstablishmentType.Restaurant;
        restaurant.Status = RestaurantStatus.Active;
        restaurant.ArchivedAt = null;
        restaurant.ArchivedByUserId = null;
        restaurant.UpdatedAt = DateTimeOffset.UtcNow;

        restaurant.Subscription ??= new Subscription { RestaurantId = restaurant.Id, StartsOn = today };
        restaurant.Subscription.Plan = "Premium";
        restaurant.Subscription.MonthlyPrice = 79m;
        restaurant.Subscription.Status = SubscriptionStatus.Active;
        restaurant.Subscription.StartsOn = new DateOnly(2026, 7, 1);
        restaurant.Subscription.ExpiresOn = new DateOnly(2027, 12, 31);
        restaurant.Subscription.GracePeriodEndsOn = null;
        restaurant.Subscription.UpdatedAt = DateTimeOffset.UtcNow;

        restaurant.Theme ??= new ThemeSettings { RestaurantId = restaurant.Id };
        restaurant.Theme.ThemeKey = "premium-gold";
        restaurant.Theme.PrimaryColor = "#141922";
        restaurant.Theme.AccentColor = "#D2B16C";
        restaurant.Theme.BackgroundImageUrl = Asset("interior-wood.jpg");
        restaurant.Theme.FontFamily = "Inter";
        restaurant.Theme.UpdatedAt = DateTimeOffset.UtcNow;

        ReplaceRestaurantContent(db, restaurant);
        await EnsureOwnerAsync(users, restaurant);
        await db.SaveChangesAsync();
        await SeedAnalyticsAsync(db, restaurant);
    }

    private static void ReplaceRestaurantContent(ApplicationDbContext db, Restaurant restaurant)
    {
        if (restaurant.Categories.Count != 0)
        {
            db.MenuItems.RemoveRange(restaurant.Categories.SelectMany(x => x.Items));
            db.MenuCategories.RemoveRange(restaurant.Categories);
        }
        if (restaurant.SpecialOffers.Count != 0) db.SpecialOffers.RemoveRange(restaurant.SpecialOffers);
        if (restaurant.BusinessHours.Count != 0) db.BusinessHours.RemoveRange(restaurant.BusinessHours);

        restaurant.Categories = [];
        restaurant.SpecialOffers = [];
        restaurant.BusinessHours = [];

        var chef = Category(restaurant.Id, "Chef preporuke", "Chef Recommendations", "Chef Empfehlungen", 1);
        var sea = Category(restaurant.Id, "Morski specijaliteti", "Seafood Specialties", "Meeresfruchte", 2);
        var grill = Category(restaurant.Id, "Grill & steak", "Grill & Steak", "Grill & Steak", 3);
        var dessert = Category(restaurant.Id, "Deserti", "Desserts", "Desserts", 4);
        var drinks = Category(restaurant.Id, "Pica", "Drinks", "Getranke", 5, MenuCategoryType.Drink);

        chef.Items.Add(Item(restaurant.Id, chef.Id, "Del Rio signature zalogaj", "Hrskavi chef zalogaj na kremastoj podlozi i redukovanom sosu.", "Del Rio Signature Bite", "Crispy chef bite served with a creamy base and reduced sauce.", "Del Rio Signature Happen", "Knuspriger Happen mit cremiger Basis und reduzierter Sauce.", 16m, Asset("signature-bites.jpg"), 1, "porcija", "Mlijeko, gluten, jaja", "Sezonski krompir, krem sir, demi-glace sos, mikro bilje", 430, 14m, 38m, 24m, 4m, 1.6m, featured: true));
        chef.Items.Add(Item(restaurant.Id, chef.Id, "Teleci medaljoni u demi-glace sosu", "Medaljoni na kremastoj podlozi, sa bogatim demi-glace sosom i svjezim zacinima.", "Veal Medallions in Demi-Glace", "Medallions on a creamy base with rich demi-glace sauce and fresh herbs.", "Kalbsmedaillons in Demi-Glace", "Medaillons auf cremiger Basis mit Demi-Glace und frischen Krautern.", 28m, Asset("demi-glace.jpg"), 2, "porcija", "Mlijeko, celer", "Teleci medaljoni, demi-glace, pire, mikro bilje", 620, 36m, 28m, 38m, 6m, 2.1m, featured: true));

        sea.Items.Add(Item(restaurant.Id, sea.Id, "Hobotnica sa zara", "Grilovana hobotnica uz kremasti krompir, limun i aromaticno maslinovo ulje.", "Grilled Octopus", "Grilled octopus with creamy potatoes, lemon and aromatic olive oil.", "Gegrillter Oktopus", "Gegrillter Oktopus mit cremigen Kartoffeln, Zitrone und Olivenol.", 32m, Asset("octopus.jpg"), 1, "porcija", "Mekusci, mlijeko", "Hobotnica, krompir, maslinovo ulje, limun, zacinsko bilje", 510, 42m, 31m, 21m, 3m, 2.4m, featured: true));
        sea.Items.Add(Item(restaurant.Id, sea.Id, "Losos Pepe Verde", "File lososa u sosu od zelenog bibera, serviran sa krompirom i povrcem.", "Salmon Pepe Verde", "Salmon fillet in green pepper sauce served with potatoes and vegetables.", "Lachs Pepe Verde", "Lachsfilet in gruner Pfeffersauce mit Kartoffeln und Gemuse.", 30m, Asset("losos-pepe-verde.jpg"), 2, "porcija", "Riba, mlijeko", "Losos, zeleni biber, vrhnje, krompir, zacinsko bilje", 690, 39m, 34m, 43m, 5m, 2.0m));
        sea.Items.Add(Item(restaurant.Id, sea.Id, "Kozice u maslinovom ulju", "Kozice u toplom maslinovom ulju sa bijelim lukom, tostom i laganim prilogom.", "Shrimps in Olive Oil", "Shrimps in warm olive oil with garlic, toast and a light side.", "Garnelen in Olivenol", "Garnelen in warmem Olivenol mit Knoblauch, Toast und Beilage.", 24m, Asset("kozice-u-ulju.jpg"), 3, "porcija", "Rakovi, gluten", "Kozice, maslinovo ulje, bijeli luk, tost, sezonski prilog", 560, 31m, 22m, 37m, 2m, 1.9m));

        grill.Items.Add(Item(restaurant.Id, grill.Id, "Beef steak salata", "Tanko rezani steak preko hrskave salate sa sezonskim povrcem i dressingom kuce.", "Beef Steak Salad", "Thinly sliced steak over crisp salad with seasonal vegetables and house dressing.", "Beef Steak Salat", "Dunn geschnittenes Steak auf knackigem Salat mit Hausdressing.", 22m, Asset("beef-steak-salad.jpg"), 1, "porcija", "Senf", "Beef steak, mix salata, krastavac, paradajz, dressing kuce", 480, 34m, 18m, 29m, 6m, 1.4m));
        grill.Items.Add(Item(restaurant.Id, grill.Id, "Del Rio steak plate", "Steak po preporuci kuce, sezonski prilog i sos po izboru.", "Del Rio Steak Plate", "House recommended steak with seasonal side and sauce of choice.", "Del Rio Steak Teller", "Steak nach Empfehlung des Hauses mit Beilage und Sauce.", 36m, Asset("cover-chef-flame.jpg"), 2, "porcija", "Mlijeko", "Steak, sezonsko povrce, krompir, sos kuce", 740, 48m, 38m, 42m, 5m, 2.3m, featured: true));

        dessert.Items.Add(Item(restaurant.Id, dessert.Id, "Eclair sa vanilijom", "Domaci eclair punjen kremom od vanilije, uz kuglu sladoleda i sos od sumskog voca.", "Vanilla Eclair", "House eclair filled with vanilla cream, served with ice cream and forest fruit sauce.", "Vanille Eclair", "Hausgemachter Eclair mit Vanillecreme, Eis und Waldfruchtsauce.", 9m, Asset("dessert-eclair.jpg"), 1, "porcija", "Gluten, mlijeko, jaja", "Choux tijesto, vanilija krema, sladoled, sos od sumskog voca", 410, 8m, 46m, 22m, 29m, 0.7m));

        drinks.Items.Add(Item(restaurant.Id, drinks.Id, "Zilavka casa", "Hercegovacko bijelo vino na casu.", "Zilavka Glass", "Herzegovinian white wine by the glass.", "Zilavka Glas", "Herzegowinischer Weisswein im Glas.", 6m, null, 1, "0.15l", "Sulfiti", "Bijelo vino", 120, 0m, 4m, 0m, 1m, 0m));
        drinks.Items.Add(Item(restaurant.Id, drinks.Id, "Blatina casa", "Hercegovacko crno vino na casu.", "Blatina Glass", "Herzegovinian red wine by the glass.", "Blatina Glas", "Herzegowinischer Rotwein im Glas.", 6m, null, 2, "0.15l", "Sulfiti", "Crno vino", 125, 0m, 4m, 0m, 1m, 0m));
        drinks.Items.Add(Item(restaurant.Id, drinks.Id, "San Pellegrino", "Gazirana mineralna voda.", "San Pellegrino", "Sparkling mineral water.", "San Pellegrino", "Mineralwasser mit Kohlensaure.", 5m, null, 3, "0.75l", null, "Mineralna voda", 0, 0m, 0m, 0m, 0m, 0m));

        restaurant.Categories.Add(chef);
        restaurant.Categories.Add(sea);
        restaurant.Categories.Add(grill);
        restaurant.Categories.Add(dessert);
        restaurant.Categories.Add(drinks);

        restaurant.SpecialOffers.Add(new SpecialOffer
        {
            RestaurantId = restaurant.Id,
            Title = "Chef tasting vecer",
            Description = "Tri slijeda po izboru chefa, idealno za prezentaciju Premium menija.",
            TitleEn = "Chef Tasting Evening",
            DescriptionEn = "Three chef-selected courses, ideal for showcasing the Premium menu.",
            TitleDe = "Chef Tasting Abend",
            DescriptionDe = "Drei vom Chef ausgewahlte Gange, ideal fur die Premium-Prasentation.",
            Items = "Signature zalogaj, hobotnica sa zara, eclair sa vanilijom",
            ItemsEn = "Signature bite, grilled octopus, vanilla eclair",
            ItemsDe = "Signature Happen, gegrillter Oktopus, Vanille Eclair",
            Price = 49m,
            OriginalPrice = 59m,
            Kind = SpecialOfferKind.Promotion,
            ImageUrl = Asset("interior-dining.jpg"),
            StartsAt = DateTimeOffset.UtcNow.AddDays(-7),
            EndsAt = DateTimeOffset.UtcNow.AddMonths(3),
            IsVisible = true
        });
        restaurant.SpecialOffers.Add(new SpecialOffer
        {
            RestaurantId = restaurant.Id,
            Title = "Dnevna preporuka kuce",
            Description = "Losos Pepe Verde uz sezonsku salatu i casu Zilavke.",
            TitleEn = "House Daily Recommendation",
            DescriptionEn = "Salmon Pepe Verde with seasonal salad and a glass of Zilavka.",
            TitleDe = "Tagesempfehlung des Hauses",
            DescriptionDe = "Lachs Pepe Verde mit saisonalem Salat und einem Glas Zilavka.",
            Items = "Losos Pepe Verde, sezonska salata, Zilavka 0.15l",
            ItemsEn = "Salmon Pepe Verde, seasonal salad, Zilavka 0.15l",
            ItemsDe = "Lachs Pepe Verde, saisonaler Salat, Zilavka 0.15l",
            Price = 34m,
            Kind = SpecialOfferKind.DailyMenu,
            ImageUrl = Asset("losos-pepe-verde.jpg"),
            IsVisible = true
        });

        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            restaurant.BusinessHours.Add(new BusinessHour
            {
                RestaurantId = restaurant.Id,
                DayOfWeek = day,
                OpensAt = day == DayOfWeek.Sunday ? new TimeOnly(12, 0) : new TimeOnly(9, 0),
                ClosesAt = day == DayOfWeek.Sunday ? new TimeOnly(22, 0) : new TimeOnly(23, 0),
                IsClosed = false
            });
        }
    }

    private static async Task EnsureOwnerAsync(UserManager<ApplicationUser> users, Restaurant restaurant)
    {
        var owner = await users.FindByEmailAsync(DelRioOwnerEmail);
        if (owner is null)
        {
            owner = new ApplicationUser
            {
                UserName = DelRioOwnerEmail,
                Email = DelRioOwnerEmail,
                EmailConfirmed = true,
                DisplayName = "Del Rio Owner",
                RestaurantId = restaurant.Id
            };
            var result = await users.CreateAsync(owner, "DelRioDemo2026!");
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        }
        else
        {
            owner.RestaurantId = restaurant.Id;
            owner.DisplayName = "Del Rio Owner";
            owner.EmailConfirmed = true;
            await users.UpdateAsync(owner);
        }

        if (!await users.IsInRoleAsync(owner, Roles.RestaurantOwner))
            await users.AddToRoleAsync(owner, Roles.RestaurantOwner);
    }

    private static async Task SeedAnalyticsAsync(ApplicationDbContext db, Restaurant restaurant)
    {
        var items = await db.MenuItems
            .Where(x => x.RestaurantId == restaurant.Id && x.IsVisible)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();
        if (items.Count == 0) return;

        db.MenuViews.RemoveRange(await db.MenuViews.Where(x => x.RestaurantId == restaurant.Id && x.Source == "showcase-seed").ToListAsync());
        db.MenuItemViews.RemoveRange(await db.MenuItemViews.Where(x => x.RestaurantId == restaurant.Id && x.Source == "showcase-seed").ToListAsync());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dailyCounts = new[] { 7, 12, 18, 24, 16, 31, 42 };
        for (var offset = 0; offset < dailyCounts.Length; offset++)
        {
            var date = today.AddDays(offset - dailyCounts.Length + 1);
            for (var i = 0; i < dailyCounts[offset]; i++)
            {
                db.MenuViews.Add(new MenuView { RestaurantId = restaurant.Id, ViewedOn = date, Source = "showcase-seed" });
            }
        }

        var topWeights = items
            .Select((item, index) => (Item: item, Count: Math.Max(6, 44 - index * 4)))
            .ToList();
        foreach (var (item, count) in topWeights)
        {
            for (var i = 0; i < count; i++)
            {
                db.MenuItemViews.Add(new MenuItemView
                {
                    RestaurantId = restaurant.Id,
                    MenuItemId = item.Id,
                    ViewedOn = today.AddDays(-(i % 21)),
                    Source = "showcase-seed",
                    SessionId = $"del-rio-demo-{item.Id:N}-{i}"
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static MenuCategory Category(Guid restaurantId, string name, string nameEn, string nameDe, int sortOrder, MenuCategoryType type = MenuCategoryType.Food) =>
        new()
        {
            RestaurantId = restaurantId,
            Name = name,
            NameEn = nameEn,
            NameDe = nameDe,
            Type = type,
            SortOrder = sortOrder,
            IsVisible = true
        };

    private static MenuItem Item(
        Guid restaurantId,
        Guid categoryId,
        string name,
        string description,
        string nameEn,
        string descriptionEn,
        string nameDe,
        string descriptionDe,
        decimal price,
        string? imageUrl,
        int sortOrder,
        string servingSize,
        string? allergens,
        string ingredients,
        int calories,
        decimal protein,
        decimal carbs,
        decimal fat,
        decimal sugar,
        decimal salt,
        bool featured = false) =>
        new()
        {
            RestaurantId = restaurantId,
            CategoryId = categoryId,
            Name = name,
            Description = description,
            NameEn = nameEn,
            DescriptionEn = descriptionEn,
            NameDe = nameDe,
            DescriptionDe = descriptionDe,
            Price = price,
            ServingSize = servingSize,
            ImageUrl = imageUrl,
            Allergens = allergens,
            Ingredients = ingredients,
            Calories = calories,
            Protein = protein,
            Carbs = carbs,
            Fat = fat,
            Sugar = sugar,
            Salt = salt,
            SortOrder = sortOrder,
            IsVisible = true,
            IsAvailable = true,
            IsFeatured = featured
        };

    private static string Asset(string fileName) => $"{ShowcaseBasePath}/{fileName}";
}
