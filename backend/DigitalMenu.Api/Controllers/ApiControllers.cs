using System.Security.Claims;
using System.Net;
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

        if (IsResendConfigured())
        {
            return await SendWithResendAsync(businessName, email, request.Phone?.Trim(), type, request.Message?.Trim(), ct)
                ? Ok()
                : await SendWithFormspreeAsync(businessName, email, request.Phone?.Trim(), type, request.Message?.Trim(), ct)
                    ? Ok()
                    : Problem("Lead notification failed.", statusCode: StatusCodes.Status502BadGateway);
        }

        return await SendWithFormspreeAsync(businessName, email, request.Phone?.Trim(), type, request.Message?.Trim(), ct)
            ? Ok()
            : Problem("Lead notification endpoint is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private bool IsResendConfigured() =>
        !string.IsNullOrWhiteSpace(configuration["LeadNotifications:ResendApiKey"]) &&
        !string.IsNullOrWhiteSpace(configuration["LeadNotifications:From"]) &&
        !string.IsNullOrWhiteSpace(configuration["LeadNotifications:AdminTo"]);

    private async Task<bool> SendWithFormspreeAsync(string businessName, string email, string? phone, string type, string? message, CancellationToken ct)
    {
        var endpoint = configuration["LeadNotifications:FormspreeEndpoint"];
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri)) return false;

        var payload = new Dictionary<string, string?>
        {
            ["_subject"] = "Novi MeniSpot upit",
            ["Naziv objekta"] = businessName,
            ["Email"] = email,
            ["_replyto"] = email,
            ["email"] = email,
            ["Telefon"] = phone,
            ["Tip objekta"] = type,
            ["Poruka"] = message
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
        return response.IsSuccessStatusCode;
    }

    private async Task<bool> SendWithResendAsync(string businessName, string email, string? phone, string type, string? message, CancellationToken ct)
    {
        var from = configuration["LeadNotifications:From"]!;
        var adminTo = configuration["LeadNotifications:AdminTo"]!;
        var replyTo = configuration["LeadNotifications:ReplyTo"];
        var publicBaseUrl = (configuration["LeadNotifications:PublicBaseUrl"] ?? "https://menispot.com").TrimEnd('/');

        var adminSubject = $"Novi upit za digitalni meni - {businessName}";
        var adminHtml = BuildAdminLeadEmail(businessName, email, phone, type, message, publicBaseUrl);
        var userSubject = "MeniSpot je primio tvoj upit";
        var userHtml = BuildLeadConfirmationEmail(businessName, publicBaseUrl);

        var adminSent = await SendResendEmailAsync(from, adminTo, adminSubject, adminHtml, LeadTextSummary(businessName, email, phone, type, message), email, ct);
        if (!adminSent) return false;

        await SendResendEmailAsync(from, email, userSubject, userHtml, $"Hvala na upitu za {businessName}. Javit cemo se uskoro.", replyTo ?? adminTo, ct);
        return true;
    }

    private async Task<bool> SendResendEmailAsync(string from, string to, string subject, string html, string text, string? replyTo, CancellationToken ct)
    {
        var apiKey = configuration["LeadNotifications:ResendApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return false;

        var payload = new Dictionary<string, object?>
        {
            ["from"] = from,
            ["to"] = new[] { to },
            ["subject"] = subject,
            ["html"] = html,
            ["text"] = text
        };
        if (!string.IsNullOrWhiteSpace(replyTo)) payload["reply_to"] = replyTo;

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new("Bearer", apiKey);
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await httpClientFactory.CreateClient().SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    private static string LeadTextSummary(string businessName, string email, string? phone, string type, string? message) =>
        $"""
        Novi MeniSpot upit

        Objekat: {businessName}
        Tip objekta: {type}
        Email: {email}
        Telefon: {phone}

        Poruka:
        {message}
        """;

    private static string BuildAdminLeadEmail(string businessName, string email, string? phone, string type, string? message, string publicBaseUrl)
    {
        var safeName = WebUtility.HtmlEncode(businessName);
        var safeEmail = WebUtility.HtmlEncode(email);
        var safePhone = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(phone) ? "Nije uneseno" : phone);
        var safeType = WebUtility.HtmlEncode(type);
        var safeMessage = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(message) ? "Nema dodatne poruke." : message).Replace("\n", "<br>");
        var mailto = $"mailto:{safeEmail}?subject={Uri.EscapeDataString($"MeniSpot upit - {businessName}")}";
        return $"""
        <div style="margin:0;background:#f3f6fa;padding:32px 18px;color:#111827;font-family:Arial,Helvetica,sans-serif">
          <div style="max-width:660px;margin:0 auto">
            <div style="padding:0 4px 18px">
              <img src="{publicBaseUrl}/menispot-mark.png" alt="MeniSpot" width="38" height="38" style="vertical-align:middle;border-radius:10px;margin-right:10px">
              <span style="font-size:20px;font-weight:800;vertical-align:middle">Meni<span style="color:#84cc16">Spot</span></span>
            </div>
            <div style="background:#ffffff;border:1px solid #dfe7f1;border-radius:20px;overflow:hidden;box-shadow:0 18px 48px rgba(15,23,42,.08)">
              <div style="background:#111827;padding:28px 30px;color:#ffffff">
                <p style="margin:0 0 10px;color:#a3e635;font-size:12px;font-weight:800;letter-spacing:.1em;text-transform:uppercase">Novi upit sa stranice</p>
                <h1 style="margin:0;font-size:28px;line-height:1.2">Novi upit za digitalni meni</h1>
                <p style="margin:12px 0 0;color:#cbd5e1;font-size:15px">Objekat: <strong style="color:#ffffff">{safeName}</strong></p>
              </div>
              <div style="padding:28px 30px">
                <table style="width:100%;border-collapse:collapse;font-size:15px">
                  <tr><td style="padding:12px 0;color:#64748b;border-bottom:1px solid #eef2f7">Tip objekta</td><td style="padding:12px 0;border-bottom:1px solid #eef2f7;font-weight:700">{safeType}</td></tr>
                  <tr><td style="padding:12px 0;color:#64748b;border-bottom:1px solid #eef2f7">Email</td><td style="padding:12px 0;border-bottom:1px solid #eef2f7"><a style="color:#2563eb" href="{mailto}">{safeEmail}</a></td></tr>
                  <tr><td style="padding:12px 0;color:#64748b">Telefon</td><td style="padding:12px 0">{safePhone}</td></tr>
                </table>
                <div style="margin-top:22px;padding:18px;border-radius:14px;background:#f8fafc;border:1px solid #e5eaf2">
                  <p style="margin:0 0 8px;color:#64748b;font-size:13px;font-weight:800;text-transform:uppercase;letter-spacing:.05em">Poruka</p>
                  <p style="margin:0;line-height:1.65;color:#172033">{safeMessage}</p>
                </div>
                <a href="{mailto}" style="display:inline-block;margin-top:24px;background:#84cc16;color:#17230a;text-decoration:none;font-weight:800;border-radius:12px;padding:13px 18px">Odgovori na upit</a>
              </div>
            </div>
            <p style="max-width:660px;margin:14px auto 0;color:#94a3b8;font-size:12px;line-height:1.5">Ovaj email je poslan automatski nakon upita na menispot.com.</p>
          </div>
        </div>
        """;
    }

    private static string BuildLeadConfirmationEmail(string businessName, string publicBaseUrl)
    {
        var safeName = WebUtility.HtmlEncode(businessName);
        return $"""
        <div style="margin:0;background:#f3f6fa;padding:32px 18px;color:#111827;font-family:Arial,Helvetica,sans-serif">
          <div style="max-width:620px;margin:0 auto">
            <div style="padding:0 4px 18px">
              <img src="{publicBaseUrl}/menispot-mark.png" alt="MeniSpot" width="38" height="38" style="vertical-align:middle;border-radius:10px;margin-right:10px">
              <span style="font-size:20px;font-weight:800;vertical-align:middle">Meni<span style="color:#84cc16">Spot</span></span>
            </div>
            <div style="background:#ffffff;border:1px solid #dfe7f1;border-radius:20px;overflow:hidden;box-shadow:0 18px 48px rgba(15,23,42,.08)">
              <div style="background:#111827;padding:30px;color:#ffffff">
                <p style="margin:0 0 10px;color:#a3e635;font-size:12px;font-weight:800;letter-spacing:.1em;text-transform:uppercase">Upit zaprimljen</p>
                <h1 style="margin:0;font-size:28px;line-height:1.2">Hvala, primili smo tvoju poruku.</h1>
                <p style="margin:14px 0 0;color:#cbd5e1;font-size:15px;line-height:1.6">Tvoj upit za <strong style="color:#ffffff">{safeName}</strong> je uspjesno poslan MeniSpot timu.</p>
              </div>
              <div style="padding:28px 30px">
                <p style="margin:0 0 16px;line-height:1.7">Pregledat cemo podatke i javiti se uskoro s prijedlogom, cijenom i sljedecim koracima.</p>
                <div style="margin:22px 0;padding:18px;border-radius:14px;background:#f8fafc;border:1px solid #e5eaf2">
                  <p style="margin:0 0 6px;color:#64748b;font-size:13px;font-weight:800;text-transform:uppercase;letter-spacing:.05em">Sta dalje?</p>
                  <p style="margin:0;line-height:1.65;color:#172033">Ako zelis dodati jos neku informaciju, fotografije ili poseban zahtjev, samo odgovori direktno na ovaj email.</p>
                </div>
                <a href="{publicBaseUrl}" style="display:inline-block;background:#84cc16;color:#17230a;text-decoration:none;font-weight:800;border-radius:12px;padding:13px 18px">Posjeti MeniSpot</a>
              </div>
            </div>
            <p style="margin:14px 4px 0;color:#94a3b8;font-size:12px;line-height:1.5">Ovo je automatska potvrda nakon slanja upita na menispot.com.</p>
          </div>
        </div>
        """;
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
public sealed class AdminRestaurantsController(IRestaurantService restaurants, IAuthService auth, IWebHostEnvironment environment) : ApiController
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
    [HttpPost("{id:guid}/images"), RequestSizeLimit(6_000_000)]
    public async Task<ActionResult> UploadImage(Guid id, IFormFile file, CancellationToken ct)
    {
        if (await restaurants.GetAdminDetailsAsync(id, ct) is null) return NotFound();
        return await ImageUploadHelper.SaveOptimizedWebpAsync(this, environment, file, Path.Combine("uploads", id.ToString("N")), ct);
    }
    [HttpDelete("{id:guid}")] public async Task<ActionResult> Delete(Guid id, CancellationToken ct) => await restaurants.DeleteAsync(id, ct) ? NoContent() : NotFound();
}

