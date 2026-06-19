using System.Text;
using DigitalMenu.Application;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace DigitalMenu.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.AddDbContext<ApplicationDbContext>(o => o.UseNpgsql(configuration.GetConnectionString("Database")));
        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.Password.RequiredLength = isDevelopment ? 5 : 8;
            o.Password.RequireDigit = !isDevelopment;
            o.Password.RequireUppercase = !isDevelopment;
            o.Password.RequireNonAlphanumeric = !isDevelopment;
            o.User.RequireUniqueEmail = true;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<ApplicationDbContext>();
        var jwt = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? throw new InvalidOperationException("JWT configuration is missing.");
        if (jwt.Key.Length < 32) throw new InvalidOperationException("JWT key must contain at least 32 characters.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });
        services.AddAuthorization();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRestaurantService, RestaurantService>();
        services.AddScoped<IMenuManagementService, MenuManagementService>();
        services.AddScoped<IPublicMenuService, PublicMenuService>();
        return services;
    }
}

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { Roles.SuperAdmin, Roles.RestaurantOwner, Roles.RestaurantStaff })
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole<Guid>(role));
        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is not null) return;
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Administrator" };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(x => x.Description)));
        await users.AddToRoleAsync(user, Roles.SuperAdmin);
    }
}
