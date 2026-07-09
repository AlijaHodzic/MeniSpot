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
        services.AddScoped<ISupportTicketService, SupportTicketService>();
        services.AddScoped<ILeadService, LeadService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
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
        await EnsureDemoMenuEnglishAsync(db);
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { Roles.SuperAdmin, Roles.RestaurantOwner, Roles.RestaurantStaff })
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole<Guid>(role));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await ShowcaseSeeder.SeedDelRioAsync(db, users);
        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        if (await users.FindByEmailAsync(email) is not null) return;
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Administrator" };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        await users.AddToRoleAsync(user, Roles.SuperAdmin);
    }

    private static async Task SeedGlobalDrinksAsync(ApplicationDbContext db)
    {
        var existing = await db.GlobalDrinks.ToDictionaryAsync(x => x.Slug);
        var sortOrder = existing.Values.Select(x => (int?)x.SortOrder).Max() ?? 0;
        var created = new List<GlobalDrink>();
        foreach (var x in PredefinedGlobalDrinks())
        {
            if (existing.ContainsKey(x.Slug)) continue;
            created.Add(new GlobalDrink
            {
                Name = x.Name,
                Slug = x.Slug,
                Category = x.Category,
                Description = x.Description,
                ImageUrl = "/menispot-mark.png",
                ServingOptions = DefaultServingOptions(x.Category),
                SortOrder = ++sortOrder,
                IsActive = true
            });
        }
        if (created.Count == 0) return;
        db.GlobalDrinks.AddRange(created);
        await db.SaveChangesAsync();
    }

    private static (string Name, string Slug, string Category, string? Description)[] PredefinedGlobalDrinks() =>
    [
        ("Coca-Cola Original", "coca-cola-original", "Gazirana pića", null),
        ("Coca-Cola Zero Sugar", "coca-cola-zero-sugar", "Gazirana pića", null),
        ("Fanta Orange", "fanta-orange", "Gazirana pića", null),
        ("Fanta Shokata", "fanta-shokata", "Gazirana pića", null),
        ("Sprite", "sprite", "Gazirana pića", null),
        ("Schweppes Tonic Water", "schweppes-tonic-water", "Gazirana pića", null),
        ("Schweppes Bitter Lemon", "schweppes-bitter-lemon", "Gazirana pića", null),
        ("Schweppes Tangerine", "schweppes-tangerine", "Gazirana pića", null),
        ("Schweppes Pink Tonic", "schweppes-pink-tonic", "Gazirana pića", null),
        ("Schweppes Mojito", "schweppes-mojito", "Gazirana pića", null),
        ("Cockta Original", "cockta-original", "Gazirana pića", null),
        ("Cockta Free", "cockta-free", "Gazirana pića", null),
        ("Pepsi", "pepsi", "Gazirana pića", null),
        ("Pepsi Max", "pepsi-max", "Gazirana pića", null),
        ("Mirinda Orange", "mirinda-orange", "Gazirana pića", null),
        ("7Up", "7up", "Gazirana pića", null),
        ("Orangina Original", "orangina-original", "Gazirana pića", null),
        ("Sky Cola", "sky-cola", "Gazirana pića", null),
        ("Sky Cola Zero", "sky-cola-zero", "Gazirana pića", null),
        ("Sensation Limeta-Kiwano", "sensation-limeta-kiwano", "Gazirana pića", null),
        ("Sensation Bazga-Limun", "sensation-bazga-limun", "Gazirana pića", null),
        ("Sanpellegrino Limonata", "sanpellegrino-limonata", "Gazirana pića", null),
        ("Sanpellegrino Aranciata", "sanpellegrino-aranciata", "Gazirana pića", null),
        ("Sanpellegrino Aranciata Rossa", "sanpellegrino-aranciata-rossa", "Gazirana pića", null),
        ("Sanpellegrino Pompelmo", "sanpellegrino-pompelmo", "Gazirana pića", null),
        ("Traubisoda", "traubisoda", "Gazirana pića", null),
        ("Spezi", "spezi", "Gazirana pića", null),

        ("Cappy Narandža", "cappy-narandza", "Negazirana pića", null),
        ("Cappy Jabuka", "cappy-jabuka", "Negazirana pića", null),
        ("Cappy Breskva", "cappy-breskva", "Negazirana pića", null),
        ("Cappy Jagoda", "cappy-jagoda", "Negazirana pića", null),
        ("Cappy Višnja", "cappy-visnja", "Negazirana pića", null),
        ("Cappy Crna ribizla", "cappy-crna-ribizla", "Negazirana pića", null),
        ("Cappy Multivitamin", "cappy-multivitamin", "Negazirana pića", null),
        ("Fructal Jabuka", "fructal-jabuka", "Negazirana pića", null),
        ("Fructal Narandža", "fructal-narandza", "Negazirana pića", null),
        ("Fructal Breskva", "fructal-breskva", "Negazirana pića", null),
        ("Fructal Jagoda", "fructal-jagoda", "Negazirana pića", null),
        ("Fructal Crna ribizla", "fructal-crna-ribizla", "Negazirana pića", null),
        ("Fructal Borovnica", "fructal-borovnica", "Negazirana pića", null),
        ("Fructal Višnja", "fructal-visnja", "Negazirana pića", null),
        ("Fructal Marelica", "fructal-marelica", "Negazirana pića", null),
        ("Fructal Multivitamin", "fructal-multivitamin", "Negazirana pića", null),
        ("Juicy Narandža", "juicy-narandza", "Negazirana pića", null),
        ("Juicy Jabuka", "juicy-jabuka", "Negazirana pića", null),
        ("Juicy Breskva", "juicy-breskva", "Negazirana pića", null),
        ("Juicy Jagoda", "juicy-jagoda", "Negazirana pića", null),
        ("Juicy Crna ribizla", "juicy-crna-ribizla", "Negazirana pića", null),
        ("Juicy Multivitamin", "juicy-multivitamin", "Negazirana pića", null),
        ("Cedevita Narandža", "cedevita-narandza", "Negazirana pića", null),
        ("Cedevita Limun", "cedevita-limun", "Negazirana pića", null),
        ("Cedevita Limeta", "cedevita-limeta", "Negazirana pića", null),
        ("Cedevita Grejp", "cedevita-grejp", "Negazirana pića", null),
        ("Cedevita Bazga-Limun", "cedevita-bazga-limun", "Negazirana pića", null),
        ("Pago Narandža", "pago-narandza", "Negazirana pića", null),
        ("Pago Jabuka", "pago-jabuka", "Negazirana pića", null),
        ("Pago Breskva", "pago-breskva", "Negazirana pića", null),
        ("Pago Jagoda", "pago-jagoda", "Negazirana pića", null),
        ("Pago Borovnica", "pago-borovnica", "Negazirana pića", null),
        ("Pago Ananas", "pago-ananas", "Negazirana pića", null),
        ("Bravo Narandža", "bravo-narandza", "Negazirana pića", null),
        ("Bravo Jabuka", "bravo-jabuka", "Negazirana pića", null),
        ("Dvojni C", "dvojni-c", "Negazirana pića", null),
        ("Ledeni čaj Breskva", "ledeni-caj-breskva", "Negazirana pića", null),
        ("Ledeni čaj Limun", "ledeni-caj-limun", "Negazirana pića", null),
        ("Ledeni čaj Šumsko voće", "ledeni-caj-sumsko-voce", "Negazirana pića", null),
        ("Nestea Breskva", "nestea-breskva", "Negazirana pića", null),
        ("Nestea Limun", "nestea-limun", "Negazirana pića", null),
        ("Fuzetea Breskva", "fuzetea-breskva", "Negazirana pića", null),
        ("Fuzetea Limun", "fuzetea-limun", "Negazirana pića", null),
        ("Fuzetea Šumsko voće", "fuzetea-sumsko-voce", "Negazirana pića", null),

        ("Cijeđena narandža", "cijedena-narandza", "Cijeđeni sokovi", null),
        ("Cijeđeni limun", "cijedeni-limun", "Cijeđeni sokovi", null),
        ("Limunada", "limunada", "Cijeđeni sokovi", null),
        ("Limunada s mentom", "limunada-s-mentom", "Cijeđeni sokovi", null),
        ("Cijeđeni grejp", "cijedeni-grejp", "Cijeđeni sokovi", null),
        ("Cijeđena jabuka", "cijedena-jabuka", "Cijeđeni sokovi", null),
        ("Cijeđena mrkva", "cijedena-mrkva", "Cijeđeni sokovi", null),
        ("Narandža i limun", "narandza-i-limun", "Cijeđeni sokovi", null),
        ("Narandža i grejp", "narandza-i-grejp", "Cijeđeni sokovi", null),
        ("Narandža, jabuka i mrkva", "narandza-jabuka-i-mrkva", "Cijeđeni sokovi", null),
        ("Limunada s đumbirom", "limunada-s-dumbirom", "Cijeđeni sokovi", null),
        ("Limunada s bazgom", "limunada-s-bazgom", "Cijeđeni sokovi", null),
        ("Sezonski cijeđeni sok", "sezonski-cijedeni-sok", "Cijeđeni sokovi", null),

        ("Red Bull Original", "red-bull-original", "Energetska pića", null),
        ("Red Bull Sugarfree", "red-bull-sugarfree", "Energetska pića", null),
        ("Red Bull Red Edition", "red-bull-red-edition", "Energetska pića", null),
        ("Red Bull Yellow Edition", "red-bull-yellow-edition", "Energetska pića", null),
        ("Red Bull White Edition", "red-bull-white-edition", "Energetska pića", null),
        ("Burn Original", "burn-original", "Energetska pića", null),
        ("Burn Zero", "burn-zero", "Energetska pića", null),
        ("Monster Energy Original", "monster-energy-original", "Energetska pića", null),
        ("Monster Ultra White", "monster-ultra-white", "Energetska pića", null),
        ("Monster Mango Loco", "monster-mango-loco", "Energetska pića", null),
        ("Hell Classic", "hell-classic", "Energetska pića", null),
        ("Hell Strong Focus", "hell-strong-focus", "Energetska pića", null),
        ("Hell Zero", "hell-zero", "Energetska pića", null),
        ("Guarana Original", "guarana-original", "Energetska pića", null),
        ("Guarana No Sleep", "guarana-no-sleep", "Energetska pića", null),
        ("Fast Energy", "fast-energy", "Energetska pića", null),
        ("Eisberg Energy", "eisberg-energy", "Energetska pića", null),

        ("Kafa", "kafa", "Topli napici", null),
        ("Espresso", "espresso", "Topli napici", null),
        ("Dupli espresso", "dupli-espresso", "Topli napici", null),
        ("Produženi espresso", "produzeni-espresso", "Topli napici", null),
        ("Espresso s mlijekom", "espresso-s-mlijekom", "Topli napici", null),
        ("Velika kafa s mlijekom", "velika-kafa-s-mlijekom", "Topli napici", null),
        ("Macchiato mali", "macchiato-mali", "Topli napici", null),
        ("Macchiato veliki", "macchiato-veliki", "Topli napici", null),
        ("Cappuccino", "cappuccino", "Topli napici", null),
        ("Caffe latte", "caffe-latte", "Topli napici", null),
        ("Latte macchiato", "latte-macchiato", "Topli napici", null),
        ("Americano", "americano", "Topli napici", null),
        ("Flat white", "flat-white", "Topli napici", null),
        ("Mocha", "mocha", "Topli napici", null),
        ("Bosanska kafa", "bosanska-kafa", "Topli napici", null),
        ("Turska kafa", "turska-kafa", "Topli napici", null),
        ("Kafa bez kofeina", "kafa-bez-kofeina", "Topli napici", null),
        ("Ledena kafa", "ledena-kafa", "Topli napici", null),
        ("Nescafe Classic", "nescafe-classic", "Topli napici", null),
        ("Nescafe s mlijekom", "nescafe-s-mlijekom", "Topli napici", null),
        ("Nescafe 3u1", "nescafe-3u1", "Topli napici", null),
        ("Nescafe Vanilija", "nescafe-vanilija", "Topli napici", null),
        ("Nescafe Čokolada", "nescafe-cokolada", "Topli napici", null),
        ("Nescafe Irish", "nescafe-irish", "Topli napici", null),
        ("Bijela kafa", "bijela-kafa", "Topli napici", null),
        ("Čaj od kamilice", "caj-od-kamilice", "Topli napici", null),
        ("Čaj od mente", "caj-od-mente", "Topli napici", null),
        ("Zeleni čaj", "zeleni-caj", "Topli napici", null),
        ("Crni čaj", "crni-caj", "Topli napici", null),
        ("Voćni čaj", "vocni-caj", "Topli napici", null),
        ("Čaj od šipka", "caj-od-sipka", "Topli napici", null),
        ("Čaj od đumbira", "caj-od-dumbira", "Topli napici", null),
        ("Čaj limun-đumbir", "caj-limun-dumbir", "Topli napici", null),
        ("Čaj od brusnice", "caj-od-brusnice", "Topli napici", null),
        ("Čaj od hibiskusa", "caj-od-hibiskusa", "Topli napici", null),
        ("Topla čokolada", "topla-cokolada", "Topli napici", null),
        ("Bijela topla čokolada", "bijela-topla-cokolada", "Topli napici", null),
        ("Kakao", "kakao", "Topli napici", null),
        ("Kuhano mlijeko", "kuhano-mlijeko", "Topli napici", null),
        ("Med dodatak", "med-dodatak", "Topli napici", null),
        ("Limun dodatak", "limun-dodatak", "Topli napici", null),
        ("Mlijeko dodatak", "mlijeko-dodatak", "Topli napici", null),
        ("Biljno mlijeko dodatak", "biljno-mlijeko-dodatak", "Topli napici", null),

        ("Olimpija", "olimpija", "Vode", null),
        ("Jana", "jana", "Vode", null),
        ("Oaza", "oaza", "Vode", null),
        ("Leda", "leda", "Vode", null),
        ("Prolom voda", "prolom-voda", "Vode", null),
        ("Rosa", "rosa", "Vode", null),
        ("Vivia", "vivia", "Vode", null),
        ("Aqua Viva", "aqua-viva", "Vode", null),
        ("Studena", "studena", "Vode", null),
        ("Evian", "evian", "Vode", null),
        ("Acqua Panna", "acqua-panna", "Vode", null),
        ("Sarajevski kiseljak", "sarajevski-kiseljak", "Vode", null),
        ("Knjaz Miloš", "knjaz-milos", "Vode", null),
        ("Jamnica", "jamnica", "Vode", null),
        ("Radenska", "radenska", "Vode", null),
        ("Donat Mg", "donat-mg", "Vode", null),
        ("San Pellegrino", "san-pellegrino", "Vode", null),
        ("Romerquelle gazirana", "romerquelle-gazirana", "Vode", null),
        ("Minaqua", "minaqua", "Vode", null),
        ("Vitinka", "vitinka", "Vode", null),
        ("Jana Vitamin Limun", "jana-vitamin-limun", "Vode", null),
        ("Jana Vitamin Narandža", "jana-vitamin-narandza", "Vode", null),
        ("Jana Vitamin Immuno", "jana-vitamin-immuno", "Vode", null),
        ("Romerquelle Lemongrass", "romerquelle-lemongrass", "Vode", null),
        ("Romerquelle Kupina-Limeta", "romerquelle-kupina-limeta", "Vode", null),

        ("Sarajevsko točeno", "sarajevsko-toceno", "Točeno pivo", null),
        ("Ožujsko točeno", "ozujsko-toceno", "Točeno pivo", null),
        ("Karlovačko točeno", "karlovacko-toceno", "Točeno pivo", null),
        ("Preminger točeno", "preminger-toceno", "Točeno pivo", null),
        ("Nektar točeno", "nektar-toceno", "Točeno pivo", null),
        ("Jelen točeno", "jelen-toceno", "Točeno pivo", null),
        ("Nikšićko točeno", "niksicko-toceno", "Točeno pivo", null),
        ("Staropramen točeno", "staropramen-toceno", "Točeno pivo", null),
        ("Stella Artois točeno", "stella-artois-toceno", "Točeno pivo", null),
        ("Becks točeno", "becks-toceno", "Točeno pivo", null),
        ("Heineken točeno", "heineken-toceno", "Točeno pivo", null),
        ("Tuborg točeno", "tuborg-toceno", "Točeno pivo", null),
        ("Carlsberg točeno", "carlsberg-toceno", "Točeno pivo", null),
        ("Hoegaarden točeno", "hoegaarden-toceno", "Točeno pivo", null),
        ("Leffe Blonde točeno", "leffe-blonde-toceno", "Točeno pivo", null),
        ("Guinness točeno", "guinness-toceno", "Točeno pivo", null),

        ("Sarajevsko Premium", "sarajevsko-premium", "Pivo", null),
        ("Sarajevsko Nefiltrirano", "sarajevsko-nefiltrirano", "Pivo", null),
        ("Preminger", "preminger", "Pivo", null),
        ("Nektar", "nektar", "Pivo", null),
        ("Ožujsko", "ozujsko", "Pivo", null),
        ("Karlovačko", "karlovacko", "Pivo", null),
        ("Pan", "pan", "Pivo", null),
        ("Jelen", "jelen", "Pivo", null),
        ("Lav", "lav", "Pivo", null),
        ("Nikšićko", "niksicko", "Pivo", null),
        ("Zaječarsko", "zajecarsko", "Pivo", null),
        ("Union", "union", "Pivo", null),
        ("Laško Zlatorog", "lasko-zlatorog", "Pivo", null),
        ("Heineken", "heineken", "Pivo", null),
        ("Heineken 0.0", "heineken-0-0", "Pivo", null),
        ("Stella Artois", "stella-artois", "Pivo", null),
        ("Becks", "becks", "Pivo", null),
        ("Tuborg Green", "tuborg-green", "Pivo", null),
        ("Carlsberg", "carlsberg", "Pivo", null),
        ("Corona Extra", "corona-extra", "Pivo", null),
        ("Budweiser Budvar", "budweiser-budvar", "Pivo", null),
        ("Bavaria", "bavaria", "Pivo", null),
        ("Bavaria 0.0", "bavaria-0-0", "Pivo", null),
        ("Staropramen", "staropramen", "Pivo", null),
        ("Guinness", "guinness", "Pivo", null),
        ("Hoegaarden", "hoegaarden", "Pivo", null),
        ("Leffe Blonde", "leffe-blonde", "Pivo", null),
        ("Erdinger Weissbier", "erdinger-weissbier", "Pivo", null),
        ("Paulaner Weissbier", "paulaner-weissbier", "Pivo", null),
        ("Ožujsko Limun", "ozujsko-limun", "Pivo", null),
        ("Karlovačko Radler", "karlovacko-radler", "Pivo", null),
        ("Jelen Fresh", "jelen-fresh", "Pivo", null),
        ("Somersby Apple", "somersby-apple", "Pivo", null),
        ("Somersby Pear", "somersby-pear", "Pivo", null),
        ("Somersby Blueberry", "somersby-blueberry", "Pivo", null),
        ("Somersby Mango & Lime", "somersby-mango-lime", "Pivo", null),

        ("Smirnoff Red", "smirnoff-red", "Alkoholni napici", null),
        ("Absolut Vodka", "absolut-vodka", "Alkoholni napici", null),
        ("Finlandia", "finlandia", "Alkoholni napici", null),
        ("Grey Goose", "grey-goose", "Alkoholni napici", null),
        ("Belvedere", "belvedere", "Alkoholni napici", null),
        ("Skyy Vodka", "skyy-vodka", "Alkoholni napici", null),
        ("Ciroc", "ciroc", "Alkoholni napici", null),
        ("Gordons", "gordons", "Alkoholni napici", null),
        ("Gordons Pink", "gordons-pink", "Alkoholni napici", null),
        ("Beefeater", "beefeater", "Alkoholni napici", null),
        ("Bombay Sapphire", "bombay-sapphire", "Alkoholni napici", null),
        ("Tanqueray", "tanqueray", "Alkoholni napici", null),
        ("Hendricks", "hendricks", "Alkoholni napici", null),
        ("Bulldog Gin", "bulldog-gin", "Alkoholni napici", null),
        ("Monkey 47", "monkey-47", "Alkoholni napici", null),
        ("Sierra Silver", "sierra-silver", "Alkoholni napici", null),
        ("Sierra Gold", "sierra-gold", "Alkoholni napici", null),
        ("Olmeca Blanco", "olmeca-blanco", "Alkoholni napici", null),
        ("Olmeca Gold", "olmeca-gold", "Alkoholni napici", null),
        ("Jose Cuervo Especial Silver", "jose-cuervo-especial-silver", "Alkoholni napici", null),
        ("Jose Cuervo Especial Gold", "jose-cuervo-especial-gold", "Alkoholni napici", null),
        ("Patron Silver", "patron-silver", "Alkoholni napici", null),
        ("Bacardi Carta Blanca", "bacardi-carta-blanca", "Alkoholni napici", null),
        ("Bacardi Carta Negra", "bacardi-carta-negra", "Alkoholni napici", null),
        ("Havana Club 3 Anos", "havana-club-3-anos", "Alkoholni napici", null),
        ("Havana Club 7 Anos", "havana-club-7-anos", "Alkoholni napici", null),
        ("Captain Morgan Spiced Gold", "captain-morgan-spiced-gold", "Alkoholni napici", null),
        ("Captain Morgan Dark", "captain-morgan-dark", "Alkoholni napici", null),
        ("Diplomatico Reserva", "diplomatico-reserva", "Alkoholni napici", null),
        ("Zacapa 23", "zacapa-23", "Alkoholni napici", null),
        ("Johnnie Walker Red Label", "johnnie-walker-red-label", "Alkoholni napici", null),
        ("Johnnie Walker Black Label", "johnnie-walker-black-label", "Alkoholni napici", null),
        ("Jack Daniels", "jack-daniels", "Alkoholni napici", null),
        ("Jack Daniels Honey", "jack-daniels-honey", "Alkoholni napici", null),
        ("Jameson", "jameson", "Alkoholni napici", null),
        ("Ballantines Finest", "ballantines-finest", "Alkoholni napici", null),
        ("Chivas Regal 12", "chivas-regal-12", "Alkoholni napici", null),
        ("Jim Beam", "jim-beam", "Alkoholni napici", null),
        ("Four Roses", "four-roses", "Alkoholni napici", null),
        ("Glenfiddich 12", "glenfiddich-12", "Alkoholni napici", null),
        ("The Glenlivet 12", "the-glenlivet-12", "Alkoholni napici", null),
        ("Monkey Shoulder", "monkey-shoulder", "Alkoholni napici", null),
        ("Stock 84", "stock-84", "Alkoholni napici", null),
        ("Vecchia Romagna", "vecchia-romagna", "Alkoholni napici", null),
        ("Hennessy VS", "hennessy-vs", "Alkoholni napici", null),
        ("Hennessy VSOP", "hennessy-vsop", "Alkoholni napici", null),
        ("Remy Martin VSOP", "remy-martin-vsop", "Alkoholni napici", null),
        ("Martell VS", "martell-vs", "Alkoholni napici", null),

        ("Šljivovica", "sljivovica", "Rakije", null),
        ("Viljamovka", "viljamovka", "Rakije", null),
        ("Kruškovača", "kruskovaca", "Rakije", null),
        ("Dunjevača", "dunjevaca", "Rakije", null),
        ("Kajsijevača", "kajsijevaca", "Rakije", null),
        ("Jabukovača", "jabukovaca", "Rakije", null),
        ("Lozovača", "lozovaca", "Rakije", null),
        ("Travarica", "travarica", "Rakije", null),
        ("Komovica", "komovica", "Rakije", null),
        ("Medovača", "medovaca", "Rakije", null),
        ("Orahovača", "orahovaca", "Rakije", null),
        ("Višnjevača", "visnjevaca", "Rakije", null),
        ("Malinovača", "malinovaca", "Rakije", null),

        ("Jagermeister", "jagermeister", "Likeri i aperitivi", null),
        ("Pelinkovac Gorki", "pelinkovac-gorki", "Likeri i aperitivi", null),
        ("Antique Pelinkovac", "antique-pelinkovac", "Likeri i aperitivi", null),
        ("Vlahov", "vlahov", "Likeri i aperitivi", null),
        ("Gorki List", "gorki-list", "Likeri i aperitivi", null),
        ("Ramazzotti", "ramazzotti", "Likeri i aperitivi", null),
        ("Amaro Montenegro", "amaro-montenegro", "Likeri i aperitivi", null),
        ("Fernet-Branca", "fernet-branca", "Likeri i aperitivi", null),
        ("Baileys", "baileys", "Likeri i aperitivi", null),
        ("Malibu", "malibu", "Likeri i aperitivi", null),
        ("Kahlua", "kahlua", "Likeri i aperitivi", null),
        ("Amaretto Disaronno", "amaretto-disaronno", "Likeri i aperitivi", null),
        ("Aperol", "aperol", "Likeri i aperitivi", null),
        ("Campari", "campari", "Likeri i aperitivi", null),
        ("Martini Bianco", "martini-bianco", "Likeri i aperitivi", null),
        ("Martini Rosso", "martini-rosso", "Likeri i aperitivi", null),
        ("Martini Extra Dry", "martini-extra-dry", "Likeri i aperitivi", null),
        ("Southern Comfort", "southern-comfort", "Likeri i aperitivi", null),
        ("Underberg", "underberg", "Likeri i aperitivi", null),

        ("Crno vino kuće", "crno-vino-kuce", "Crna vina", null),
        ("Vranac", "vranac", "Crna vina", null),
        ("Blatina", "blatina", "Crna vina", null),
        ("Merlot", "merlot", "Crna vina", null),
        ("Cabernet Sauvignon", "cabernet-sauvignon", "Crna vina", null),
        ("Pinot Noir", "pinot-noir", "Crna vina", null),
        ("Syrah", "syrah", "Crna vina", null),
        ("Plavac Mali", "plavac-mali", "Crna vina", null),
        ("Prokupac", "prokupac", "Crna vina", null),
        ("Cuvee crno", "cuvee-crno", "Crna vina", null),
        ("Bijelo vino kuće", "bijelo-vino-kuce", "Bijela vina", null),
        ("Žilavka", "zilavka", "Bijela vina", null),
        ("Chardonnay", "chardonnay", "Bijela vina", null),
        ("Sauvignon Blanc", "sauvignon-blanc", "Bijela vina", null),
        ("Graševina", "grasevina", "Bijela vina", null),
        ("Malvazija", "malvazija", "Bijela vina", null),
        ("Pinot Grigio", "pinot-grigio", "Bijela vina", null),
        ("Riesling", "riesling", "Bijela vina", null),
        ("Traminac", "traminac", "Bijela vina", null),
        ("Muscat", "muscat", "Bijela vina", null),
        ("Cuvee bijelo", "cuvee-bijelo", "Bijela vina", null),
        ("Rose vino kuće", "rose-vino-kuce", "Rosé vina", null),
        ("Vranac Rose", "vranac-rose", "Rosé vina", null),
        ("Blatina Rose", "blatina-rose", "Rosé vina", null),
        ("Cabernet Sauvignon Rose", "cabernet-sauvignon-rose", "Rosé vina", null),
        ("Pinot Noir Rose", "pinot-noir-rose", "Rosé vina", null),
        ("Provence Rose", "provence-rose", "Rosé vina", null),
    ];

    private static async Task NormalizeGlobalDrinkCategoriesAsync(ApplicationDbContext db)
    {
        var drinks = await db.GlobalDrinks.ToListAsync();
        var changed = false;
        foreach (var drink in drinks)
        {
            var category = drink.Slug switch
            {
                "prirodna-voda-033" or "kisela-voda-033" or "prirodna-voda" or "kisela-voda" => "Vode",
                "cedevita" => "Negazirana pića",
                "red-bull" => "Energetska pića",
                "espresso" or "americano" or "cappuccino" or "latte" or "caj" => "Topli napici",
                "heineken-033" or "tuborg-033" or "sarajevsko-033" or "heineken" or "tuborg" or "sarajevsko" => "Pivo",
                "bijelo-vino-01" or "bijelo-vino" => "Bijela vina",
                "crno-vino-01" or "crno-vino" => "Crna vina",
                _ when drink.Category is "Bezalkoholna pića" => "Gazirana pića",
                _ when drink.Category is "Piva" => "Pivo",
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
            if ((category is "Crna vina" or "Bijela vina" or "Rosé vina") && IsDefaultGlassDescription(drink.Description))
            {
                drink.Description = null;
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
        "Energetska pića" => "0.25l, 0.33l, 0.50l",
        "Topli napici" => "porcija",
        "Točeno pivo" => "0.20l, 0.25l, 0.30l, 0.33l, 0.40l, 0.50l, 1.00l",
        "Pivo" => "0.25l, 0.33l, 0.50l",
        "Alkoholni napici" or "Rakije" or "Likeri i aperitivi" => "0.03l, 0.04l, 0.05l",
        "Crna vina" or "Bijela vina" or "Rosé vina" => "0.10l, 0.15l, 0.187l, 0.75l, 1.00l",
        _ => "porcija"
    };

    private static string NormalizeDrinkLibraryName(string name) =>
        name.Replace(" 0.25l", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" 0.33l", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" 0.1l", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

    private static bool IsDefaultGlassDescription(string? value) =>
        string.Equals(value?.Trim(), "Vino na čašu", StringComparison.OrdinalIgnoreCase);

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

    private static async Task EnsureDemoMenuEnglishAsync(ApplicationDbContext db)
    {
        var restaurant = await db.Restaurants
            .Include(x => x.Categories).ThenInclude(x => x.Items).ThenInclude(x => x.GlobalDrink)
            .Include(x => x.SpecialOffers)
            .FirstOrDefaultAsync(x => x.Slug == "test");
        if (restaurant is null) return;

        var changed = false;
        var enabledLanguages = restaurant.EnabledLanguages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        enabledLanguages.Add("bs");
        enabledLanguages.Add("en");
        var normalizedLanguages = string.Join(',', new[] { "bs", "en", "de" }.Where(enabledLanguages.Contains));
        if (restaurant.EnabledLanguages != normalizedLanguages)
        {
            restaurant.EnabledLanguages = normalizedLanguages;
            changed = true;
        }

        foreach (var category in restaurant.Categories)
        {
            if (ShouldReplaceDemoEnglish(category.NameEn, category.Name))
            {
                category.NameEn = TranslateDemoCategory(category.Name);
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(category.Description) && ShouldReplaceDemoEnglish(category.DescriptionEn, category.Description))
            {
                category.DescriptionEn = TranslateDemoText(category.Description);
                changed = true;
            }

            foreach (var item in category.Items)
            {
                if (ShouldReplaceDemoEnglish(item.NameEn, item.Name))
                {
                    item.NameEn = item.GlobalDrink?.Name ?? TranslateDemoItemName(item.Name);
                    changed = true;
                }
                if (!string.IsNullOrWhiteSpace(item.Description) && ShouldReplaceDemoEnglish(item.DescriptionEn, item.Description))
                {
                    item.DescriptionEn = item.GlobalDrink?.Description ?? TranslateDemoText(item.Description);
                    changed = true;
                }
            }
        }

        foreach (var offer in restaurant.SpecialOffers)
        {
            if (ShouldReplaceDemoEnglish(offer.TitleEn, offer.Title))
            {
                offer.TitleEn = TranslateDemoItemName(offer.Title);
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(offer.Description) && ShouldReplaceDemoEnglish(offer.DescriptionEn, offer.Description))
            {
                offer.DescriptionEn = TranslateDemoText(offer.Description);
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(offer.Items) && ShouldReplaceDemoEnglish(offer.ItemsEn, offer.Items))
            {
                offer.ItemsEn = string.Join('\n', offer.Items.Split('\n').Select(TranslateDemoItemName));
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync();
    }

    private static string TranslateDemoCategory(string value)
    {
        var key = NormalizeDemoTranslationKey(value);
        return key switch
        {
            "hrana" => "Food",
            "pica" => "Drinks",
            "predjela" => "Starters",
            "glavna-jela" => "Main Dishes",
            "jela-sa-rostilja" or "rostilj" or "grill" => "Grill",
            "pizza" or "pizze" => "Pizza",
            "burgeri" => "Burgers",
            "paste" or "tjestenine" => "Pasta",
            "salate" => "Salads",
            "supe" or "juhe" => "Soups",
            "deserti" => "Desserts",
            "topli-napici" => "Hot Drinks",
            "gazirana-pica" => "Soft Drinks",
            "negazirana-pica" => "Still Drinks",
            "cijedeni-sokovi" => "Fresh Juices",
            "energetska-pica" => "Energy Drinks",
            "vode" => "Water",
            "pivo" or "piva" => "Beer",
            "toceno-pivo" => "Draft Beer",
            "alkoholni-napici" => "Spirits",
            "rakije" => "Fruit Brandies",
            "likeri-i-aperitivi" => "Liqueurs and Aperitifs",
            "crna-vina" => "Red Wines",
            "bijela-vina" => "White Wines",
            "rose-vina" => "Rose Wines",
            _ => TranslateDemoText(value)
        };
    }

    private static string TranslateDemoItemName(string value)
    {
        var key = NormalizeDemoTranslationKey(value);
        return key switch
        {
            "bosanski-lonac" => "Bosnian pot stew",
            "omlet" => "Omelette",
            "grah" => "Bean stew",
            "grah-sa-teletinom" => "Bean stew with veal",
            "cevapi" or "cevapcici" => "Cevapi",
            "pljeskavica" => "Grilled burger patty",
            "pileci-file" => "Chicken fillet",
            "teleci-medaljoni" => "Veal medallions",
            "begova-corba" => "Bey's soup",
            "tarhana" => "Tarhana soup",
            "mijesano-meso" => "Mixed grill",
            "piletina" => "Chicken",
            "teletina" => "Veal",
            "riba" => "Fish",
            "salata" => "Salad",
            "sopska-salata" => "Shopska salad",
            "palacinke" => "Pancakes",
            "baklava" => "Baklava",
            "tufahija" => "Stuffed apple dessert",
            _ => TranslateDemoText(value)
        };
    }

    private static string TranslateDemoText(string value) =>
        value.Trim()
            .Replace("Ponuda:", "Selection:", StringComparison.OrdinalIgnoreCase)
            .Replace("Grah sa teletinom", "Bean stew with veal", StringComparison.OrdinalIgnoreCase)
            .Replace("Bosanski lonac", "Bosnian pot stew", StringComparison.OrdinalIgnoreCase)
            .Replace("domaće", "homemade", StringComparison.OrdinalIgnoreCase)
            .Replace("domaći", "homemade", StringComparison.OrdinalIgnoreCase)
            .Replace("svježe", "fresh", StringComparison.OrdinalIgnoreCase)
            .Replace("svježi", "fresh", StringComparison.OrdinalIgnoreCase)
            .Replace("telećim", "veal", StringComparison.OrdinalIgnoreCase)
            .Replace("teletinom", "veal", StringComparison.OrdinalIgnoreCase)
            .Replace("porcija", "serving", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldReplaceDemoEnglish(string? current, string source)
    {
        if (string.IsNullOrWhiteSpace(current)) return true;
        var trimmed = current.Trim();
        return string.Equals(trimmed, source.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, TranslateDemoText(source), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDemoTranslationKey(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Replace("č", "c").Replace("ć", "c").Replace("š", "s").Replace("ž", "z").Replace("đ", "d");
        return System.Text.RegularExpressions.Regex.Replace(normalized, @"[^a-z0-9]+", "-").Trim('-');
    }
}
