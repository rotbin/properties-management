using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Entities.Finance;
using BuildingManagement.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingManagement.Infrastructure.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, bool useInMemory = false)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        if (useInMemory)
        {
            // InMemory provider: schema already created via EnsureCreatedAsync in Program.cs
            logger.LogInformation("Using InMemory database provider (no migrations).");
        }
        else
        {
            // Apply pending migrations for real DB providers
            await context.Database.MigrateAsync();
        }

        // Seed roles
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Created role: {Role}", role);
            }
        }

        // Check if already seeded
        if (await context.Buildings.AnyAsync())
        {
            logger.LogInformation("Database already seeded. Skipping.");
            return;
        }

        // Seed Vendors
        var cleaningVendor = new Vendor
        {
            Name = "CleanCo Services",
            ServiceType = VendorServiceType.Cleaning,
            Phone = "050-1234567",
            Email = "info@cleanco.example.com",
            ContactName = "David Cohen",
            Notes = "Primary cleaning vendor"
        };
        var gardenerVendor = new Vendor
        {
            Name = "Green Gardens Ltd",
            ServiceType = VendorServiceType.Gardening,
            Phone = "050-7654321",
            Email = "info@greengardens.example.com",
            ContactName = "Sarah Levi",
            Notes = "Gardening and landscaping"
        };
        context.Vendors.AddRange(cleaningVendor, gardenerVendor);
        await context.SaveChangesAsync();

        // Seed Building
        var building1 = new Building
        {
            Name = "Sunset Towers",
            AddressLine = "123 Herzl Street",
            City = "Tel Aviv",
            PostalCode = "6120101",
            Notes = "Main residential building, 10 floors"
        };
        context.Buildings.Add(building1);
        await context.SaveChangesAsync();

        // Seed Units
        var units = new List<Unit>();
        for (int floor = 1; floor <= 5; floor++)
        {
            for (int apt = 1; apt <= 4; apt++)
            {
                units.Add(new Unit
                {
                    BuildingId = building1.Id,
                    UnitNumber = $"{floor}0{apt}",
                    Floor = floor,
                    SizeSqm = 75 + apt * 10,
                    OwnerName = floor == 1 && apt == 1 ? "Tenant User" : null
                });
            }
        }
        context.Units.AddRange(units);
        await context.SaveChangesAsync();

        var unit1 = units.First(u => u.UnitNumber == "101");

        // Seed Users
        const string devPassword = "Demo@123!";

        var adminUser = new ApplicationUser
        {
            UserName = "admin@example.com",
            Email = "admin@example.com",
            EmailConfirmed = true,
            FullName = "System Admin",
            Phone = "050-0000001"
        };
        await CreateUserWithRole(userManager, adminUser, devPassword, AppRoles.Admin, logger);

        var managerUser = new ApplicationUser
        {
            UserName = "manager@example.com",
            Email = "manager@example.com",
            EmailConfirmed = true,
            FullName = "Building Manager",
            Phone = "050-0000002"
        };
        await CreateUserWithRole(userManager, managerUser, devPassword, AppRoles.Manager, logger);

        var tenantUser = new ApplicationUser
        {
            UserName = "tenant@example.com",
            Email = "tenant@example.com",
            EmailConfirmed = true,
            FullName = "John Tenant",
            Phone = "050-0000003"
        };
        await CreateUserWithRole(userManager, tenantUser, devPassword, AppRoles.Tenant, logger);

        var vendorUser = new ApplicationUser
        {
            UserName = "vendor@example.com",
            Email = "vendor@example.com",
            EmailConfirmed = true,
            FullName = "Vendor User",
            Phone = "050-0000004",
            VendorId = cleaningVendor.Id
        };
        await CreateUserWithRole(userManager, vendorUser, devPassword, AppRoles.Vendor, logger);

        // Link tenant to unit
        unit1.TenantUserId = tenantUser.Id;
        context.Units.Update(unit1);

        // Link manager to building
        context.BuildingManagers.Add(new BuildingManager
        {
            UserId = managerUser.Id,
            BuildingId = building1.Id
        });
        await context.SaveChangesAsync();

        // Seed Assets
        var elevator = new Asset
        {
            BuildingId = building1.Id,
            Name = "Main Elevator",
            AssetType = AssetType.Elevator,
            LocationDescription = "Central elevator shaft",
            SerialNumber = "ELV-2023-001",
            InstallDate = new DateTime(2023, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            WarrantyUntil = new DateTime(2028, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            Notes = "Monthly maintenance required"
        };
        var generator = new Asset
        {
            BuildingId = building1.Id,
            Name = "Backup Generator",
            AssetType = AssetType.Generator,
            LocationDescription = "Basement, room B-2",
            SerialNumber = "GEN-2022-005",
            InstallDate = new DateTime(2022, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            WarrantyUntil = new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        context.Assets.AddRange(elevator, generator);
        await context.SaveChangesAsync();

        // Seed PreventivePlan
        context.PreventivePlans.Add(new PreventivePlan
        {
            AssetId = elevator.Id,
            Title = "Monthly Elevator Maintenance",
            FrequencyType = FrequencyType.Monthly,
            Interval = 1,
            NextDueDate = DateTime.UtcNow.AddDays(7),
            ChecklistText = "1. Check cables\n2. Lubricate mechanisms\n3. Test emergency stop\n4. Inspect doors\n5. Update log"
        });
        await context.SaveChangesAsync();

        // Seed global Fake payment provider config
        if (!await context.PaymentProviderConfigs.AnyAsync())
        {
            context.PaymentProviderConfigs.Add(new PaymentProviderConfig
            {
                BuildingId = null, // global default
                ProviderType = PaymentProviderType.Fake,
                IsActive = true,
                SupportedFeatures = ProviderFeatures.HostedPaymentPage | ProviderFeatures.Tokenization
                    | ProviderFeatures.RecurringCharges | ProviderFeatures.Refunds | ProviderFeatures.Webhooks,
                Currency = "ILS"
            });
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded global Fake payment provider config.");
        }

        logger.LogInformation("Database seeded successfully with demo data.");
    }

    private static async Task CreateUserWithRole(UserManager<ApplicationUser> userManager, ApplicationUser user, string password, string role, ILogger logger)
    {
        var existing = await userManager.FindByEmailAsync(user.Email!);
        if (existing != null) return;

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
            logger.LogInformation("Created user {Email} with role {Role}", user.Email, role);
        }
        else
        {
            logger.LogError("Failed to create user {Email}: {Errors}", user.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
