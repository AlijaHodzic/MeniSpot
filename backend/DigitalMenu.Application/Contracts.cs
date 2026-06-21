using DigitalMenu.Domain;

namespace DigitalMenu.Application;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string RestaurantOwner = "RestaurantOwner";
    public const string RestaurantStaff = "RestaurantStaff";
}

public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, string Role, Guid? RestaurantId);
public sealed record CreateRestaurantRequest(
    string Name,
    string Slug,
    EstablishmentType Type,
    RestaurantStatus Status,
    string OwnerEmail,
    string OwnerPassword,
    int TrialDays,
    string? Description,
    string? LogoUrl,
    string? CoverImageUrl,
    string? Address,
    string? Phone,
    string? Email,
    string? WebsiteUrl,
    string? InstagramUrl,
    string Currency,
    string DefaultLanguage,
    string ThemeKey);
public sealed record UpdateRestaurantRequest(string Name, string? Description, string? LogoUrl, string? CoverImageUrl, string? Address, string? Phone, string? Email, string? WebsiteUrl, string? InstagramUrl, string Currency, string DefaultLanguage, EstablishmentType Type, string ThemeKey);
public sealed record UpdateOwnerAccessRequest(string Email, string? NewPassword);
public sealed record SetSubscriptionRequest(SubscriptionStatus Status, string Plan, DateOnly StartsOn, DateOnly ExpiresOn, DateOnly? GracePeriodEndsOn);
public sealed record ThemeRequest(string ThemeKey, string PrimaryColor, string AccentColor, string? BackgroundImageUrl, string FontFamily);
public sealed record CategoryRequest(string Name, string? Description, int SortOrder, bool IsVisible);
public sealed record MenuItemRequest(Guid CategoryId, string Name, string? Description, decimal Price, string? ImageUrl, string? Allergens, int SortOrder, bool IsVisible, bool IsAvailable, bool IsVegetarian, bool IsSpicy, bool IsFeatured);
public sealed record SpecialOfferRequest(string Title, string? Description, decimal? Price, string? ImageUrl, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, bool IsVisible);
public sealed record BusinessHourRequest(DayOfWeek DayOfWeek, TimeOnly? OpensAt, TimeOnly? ClosesAt, bool IsClosed);
public sealed record RestaurantSummary(Guid Id, string Name, string Slug, EstablishmentType Type, string? LogoUrl, string? Address, RestaurantStatus Status, string Plan, SubscriptionStatus SubscriptionStatus, DateOnly ExpiresOn);
public sealed record AdminSubscriptionDetails(SubscriptionStatus Status, string Plan, DateOnly StartsOn, DateOnly ExpiresOn, DateOnly? GracePeriodEndsOn);
public sealed record AdminRestaurantDetails(Guid Id, string Name, string Slug, string? Description, string? LogoUrl, string? CoverImageUrl, string? Address, string? Phone, string? Email, string? WebsiteUrl, string? InstagramUrl, string Currency, string DefaultLanguage, EstablishmentType Type, RestaurantStatus Status, string ThemeKey, string? OwnerEmail, AdminSubscriptionDetails Subscription);
public sealed record AdminGrowthPoint(string Month, int Restaurants);
public sealed record AdminStatusCount(string Status, int Count);
public sealed record AdminRecentRestaurant(Guid Id, string Name, RestaurantStatus Status, string Plan, DateTimeOffset UpdatedAt);
public sealed record AdminThemeUsage(string ThemeKey, int Count);
public sealed record AdminDashboardSummary(int TotalRestaurants, int ActiveRestaurants, int ActiveLicenses, int TrialLicenses, int ExpiringSoon, IReadOnlyList<AdminGrowthPoint> Growth, IReadOnlyList<AdminStatusCount> SubscriptionBreakdown, IReadOnlyList<AdminRecentRestaurant> RecentRestaurants, IReadOnlyList<AdminThemeUsage> ThemeUsage);
public sealed record PublicMenu(Restaurant Restaurant);

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}

public interface IRestaurantService
{
    Task<IReadOnlyList<RestaurantSummary>> GetAllAsync(CancellationToken cancellationToken);
    Task<AdminDashboardSummary> GetDashboardAsync(CancellationToken cancellationToken);
    Task<AdminRestaurantDetails?> GetAdminDetailsAsync(Guid id, CancellationToken cancellationToken);
    Task<Restaurant?> GetAsync(Guid id, Guid? tenantId, bool isSuperAdmin, CancellationToken cancellationToken);
    Task<Restaurant> CreateAsync(CreateRestaurantRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Guid id, UpdateRestaurantRequest request, Guid? tenantId, bool isSuperAdmin, CancellationToken cancellationToken);
    Task<bool> SetStatusAsync(Guid id, RestaurantStatus status, CancellationToken cancellationToken);
    Task<bool> SetSubscriptionAsync(Guid id, SetSubscriptionRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateOwnerAccessAsync(Guid id, UpdateOwnerAccessRequest request, CancellationToken cancellationToken);
}

public interface IMenuManagementService
{
    Task<MenuCategory?> SaveCategoryAsync(Guid restaurantId, Guid? id, CategoryRequest request, CancellationToken cancellationToken);
    Task<MenuItem?> SaveItemAsync(Guid restaurantId, Guid? id, MenuItemRequest request, CancellationToken cancellationToken);
    Task<SpecialOffer?> SaveOfferAsync(Guid restaurantId, Guid? id, SpecialOfferRequest request, CancellationToken cancellationToken);
    Task<bool> SetThemeAsync(Guid restaurantId, ThemeRequest request, CancellationToken cancellationToken);
    Task<bool> SetBusinessHoursAsync(Guid restaurantId, IReadOnlyCollection<BusinessHourRequest> request, CancellationToken cancellationToken);
    Task<bool> DeleteCategoryAsync(Guid restaurantId, Guid id, CancellationToken cancellationToken);
    Task<bool> DeleteItemAsync(Guid restaurantId, Guid id, CancellationToken cancellationToken);
    Task<bool> DeleteOfferAsync(Guid restaurantId, Guid id, CancellationToken cancellationToken);
}

public interface IPublicMenuService
{
    Task<PublicMenu?> GetAsync(string slug, CancellationToken cancellationToken);
}
