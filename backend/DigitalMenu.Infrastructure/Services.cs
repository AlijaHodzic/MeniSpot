using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using DigitalMenu.Application;
using DigitalMenu.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DigitalMenu.Infrastructure;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}

internal static class AssetUrl
{
    public static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return null;
        if (trimmed.StartsWith('/')) return trimmed;
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.AbsolutePath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            ? uri.PathAndQuery
            : trimmed;
    }
}

public sealed class AuthService(UserManager<ApplicationUser> users, ApplicationDbContext db, IOptions<JwtOptions> options) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || !await users.CheckPasswordAsync(user, request.Password)) return null;
        return await CreateSessionAsync(user);
    }

    public async Task<LoginResponse?> ImpersonateRestaurantOwnerAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var owner = await (from user in db.Users
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where user.RestaurantId == restaurantId && role.Name == Roles.RestaurantOwner
            select user).FirstOrDefaultAsync(cancellationToken);
        return owner is null ? null : await CreateSessionAsync(owner);
    }

    public async Task<ChangePasswordResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return new ChangePasswordResult(false, new[] { "User was not found." });

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        return new ChangePasswordResult(result.Succeeded, result.Errors.Select(x => x.Description).ToArray());
    }

    private async Task<LoginResponse> CreateSessionAsync(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(options.Value.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(ClaimTypes.Role, role)
        };
        if (user.RestaurantId is not null) claims.Add(new("restaurant_id", user.RestaurantId.Value.ToString()));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Value.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(options.Value.Issuer, options.Value.Audience, claims, now.UtcDateTime, expires.UtcDateTime, credentials);
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, role, user.RestaurantId);
    }
}

public sealed class RestaurantService(ApplicationDbContext db, UserManager<ApplicationUser> users) : IRestaurantService
{
    public async Task<IReadOnlyList<RestaurantSummary>> GetAllAsync(CancellationToken ct)
    {
        var rows = await db.Restaurants.AsNoTracking()
        .Where(x => x.Subscription != null && x.Status != RestaurantStatus.Archived).OrderBy(x => x.Name)
        .Select(x => new { x.Id, x.Name, x.Slug, x.Type, x.LogoUrl, x.Address, x.Status, x.Subscription!.Plan, SubscriptionStatus = x.Subscription.Status, x.Subscription.ExpiresOn }).ToListAsync(ct);
        return rows.Select(x => new RestaurantSummary(x.Id, x.Name, x.Slug, x.Type, AssetUrl.Normalize(x.LogoUrl), x.Address, x.Status, NormalizePlan(x.Plan), x.SubscriptionStatus, x.ExpiresOn)).ToList();
    }

    public async Task<AdminDashboardSummary> GetDashboardAsync(CancellationToken ct)
    {
        var rows = await db.Restaurants.AsNoTracking().Where(x => x.Subscription != null && x.Status != RestaurantStatus.Archived).Select(x => new
        {
            x.Id, x.Name, x.Type, x.Status, x.CreatedAt, x.UpdatedAt,
            x.LogoUrl, x.CoverImageUrl,
            ProductCount = x.Categories.SelectMany(c => c.Items).Count(),
            Plan = x.Subscription!.Plan, SubscriptionStatus = x.Subscription.Status,
            x.Subscription.ExpiresOn, x.Subscription.GracePeriodEndsOn,
            ThemeKey = x.Theme != null ? x.Theme.ThemeKey : null
        }).ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(-5);
        var growth = Enumerable.Range(0, 6).Select(offset =>
        {
            var month = monthStart.AddMonths(offset);
            var next = month.AddMonths(1);
            return new AdminGrowthPoint(month.ToString("yyyy-MM"), rows.Count(x => x.CreatedAt >= month && x.CreatedAt < next));
        }).ToList();
        var activeLicenses = rows.Count(x => x.Status == RestaurantStatus.Active &&
            (x.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trial ||
             x.SubscriptionStatus == SubscriptionStatus.Overdue && x.GracePeriodEndsOn >= today));
        var breakdown = rows.GroupBy(x => x.SubscriptionStatus.ToString())
            .Select(x => new AdminStatusCount(x.Key, x.Count())).OrderByDescending(x => x.Count).ToList();
        var recent = rows.OrderByDescending(x => x.UpdatedAt).Take(6)
            .Select(x => new AdminRecentRestaurant(x.Id, x.Name, x.Status, NormalizePlan(x.Plan), x.UpdatedAt)).ToList();
        var themeUsage = rows.GroupBy(x => NormalizeThemeKey(x.ThemeKey, x.Type))
            .Select(x => new AdminThemeUsage(x.Key, x.Count())).OrderByDescending(x => x.Count).ToList();
        var newSupport = await db.SupportTickets.AsNoTracking().CountAsync(x => x.Status == SupportTicketStatus.New, ct);
        var newLeads = await db.Leads.AsNoTracking().CountAsync(x => x.Status == LeadStatus.New, ct);
        return new AdminDashboardSummary(
            rows.Count,
            rows.Count(x => x.Status == RestaurantStatus.Active),
            activeLicenses,
            rows.Count(x => x.SubscriptionStatus == SubscriptionStatus.Trial),
            rows.Count(x => x.ExpiresOn >= today && x.ExpiresOn <= today.AddDays(14)),
            newSupport,
            newLeads,
            rows.Count(x => x.ProductCount == 0),
            rows.Count(x => string.IsNullOrWhiteSpace(x.LogoUrl) || string.IsNullOrWhiteSpace(x.CoverImageUrl)),
            growth, breakdown, recent, themeUsage);
    }

