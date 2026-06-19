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
        .Select(x => new RestaurantSummary(x.Id, x.Name, x.Slug, x.Status, x.Subscription!.Status, x.Subscription.ExpiresOn)).ToListAsync(ct);

    public Task<Restaurant?> GetAsync(Guid id, Guid? tenantId, bool admin, CancellationToken ct) => db.Restaurants.AsNoTracking()
        .Include(x => x.Subscription).Include(x => x.Theme).Include(x => x.BusinessHours)
        .Include(x => x.Categories).ThenInclude(x => x.Items).Include(x => x.SpecialOffers)
        .FirstOrDefaultAsync(x => x.Id == id && (admin || x.Id == tenantId), ct);

    public async Task<Restaurant> CreateAsync(CreateRestaurantRequest request, CancellationToken ct)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (await db.Restaurants.AnyAsync(x => x.Slug == slug, ct)) throw new InvalidOperationException("Slug is already in use.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var restaurant = new Restaurant { Name = request.Name.Trim(), Slug = slug, Type = request.Type, Status = RestaurantStatus.Active };
        restaurant.Subscription = new Subscription { RestaurantId = restaurant.Id, StartsOn = today, ExpiresOn = today.AddDays(request.TrialDays), Status = SubscriptionStatus.Trial };
        restaurant.Theme = new ThemeSettings { RestaurantId = restaurant.Id };
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
        var x = await db.Restaurants.FirstOrDefaultAsync(x => x.Id == id && (admin || x.Id == tenantId), ct);
        if (x is null) return false;
        x.Name = r.Name.Trim(); x.Description = r.Description; x.LogoUrl = r.LogoUrl; x.CoverImageUrl = r.CoverImageUrl;
        x.Address = r.Address; x.Phone = r.Phone; x.Email = r.Email; x.WebsiteUrl = r.WebsiteUrl; x.InstagramUrl = r.InstagramUrl;
        x.Currency = r.Currency.ToUpperInvariant(); x.DefaultLanguage = r.DefaultLanguage.ToLowerInvariant(); x.Type = r.Type; x.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct); return true;
    }

    public async Task<bool> SetStatusAsync(Guid id, RestaurantStatus status, CancellationToken ct)
    {
        var x = await db.Restaurants.FindAsync([id], ct); if (x is null) return false;
        x.Status = status; x.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return true;
    }

    public async Task<bool> SetSubscriptionAsync(Guid id, SetSubscriptionRequest r, CancellationToken ct)
    {
        var x = await db.Subscriptions.SingleOrDefaultAsync(x => x.RestaurantId == id, ct); if (x is null) return false;
        x.Status = r.Status; x.Plan = r.Plan; x.StartsOn = r.StartsOn; x.ExpiresOn = r.ExpiresOn; x.GracePeriodEndsOn = r.GracePeriodEndsOn; x.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct); return true;
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
