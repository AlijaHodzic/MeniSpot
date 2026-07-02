using System.Security.Claims;
using System.Net.Http.Json;
using DigitalMenu.Application;
using DigitalMenu.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace DigitalMenu.Api.Controllers;

[ApiController]
public abstract class ApiController : ControllerBase
{
    protected Guid? RestaurantId => Guid.TryParse(User.FindFirstValue("restaurant_id"), out var id) ? id : null;
    protected bool IsSuperAdmin => User.IsInRole(Roles.SuperAdmin);
    protected ActionResult MissingTenant() => Forbid();
}

internal static class ImageUploadHelper
{
    private const long MaxUploadBytes = 5_242_880;
    private static readonly HashSet<string> SupportedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public static async Task<ActionResult> SaveOptimizedWebpAsync(
        ControllerBase controller,
        IWebHostEnvironment environment,
        IFormFile file,
        string relativeDirectory,
        CancellationToken ct)
    {
        if (file.Length is <= 0 or > MaxUploadBytes) return controller.BadRequest("Image must be smaller than 5 MB.");
        if (!SupportedContentTypes.Contains(file.ContentType)) return controller.BadRequest("Only JPEG, PNG and WebP images are supported.");

        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var directory = Path.Combine(webRoot, relativeDirectory);
        Directory.CreateDirectory(directory);

        try
        {
            await using var input = file.OpenReadStream();
            using var image = await Image.LoadAsync(input, ct);
            image.Mutate(x => x.AutoOrient().Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(1600, 1600)
            }));

            var fileName = $"{Guid.NewGuid():N}.webp";
            var fullPath = Path.Combine(directory, fileName);
            await image.SaveAsWebpAsync(fullPath, new WebpEncoder { Quality = 78 }, ct);

            var path = $"/{Path.Combine(relativeDirectory, fileName).Replace('\\', '/')}";
            return controller.Ok(new { Url = path });
        }
        catch (UnknownImageFormatException)
        {
            return controller.BadRequest("Only valid JPEG, PNG and WebP images are supported.");
        }
        catch (InvalidImageContentException)
        {
            return controller.BadRequest("The uploaded image is corrupted or unsupported.");
        }
    }
}

[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ApiController
{
    [HttpPost("login"), AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
        => await auth.LoginAsync(request, ct) is { } result ? Ok(result) : Unauthorized();

    [HttpPost("change-password"), Authorize]
    public async Task<ActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdValue, out var userId)) return Unauthorized();

        var result = await auth.ChangePasswordAsync(userId, request, ct);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    }
}

public sealed record LeadRequest(
    string BusinessName,
    string Email,
    string? Phone,
    string Type,
    string? Message,
    string? Website);

[Route("api/leads")]
public sealed class LeadsController(IHttpClientFactory httpClientFactory, IConfiguration configuration) : ApiController
{
    [HttpPost, AllowAnonymous]
    public async Task<ActionResult> Submit(LeadRequest request, CancellationToken ct)
    {
        if (!IsAllowedFrontendRequest()) return BadRequest();

        if (!string.IsNullOrWhiteSpace(request.Website)) return Ok();

        var businessName = request.BusinessName.Trim();
        var email = request.Email.Trim();
        var type = request.Type.Trim();
        if (businessName.Length < 2) return BadRequest();
        if (!email.Contains('@') || !email.Contains('.')) return BadRequest();
        if (string.IsNullOrWhiteSpace(type)) return BadRequest();

        var endpoint = configuration["LeadNotifications:FormspreeEndpoint"];
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            return Problem("Lead notification endpoint is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var payload = new Dictionary<string, string?>
        {
            ["_subject"] = "Novi MeniSpot upit",
            ["Naziv objekta"] = businessName,
            ["Email"] = email,
            ["_replyto"] = email,
            ["email"] = email,
            ["Telefon"] = request.Phone?.Trim(),
            ["Tip objekta"] = type,
            ["Poruka"] = request.Message?.Trim()
        };

        using var formspreeRequest = new HttpRequestMessage(HttpMethod.Post, endpointUri)
        {
            Content = JsonContent.Create(payload)
        };
        formspreeRequest.Headers.Accept.ParseAdd("application/json");
        if (Request.Headers.Origin.FirstOrDefault() is { Length: > 0 } origin)
        {
            formspreeRequest.Headers.TryAddWithoutValidation("Origin", origin);
        }
        if (Request.Headers.Referer.FirstOrDefault() is { Length: > 0 } referer)
        {
            formspreeRequest.Headers.TryAddWithoutValidation("Referer", referer);
        }

        using var response = await httpClientFactory.CreateClient().SendAsync(formspreeRequest, ct);
        return response.IsSuccessStatusCode ? Ok() : Problem("Lead notification failed.", statusCode: StatusCodes.Status502BadGateway);
    }

    private bool IsAllowedFrontendRequest()
    {
        var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length == 0) return true;

        var requestOrigin = Request.Headers.Origin.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(requestOrigin)) return MatchesAllowedOrigin(requestOrigin, allowedOrigins);