    public async Task<AdminRestaurantDetails?> GetAdminDetailsAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Restaurants.AsNoTracking().Where(x => x.Id == id && x.Subscription != null && x.Status != RestaurantStatus.Archived).Select(x => new
        {
            x.Id, x.Name, x.Slug, x.Description, x.LogoUrl, x.CoverImageUrl, x.Address, x.Phone, x.Email,
            x.WebsiteUrl, x.InstagramUrl, x.Currency, x.DefaultLanguage, x.EnabledLanguages, x.Type, x.Status,
            ThemeKey = x.Theme != null ? x.Theme.ThemeKey : null,
            SubscriptionStatus = x.Subscription!.Status, x.Subscription.Plan, x.Subscription.MonthlyPrice, x.Subscription.StartsOn,
            x.Subscription.ExpiresOn, x.Subscription.GracePeriodEndsOn
        }).FirstOrDefaultAsync(ct);
        if (item is null) return null;
        var ownerEmail = await (from user in db.Users
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where user.RestaurantId == id && role.Name == Roles.RestaurantOwner
            select user.Email).FirstOrDefaultAsync(ct);
        return new AdminRestaurantDetails(
            item.Id, item.Name, item.Slug, item.Description, AssetUrl.Normalize(item.LogoUrl), AssetUrl.Normalize(item.CoverImageUrl), item.Address, item.Phone,
            item.Email, item.WebsiteUrl, item.InstagramUrl, item.Currency, item.DefaultLanguage, NormalizeEnabledLanguages(item.EnabledLanguages, item.DefaultLanguage), item.Type, item.Status,
            NormalizeThemeKey(item.ThemeKey, item.Type), ownerEmail,
            new AdminSubscriptionDetails(item.SubscriptionStatus, item.Plan, item.MonthlyPrice, item.StartsOn, item.ExpiresOn, item.GracePeriodEndsOn));
    }

    public async Task<OwnerRestaurantDetails?> GetAsync(Guid id, Guid? tenantId, bool admin, CancellationToken ct)
    {
        var restaurant = await db.Restaurants.AsNoTracking().AsSplitQuery()
        .Include(x => x.Subscription).Include(x => x.Theme).Include(x => x.BusinessHours)
        .Include(x => x.Categories).ThenInclude(x => x.Items).ThenInclude(x => x.GlobalDrink).Include(x => x.SpecialOffers)
        .FirstOrDefaultAsync(x => x.Id == id && x.Status != RestaurantStatus.Archived && (admin || x.Id == tenantId), ct);
        if (restaurant is null) return null;
        return ToOwnerDetails(restaurant, await GetMenuAnalyticsAsync(restaurant.Id, ct));
    }

    public async Task<Restaurant> CreateAsync(CreateRestaurantRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("Restaurant name is required.");
        if (string.IsNullOrWhiteSpace(request.Slug)) throw new InvalidOperationException("Restaurant slug is required.");
        if (string.IsNullOrWhiteSpace(request.OwnerEmail)) throw new InvalidOperationException("Owner email is required.");
        if (request.TrialDays is < 1 or > 365) throw new InvalidOperationException("Trial period must be between 1 and 365 days.");
        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3) throw new InvalidOperationException("Currency must use a three-letter code.");
        if (!SupportedThemes.Contains(request.ThemeKey)) throw new InvalidOperationException("Selected theme is not supported.");
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await db.Restaurants.AnyAsync(x => x.Slug == slug, ct)) throw new InvalidOperationException("Slug is already in use.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var restaurant = new Restaurant
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Type = request.Type,
            Status = request.Status,
            Description = request.Description,
            LogoUrl = AssetUrl.Normalize(request.LogoUrl),
            CoverImageUrl = AssetUrl.Normalize(request.CoverImageUrl),
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            WebsiteUrl = request.WebsiteUrl,
            InstagramUrl = request.InstagramUrl,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            DefaultLanguage = NormalizeDefaultLanguage(request.DefaultLanguage),
            EnabledLanguages = NormalizeEnabledLanguages(request.EnabledLanguages, request.DefaultLanguage)
        };
        var themeColors = ThemeColors(request.ThemeKey);
        var plan = NormalizePlan(request.Plan);
        restaurant.Subscription = new Subscription
        {
            RestaurantId = restaurant.Id,
            StartsOn = today,
            ExpiresOn = today.AddDays(request.TrialDays),
            Status = SubscriptionStatus.Trial,
            Plan = plan,
            MonthlyPrice = NormalizeMonthlyPrice(plan, request.MonthlyPrice)
        };
        restaurant.Theme = new ThemeSettings { RestaurantId = restaurant.Id, ThemeKey = request.ThemeKey, PrimaryColor = themeColors.Primary, AccentColor = themeColors.Accent };
        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync(ct);
        var user = new ApplicationUser { UserName = request.OwnerEmail, Email = request.OwnerEmail, EmailConfirmed = true, RestaurantId = restaurant.Id, DisplayName = request.Name };
        var result = await users.CreateAsync(user, request.OwnerPassword);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        await users.AddToRoleAsync(user, Roles.RestaurantOwner);
        await transaction.CommitAsync(ct);
        return restaurant;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateRestaurantRequest r, Guid? tenantId, bool admin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new InvalidOperationException("Restaurant name is required.");
        if (string.IsNullOrWhiteSpace(r.Currency) || r.Currency.Trim().Length != 3) throw new InvalidOperationException("Currency must use a three-letter code.");
        var x = await db.Restaurants.Include(x => x.Theme).FirstOrDefaultAsync(x => x.Id == id && x.Status != RestaurantStatus.Archived && (admin || x.Id == tenantId), ct);
        if (x is null) return false;
        if (!SupportedThemes.Contains(r.ThemeKey)) throw new InvalidOperationException("Selected theme is not supported.");
        if (admin && !string.IsNullOrWhiteSpace(r.Slug))
        {
            var slug = NormalizeRestaurantSlug(r.Slug);
            if (string.IsNullOrWhiteSpace(slug)) throw new InvalidOperationException("Restaurant slug is required.");
            if (await db.Restaurants.AnyAsync(item => item.Id != x.Id && item.Slug == slug, ct)) throw new InvalidOperationException("Slug is already in use.");
            x.Slug = slug;
        }
        x.Name = r.Name.Trim(); x.Description = r.Description; x.LogoUrl = AssetUrl.Normalize(r.LogoUrl); x.CoverImageUrl = AssetUrl.Normalize(r.CoverImageUrl);
        x.Address = r.Address; x.Phone = r.Phone; x.Email = r.Email; x.WebsiteUrl = r.WebsiteUrl; x.InstagramUrl = r.InstagramUrl;
        x.Currency = r.Currency.ToUpperInvariant(); x.DefaultLanguage = NormalizeDefaultLanguage(r.DefaultLanguage); x.EnabledLanguages = NormalizeEnabledLanguages(r.EnabledLanguages, r.DefaultLanguage); x.Type = r.Type;
        var themeColors = ThemeColors(r.ThemeKey);
        if (x.Theme is null) { x.Theme = new ThemeSettings { RestaurantId = x.Id, ThemeKey = r.ThemeKey, PrimaryColor = themeColors.Primary, AccentColor = themeColors.Accent }; }
        else { x.Theme.ThemeKey = r.ThemeKey; x.Theme.PrimaryColor = themeColors.Primary; x.Theme.AccentColor = themeColors.Accent; }
        x.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct); return true;
    }

    public async Task<bool> SetStatusAsync(Guid id, RestaurantStatus status, CancellationToken ct)
    {
        var x = await db.Restaurants.Include(x => x.Subscription).FirstOrDefaultAsync(x => x.Id == id && x.Status != RestaurantStatus.Archived, ct); if (x is null) return false;
        x.Status = status;
        if (x.Subscription is not null)
        {
            var subscriptionStatus = status switch
            {
                RestaurantStatus.Suspended => SubscriptionStatus.Suspended,
                RestaurantStatus.Cancelled => SubscriptionStatus.Cancelled,
                RestaurantStatus.Active => ActiveSubscriptionStatus(x.Subscription),
                _ => x.Subscription.Status
            };
            x.Subscription.Status = subscriptionStatus;
            if (status == RestaurantStatus.Active && subscriptionStatus == SubscriptionStatus.Suspended)
                x.Status = RestaurantStatus.Suspended;
            x.Subscription.UpdatedAt = DateTimeOffset.UtcNow;
        }
        x.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return true;
    }

    public async Task<bool> SetSubscriptionAsync(Guid id, SetSubscriptionRequest r, CancellationToken ct)
    {
        var plan = NormalizePlan(r.Plan);
        if (r.MonthlyPrice < 0) throw new InvalidOperationException("Monthly price cannot be negative.");
        if (r.ExpiresOn < r.StartsOn) throw new InvalidOperationException("Subscription expiry cannot be before its start date.");
        if (r.GracePeriodEndsOn < r.ExpiresOn) throw new InvalidOperationException("Grace period cannot end before the subscription expiry date.");
        var x = await db.Subscriptions.Include(x => x.Restaurant).SingleOrDefaultAsync(x => x.RestaurantId == id && x.Restaurant.Status != RestaurantStatus.Archived, ct); if (x is null) return false;
        x.Status = r.Status; x.Plan = plan; x.MonthlyPrice = r.MonthlyPrice; x.StartsOn = r.StartsOn; x.ExpiresOn = r.ExpiresOn; x.GracePeriodEndsOn = r.GracePeriodEndsOn; x.UpdatedAt = DateTimeOffset.UtcNow;
        x.Restaurant.Status = r.Status switch
        {
            SubscriptionStatus.Suspended => RestaurantStatus.Suspended,
            SubscriptionStatus.Cancelled => RestaurantStatus.Cancelled,
            SubscriptionStatus.Active or SubscriptionStatus.Trial or SubscriptionStatus.Overdue => RestaurantStatus.Active,
            _ => x.Restaurant.Status
        };
        x.Restaurant.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct); return true;
    }

    public async Task<bool> UpdateOwnerAccessAsync(Guid id, UpdateOwnerAccessRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) throw new InvalidOperationException("Owner email is required.");
        if (!await db.Restaurants.AnyAsync(x => x.Id == id && x.Status != RestaurantStatus.Archived, ct)) return false;
        var owner = await (from user in db.Users
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where user.RestaurantId == id && role.Name == Roles.RestaurantOwner
            select user).FirstOrDefaultAsync(ct);
        if (owner is null) return false;
        var email = request.Email.Trim();
        if (!string.Equals(owner.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await users.FindByEmailAsync(email);
            if (existing is not null && existing.Id != owner.Id) throw new InvalidOperationException("Email is already in use.");
            EnsureIdentityResult(await users.SetEmailAsync(owner, email));
            EnsureIdentityResult(await users.SetUserNameAsync(owner, email));
            owner.EmailConfirmed = true;
            EnsureIdentityResult(await users.UpdateAsync(owner));
        }
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            var token = await users.GeneratePasswordResetTokenAsync(owner);
            EnsureIdentityResult(await users.ResetPasswordAsync(owner, token, request.NewPassword));
        }
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid? archivedByUserId, CancellationToken ct)
    {
        var restaurant = await db.Restaurants.Include(x => x.Subscription).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (restaurant is null) return false;
        if (restaurant.Status == RestaurantStatus.Archived) return true;

        var now = DateTimeOffset.UtcNow;
        restaurant.Status = RestaurantStatus.Archived;
        restaurant.ArchivedAt = now;
        restaurant.ArchivedByUserId = archivedByUserId;
        restaurant.UpdatedAt = now;
        if (restaurant.Subscription is not null)
        {
            restaurant.Subscription.Status = SubscriptionStatus.Cancelled;
            restaurant.Subscription.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string DefaultThemeKey(EstablishmentType type) => type switch
    {
        EstablishmentType.Cafe => "natural-green",
        EstablishmentType.FastFood => "warm-orange",
        EstablishmentType.Bar or EstablishmentType.Club or EstablishmentType.ShishaBar => "modern-dark",
        _ => "classic-light"
    };

    private static string NormalizeThemeKey(string? key, EstablishmentType type) =>
        string.IsNullOrWhiteSpace(key) || key == "restaurant" ? DefaultThemeKey(type) : key;

    private static string NormalizeRestaurantSlug(string? value) =>
        Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

    private static string NormalizePlan(string? value) => (value ?? string.Empty).Trim() switch
    {
        "Standard" => "Pro",
        "Enterprise" => "Premium",
        "Basic" or "" => "Start",
        "Start" or "Pro" or "Premium" => value!.Trim(),
        _ => throw new InvalidOperationException("Unsupported subscription plan.")
    };

    private static decimal NormalizeMonthlyPrice(string plan, decimal? price) =>
        price is > 0 ? price.Value : plan switch
        {
            "Pro" => 49m,
            "Premium" => 79m,
            _ => 29m
        };

    private static readonly string[] SupportedLanguages = ["bs", "en", "de"];

    private static string NormalizeDefaultLanguage(string? value)
    {
        var language = (value ?? "bs").Trim().ToLowerInvariant();
        return SupportedLanguages.Contains(language) ? language : "bs";
    }

    private static string NormalizeEnabledLanguages(string? value, string? defaultLanguage)
    {
        var selected = (value ?? "bs,en")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Where(SupportedLanguages.Contains)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        selected.Add("bs");
        selected.Add("en");
        selected.Add(NormalizeDefaultLanguage(defaultLanguage));

        return string.Join(",", SupportedLanguages.Where(selected.Contains));
    }

    private static readonly string[] SupportedThemes =
    [
        "classic-light", "premium-gold", "burgundy-dining", "mediterranean-blue", "olive-linen", "ocean-slate",
        "coffee-cream", "urban-espresso", "soft-pastel", "natural-green", "rose-latte", "cocoa-mint",
        "neon-night", "royal-violet",
        "warm-orange", "street-red", "yellow-pop", "burger-black", "lime-street", "modern-dark"
    ];

    internal static bool IsSupportedTheme(string themeKey) => SupportedThemes.Contains(themeKey);

    private static (string Primary, string Accent) ThemeColors(string themeKey) => themeKey switch
    {
        "premium-gold" => ("#27272a", "#c8a96e"),
        "burgundy-dining" => ("#27272a", "#be123c"),
        "mediterranean-blue" => ("#ffffff", "#2563eb"),
        "olive-linen" => ("#ffffff", "#64748b"),
        "ocean-slate" => ("#1e293b", "#38bdf8"),
        "coffee-cream" => ("#ffffff", "#92400e"),
        "urban-espresso" => ("#292524", "#d97706"),
        "soft-pastel" => ("#ffffff", "#db2777"),
        "natural-green" => ("#ffffff", "#65a30d"),
        "rose-latte" => ("#ffffff", "#e11d48"),
        "cocoa-mint" => ("#2f2722", "#2dd4bf"),
        "neon-night" => ("#18181b", "#22d3ee"),
        "royal-violet" => ("#241a3a", "#a855f7"),
        "warm-orange" => ("#ffffff", "#f97316"),
        "street-red" => ("#ffffff", "#dc2626"),
        "yellow-pop" => ("#ffffff", "#eab308"),
        "burger-black" => ("#1f2937", "#fb923c"),
        "lime-street" => ("#ffffff", "#84cc16"),
        "modern-dark" => ("#1f2937", "#f59e0b"),
        _ => ("#ffffff", "#84cc16")
    };

    internal static OwnerRestaurantDetails ToOwnerDetails(Restaurant restaurant, OwnerMenuAnalytics? analytics = null) => new(
        restaurant.Id, restaurant.Name, restaurant.Slug, restaurant.Description, AssetUrl.Normalize(restaurant.LogoUrl), AssetUrl.Normalize(restaurant.CoverImageUrl),
        restaurant.Address, restaurant.Phone, restaurant.Email, restaurant.WebsiteUrl, restaurant.InstagramUrl,
        restaurant.Currency, restaurant.DefaultLanguage, NormalizeEnabledLanguages(restaurant.EnabledLanguages, restaurant.DefaultLanguage), restaurant.Type, restaurant.Status,
        NormalizePlan(restaurant.Subscription?.Plan),
        new OwnerTheme(restaurant.Theme?.ThemeKey ?? NormalizeThemeKey(null, restaurant.Type), restaurant.Theme?.PrimaryColor ?? "#111827", restaurant.Theme?.AccentColor ?? "#84cc16", AssetUrl.Normalize(restaurant.Theme?.BackgroundImageUrl), restaurant.Theme?.FontFamily ?? "Inter"),
        restaurant.BusinessHours.OrderBy(x => x.DayOfWeek).Select(x => new OwnerBusinessHour(x.DayOfWeek, x.OpensAt, x.ClosesAt, x.IsClosed)).ToList(),
        restaurant.Categories.OrderBy(x => x.SortOrder).Select(x => new OwnerMenuCategory(x.Id, x.Name, x.Description, x.NameEn, x.DescriptionEn, x.NameDe, x.DescriptionDe, x.Type, x.SortOrder, x.IsVisible,
            x.Items.OrderBy(i => i.SortOrder).Select(i => new OwnerMenuItem(i.Id, i.CategoryId, i.GlobalDrinkId, i.Name, i.Description, i.NameEn, i.DescriptionEn, i.NameDe, i.DescriptionDe, i.Price, i.ServingSize, AssetUrl.Normalize(i.ImageUrl ?? (i.GlobalDrink == null ? null : i.GlobalDrink.ImageUrl)), i.Allergens, i.SortOrder, i.IsVisible, i.IsAvailable, i.IsVegetarian, i.IsSpicy, i.IsFeatured)).ToList())).ToList(),
        restaurant.SpecialOffers.OrderByDescending(x => x.CreatedAt).Select(x => new OwnerSpecialOffer(x.Id, x.Title, x.Description, x.TitleEn, x.DescriptionEn, x.ItemsEn, x.TitleDe, x.DescriptionDe, x.ItemsDe, x.Price, x.OriginalPrice, AssetUrl.Normalize(x.ImageUrl), x.StartsAt, x.EndsAt, x.IsVisible, x.Kind, x.Items)).ToList(),
        analytics ?? EmptyMenuAnalytics());

    private async Task<OwnerMenuAnalytics> GetMenuAnalyticsAsync(Guid restaurantId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = today.AddDays(-6);
        var monthStart = today.AddDays(-29);
        var rows = await db.MenuViews.AsNoTracking()
            .Where(x => x.RestaurantId == restaurantId && x.ViewedOn >= start && x.ViewedOn <= today)
            .GroupBy(x => x.ViewedOn)
            .Select(x => new { Date = x.Key, Views = x.Count() })
            .ToListAsync(ct);
        var total = await db.MenuViews.AsNoTracking().CountAsync(x => x.RestaurantId == restaurantId, ct);
        var monthTotal = await db.MenuViews.AsNoTracking().CountAsync(x => x.RestaurantId == restaurantId && x.ViewedOn >= monthStart && x.ViewedOn <= today, ct);
        var topItemRows = await db.MenuItemViews.AsNoTracking()
            .Where(x => x.RestaurantId == restaurantId && x.ViewedOn >= monthStart && x.ViewedOn <= today)
            .GroupBy(x => new { x.MenuItemId, x.MenuItem.Name })
            .Select(x => new { x.Key.MenuItemId, x.Key.Name, Views = x.Count() })
            .OrderByDescending(x => x.Views)
            .Take(5)
            .ToListAsync(ct);
        var topItems = topItemRows.Select(x => new OwnerTopMenuItem(x.MenuItemId, x.Name, x.Views)).ToList();
        var weekly = Enumerable.Range(0, 7).Select(offset =>
        {
            var date = start.AddDays(offset);
            return new OwnerMenuViewPoint(date, DayLabel(date.DayOfWeek), rows.FirstOrDefault(x => x.Date == date)?.Views ?? 0);
        }).ToList();
        return new OwnerMenuAnalytics(total, weekly.Sum(x => x.Views), monthTotal, weekly, topItems);
    }

    private static OwnerMenuAnalytics EmptyMenuAnalytics() =>
        new(0, 0, 0, Enumerable.Range(0, 7).Select(offset =>
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(offset - 6);
            return new OwnerMenuViewPoint(date, DayLabel(date.DayOfWeek), 0);
        }).ToList(), []);

    private static string DayLabel(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pon",
        DayOfWeek.Tuesday => "Uto",
        DayOfWeek.Wednesday => "Sri",
        DayOfWeek.Thursday => "Cet",
        DayOfWeek.Friday => "Pet",
        DayOfWeek.Saturday => "Sub",
        DayOfWeek.Sunday => "Ned",
        _ => day.ToString()
    };

    private static SubscriptionStatus ActiveSubscriptionStatus(Subscription subscription)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (subscription.ExpiresOn >= today) return subscription.Status == SubscriptionStatus.Trial ? SubscriptionStatus.Trial : SubscriptionStatus.Active;
        return subscription.GracePeriodEndsOn >= today ? SubscriptionStatus.Overdue : SubscriptionStatus.Suspended;
    }

    private static void EnsureIdentityResult(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
    }
}

