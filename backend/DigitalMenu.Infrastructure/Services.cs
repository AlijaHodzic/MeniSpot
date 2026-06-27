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
    public async Task<IReadOnlyList<RestaurantSummary>> GetAllAsync(CancellationToken ct) => await db.Restaurants.AsNoTracking()
        .Where(x => x.Subscription != null).OrderBy(x => x.Name)
        .Select(x => new RestaurantSummary(x.Id, x.Name, x.Slug, x.Type, x.LogoUrl, x.Address, x.Status, x.Subscription!.Plan, x.Subscription.Status, x.Subscription.ExpiresOn)).ToListAsync(ct);

    public async Task<AdminDashboardSummary> GetDashboardAsync(CancellationToken ct)
    {
        var rows = await db.Restaurants.AsNoTracking().Where(x => x.Subscription != null).Select(x => new
        {
            x.Id, x.Name, x.Type, x.Status, x.CreatedAt, x.UpdatedAt,
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
            .Select(x => new AdminRecentRestaurant(x.Id, x.Name, x.Status, x.Plan, x.UpdatedAt)).ToList();
        var themeUsage = rows.GroupBy(x => NormalizeThemeKey(x.ThemeKey, x.Type))
            .Select(x => new AdminThemeUsage(x.Key, x.Count())).OrderByDescending(x => x.Count).ToList();
        return new AdminDashboardSummary(
            rows.Count,
            rows.Count(x => x.Status == RestaurantStatus.Active),
            activeLicenses,
            rows.Count(x => x.SubscriptionStatus == SubscriptionStatus.Trial),
            rows.Count(x => x.ExpiresOn >= today && x.ExpiresOn <= today.AddDays(14)),
            growth, breakdown, recent, themeUsage);
    }

    public async Task<AdminRestaurantDetails?> GetAdminDetailsAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Restaurants.AsNoTracking().Where(x => x.Id == id && x.Subscription != null).Select(x => new
        {
            x.Id, x.Name, x.Slug, x.Description, x.LogoUrl, x.CoverImageUrl, x.Address, x.Phone, x.Email,
            x.WebsiteUrl, x.InstagramUrl, x.Currency, x.DefaultLanguage, x.Type, x.Status,
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
            item.Id, item.Name, item.Slug, item.Description, item.LogoUrl, item.CoverImageUrl, item.Address, item.Phone,
            item.Email, item.WebsiteUrl, item.InstagramUrl, item.Currency, item.DefaultLanguage, item.Type, item.Status,
            NormalizeThemeKey(item.ThemeKey, item.Type), ownerEmail,
            new AdminSubscriptionDetails(item.SubscriptionStatus, item.Plan, item.MonthlyPrice, item.StartsOn, item.ExpiresOn, item.GracePeriodEndsOn));
    }

    public async Task<OwnerRestaurantDetails?> GetAsync(Guid id, Guid? tenantId, bool admin, CancellationToken ct)
    {
        var restaurant = await db.Restaurants.AsNoTracking().AsSplitQuery()
        .Include(x => x.Subscription).Include(x => x.Theme).Include(x => x.BusinessHours)
        .Include(x => x.Categories).ThenInclude(x => x.Items).ThenInclude(x => x.GlobalDrink).Include(x => x.SpecialOffers)
        .FirstOrDefaultAsync(x => x.Id == id && (admin || x.Id == tenantId), ct);
        return restaurant is null ? null : ToOwnerDetails(restaurant);
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
            LogoUrl = request.LogoUrl,
            CoverImageUrl = request.CoverImageUrl,
            Address = request.Address,
            Phone = request.Phone,
            Email = request.Email,
            WebsiteUrl = request.WebsiteUrl,
            InstagramUrl = request.InstagramUrl,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            DefaultLanguage = request.DefaultLanguage.Trim().ToLowerInvariant()
        };
        restaurant.Subscription = new Subscription { RestaurantId = restaurant.Id, StartsOn = today, ExpiresOn = today.AddDays(request.TrialDays), Status = SubscriptionStatus.Trial, MonthlyPrice = 39.90m };
        restaurant.Theme = new ThemeSettings { RestaurantId = restaurant.Id, ThemeKey = request.ThemeKey };
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
        var x = await db.Restaurants.Include(x => x.Theme).FirstOrDefaultAsync(x => x.Id == id && (admin || x.Id == tenantId), ct);
        if (x is null) return false;
        if (!SupportedThemes.Contains(r.ThemeKey)) throw new InvalidOperationException("Selected theme is not supported.");
        if (admin && !string.IsNullOrWhiteSpace(r.Slug))
        {
            var slug = NormalizeRestaurantSlug(r.Slug);
            if (string.IsNullOrWhiteSpace(slug)) throw new InvalidOperationException("Restaurant slug is required.");
            if (await db.Restaurants.AnyAsync(item => item.Id != x.Id && item.Slug == slug, ct)) throw new InvalidOperationException("Slug is already in use.");
            x.Slug = slug;
        }
        x.Name = r.Name.Trim(); x.Description = r.Description; x.LogoUrl = r.LogoUrl; x.CoverImageUrl = r.CoverImageUrl;
        x.Address = r.Address; x.Phone = r.Phone; x.Email = r.Email; x.WebsiteUrl = r.WebsiteUrl; x.InstagramUrl = r.InstagramUrl;
        x.Currency = r.Currency.ToUpperInvariant(); x.DefaultLanguage = r.DefaultLanguage.ToLowerInvariant(); x.Type = r.Type;
        if (x.Theme is null) { x.Theme = new ThemeSettings { RestaurantId = x.Id, ThemeKey = r.ThemeKey }; }
        else x.Theme.ThemeKey = r.ThemeKey;
        x.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct); return true;
    }

    public async Task<bool> SetStatusAsync(Guid id, RestaurantStatus status, CancellationToken ct)
    {
        var x = await db.Restaurants.Include(x => x.Subscription).FirstOrDefaultAsync(x => x.Id == id, ct); if (x is null) return false;
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
        if (string.IsNullOrWhiteSpace(r.Plan)) throw new InvalidOperationException("Subscription plan is required.");
        if (r.MonthlyPrice < 0) throw new InvalidOperationException("Monthly price cannot be negative.");
        if (r.ExpiresOn < r.StartsOn) throw new InvalidOperationException("Subscription expiry cannot be before its start date.");
        if (r.GracePeriodEndsOn < r.ExpiresOn) throw new InvalidOperationException("Grace period cannot end before the subscription expiry date.");
        var x = await db.Subscriptions.Include(x => x.Restaurant).SingleOrDefaultAsync(x => x.RestaurantId == id, ct); if (x is null) return false;
        x.Status = r.Status; x.Plan = r.Plan; x.MonthlyPrice = r.MonthlyPrice; x.StartsOn = r.StartsOn; x.ExpiresOn = r.ExpiresOn; x.GracePeriodEndsOn = r.GracePeriodEndsOn; x.UpdatedAt = DateTimeOffset.UtcNow;
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

    private static string DefaultThemeKey(EstablishmentType type) => type switch
    {
        EstablishmentType.Cafe => "natural-green",
        EstablishmentType.Bar or EstablishmentType.Club or EstablishmentType.ShishaBar => "modern-dark",
        _ => "classic-light"
    };

    private static string NormalizeThemeKey(string? key, EstablishmentType type) =>
        string.IsNullOrWhiteSpace(key) || key == "restaurant" ? DefaultThemeKey(type) : key;

    private static string NormalizeRestaurantSlug(string? value) =>
        Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

    private static readonly string[] SupportedThemes = ["modern-dark", "classic-light", "premium-gold", "natural-green"];

    internal static OwnerRestaurantDetails ToOwnerDetails(Restaurant restaurant) => new(
        restaurant.Id, restaurant.Name, restaurant.Slug, restaurant.Description, restaurant.LogoUrl, restaurant.CoverImageUrl,
        restaurant.Address, restaurant.Phone, restaurant.Email, restaurant.WebsiteUrl, restaurant.InstagramUrl,
        restaurant.Currency, restaurant.DefaultLanguage, restaurant.Type, restaurant.Status,
        new OwnerTheme(restaurant.Theme?.ThemeKey ?? NormalizeThemeKey(null, restaurant.Type), restaurant.Theme?.PrimaryColor ?? "#111827", restaurant.Theme?.AccentColor ?? "#84cc16", restaurant.Theme?.BackgroundImageUrl, restaurant.Theme?.FontFamily ?? "Inter"),
        restaurant.BusinessHours.OrderBy(x => x.DayOfWeek).Select(x => new OwnerBusinessHour(x.DayOfWeek, x.OpensAt, x.ClosesAt, x.IsClosed)).ToList(),
        restaurant.Categories.OrderBy(x => x.SortOrder).Select(x => new OwnerMenuCategory(x.Id, x.Name, x.Description, x.SortOrder, x.IsVisible,
            x.Items.OrderBy(i => i.SortOrder).Select(i => new OwnerMenuItem(i.Id, i.CategoryId, i.GlobalDrinkId, i.Name, i.Description, i.Price, i.ServingSize, i.ImageUrl ?? (i.GlobalDrink == null ? null : i.GlobalDrink.ImageUrl), i.Allergens, i.SortOrder, i.IsVisible, i.IsAvailable, i.IsVegetarian, i.IsSpicy, i.IsFeatured)).ToList())).ToList(),
        restaurant.SpecialOffers.OrderByDescending(x => x.CreatedAt).Select(x => new OwnerSpecialOffer(x.Id, x.Title, x.Description, x.Price, x.OriginalPrice, x.ImageUrl, x.StartsAt, x.EndsAt, x.IsVisible, x.Kind, x.Items)).ToList());

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

public sealed class BillingService(ApplicationDbContext db) : IBillingService
{
    public async Task<BillingOverview> GetOverviewAsync(CancellationToken ct)
    {
        await SynchronizeExpiredSubscriptionsAsync(ct);
        var accounts = await db.Restaurants.AsNoTracking().Where(x => x.Subscription != null).OrderBy(x => x.Name)
            .Select(x => new BillingAccountSummary(
                x.Id, x.Name, x.Slug, x.Subscription!.Plan, x.Subscription.MonthlyPrice, x.Currency,
                x.Subscription.Status, x.Subscription.ExpiresOn, x.Subscription.GracePeriodEndsOn,
                x.Payments.OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.CreatedAt).Select(p => (DateOnly?)p.PaidOn).FirstOrDefault(),
                x.Payments.OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.CreatedAt).Select(p => (decimal?)p.Amount).FirstOrDefault()))
            .ToListAsync(ct);
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
        if (!await db.Restaurants.AnyAsync(x => x.Id == restaurantId, ct)) return null;
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
        var restaurant = await db.Restaurants.Include(x => x.Subscription).FirstOrDefaultAsync(x => x.Id == restaurantId, ct);
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
        var restaurants = await db.Restaurants.Include(x => x.Subscription).Where(x => x.Subscription != null).ToListAsync(ct);
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
}