[Route("api/admin/billing"), Authorize(Roles = Roles.SuperAdmin)]
public sealed class AdminBillingController(IBillingService billing) : ApiController
{
    [HttpGet] public async Task<ActionResult> Overview(CancellationToken ct) => Ok(await billing.GetOverviewAsync(ct));
    [HttpGet("{restaurantId:guid}/payments")] public async Task<ActionResult> History(Guid restaurantId, CancellationToken ct) => await billing.GetHistoryAsync(restaurantId, ct) is { } items ? Ok(items) : NotFound();
    [HttpPost("{restaurantId:guid}/payments")] public async Task<ActionResult> Record(Guid restaurantId, RecordManualPaymentRequest request, CancellationToken ct) => await billing.RecordPaymentAsync(restaurantId, request, ct) is { } item ? Ok(item) : NotFound();
}

[Route("api/admin/support"), Authorize(Roles = Roles.SuperAdmin)]
public sealed class AdminSupportController(ISupportTicketService support) : ApiController
{
    [HttpGet] public async Task<ActionResult> All(CancellationToken ct) => Ok(await support.GetAdminAsync(ct));
    [HttpPut("{id:guid}")] public async Task<ActionResult> Update(Guid id, UpdateSupportTicketRequest request, CancellationToken ct) => await support.UpdateAsync(id, request, ct) is { } item ? Ok(item) : NotFound();
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
public sealed class RestaurantController(IRestaurantService restaurants, IMenuManagementService menu, ISupportTicketService support, IWebHostEnvironment environment, IConfiguration configuration, IHttpClientFactory httpClientFactory) : ApiController
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
    [HttpGet("support")] public async Task<ActionResult> SupportTickets(CancellationToken ct) => RestaurantId is { } rid ? Ok(await support.GetOwnerAsync(rid, ct)) : MissingTenant();
    [HttpPost("support")]
    public async Task<ActionResult> CreateSupportTicket(CreateSupportTicketRequest request, CancellationToken ct)
    {
        if (RestaurantId is not { } rid) return MissingTenant();
        try
        {
            if (await support.CreateAsync(rid, request, ct) is not { } ticket) return MissingTenant();
            await SendSupportTicketEmailAsync(ticket, ct);
            return Ok(ticket);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [HttpPost("support/images"), RequestSizeLimit(6_000_000)]
    public async Task<ActionResult> UploadSupportImage(IFormFile file, CancellationToken ct)
    {
        if (RestaurantId is not { } rid) return MissingTenant();
        return await ImageUploadHelper.SaveOptimizedWebpAsync(this, environment, file, Path.Combine("uploads", rid.ToString("N"), "support"), ct);
    }

    private async Task SendSupportTicketEmailAsync(SupportTicketSummary ticket, CancellationToken ct)
    {
        var apiKey = configuration["LeadNotifications:ResendApiKey"];
        var from = configuration["LeadNotifications:From"];
        var adminTo = configuration["LeadNotifications:AdminTo"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(adminTo)) return;

        var publicBaseUrl = (configuration["LeadNotifications:PublicBaseUrl"] ?? "https://menispot.com").TrimEnd('/');
        var adminUrl = $"{publicBaseUrl}/admin/support";
        var attachmentUrl = string.IsNullOrWhiteSpace(ticket.AttachmentUrl) ? null : $"{publicBaseUrl}{ticket.AttachmentUrl}";
        var safeRestaurant = WebUtility.HtmlEncode(ticket.RestaurantName);
        var safeTitle = WebUtility.HtmlEncode(ticket.Title);
        var safeMessage = WebUtility.HtmlEncode(ticket.Message).Replace("\n", "<br>");
        var html = $"""
        <div style="margin:0;background:#f3f6fa;padding:32px 18px;color:#111827;font-family:Arial,Helvetica,sans-serif">
          <div style="max-width:660px;margin:0 auto;background:#ffffff;border:1px solid #dfe7f1;border-radius:20px;overflow:hidden">
            <div style="background:#111827;padding:28px 30px;color:#ffffff">
              <p style="margin:0 0 10px;color:#a3e635;font-size:12px;font-weight:800;letter-spacing:.1em;text-transform:uppercase">Novi zahtjev za podrsku</p>
              <h1 style="margin:0;font-size:26px;line-height:1.2">{safeTitle}</h1>
              <p style="margin:12px 0 0;color:#cbd5e1">Restoran: <strong style="color:#ffffff">{safeRestaurant}</strong></p>
            </div>
            <div style="padding:26px 30px">
              <p style="margin:0 0 14px;color:#64748b">Tip: <strong>{ticket.Type}</strong> · Prioritet: <strong>{ticket.Priority}</strong> · Paket: <strong>{ticket.RestaurantPlan}</strong></p>
              <div style="border:1px solid #e5eaf2;border-radius:14px;background:#f8fafc;padding:18px;line-height:1.65">{safeMessage}</div>
              {(attachmentUrl is null ? "" : $"""<p style="margin:18px 0 0"><a href="{attachmentUrl}" style="color:#2563eb;font-weight:700">Otvori screenshot/prilog</a></p>""")}
              <a href="{adminUrl}" style="display:inline-block;margin-top:22px;background:#84cc16;color:#17230a;text-decoration:none;font-weight:800;border-radius:12px;padding:13px 18px">Otvori podrsku</a>
            </div>
          </div>
        </div>
        """;
        var payload = new Dictionary<string, object?>
        {
            ["from"] = from,
            ["to"] = new[] { adminTo },
            ["subject"] = $"Novi zahtjev za podrsku - {ticket.RestaurantName}",
            ["html"] = html,
            ["text"] = $"Novi zahtjev za podrsku\nRestoran: {ticket.RestaurantName}\nNaslov: {ticket.Title}\nPoruka:\n{ticket.Message}"
        };
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails") { Content = JsonContent.Create(payload) };
        httpRequest.Headers.Authorization = new("Bearer", apiKey);
        httpRequest.Headers.Accept.ParseAdd("application/json");
        using var _ = await httpClientFactory.CreateClient().SendAsync(httpRequest, ct);
    }
}

[Route("api/public/menus")]
public sealed class PublicMenusController(IPublicMenuService menus) : ApiController
{
    [HttpGet("{slug}"), AllowAnonymous] public async Task<ActionResult> Get(string slug, CancellationToken ct) => await menus.GetAsync(slug.Trim().ToLowerInvariant(), ct) is { } x ? Ok(x) : NotFound();
}