public sealed class AuditLogService(ApplicationDbContext db) : IAuditLogService
{
    public async Task RecordAsync(AuditLogRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Action) || string.IsNullOrWhiteSpace(request.EntityType)) return;

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = request.ActorUserId,
            ActorEmail = Trim(request.ActorEmail, 180),
            ActorRole = Trim(request.ActorRole, 80),
            Action = Trim(request.Action, 120) ?? string.Empty,
            EntityType = Trim(request.EntityType, 120) ?? string.Empty,
            EntityId = request.EntityId,
            RestaurantId = request.RestaurantId,
            Summary = Trim(request.Summary, 2000),
            IpAddress = Trim(request.IpAddress, 80)
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Trim(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return null;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public sealed class BillingService(ApplicationDbContext db) : IBillingService
{
    public async Task<BillingOverview> GetOverviewAsync(CancellationToken ct)
    {
        await SynchronizeExpiredSubscriptionsAsync(ct);
        var rows = await db.Restaurants.AsNoTracking().Where(x => x.Subscription != null && x.Status != RestaurantStatus.Archived).OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Slug,
                Plan = x.Subscription!.Plan,
                x.Subscription.MonthlyPrice,
                x.Currency,
                x.Subscription.Status,
                x.Subscription.ExpiresOn,
                x.Subscription.GracePeriodEndsOn,
                LastPaidOn = x.Payments.OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.CreatedAt).Select(p => (DateOnly?)p.PaidOn).FirstOrDefault(),
                LastPaymentAmount = x.Payments.OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.CreatedAt).Select(p => (decimal?)p.Amount).FirstOrDefault()
            })
            .ToListAsync(ct);
        var accounts = rows.Select(x => new BillingAccountSummary(
            x.Id, x.Name, x.Slug, NormalizePlan(x.Plan), x.MonthlyPrice, x.Currency,
            x.Status, x.ExpiresOn, x.GracePeriodEndsOn, x.LastPaidOn, x.LastPaymentAmount)).ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var paidThisMonth = await db.SubscriptionPayments.AsNoTracking()
            .Where(x => x.PaidOn >= monthStart && x.PaidOn <= today)
            .GroupBy(x => x.Currency).Select(x => new BillingMoneyTotal(x.Key, x.Sum(p => p.Amount))).ToListAsync(ct);
        var recurring = accounts.Where(x => x.Status is SubscriptionStatus.Active or SubscriptionStatus.Trial or SubscriptionStatus.Overdue)
            .GroupBy(x => x.Currency).Select(x => new BillingMoneyTotal(x.Key, x.Sum(a => a.MonthlyPrice))).ToList();
        return new BillingOverview(
            recurring,
            paidThisMonth,
            accounts.Count(x => x.Status == SubscriptionStatus.Overdue),
            accounts.Count(x => x.ExpiresOn >= today && x.ExpiresOn <= today.AddDays(14)),
            accounts);
    }

    public async Task<IReadOnlyList<PaymentHistoryItem>?> GetHistoryAsync(Guid restaurantId, CancellationToken ct)
    {
        if (!await db.Restaurants.AnyAsync(x => x.Id == restaurantId && x.Status != RestaurantStatus.Archived, ct)) return null;
        return await db.SubscriptionPayments.AsNoTracking().Where(x => x.RestaurantId == restaurantId)
            .OrderByDescending(x => x.PaidOn).ThenByDescending(x => x.CreatedAt)
            .Select(x => new PaymentHistoryItem(x.Id, x.Amount, x.Currency, x.PaidOn, x.PeriodStartsOn, x.PeriodEndsOn, x.CoverageMonths, x.Method, x.Reference, x.Note, x.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<PaymentHistoryItem?> RecordPaymentAsync(Guid restaurantId, RecordManualPaymentRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0) throw new InvalidOperationException("Payment amount must be greater than zero.");
        if (request.CoverageMonths is < 1 or > 24) throw new InvalidOperationException("Coverage must be between 1 and 24 months.");
        if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Trim().Length != 3) throw new InvalidOperationException("Currency must use a three-letter code.");
        var restaurant = await db.Restaurants.Include(x => x.Subscription).FirstOrDefaultAsync(x => x.Id == restaurantId && x.Status != RestaurantStatus.Archived, ct);
        if (restaurant?.Subscription is null) return null;
        var periodStart = restaurant.Subscription.ExpiresOn >= request.PaidOn
            ? restaurant.Subscription.ExpiresOn.AddDays(1)
            : request.PaidOn;
        var periodEnd = periodStart.AddMonths(request.CoverageMonths).AddDays(-1);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var payment = new SubscriptionPayment
        {
            RestaurantId = restaurantId,
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            PaidOn = request.PaidOn,
            PeriodStartsOn = periodStart,
            PeriodEndsOn = periodEnd,
            CoverageMonths = request.CoverageMonths,
            Method = request.Method,
            Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };
        db.SubscriptionPayments.Add(payment);
        restaurant.Subscription.Status = SubscriptionStatus.Active;
        restaurant.Subscription.ExpiresOn = periodEnd;
        restaurant.Subscription.GracePeriodEndsOn = null;
        restaurant.Subscription.UpdatedAt = DateTimeOffset.UtcNow;
        restaurant.Status = RestaurantStatus.Active;
        restaurant.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new PaymentHistoryItem(payment.Id, payment.Amount, payment.Currency, payment.PaidOn, payment.PeriodStartsOn, payment.PeriodEndsOn, payment.CoverageMonths, payment.Method, payment.Reference, payment.Note, payment.CreatedAt);
    }

    private async Task SynchronizeExpiredSubscriptionsAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var restaurants = await db.Restaurants.Include(x => x.Subscription).Where(x => x.Subscription != null && x.Status != RestaurantStatus.Archived).ToListAsync(ct);
        var changed = false;
        foreach (var restaurant in restaurants)
        {
            var subscription = restaurant.Subscription!;
            var previousRestaurantStatus = restaurant.Status;
            var previousSubscriptionStatus = subscription.Status;
            if (restaurant.Status == RestaurantStatus.Cancelled || subscription.Status == SubscriptionStatus.Cancelled)
            {
                restaurant.Status = RestaurantStatus.Cancelled;
                subscription.Status = SubscriptionStatus.Cancelled;
            }
            else if (restaurant.Status == RestaurantStatus.Suspended || subscription.Status == SubscriptionStatus.Suspended)
            {
                restaurant.Status = RestaurantStatus.Suspended;
                subscription.Status = SubscriptionStatus.Suspended;
            }
            else if (subscription.ExpiresOn < today)
            {
                var inGracePeriod = subscription.GracePeriodEndsOn >= today;
                subscription.Status = inGracePeriod ? SubscriptionStatus.Overdue : SubscriptionStatus.Suspended;
                if (!inGracePeriod) restaurant.Status = RestaurantStatus.Suspended;
            }
            if (restaurant.Status == previousRestaurantStatus && subscription.Status == previousSubscriptionStatus) continue;
            subscription.UpdatedAt = DateTimeOffset.UtcNow;
            restaurant.UpdatedAt = DateTimeOffset.UtcNow;
            changed = true;
        }
        if (changed) await db.SaveChangesAsync(ct);
    }

    private static string NormalizePlan(string? value) => (value ?? string.Empty).Trim() switch
    {
        "Standard" => "Pro",
        "Enterprise" => "Premium",
        "Basic" or "" => "Start",
        "Start" or "Pro" or "Premium" => value!.Trim(),
        _ => "Start"
    };
}

