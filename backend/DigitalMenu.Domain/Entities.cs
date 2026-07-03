namespace DigitalMenu.Domain;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum RestaurantStatus { Draft, Active, Suspended, Cancelled }
public enum SubscriptionStatus { Trial, Active, Overdue, Suspended, Cancelled }
public enum EstablishmentType { Restaurant, Cafe, Bar, Club, FastFood, Other, ShishaBar }
public enum PaymentMethod { BankTransfer, Cash, Card, Other }
public enum SpecialOfferKind { Promotion, DailyMenu }
public enum MenuCategoryType { Food, Drink }
public enum SupportTicketType { MenuChange, Image, Theme, TechnicalProblem, Other }
public enum SupportTicketPriority { Normal, Urgent }
public enum SupportTicketStatus { New, InProgress, Resolved, Closed }
public enum LeadStatus { New, Contacted, Won, Lost }

public sealed class Restaurant : Entity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string Currency { get; set; } = "BAM";
    public string DefaultLanguage { get; set; } = "bs";
    public string EnabledLanguages { get; set; } = "bs,en";
    public EstablishmentType Type { get; set; }
    public RestaurantStatus Status { get; set; } = RestaurantStatus.Draft;
    public Subscription? Subscription { get; set; }
    public ThemeSettings? Theme { get; set; }
    public ICollection<MenuCategory> Categories { get; set; } = [];
    public ICollection<SpecialOffer> SpecialOffers { get; set; } = [];
    public ICollection<BusinessHour> BusinessHours { get; set; } = [];
    public ICollection<SubscriptionPayment> Payments { get; set; } = [];
    public ICollection<MenuView> MenuViews { get; set; } = [];
    public ICollection<MenuItemView> MenuItemViews { get; set; } = [];
    public ICollection<SupportTicket> SupportTickets { get; set; } = [];

    public bool IsPubliclyAvailable(DateOnly today) => Status == RestaurantStatus.Active && Subscription?.IsPubliclyAvailable(today) == true;
}

public sealed class Subscription : Entity
{
    public Guid RestaurantId { get; set; }
    public string Plan { get; set; } = "Start";
    public decimal MonthlyPrice { get; set; } = 29m;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;
    public DateOnly StartsOn { get; set; }
    public DateOnly ExpiresOn { get; set; }
    public DateOnly? GracePeriodEndsOn { get; set; }
    public Restaurant Restaurant { get; set; } = null!;

    public bool IsPubliclyAvailable(DateOnly today) => Status is SubscriptionStatus.Active or SubscriptionStatus.Trial || Status == SubscriptionStatus.Overdue && GracePeriodEndsOn >= today;
}

public sealed class SubscriptionPayment : Entity
{
    public Guid RestaurantId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BAM";
    public DateOnly PaidOn { get; set; }
    public DateOnly PeriodStartsOn { get; set; }
    public DateOnly PeriodEndsOn { get; set; }
    public int CoverageMonths { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public Restaurant Restaurant { get; set; } = null!;
}

public sealed class MenuView : Entity
{
    public Guid RestaurantId { get; set; }
    public DateOnly ViewedOn { get; set; }
    public string? Source { get; set; }
    public Restaurant Restaurant { get; set; } = null!;
}

public sealed class MenuItemView : Entity
{
    public Guid RestaurantId { get; set; }
    public Guid MenuItemId { get; set; }
    public DateOnly ViewedOn { get; set; }
    public string? Source { get; set; }
    public Restaurant Restaurant { get; set; } = null!;
    public MenuItem MenuItem { get; set; } = null!;
}

public sealed class Lead : Entity
{
    public string BusinessName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Message { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
}

public sealed class SupportTicket : Entity
{
    public Guid RestaurantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public SupportTicketType Type { get; set; } = SupportTicketType.Other;
    public SupportTicketPriority Priority { get; set; } = SupportTicketPriority.Normal;
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.New;
    public string Message { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Restaurant Restaurant { get; set; } = null!;
}

public sealed class ThemeSettings : Entity
{
    public Guid RestaurantId { get; set; }
    public string ThemeKey { get; set; } = "restaurant";
    public string PrimaryColor { get; set; } = "#111827";
    public string AccentColor { get; set; } = "#F59E0B";
    public string? BackgroundImageUrl { get; set; }
    public string FontFamily { get; set; } = "Inter";
    public Restaurant Restaurant { get; set; } = null!;
}

public sealed class MenuCategory : Entity
{
    public Guid RestaurantId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? NameEn { get; set; }
    public string? DescriptionEn { get; set; }
    public string? NameDe { get; set; }
    public string? DescriptionDe { get; set; }
    public MenuCategoryType Type { get; set; } = MenuCategoryType.Food;
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public Restaurant Restaurant { get; set; } = null!;
    public ICollection<MenuItem> Items { get; set; } = [];
}

public sealed class MenuItem : Entity
{
    public Guid RestaurantId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? GlobalDrinkId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? NameEn { get; set; }
    public string? DescriptionEn { get; set; }
    public string? NameDe { get; set; }
    public string? DescriptionDe { get; set; }
    public decimal Price { get; set; }
    public string? ServingSize { get; set; }
    public string? ImageUrl { get; set; }
    public string? Allergens { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
    public bool IsVegetarian { get; set; }
    public bool IsSpicy { get; set; }
    public bool IsFeatured { get; set; }
    public MenuCategory Category { get; set; } = null!;
    public GlobalDrink? GlobalDrink { get; set; }
    public ICollection<MenuItemView> Views { get; set; } = [];
}

public sealed class GlobalDrink : Entity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string Category { get; set; } = "Pića";
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? ServingOptions { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class SpecialOffer : Entity
{
    public Guid RestaurantId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? TitleEn { get; set; }
    public string? DescriptionEn { get; set; }
    public string? ItemsEn { get; set; }
    public string? TitleDe { get; set; }
    public string? DescriptionDe { get; set; }
    public string? ItemsDe { get; set; }
    public decimal? Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public SpecialOfferKind Kind { get; set; }
    public string? Items { get; set; }
    public string? ImageUrl { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public bool IsVisible { get; set; } = true;
    public Restaurant Restaurant { get; set; } = null!;
}

public sealed class BusinessHour : Entity
{
    public Guid RestaurantId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly? OpensAt { get; set; }
    public TimeOnly? ClosesAt { get; set; }
    public bool IsClosed { get; set; }
    public Restaurant Restaurant { get; set; } = null!;
}
