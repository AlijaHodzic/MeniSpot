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
            ("Coca-Cola 0.25l", "coca-cola-025", "Bezalkoholna pića", "Gazirano bezalkoholno piće"),
            ("Coca-Cola Zero 0.25l", "coca-cola-zero-025", "Bezalkoholna pića", "Gazirano bezalkoholno piće bez šećera"),
            ("Fanta 0.25l", "fanta-025", "Bezalkoholna pića", "Gazirano bezalkoholno piće"),
            ("Sprite 0.25l", "sprite-025", "Bezalkoholna pića", "Gazirano bezalkoholno piće"),
            ("Schweppes Tonic 0.25l", "schweppes-tonic-025", "Bezalkoholna pića", "Tonic water"),
            ("Cedevita", "cedevita", "Bezalkoholna pića", "Vitaminski napitak"),
            ("Prirodna voda 0.33l", "prirodna-voda-033", "Vode", "Negazirana voda"),
            ("Kisela voda 0.33l", "kisela-voda-033", "Vode", "Gazirana voda"),
            ("Espresso", "espresso", "Topli napici", "Kratka kafa"),
            ("Americano", "americano", "Topli napici", "Produžena kafa"),
            ("Cappuccino", "cappuccino", "Topli napici", "Kafa s mlijekom"),
            ("Latte", "latte", "Topli napici", "Kafa s mlijekom"),
            ("Čaj", "caj", "Topli napici", "Topli čaj"),
            ("Red Bull 0.25l", "red-bull-025", "Energetska pića", "Energetsko piće"),
            ("Heineken 0.33l", "heineken-033", "Piva", "Pivo"),
            ("Tuborg 0.33l", "tuborg-033", "Piva", "Pivo"),
            ("Sarajevsko 0.33l", "sarajevsko-033", "Piva", "Pivo"),
            ("Bijelo vino 0.1l", "bijelo-vino-01", "Vina", "Vino na čašu"),
            ("Crno vino 0.1l", "crno-vino-01", "Vina", "Vino na čašu"),
        };
        db.GlobalDrinks.AddRange(drinks.Select((x, index) => new GlobalDrink
        {
            Name = x.Name,
            Slug = x.Slug,
            Category = x.Category,
            Description = x.Description,
            ImageUrl = "/menispot-mark.png",
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
                "prirodna-voda-033" or "kisela-voda-033" => "Vode",
                "espresso" or "americano" or "cappuccino" or "latte" or "caj" => "Topli napici",
                "heineken-033" or "tuborg-033" or "sarajevsko-033" => "Pivo",
                "bijelo-vino-01" => "Bijela vina",
                "crno-vino-01" => "Crna vina",
                _ when drink.Category is "Bezalkoholna piÄ‡a" or "Bezalkoholna pića" or "Energetska piÄ‡a" or "Energetska pića" => "Gazirana pića",
                _ => drink.Category
            };
            if (drink.Category == category) continue;
            drink.Category = category;
            drink.UpdatedAt = DateTimeOffset.UtcNow;
            changed = true;
        }
        if (changed) await db.SaveChangesAsync();
    }
}