public sealed class SupportTicketService(ApplicationDbContext db) : ISupportTicketService
{
    public async Task<IReadOnlyList<SupportTicketSummary>> GetAdminAsync(CancellationToken ct)
    {
        var rows = await Query().OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return rows.Select(ToSummary).ToList();
    }

    public async Task<IReadOnlyList<SupportTicketSummary>> GetOwnerAsync(Guid restaurantId, CancellationToken ct)
    {
        var rows = await Query().Where(x => x.RestaurantId == restaurantId).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return rows.Select(ToSummary).ToList();
    }

    public async Task<SupportTicketSummary?> CreateAsync(Guid restaurantId, CreateSupportTicketRequest request, CancellationToken ct)
    {
        var restaurant = await db.Restaurants.Include(x => x.Subscription).FirstOrDefaultAsync(x => x.Id == restaurantId && x.Status != RestaurantStatus.Archived, ct);
        if (restaurant?.Subscription is null) return null;
        var plan = NormalizePlan(restaurant.Subscription.Plan);
        if (plan == "Start") throw new InvalidOperationException("Support tickets are available on Pro and Premium plans.");

        var title = request.Title.Trim();
        var message = request.Message.Trim();
        if (title.Length < 3) throw new InvalidOperationException("Support ticket title is required.");
        if (message.Length < 5) throw new InvalidOperationException("Support ticket message is required.");

        var ticket = new SupportTicket
        {
            RestaurantId = restaurantId,
            Title = title,
            Type = request.Type,
            Priority = request.Priority,
            Message = message,
            AttachmentUrl = string.IsNullOrWhiteSpace(request.AttachmentUrl) ? null : request.AttachmentUrl.Trim(),
            Status = SupportTicketStatus.New
        };
        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        return ToSummary(await Query().SingleAsync(x => x.Id == ticket.Id, ct));
    }

