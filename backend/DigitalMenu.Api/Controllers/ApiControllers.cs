using System.Security.Claims;
using DigitalMenu.Application;
using DigitalMenu.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalMenu.Api.Controllers;

[ApiController]
public abstract class ApiController : ControllerBase
{
    protected Guid? RestaurantId => Guid.TryParse(User.FindFirstValue("restaurant_id"), out var id) ? id : null;
    protected bool IsSuperAdmin => User.IsInRole(Roles.SuperAdmin);
    protected ActionResult MissingTenant() => Forbid();
}

[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ApiController
{
    [HttpPost("login"), AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
        => await auth.LoginAsync(request, ct) is { } result ? Ok(result) : Unauthorized();
}

[Route("api/admin/restaurants"), Authorize(Roles = Roles.SuperAdmin)]
public sealed class AdminRestaurantsController(IRestaurantService restaurants) : ApiController
{
    [HttpGet] public async Task<ActionResult> All(CancellationToken ct) => Ok(await restaurants.GetAllAsync(ct));
    [HttpGet("dashboard")] public async Task<ActionResult> Dashboard(CancellationToken ct) => Ok(await restaurants.GetDashboardAsync(ct));
    [HttpGet("{id:guid}")] public async Task<ActionResult> One(Guid id, CancellationToken ct) => await restaurants.GetAdminDetailsAsync(id, ct) is { } x ? Ok(x) : NotFound();
    [HttpPost] public async Task<ActionResult> Create(CreateRestaurantRequest request, CancellationToken ct) { var x = await restaurants.CreateAsync(request, ct); return CreatedAtAction(nameof(One), new { id = x.Id }, await restaurants.GetAdminDetailsAsync(x.Id, ct)); }
    [HttpPut("{id:guid}")] public async Task<ActionResult> Update(Guid id, UpdateRestaurantRequest request, CancellationToken ct) => await restaurants.UpdateAsync(id, request, null, true, ct) ? NoContent() : NotFound();
    [HttpPut("{id:guid}/status")] public async Task<ActionResult> Status(Guid id, [FromBody] RestaurantStatus status, CancellationToken ct) => await restaurants.SetStatusAsync(id, status, ct) ? NoContent() : NotFound();
    [HttpPut("{id:guid}/subscription")] public async Task<ActionResult> Subscription(Guid id, SetSubscriptionRequest request, CancellationToken ct) => await restaurants.SetSubscriptionAsync(id, request, ct) ? NoContent() : NotFound();
    [HttpPut("{id:guid}/owner-access")] public async Task<ActionResult> OwnerAccess(Guid id, UpdateOwnerAccessRequest request, CancellationToken ct) => await restaurants.UpdateOwnerAccessAsync(id, request, ct) ? NoContent() : NotFound();
}

[Route("api/admin/billing"), Authorize(Roles = Roles.SuperAdmin)]
public sealed class AdminBillingController(IBillingService billing) : ApiController
{
    [HttpGet] public async Task<ActionResult> Overview(CancellationToken ct) => Ok(await billing.GetOverviewAsync(ct));
    [HttpGet("{restaurantId:guid}/payments")] public async Task<ActionResult> History(Guid restaurantId, CancellationToken ct) => await billing.GetHistoryAsync(restaurantId, ct) is { } items ? Ok(items) : NotFound();
    [HttpPost("{restaurantId:guid}/payments")] public async Task<ActionResult> Record(Guid restaurantId, RecordManualPaymentRequest request, CancellationToken ct) => await billing.RecordPaymentAsync(restaurantId, request, ct) is { } item ? Ok(item) : NotFound();
}

[Route("api/restaurant"), Authorize(Roles = Roles.RestaurantOwner + "," + Roles.RestaurantStaff)]
public sealed class RestaurantController(IRestaurantService restaurants, IMenuManagementService menu, IWebHostEnvironment environment) : ApiController
{
    [HttpGet] public async Task<ActionResult> Get(CancellationToken ct) => RestaurantId is { } rid && await restaurants.GetAsync(rid, rid, false, ct) is { } x ? Ok(x) : MissingTenant();
    [HttpPut] public async Task<ActionResult> Update(UpdateRestaurantRequest request, CancellationToken ct) => RestaurantId is { } rid && await restaurants.UpdateAsync(rid, request, rid, false, ct) ? NoContent() : MissingTenant();
    [HttpPost("categories")] public async Task<ActionResult> AddCategory(CategoryRequest request, CancellationToken ct) => RestaurantId is { } rid ? Ok(await menu.SaveCategoryAsync(rid, null, request, ct)) : MissingTenant();
    [HttpPut("categories/{id:guid}")] public async Task<ActionResult> EditCategory(Guid id, CategoryRequest request, CancellationToken ct) => RestaurantId is { } rid && await menu.SaveCategoryAsync(rid, id, request, ct) is { } x ? Ok(x) : NotFound();
    [HttpDelete("categories/{id:guid}")] public async Task<ActionResult> DeleteCategory(Guid id, CancellationToken ct) => RestaurantId is { } rid && await menu.DeleteCategoryAsync(rid, id, ct) ? NoContent() : NotFound();
    [HttpPost("items")] public async Task<ActionResult> AddItem(MenuItemRequest request, CancellationToken ct) => RestaurantId is { } rid && await menu.SaveItemAsync(rid, null, request, ct) is { } x ? Ok(x) : BadRequest();
    [HttpPut("items/{id:guid}")] public async Task<ActionResult> EditItem(Guid id, MenuItemRequest request, CancellationToken ct) => RestaurantId is { } rid && await menu.SaveItemAsync(rid, id, request, ct) is { } x ? Ok(x) : NotFound();
    [HttpDelete("items/{id:guid}")] public async Task<ActionResult> DeleteItem(Guid id, CancellationToken ct) => RestaurantId is { } rid && await menu.DeleteItemAsync(rid, id, ct) ? NoContent() : NotFound();
    [HttpPost("offers")] public async Task<ActionResult> AddOffer(SpecialOfferRequest request, CancellationToken ct) => RestaurantId is { } rid ? Ok(await menu.SaveOfferAsync(rid, null, request, ct)) : MissingTenant();
    [HttpPut("offers/{id:guid}")] public async Task<ActionResult> EditOffer(Guid id, SpecialOfferRequest request, CancellationToken ct) => RestaurantId is { } rid && await menu.SaveOfferAsync(rid, id, request, ct) is { } x ? Ok(x) : NotFound();
    [HttpDelete("offers/{id:guid}")] public async Task<ActionResult> DeleteOffer(Guid id, CancellationToken ct) => RestaurantId is { } rid && await menu.DeleteOfferAsync(rid, id, ct) ? NoContent() : NotFound();
    [HttpPut("theme")] public async Task<ActionResult> Theme(ThemeRequest request, CancellationToken ct) => RestaurantId is { } rid && await menu.SetThemeAsync(rid, request, ct) ? NoContent() : NotFound();
    [HttpPut("business-hours")] public async Task<ActionResult> Hours(IReadOnlyCollection<BusinessHourRequest> request, CancellationToken ct) => RestaurantId is { } rid && await menu.SetBusinessHoursAsync(rid, request, ct) ? NoContent() : MissingTenant();
    [HttpPost("images"), RequestSizeLimit(6_000_000)]
    public async Task<ActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (RestaurantId is not { } rid) return MissingTenant();
        if (file.Length is <= 0 or > 5_242_880) return BadRequest("Image must be smaller than 5 MB.");
        var extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp" };
        if (!extensions.TryGetValue(file.ContentType, out var extension)) return BadRequest("Only JPEG, PNG and WebP images are supported.");
        var relativeDirectory = Path.Combine("uploads", rid.ToString("N"));
        var directory = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), relativeDirectory);
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        await using var stream = System.IO.File.Create(Path.Combine(directory, fileName));
        await file.CopyToAsync(stream, ct);
        var path = $"/{relativeDirectory.Replace('\\', '/')}/{fileName}";
        return Ok(new { Url = $"{Request.Scheme}://{Request.Host}{path}" });
    }
}

[Route("api/public/menus")]
public sealed class PublicMenusController(IPublicMenuService menus) : ApiController
{
    [HttpGet("{slug}"), AllowAnonymous] public async Task<ActionResult> Get(string slug, CancellationToken ct) => await menus.GetAsync(slug.Trim().ToLowerInvariant(), ct) is { } x ? Ok(x) : NotFound();
}
