using System.Text;
using DigitalMenu.Application;
using DigitalMenu.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace DigitalMenu.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Database")));
        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.Password.RequiredLength = isDevelopment ? 5 : 8;
            o.Password.RequireDigit = !isDevelopment;
            o.Password.RequireUppercase = !isDevelopment;
            o.Password.RequireNonAlphanumeric = !isDevelopment;
            o.User.RequireUniqueEmail = true;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        var jwt = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? throw new InvalidOperationException("JWT configuration is missing.");
        if (jwt.Key.Length < 32) throw new InvalidOperationException("JWT key must contain at least 32 characters.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });
        services.AddAuthorization();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        services.AddScoped<IMenuManagementService, MenuManagementService>();
        services.AddScoped<IGlobalDrinkService, GlobalDrinkService>();
        services.AddScoped<IPublicMenuService, PublicMenuService>();
        services.AddScoped<IBillingService, BillingService>();
        return services;
    }
}

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        await SeedGlobalDrinksAsync(db);
        await NormalizeGlobalDrinkCategoriesAsync(db);
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { Roles.SuperAdmin, Roles.RestaurantOwner, Roles.RestaurantStaff })
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole<Guid>(role));
        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is not null) return;
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Administrator" };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        await users.AddToRoleAsync(user, Roles.SuperAdmin);
    }

    private static async Task SeedGlobalDrinksAsync(ApplicationDbContext db)
    {
        if (await db.GlobalDrinks.AnyAsync()) return;
        var drinks = new (string Name, string Slug, string Category, string? Description)[]
        {
            ("Coca-Cola", "coca-cola", "Bezalkoholna pića", "Gazirano bezalkoholno piće"),
            ("Coca-Cola Zero", "coca-cola-zero", "Bezalkoholna pića", "Gazirano bezalkoholno piće bez šećera"),
            ("Fanta", "fanta", "Bezalkoholna pića", "Gazirano bezalkoholno piće"),
            ("Sprite", "sprite", "Bezalkoholna pića", "Gazirano bezalkoholno piće"),
            ("Schweppes Tonic", "schweppes-tonic", "Bezalkoholna pića", "Tonic water"),
            ("Cedevita", "cedevita", "Bezalkoholna pića", "Vitaminski napitak"),
            ("Prirodna voda", "prirodna-voda", "Vode", "Negazirana voda"),
            ("Kisela voda", "kisela-voda", "Vode", "Gazirana voda"),
            ("Espresso", "espresso", "Topli napici", "Kratka kafa"),
            ("Americano", "americano", "Topli napici", "Produžena kafa"),
            ("Cappuccino", "cappuccino", "Topli napici", "Kafa s mlijekom"),
            ("Latte", "latte", "Topli napici", "Kafa s mlijekom"),
            ("Čaj", "caj", "Topli napici", "Topli čaj"),
            ("Red Bull", "red-bull", "Energetska pića", "Energetsko piće"),
            ("Heineken", "heineken", "Piva", "Pivo"),
            ("Tuborg", "tuborg", "Piva", "Pivo"),
            ("Sarajevsko", "sarajevsko", "Piva", "Pivo"),
            ("Bijelo vino", "bijelo-vino", "Vina", "Vino na čašu"),
            ("Crno vino", "crno-vino", "Vina", "Vino na čašu"),
        };
        db.GlobalDrinks.AddRange(drinks.Select((x, index) => new GlobalDrink
        {
            Name = x.Name,
            Slug = x.Slug,
            Category = x.Category,
            Description = x.Description,
            ImageUrl = "/menispot-mark.png",
            ServingOptions = DefaultServingOptions(x.Category),
            SortOrder = index + 1,
            IsActive = true
        }));
        await db.SaveChangesAsync();
    }

    private static async Task NormalizeGlobalDrinkCategoriesAsync(ApplicationDbContext db)
    {
        var drinks = await db.GlobalDrinks.ToListAsync();
        var changed = false;
        foreach (var drink in drinks)
        {
            var category = drink.Slug switch
            {
                "prirodna-voda-033" or "kisela-voda-033" or "prirodna-voda" or "kisela-voda" => "Vode",
                "espresso" or "americano" or "cappuccino" or "latte" or "caj" => "Topli napici",
                "heineken-033" or "tuborg-033" or "sarajevsko-033" or "heineken" or "tuborg" or "sarajevsko" => "Pivo",
                "bijelo-vino-01" or "bijelo-vino" => "Bijela vina",
                "crno-vino-01" or "crno-vino" => "Crna vina",
                _ when drink.Category is "Bezalkoholna piÄ‡a" or "Bezalkoholna pića" or "Energetska piÄ‡a" or "Energetska pića" => "Gazirana pića",
                _ => drink.Category
            };
            var normalizedName = NormalizeDrinkLibraryName(drink.Name);
            var normalizedSlug = NormalizeDrinkLibrarySlug(drink.Slug);
            var itemChanged = false;
            if (drink.Category != category)
            {
                drink.Category = category;
                itemChanged = true;
            }
            if (drink.Name != normalizedName)
            {
                drink.Name = normalizedName;
                itemChanged = true;
            }
            if (drink.Slug != normalizedSlug && !drinks.Any(x => x.Id != drink.Id && x.Slug == normalizedSlug))
            {
                drink.Slug = normalizedSlug;
                itemChanged = true;
            }
            if (itemChanged)
            {
                drink.UpdatedAt = DateTimeOffset.UtcNow;
                changed = true;
            }
        }
        foreach (var drink in drinks.Where(x => string.IsNullOrWhiteSpace(x.ServingOptions)))
        {
            drink.ServingOptions = DefaultServingOptions(drink.Category);
            drink.UpdatedAt = DateTimeOffset.UtcNow;
            changed = true;
        }
        if (changed) await db.SaveChangesAsync();
    }

    private static string DefaultServingOptions(string category) => category switch
    {
        "Vode" => "0.25l, 0.33l, 0.50l, 0.75l, 1.00l",
        "Gazirana pića" or "Negazirana pića" => "0.20l, 0.25l, 0.33l, 0.50l, 1.00l",
        "Cijeđeni sokovi" => "0.20l, 0.25l, 0.30l, 0.40l, 0.50l",
        "Topli napici" => "porcija",
        "Točeno pivo" => "0.20l, 0.25l, 0.30l, 0.33l, 0.40l, 0.50l, 1.00l",
        "Pivo" => "0.25l, 0.33l, 0.50l",
        "Alkoholni napici" => "0.03l, 0.04l, 0.05l",
        "Crna vina" or "Bijela vina" => "0.10l, 0.15l, 0.187l, 0.75l, 1.00l",
        _ => "porcija"
    };

    private static string NormalizeDrinkLibraryName(string name) =>
        name.Replace(" 0.25l", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" 0.33l", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" 0.1l", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

    private static string NormalizeDrinkLibrarySlug(string slug) => slug switch
    {
        "coca-cola-025" => "coca-cola",
        "coca-cola-zero-025" => "coca-cola-zero",
        "fanta-025" => "fanta",
        "sprite-025" => "sprite",
        "schweppes-tonic-025" => "schweppes-tonic",
        "prirodna-voda-033" => "prirodna-voda",
        "kisela-voda-033" => "kisela-voda",
        "red-bull-025" => "red-bull",
        "heineken-033" => "heineken",
        "tuborg-033" => "tuborg",
        "sarajevsko-033" => "sarajevsko",
        "bijelo-vino-01" => "bijelo-vino",
        "crno-vino-01" => "crno-vino",
        _ => slug
    };
}