    public async Task<SupportTicketSummary?> UpdateAsync(Guid id, UpdateSupportTicketRequest request, CancellationToken ct)
    {
        var ticket = await db.SupportTickets.Include(x => x.Restaurant).ThenInclude(x => x.Subscription).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return null;

        ticket.Status = request.Status;
        ticket.AdminNote = string.IsNullOrWhiteSpace(request.AdminNote) ? null : request.AdminNote.Trim();
        ticket.ResolvedAt = request.Status is SupportTicketStatus.Resolved or SupportTicketStatus.Closed ? DateTimeOffset.UtcNow : null;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToSummary(await Query().SingleAsync(x => x.Id == ticket.Id, ct));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return false;

        db.SupportTickets.Remove(ticket);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<SupportTicket> Query() => db.SupportTickets.AsNoTracking().Include(x => x.Restaurant).ThenInclude(x => x.Subscription).Where(x => x.Restaurant.Status != RestaurantStatus.Archived);

    private static SupportTicketSummary ToSummary(SupportTicket x) => new(
        x.Id,
        x.RestaurantId,
        x.Restaurant.Name,
        x.Restaurant.Slug,
        NormalizePlan(x.Restaurant.Subscription?.Plan),
        x.Title,
        x.Type,
        x.Priority,
        x.Status,
        x.Message,
        AssetUrl.Normalize(x.AttachmentUrl),
        x.AdminNote,
        x.CreatedAt,
        x.UpdatedAt,
        x.ResolvedAt);

    private static string NormalizePlan(string? value) => (value ?? string.Empty).Trim() switch
    {
        "Standard" => "Pro",
        "Enterprise" => "Premium",
        "Basic" or "" => "Start",
        "Start" or "Pro" or "Premium" => value!.Trim(),
        _ => "Start"
    };
}

public sealed class LeadService(ApplicationDbContext db) : ILeadService
{
    public async Task CreateAsync(string businessName, string email, string? phone, string type, string? message, CancellationToken ct)
    {
        db.Leads.Add(new Lead
        {
            BusinessName = businessName.Trim(),
            Email = email.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            Type = type.Trim(),
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            Status = LeadStatus.New
        });
        await db.SaveChangesAsync(ct);
    }
}

public sealed class MenuManagementService(ApplicationDbContext db) : IMenuManagementService
{
    public async Task<MenuCategory?> SaveCategoryAsync(Guid rid, Guid? id, CategoryRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new InvalidOperationException("Category name is required.");
        if (r.SortOrder < 0) throw new InvalidOperationException("Category sort order cannot be negative.");
        var x = id is null ? new MenuCategory { RestaurantId = rid, Name = r.Name } : await db.MenuCategories.FirstOrDefaultAsync(x => x.Id == id && x.RestaurantId == rid, ct);
        if (x is null) return null; x.Name = r.Name.Trim(); x.Description = r.Description; x.NameEn = CleanOptional(r.NameEn); x.DescriptionEn = CleanOptional(r.DescriptionEn); x.NameDe = CleanOptional(r.NameDe); x.DescriptionDe = CleanOptional(r.DescriptionDe); x.Type = r.Type; x.SortOrder = r.SortOrder; x.IsVisible = r.IsVisible; x.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) db.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<MenuItem?> SaveItemAsync(Guid rid, Guid? id, MenuItemRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new InvalidOperationException("Menu item name is required.");
        if (r.Price < 0) throw new InvalidOperationException("Menu item price cannot be negative.");
        if (r.SortOrder < 0) throw new InvalidOperationException("Menu item sort order cannot be negative.");
        if (!await db.MenuCategories.AnyAsync(x => x.Id == r.CategoryId && x.RestaurantId == rid, ct)) return null;
        var x = id is null ? new MenuItem { RestaurantId = rid, CategoryId = r.CategoryId, Name = r.Name } : await db.MenuItems.FirstOrDefaultAsync(x => x.Id == id && x.RestaurantId == rid, ct);
        if (x is null) return null; x.CategoryId = r.CategoryId; x.Name = r.Name.Trim(); x.Description = r.Description; x.NameEn = CleanOptional(r.NameEn); x.DescriptionEn = CleanOptional(r.DescriptionEn); x.NameDe = CleanOptional(r.NameDe); x.DescriptionDe = CleanOptional(r.DescriptionDe); x.Price = r.Price; x.ServingSize = NormalizeServingSize(r.ServingSize); x.ImageUrl = AssetUrl.Normalize(r.ImageUrl); x.Allergens = r.Allergens; x.SortOrder = r.SortOrder; x.IsVisible = r.IsVisible; x.IsAvailable = r.IsAvailable; x.IsVegetarian = r.IsVegetarian; x.IsSpicy = r.IsSpicy; x.IsFeatured = r.IsFeatured; x.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) db.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<IReadOnlyList<GlobalDrinkSummary>> GetDrinkLibraryAsync(CancellationToken ct)
    {
        var rows = await db.GlobalDrinks.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Category).ThenBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Category, x.Description, x.ImageUrl, x.ServingOptions, x.SortOrder }).ToListAsync(ct);
        return rows.Select(x => new GlobalDrinkSummary(x.Id, x.Name, x.Category, x.Description, AssetUrl.Normalize(x.ImageUrl), x.ServingOptions, x.SortOrder)).ToList();
    }

    public async Task<IReadOnlyList<MenuItem>> AddLibraryDrinksAsync(Guid rid, AddLibraryDrinksRequest r, CancellationToken ct)
    {
        if (r.Drinks.Count == 0) return [];
        if (r.Drinks.Any(x => x.Price < 0)) throw new InvalidOperationException("Drink prices cannot be negative.");
        var drinkIds = r.Drinks.Select(x => x.DrinkId).Distinct().ToArray();
        var drinks = await db.GlobalDrinks.Where(x => x.IsActive && drinkIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var existing = await db.MenuItems.Where(x => x.RestaurantId == rid && x.GlobalDrinkId != null && drinkIds.Contains(x.GlobalDrinkId.Value))
            .Select(x => new { DrinkId = x.GlobalDrinkId!.Value, x.ServingSize }).ToListAsync(ct);
        var existingSet = existing.Select(x => LibraryKey(x.DrinkId, x.ServingSize)).ToHashSet();
        var categories = await db.MenuCategories.Where(x => x.RestaurantId == rid).ToListAsync(ct);
        var selectedCategory = r.CategoryId is { } id ? categories.FirstOrDefault(x => x.Id == id) : null;
        var categorySort = categories.Select(x => (int?)x.SortOrder).Max() ?? 0;
        var itemSortOrders = await db.MenuItems.Where(x => x.RestaurantId == rid).GroupBy(x => x.CategoryId)
            .Select(x => new { CategoryId = x.Key, SortOrder = x.Max(item => item.SortOrder) }).ToDictionaryAsync(x => x.CategoryId, x => x.SortOrder, ct);
        var created = new List<MenuItem>();
        foreach (var selection in r.Drinks.Where(x => !existingSet.Contains(LibraryKey(x.DrinkId, x.ServingSize))))
        {
            if (!drinks.TryGetValue(selection.DrinkId, out var drink)) continue;
            var servingSize = NormalizeServingSize(selection.ServingSize);
            var category = selectedCategory ?? categories.FirstOrDefault(x => x.Type == MenuCategoryType.Drink && string.Equals(x.Name, drink.Category, StringComparison.OrdinalIgnoreCase));
            if (category is null)
            {
                category = new MenuCategory { RestaurantId = rid, Name = drink.Category, Description = $"Ponuda: {drink.Category}", Type = MenuCategoryType.Drink, SortOrder = ++categorySort, IsVisible = true };
                categories.Add(category);
                db.MenuCategories.Add(category);
                itemSortOrders[category.Id] = 0;
            }
            var sortOrder = itemSortOrders.GetValueOrDefault(category.Id) + 1;
            itemSortOrders[category.Id] = sortOrder;
            var item = new MenuItem
            {
                RestaurantId = rid,
                CategoryId = category.Id,
                GlobalDrinkId = drink.Id,
                Name = drink.Name,
                Description = drink.Description,
                Price = selection.Price,
                ServingSize = servingSize,
                ImageUrl = null,
                SortOrder = sortOrder,
                IsVisible = selection.IsVisible,
                IsAvailable = selection.IsAvailable
            };
            db.MenuItems.Add(item);
            created.Add(item);
            existingSet.Add(LibraryKey(drink.Id, servingSize));
        }
        await db.SaveChangesAsync(ct);
        return created;
    }
    public async Task<SpecialOffer?> SaveOfferAsync(Guid rid, Guid? id, SpecialOfferRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) throw new InvalidOperationException("Offer title is required.");
        if (r.Price < 0 || r.OriginalPrice < 0) throw new InvalidOperationException("Offer prices cannot be negative.");
        if (r.EndsAt < r.StartsAt) throw new InvalidOperationException("Offer end cannot be before its start.");
        var x = id is null ? new SpecialOffer { RestaurantId = rid, Title = r.Title } : await db.SpecialOffers.FirstOrDefaultAsync(x => x.Id == id && x.RestaurantId == rid, ct);
        if (x is null) return null; x.Title = r.Title.Trim(); x.Description = r.Description; x.TitleEn = CleanOptional(r.TitleEn); x.DescriptionEn = CleanOptional(r.DescriptionEn); x.ItemsEn = CleanOptional(r.ItemsEn); x.TitleDe = CleanOptional(r.TitleDe); x.DescriptionDe = CleanOptional(r.DescriptionDe); x.ItemsDe = CleanOptional(r.ItemsDe); x.Price = r.Price; x.OriginalPrice = r.OriginalPrice; x.ImageUrl = AssetUrl.Normalize(r.ImageUrl); x.StartsAt = r.StartsAt; x.EndsAt = r.EndsAt; x.IsVisible = r.IsVisible; x.Kind = r.Kind; x.Items = r.Items; x.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) db.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<bool> SetThemeAsync(Guid rid, ThemeRequest r, CancellationToken ct)
    {
        if (!RestaurantService.IsSupportedTheme(r.ThemeKey)) throw new InvalidOperationException("Selected theme is not supported.");
        var x = await db.ThemeSettings.SingleOrDefaultAsync(x => x.RestaurantId == rid, ct); if (x is null) return false;
        x.ThemeKey = r.ThemeKey; x.PrimaryColor = r.PrimaryColor; x.AccentColor = r.AccentColor; x.BackgroundImageUrl = AssetUrl.Normalize(r.BackgroundImageUrl); x.FontFamily = r.FontFamily; x.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return true;
    }
    public async Task<bool> SetBusinessHoursAsync(Guid rid, IReadOnlyCollection<BusinessHourRequest> r, CancellationToken ct)
    {
        if (r.Select(x => x.DayOfWeek).Distinct().Count() != r.Count) throw new InvalidOperationException("Business hours contain duplicate days.");
        if (r.Any(x => !x.IsClosed && (x.OpensAt is null || x.ClosesAt is null))) throw new InvalidOperationException("Opening and closing time are required for open days.");
        var existing = await db.BusinessHours.Where(x => x.RestaurantId == rid).ToListAsync(ct); db.BusinessHours.RemoveRange(existing);
        db.BusinessHours.AddRange(r.Select(x => new BusinessHour { RestaurantId = rid, DayOfWeek = x.DayOfWeek, OpensAt = x.OpensAt, ClosesAt = x.ClosesAt, IsClosed = x.IsClosed })); await db.SaveChangesAsync(ct); return true;
    }
    public Task<bool> DeleteCategoryAsync(Guid rid, Guid id, CancellationToken ct) => DeleteAsync(db.MenuCategories, rid, id, ct);
    public Task<bool> DeleteItemAsync(Guid rid, Guid id, CancellationToken ct) => DeleteAsync(db.MenuItems, rid, id, ct);
    public Task<bool> DeleteOfferAsync(Guid rid, Guid id, CancellationToken ct) => DeleteAsync(db.SpecialOffers, rid, id, ct);
    private static string? NormalizeServingSize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? CleanOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string LibraryKey(Guid drinkId, string? servingSize) => $"{drinkId:N}:{NormalizeServingSize(servingSize)?.ToLowerInvariant() ?? string.Empty}";
    private async Task<bool> DeleteAsync<T>(DbSet<T> set, Guid rid, Guid id, CancellationToken ct) where T : Entity
    {
        var x = await set.FirstOrDefaultAsync(x => x.Id == id && EF.Property<Guid>(x, "RestaurantId") == rid, ct); if (x is null) return false;
        set.Remove(x); await db.SaveChangesAsync(ct); return true;
    }
}

