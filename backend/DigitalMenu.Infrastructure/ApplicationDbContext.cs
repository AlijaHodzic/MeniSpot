using DigitalMenu.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DigitalMenu.Infrastructure;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid? RestaurantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<ThemeSettings> ThemeSettings => Set<ThemeSettings>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<GlobalDrink> GlobalDrinks => Set<GlobalDrink>();
    public DbSet<SpecialOffer> SpecialOffers => Set<SpecialOffer>();
    public DbSet<BusinessHour> BusinessHours => Set<BusinessHour>();
    public DbSet<SubscriptionPayment> SubscriptionPayments => Set<SubscriptionPayment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("digital_menu");

        builder.Entity<Restaurant>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(160);
            e.Property(x => x.Slug).HasMaxLength(100);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.HasOne(x => x.Subscription).WithOne(x => x.Restaurant).HasForeignKey<Subscription>(x => x.RestaurantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Theme).WithOne(x => x.Restaurant).HasForeignKey<ThemeSettings>(x => x.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<MenuCategory>(e =>
        {
            e.HasIndex(x => new { x.RestaurantId, x.SortOrder });
            e.Property(x => x.Name).HasMaxLength(120);
            e.HasOne(x => x.Restaurant).WithMany(x => x.Categories).HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<MenuItem>(e =>
        {
            e.HasIndex(x => new { x.RestaurantId, x.CategoryId, x.SortOrder });
            e.Property(x => x.Name).HasMaxLength(160);
            e.Property(x => x.ServingSize).HasMaxLength(40);
            e.Property(x => x.Price).HasPrecision(12, 2);
            e.HasOne(x => x.Category).WithMany(x => x.Items).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.GlobalDrink).WithMany().HasForeignKey(x => x.GlobalDrinkId).OnDelete(DeleteBehavior.SetNull);
        });
        builder.Entity<GlobalDrink>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.Category, x.SortOrder });
            e.Property(x => x.Name).HasMaxLength(160);
            e.Property(x => x.Slug).HasMaxLength(120);
            e.Property(x => x.Category).HasMaxLength(80);
            e.Property(x => x.ServingOptions).HasMaxLength(500);
        });
        builder.Entity<SpecialOffer>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(160);
            e.Property(x => x.Items).HasMaxLength(2000);
            e.Property(x => x.Price).HasPrecision(12, 2);
            e.Property(x => x.OriginalPrice).HasPrecision(12, 2);
            e.HasOne(x => x.Restaurant).WithMany(x => x.SpecialOffers).HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<BusinessHour>(e =>
        {
            e.HasIndex(x => new { x.RestaurantId, x.DayOfWeek }).IsUnique();
            e.HasOne(x => x.Restaurant).WithMany(x => x.BusinessHours).HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<SubscriptionPayment>(e =>
        {
            e.HasIndex(x => new { x.RestaurantId, x.PaidOn });
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Reference).HasMaxLength(120);
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasOne(x => x.Restaurant).WithMany(x => x.Payments).HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<Subscription>().Property(x => x.MonthlyPrice).HasPrecision(12, 2);
        builder.Entity<ApplicationUser>().HasIndex(x => x.RestaurantId);
    }
}
