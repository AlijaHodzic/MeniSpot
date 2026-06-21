using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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

public sealed class AuthService(UserManager<ApplicationUser> users, IOptions<JwtOptions> options) : IAuthService
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || !await users.CheckPasswordAsync(user, request.Password)) return null;
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

    public Task<Restaurant?> GetAsync(Guid id, Guid? tenantId, bool admin, CancellationToken ct) => db.Restaurants.AsNoTracking()
        .Include(x => x.Subscription).Include(x => x.Theme).Include(x => x.BusinessHours)
        .Include(x => x.Categories).ThenInclude(x => x.Items).Include(x => x.SpecialOffers)
        .FirstOrDefaultAsync(x => x.Id == id && (admin || x.Id == tenantId), ct);

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
        var x = await db.Restaurants.FindAsync([id], ct); if (x is null) return false;
        x.Status = status; x.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return true;
    }

    public async Task<bool> SetSubscriptionAsync(Guid id, SetSubscriptionRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Plan)) throw new InvalidOperationException("Subscription plan is required.");
        if (r.MonthlyPrice < 0) throw new InvalidOperationException("Monthly price cannot be negative.");
        if (r.ExpiresOn < r.StartsOn) throw new InvalidOperationException("Subscription expiry cannot be before its start date.");
        if (r.GracePeriodEndsOn < r.ExpiresOn) throw new InvalidOperationException("Grace period cannot end before the subscription expiry date.");
        var x = await db.Subscriptions.SingleOrDefaultAsync(x => x.RestaurantId == id, ct); if (x is null) return false;
        x.Status = r.Status; x.Plan = r.Plan; x.MonthlyPrice = r.MonthlyPrice; x.StartsOn = r.StartsOn; x.ExpiresOn = r.ExpiresOn; x.GracePeriodEndsOn = r.GracePeriodEndsOn; x.UpdatedAt = DateTimeOffset.UtcNow;
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
        EstablishmentType.Bar or EstablishmentType.Club => "modern-dark",
        _ => "classic-light"
    };

    private static string NormalizeThemeKey(string? key, EstablishmentType type) =>
        string.IsNullOrWhiteSpace(key) || key == "restaurant" ? DefaultThemeKey(type) : key;

    private static readonly string[] SupportedThemes = ["modern-dark", "classic-light", "premium-gold", "natural-green"];

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
        restaurant.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new PaymentHistoryItem(payment.Id, payment.Amount, payment.Currency, payment.PaidOn, payment.PeriodStartsOn, payment.PeriodEndsOn, payment.CoverageMonths, payment.Method, payment.Reference, payment.Note, payment.CreatedAt);
    }

    private async Task SynchronizeExpiredSubscriptionsAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var subscriptions = await db.Subscriptions.Where(x =>
            x.ExpiresOn < today && (x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.Trial || x.Status == SubscriptionStatus.Overdue)).ToListAsync(ct);
        foreach (var subscription in subscriptions)
        {
            subscription.Status = subscription.GracePeriodEndsOn >= today ? SubscriptionStatus.Overdue : SubscriptionStatus.Suspended;
            subscription.UpdatedAt = DateTimeOffset.UtcNow;
        }
        if (subscriptions.Count > 0) await db.SaveChangesAsync(ct);
    }
}