public sealed class GlobalDrinkService(ApplicationDbContext db) : IGlobalDrinkService
{
    public async Task<IReadOnlyList<AdminGlobalDrink>> GetAllAsync(CancellationToken ct)
    {
        var rows = await db.GlobalDrinks.AsNoTracking()
            .OrderBy(x => x.Category).ThenBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Slug, x.Category, x.Description, x.ImageUrl, x.ServingOptions, x.SortOrder, x.IsActive, x.UpdatedAt })
            .ToListAsync(ct);
        return rows.Select(x => new AdminGlobalDrink(x.Id, x.Name, x.Slug, x.Category, x.Description, AssetUrl.Normalize(x.ImageUrl), x.ServingOptions, x.SortOrder, x.IsActive, x.UpdatedAt)).ToList();
    }

    public async Task<AdminGlobalDrink?> SaveAsync(Guid? id, GlobalDrinkRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new InvalidOperationException("Drink name is required.");
        if (string.IsNullOrWhiteSpace(r.Category)) throw new InvalidOperationException("Drink category is required.");
        if (r.SortOrder < 0) throw new InvalidOperationException("Drink sort order cannot be negative.");
        var slug = NormalizeSlug(string.IsNullOrWhiteSpace(r.Slug) ? r.Name : r.Slug);
        if (string.IsNullOrWhiteSpace(slug)) throw new InvalidOperationException("Drink slug is required.");
        if (await db.GlobalDrinks.AnyAsync(x => x.Slug == slug && x.Id != id, ct)) throw new InvalidOperationException("Drink slug is already in use.");

        var item = id is null ? new GlobalDrink { Name = r.Name.Trim(), Slug = slug } : await db.GlobalDrinks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return null;
        item.Name = r.Name.Trim();
        item.Slug = slug;
        item.Category = r.Category.Trim();
        item.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        item.ImageUrl = AssetUrl.Normalize(r.ImageUrl);
        item.ServingOptions = NormalizeServingOptions(r.ServingOptions);
        item.SortOrder = r.SortOrder;
        item.IsActive = r.IsActive;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) db.GlobalDrinks.Add(item);
        await db.SaveChangesAsync(ct);
        return ToAdmin(item);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var item = await db.GlobalDrinks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return false;
        db.GlobalDrinks.Remove(item);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static AdminGlobalDrink ToAdmin(GlobalDrink x) =>
        new(x.Id, x.Name, x.Slug, x.Category, x.Description, AssetUrl.Normalize(x.ImageUrl), x.ServingOptions, x.SortOrder, x.IsActive, x.UpdatedAt);

    private static string NormalizeSlug(string? value) =>
        Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

    private static string? NormalizeServingOptions(string? value)
    {
        var options = (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return options.Length == 0 ? null : string.Join(", ", options);
    }
}