public sealed class MenuManagementService(ApplicationDbContext db) : IMenuManagementService
{
    public async Task<MenuCategory?> SaveCategoryAsync(Guid rid, Guid? id, CategoryRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new InvalidOperationException("Category name is required.");
        if (r.SortOrder < 0) throw new InvalidOperationException("Category sort order cannot be negative.");
        var x = id is null ? new MenuCategory { RestaurantId = rid, Name = r.Name } : await db.MenuCategories.FirstOrDefaultAsync(x => x.Id == id && x.RestaurantId == rid, ct);
        if (x is null) return null; x.Name = r.Name.Trim(); x.Description = r.Description; x.SortOrder = r.SortOrder; x.IsVisible = r.IsVisible; x.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) db.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<MenuItem?> SaveItemAsync(Guid rid, Guid? id, MenuItemRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new InvalidOperationException("Menu item name is required.");
        if (r.Price < 0) throw new InvalidOperationException("Menu item price cannot be negative.");
        if (r.SortOrder < 0) throw new InvalidOperationException("Menu item sort order cannot be negative.");
        if (!await db.MenuCategories.AnyAsync(x => x.Id == r.CategoryId && x.RestaurantId == rid, ct)) return null;
        var x = id is null ? new MenuItem { RestaurantId = rid, CategoryId = r.CategoryId, Name = r.Name } : await db.MenuItems.FirstOrDefaultAsync(x => x.Id == id && x.RestaurantId == rid, ct);
        if (x is null) return null; x.CategoryId = r.CategoryId; x.Name = r.Name.Trim(); x.Description = r.Description; x.Price = r.Price; x.ServingSize = NormalizeServingSize(r.ServingSize); x.ImageUrl = r.ImageUrl; x.Allergens = r.Allergens; x.SortOrder = r.SortOrder; x.IsVisible = r.IsVisible; x.IsAvailable = r.IsAvailable; x.IsVegetarian = r.IsVegetarian; x.IsSpicy = r.IsSpicy; x.IsFeatured = r.IsFeatured; x.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) db.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<IReadOnlyList<GlobalDrinkSummary>> GetDrinkLibraryAsync(CancellationToken ct) =>
        await db.GlobalDrinks.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Category).ThenBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new GlobalDrinkSummary(x.Id, x.Name, x.Category, x.Description, x.ImageUrl, x.ServingOptions, x.SortOrder)).ToListAsync(ct);

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
            var category = selectedCategory ?? categories.FirstOrDefault(x => string.Equals(x.Name, drink.Category, StringComparison.OrdinalIgnoreCase));
            if (category is null)
            {
                category = new MenuCategory { RestaurantId = rid, Name = drink.Category, Description = $"Ponuda: {drink.Category}", SortOrder = ++categorySort, IsVisible = true };
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
        if (x is null) return null; x.Title = r.Title.Trim(); x.Description = r.Description; x.Price = r.Price; x.OriginalPrice = r.OriginalPrice; x.ImageUrl = r.ImageUrl; x.StartsAt = r.StartsAt; x.EndsAt = r.EndsAt; x.IsVisible = r.IsVisible; x.Kind = r.Kind; x.Items = r.Items; x.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) db.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<bool> SetThemeAsync(Guid rid, ThemeRequest r, CancellationToken ct)
    {
        var x = await db.ThemeSettings.SingleOrDefaultAsync(x => x.RestaurantId == rid, ct); if (x is null) return false;
        x.ThemeKey = r.ThemeKey; x.PrimaryColor = r.PrimaryColor; x.AccentColor = r.AccentColor; x.BackgroundImageUrl = r.BackgroundImageUrl; x.FontFamily = r.FontFamily; x.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return true;
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
    private static string LibraryKey(Guid drinkId, string? servingSize) => $"{drinkId:N}:{NormalizeServingSize(servingSize)?.ToLowerInvariant() ?? string.Empty}";
    private async Task<bool> DeleteAsync<T>(DbSet<T> set, Guid rid, Guid id, CancellationToken ct) where T : Entity
    {
        var x = await set.FirstOrDefaultAsync(x => x.Id == id && EF.Property<Guid>(x, "RestaurantId") == rid, ct); if (x is null) return false;
        set.Remove(x); await db.SaveChangesAsync(ct); return true;
    }
}