public sealed class MenuManagementService(ApplicationDbContext db) : IMenuManagementService
{
    public async Task<MenuCategory?> SaveCategoryAsync(Guid rid, Guid? id, CategoryRequest r, CancellationToken ct)
    {
        var x = id is null ? new MenuCategory { RestaurantId = rid, Name = r.Name } : await db.MenuCategories.FirstOrDefaultAsync(x => x.Id == id && x.RestaurantId == rid, ct);
        if (x is null) return null; x.Name = r.Name.Trim(); x.Description = r.Description; x.SortOrder = r.SortOrder; x.IsVisible = r.IsVisible; x.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) db.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<MenuItem?> SaveItemAsync(Guid rid, Guid? id, MenuItemRequest r, CancellationToken ct)
    {
        if (!await db.MenuCategories.AnyAsync(x => x.Id == r.CategoryId && x.RestaurantId == rid, ct)) return null;
        var x = id is null ? new MenuItem { RestaurantId = rid, CategoryId = r.CategoryId, Name = r.Name } : await db.MenuItems.FirstOrDefaultAsync(x => x.Id == id && x.RestaurantId == rid, ct);
        if (x is null) return null; x.CategoryId = r.CategoryId; x.Name = r.Name.Trim(); x.Description = r.Description; x.Price = r.Price; x.ImageUrl = r.ImageUrl; x.Allergens = r.Allergens; x.SortOrder = r.SortOrder; x.IsVisible = r.IsVisible; x.IsAvailable = r.IsAvailable; x.IsVegetarian = r.IsVegetarian; x.IsSpicy = r.IsSpicy; x.IsFeatured = r.IsFeatured; x.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) db.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<SpecialOffer?> SaveOfferAsync(Guid rid, Guid? id, SpecialOfferRequest r, CancellationToken ct)
    {
        var x = id is null ? new SpecialOffer { RestaurantId = rid, Title = r.Title } : await db.SpecialOffers.FirstOrDefaultAsync(x => x.Id == id && x.RestaurantId == rid, ct);
        if (x is null) return null; x.Title = r.Title.Trim(); x.Description = r.Description; x.Price = r.Price; x.ImageUrl = r.ImageUrl; x.StartsAt = r.StartsAt; x.EndsAt = r.EndsAt; x.IsVisible = r.IsVisible; x.UpdatedAt = DateTimeOffset.UtcNow;
        if (id is null) db.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<bool> SetThemeAsync(Guid rid, ThemeRequest r, CancellationToken ct)
    {
        var x = await db.ThemeSettings.SingleOrDefaultAsync(x => x.RestaurantId == rid, ct); if (x is null) return false;
        x.ThemeKey = r.ThemeKey; x.PrimaryColor = r.PrimaryColor; x.AccentColor = r.AccentColor; x.BackgroundImageUrl = r.BackgroundImageUrl; x.FontFamily = r.FontFamily; x.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return true;
    }
    public async Task<bool> SetBusinessHoursAsync(Guid rid, IReadOnlyCollection<BusinessHourRequest> r, CancellationToken ct)
    {
        var existing = await db.BusinessHours.Where(x => x.RestaurantId == rid).ToListAsync(ct); db.BusinessHours.RemoveRange(existing);
        db.BusinessHours.AddRange(r.Select(x => new BusinessHour { RestaurantId = rid, DayOfWeek = x.DayOfWeek, OpensAt = x.OpensAt, ClosesAt = x.ClosesAt, IsClosed = x.IsClosed })); await db.SaveChangesAsync(ct); return true;
    }
    public Task<bool> DeleteCategoryAsync(Guid rid, Guid id, CancellationToken ct) => DeleteAsync(db.MenuCategories, rid, id, ct);
    public Task<bool> DeleteItemAsync(Guid rid, Guid id, CancellationToken ct) => DeleteAsync(db.MenuItems, rid, id, ct);
    public Task<bool> DeleteOfferAsync(Guid rid, Guid id, CancellationToken ct) => DeleteAsync(db.SpecialOffers, rid, id, ct);
    private async Task<bool> DeleteAsync<T>(DbSet<T> set, Guid rid, Guid id, CancellationToken ct) where T : Entity
    {
        var x = await set.FirstOrDefaultAsync(x => x.Id == id && EF.Property<Guid>(x, "RestaurantId") == rid, ct); if (x is null) return false;
        set.Remove(x); await db.SaveChangesAsync(ct); return true;
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
            .Include(x => x.Categories.Where(c => c.IsVisible).OrderBy(c => c.SortOrder)).ThenInclude(c => c.Items.Where(i => i.IsVisible).OrderBy(i => i.SortOrder))
            .Include(x => x.SpecialOffers.Where(o => o.IsVisible && (o.StartsAt == null || o.StartsAt <= now) && (o.EndsAt == null || o.EndsAt >= now)))
            .FirstOrDefaultAsync(x => x.Slug == slug && x.Status == RestaurantStatus.Active, ct);
        return restaurant?.Subscription?.IsPubliclyAvailable(today) == true ? new PublicMenu(restaurant) : null;
    }
}