        var referer = Request.Headers.Referer.FirstOrDefault();
        return !string.IsNullOrWhiteSpace(referer) && MatchesAllowedOrigin(referer, allowedOrigins);
    }

    private static bool MatchesAllowedOrigin(string value, string[] allowedOrigins)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var requestUri)) return false;
        var requestOrigin = requestUri.GetLeftPart(UriPartial.Authority);
        return allowedOrigins.Any(origin => string.Equals(origin.TrimEnd('/'), requestOrigin, StringComparison.OrdinalIgnoreCase));
    }
}

[Route("api/admin/restaurants"), Authorize(Roles = Roles.SuperAdmin)]
public sealed class AdminRestaurantsController(IRestaurantService restaurants, IAuthService auth) : ApiController
{
    [HttpGet] public async Task<ActionResult> All(CancellationToken ct) => Ok(await restaurants.GetAllAsync(ct));
    [HttpGet("dashboard")] public async Task<ActionResult> Dashboard(CancellationToken ct) => Ok(await restaurants.GetDashboardAsync(ct));
    [HttpGet("{id:guid}")] public async Task<ActionResult> One(Guid id, CancellationToken ct) => await restaurants.GetAdminDetailsAsync(id, ct) is { } x ? Ok(x) : NotFound();
    [HttpPost] public async Task<ActionResult> Create(CreateRestaurantRequest request, CancellationToken ct) { var x = await restaurants.CreateAsync(request, ct); return CreatedAtAction(nameof(One), new { id = x.Id }, await restaurants.GetAdminDetailsAsync(x.Id, ct)); }
    [HttpPut("{id:guid}")] public async Task<ActionResult> Update(Guid id, UpdateRestaurantRequest request, CancellationToken ct) => await restaurants.UpdateAsync(id, request, null, true, ct) ? NoContent() : NotFound();
    [HttpPut("{id:guid}/status")] public async Task<ActionResult> Status(Guid id, [FromBody] RestaurantStatus status, CancellationToken ct) => await restaurants.SetStatusAsync(id, status, ct) ? NoContent() : NotFound();
    [HttpPut("{id:guid}/subscription")] public async Task<ActionResult> Subscription(Guid id, SetSubscriptionRequest request, CancellationToken ct) => await restaurants.SetSubscriptionAsync(id, request, ct) ? NoContent() : NotFound();
    [HttpPut("{id:guid}/owner-access")] public async Task<ActionResult> OwnerAccess(Guid id, UpdateOwnerAccessRequest request, CancellationToken ct) => await restaurants.UpdateOwnerAccessAsync(id, request, ct) ? NoContent() : NotFound();
    [HttpPost("{id:guid}/impersonate")] public async Task<ActionResult<LoginResponse>> Impersonate(Guid id, CancellationToken ct) => await auth.ImpersonateRestaurantOwnerAsync(id, ct) is { } result ? Ok(result) : NotFound();
}

