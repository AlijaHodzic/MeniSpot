using DigitalMenu.Domain;

namespace DigitalMenu.Tests;

public sealed class SubscriptionTests
{
    private static readonly DateOnly Today = new(2026, 6, 19);

    [Theory]
    [InlineData(SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.Trial, true)]
    [InlineData(SubscriptionStatus.Suspended, false)]
    [InlineData(SubscriptionStatus.Cancelled, false)]
    public void Availability_depends_on_status(SubscriptionStatus status, bool expected)
    {
        var subscription = Create(status);
        Assert.Equal(expected, subscription.IsPubliclyAvailable(Today));
    }

    [Fact]
    public void Overdue_subscription_is_available_during_grace_period()
    {
        var subscription = Create(SubscriptionStatus.Overdue);
        subscription.GracePeriodEndsOn = Today.AddDays(3);
        Assert.True(subscription.IsPubliclyAvailable(Today));
    }

    [Fact]
    public void Overdue_subscription_is_unavailable_after_grace_period()
    {
        var subscription = Create(SubscriptionStatus.Overdue);
        subscription.GracePeriodEndsOn = Today.AddDays(-1);
        Assert.False(subscription.IsPubliclyAvailable(Today));
    }

    [Theory]
    [InlineData(RestaurantStatus.Active, true)]
    [InlineData(RestaurantStatus.Draft, false)]
    [InlineData(RestaurantStatus.Suspended, false)]
    [InlineData(RestaurantStatus.Cancelled, false)]
    [InlineData(RestaurantStatus.Archived, false)]
    public void Public_menu_requires_an_active_restaurant(RestaurantStatus status, bool expected)
    {
        var restaurant = new Restaurant { Name = "Test", Slug = "test", Status = status, Subscription = Create(SubscriptionStatus.Active) };
        Assert.Equal(expected, restaurant.IsPubliclyAvailable(Today));
    }

    private static Subscription Create(SubscriptionStatus status) => new()
    {
        RestaurantId = Guid.NewGuid(), Status = status, StartsOn = Today.AddDays(-30), ExpiresOn = Today
    };
}