public sealed class PublicMenuService(ApplicationDbContext db) : IPublicMenuService
{
    public async Task<PublicMenu?> GetAsync(string slug, CancellationToken ct)
    {
        var resolvedSlug = ResolvePublicSlug(slug);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var restaurant = await db.Restaurants.AsNoTracking().AsSplitQuery()
            .Include(x => x.Subscription).Include(x => x.Theme).Include(x => x.BusinessHours)
            .Include(x => x.Categories.Where(c => c.IsVisible).OrderBy(c => c.SortOrder)).ThenInclude(c => c.Items.Where(i => i.IsVisible).OrderBy(i => i.SortOrder)).ThenInclude(i => i.GlobalDrink)
            .Include(x => x.SpecialOffers.Where(o => o.IsVisible && (o.StartsAt == null || o.StartsAt <= now) && (o.EndsAt == null || o.EndsAt >= now)))
            .FirstOrDefaultAsync(x => x.Slug == resolvedSlug && x.Status == RestaurantStatus.Active, ct);
        if (restaurant?.IsPubliclyAvailable(today) != true) return null;
        db.MenuViews.Add(new MenuView { RestaurantId = restaurant.Id, ViewedOn = today, Source = "menu" });
        var itemViews = restaurant.Categories
            .SelectMany(category => category.Items)
            .Where(item => item.IsVisible && item.IsAvailable)
            .Select(item => new MenuItemView { RestaurantId = restaurant.Id, MenuItemId = item.Id, ViewedOn = today, Source = "public-menu" })
            .ToList();
        if (itemViews.Count > 0) db.MenuItemViews.AddRange(itemViews);
        await db.SaveChangesAsync(ct);
        return new PublicMenu(RestaurantService.ToOwnerDetails(restaurant));
    }

    private static string ResolvePublicSlug(string slug) => slug switch
    {
        "demo-meni" => "test",
        _ => slug
    };
}