public sealed class GlobalDrinkService(ApplicationDbContext db) : IGlobalDrinkService
{
    public async Task<IReadOnlyList<AdminGlobalDrink>> GetAllAsync(CancellationToken ct) =>
        await db.GlobalDrinks.AsNoTracking()
            .OrderBy(x => x.Category).ThenBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new AdminGlobalDrink(x.Id, x.Name, x.Slug, x.Category, x.Description, x.ImageUrl, x.ServingOptions, x.SortOrder, x.IsActive, x.UpdatedAt))
            .ToListAsync(ct);

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
        item.ImageUrl = string.IsNullOrWhiteSpace(r.ImageUrl) ? null : r.ImageUrl.Trim();
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
        item.IsActive = false;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static AdminGlobalDrink ToAdmin(GlobalDrink x) =>
        new(x.Id, x.Name, x.Slug, x.Category, x.Description, x.ImageUrl, x.ServingOptions, x.SortOrder, x.IsActive, x.UpdatedAt);

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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;
        var restaurant = await db.Restaurants.AsNoTracking().AsSplitQuery()
            .Include(x => x.Subscription).Include(x => x.Theme).Include(x => x.BusinessHours)
            .Include(x => x.Categories.Where(c => c.IsVisible).OrderBy(c => c.SortOrder)).ThenInclude(c => c.Items.Where(i => i.IsVisible).OrderBy(i => i.SortOrder)).ThenInclude(i => i.GlobalDrink)
            .Include(x => x.SpecialOffers.Where(o => o.IsVisible && (o.StartsAt == null || o.StartsAt <= now) && (o.EndsAt == null || o.EndsAt >= now)))
            .FirstOrDefaultAsync(x => x.Slug == slug && x.Status == RestaurantStatus.Active, ct);
        return restaurant?.IsPubliclyAvailable(today) == true ? new PublicMenu(RestaurantService.ToOwnerDetails(restaurant)) : null;
    }
}