[Route("api/admin/billing"), Authorize(Roles = Roles.SuperAdmin)]
public sealed class AdminBillingController(IBillingService billing) : ApiController
{
    [HttpGet] public async Task<ActionResult> Overview(CancellationToken ct) => Ok(await billing.GetOverviewAsync(ct));
    [HttpGet("{restaurantId:guid}/payments")] public async Task<ActionResult> History(Guid restaurantId, CancellationToken ct) => await billing.GetHistoryAsync(restaurantId, ct) is { } items ? Ok(items) : NotFound();
    [HttpPost("{restaurantId:guid}/payments")] public async Task<ActionResult> Record(Guid restaurantId, RecordManualPaymentRequest request, CancellationToken ct) => await billing.RecordPaymentAsync(restaurantId, request, ct) is { } item ? Ok(item) : NotFound();
}

[Route("api/admin/drinks"), Authorize(Roles = Roles.SuperAdmin)]
public sealed class AdminDrinksController(IGlobalDrinkService drinks, IWebHostEnvironment environment) : ApiController
{
    [HttpGet] public async Task<ActionResult> All(CancellationToken ct) => Ok(await drinks.GetAllAsync(ct));
    [HttpPost] public async Task<ActionResult> Create(GlobalDrinkRequest request, CancellationToken ct) => Ok(await drinks.SaveAsync(null, request, ct));
    [HttpPut("{id:guid}")] public async Task<ActionResult> Update(Guid id, GlobalDrinkRequest request, CancellationToken ct) => await drinks.SaveAsync(id, request, ct) is { } item ? Ok(item) : NotFound();
    [HttpDelete("{id:guid}")] public async Task<ActionResult> Delete(Guid id, CancellationToken ct) => await drinks.DeleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("images"), RequestSizeLimit(6_000_000)]
    public async Task<ActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        return await ImageUploadHelper.SaveOptimizedWebpAsync(this, environment, file, Path.Combine("uploads", "drinks"), ct);
    }
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
    [HttpGet("drink-library")] public async Task<ActionResult> DrinkLibrary(CancellationToken ct) => Ok(await menu.GetDrinkLibraryAsync(ct));
    [HttpPost("drink-library/items")] public async Task<ActionResult> AddLibraryDrinks(AddLibraryDrinksRequest request, CancellationToken ct) => RestaurantId is { } rid ? Ok(await menu.AddLibraryDrinksAsync(rid, request, ct)) : MissingTenant();
    [HttpPost("offers")] public async Task<ActionResult> AddOffer(SpecialOfferRequest request, CancellationToken ct) => RestaurantId is { } rid ? Ok(await menu.SaveOfferAsync(rid, null, request, ct)) : MissingTenant();
    [HttpPut("offers/{id:guid}")] public async Task<ActionResult> EditOffer(Guid id, SpecialOfferRequest request, CancellationToken ct) => RestaurantId is { } rid && await menu.SaveOfferAsync(rid, id, request, ct) is { } x ? Ok(x) : NotFound();
    [HttpDelete("offers/{id:guid}")] public async Task<ActionResult> DeleteOffer(Guid id, CancellationToken ct) => RestaurantId is { } rid && await menu.DeleteOfferAsync(rid, id, ct) ? NoContent() : NotFound();
    [HttpPut("theme")] public async Task<ActionResult> Theme(ThemeRequest request, CancellationToken ct) => RestaurantId is { } rid && await menu.SetThemeAsync(rid, request, ct) ? NoContent() : NotFound();
    [HttpPut("business-hours")] public async Task<ActionResult> Hours(IReadOnlyCollection<BusinessHourRequest> request, CancellationToken ct) => RestaurantId is { } rid && await menu.SetBusinessHoursAsync(rid, request, ct) ? NoContent() : MissingTenant();
    [HttpPost("images"), RequestSizeLimit(6_000_000)]
    public async Task<ActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (RestaurantId is not { } rid) return MissingTenant();
        return await ImageUploadHelper.SaveOptimizedWebpAsync(this, environment, file, Path.Combine("uploads", rid.ToString("N")), ct);
    }
}

[Route("api/public/menus")]
public sealed class PublicMenusController(IPublicMenuService menus) : ApiController
{
    [HttpGet("{slug}"), AllowAnonymous] public async Task<ActionResult> Get(string slug, CancellationToken ct) => await menus.GetAsync(slug.Trim().ToLowerInvariant(), ct) is { } x ? Ok(x) : NotFound();
}
