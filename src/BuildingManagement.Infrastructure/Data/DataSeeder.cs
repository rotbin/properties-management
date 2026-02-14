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

        // ─── Tenant names per unit ──────────────────────────
        var tenantNames = new Dictionary<string, (string fullName, string phone, string email)>
        {
            ["101"] = ("Yossi Cohen", "050-1111001", "yossi@example.com"),
            ["102"] = ("Sarah Levi", "050-1111002", "sarah.l@example.com"),
            ["103"] = ("David Mizrahi", "050-1111003", "david.m@example.com"),
            ["104"] = ("Rachel Green", "050-1111004", "rachel.g@example.com"),
            ["201"] = ("Moshe Goldberg", "050-1111005", "moshe.g@example.com"),
            ["202"] = ("Yael Shapiro", "050-1111006", "yael.s@example.com"),
            ["203"] = ("Avi Ben-David", "050-1111007", "avi.bd@example.com"),
            ["204"] = ("Noa Friedman", "050-1111008", "noa.f@example.com"),
            ["301"] = ("Eitan Katz", "050-1111009", "eitan.k@example.com"),
            ["302"] = ("Michal Peretz", "050-1111010", "michal.p@example.com"),
            ["303"] = ("Omer Alon", "050-1111011", "omer.a@example.com"),
            ["304"] = ("Tamar Rosen", "050-1111012", "tamar.r@example.com"),
            ["401"] = ("Uri Navon", "050-1111013", "uri.n@example.com"),
            ["402"] = ("Shira Dahan", "050-1111014", "shira.d@example.com"),
            ["403"] = ("Ron Azulay", "050-1111015", "ron.a@example.com"),
            ["404"] = ("Liat Baruch", "050-1111016", "liat.b@example.com"),
            ["501"] = ("Gal Engel", "050-1111017", "gal.e@example.com"),
            ["502"] = ("Dana Haim", "050-1111018", "dana.h@example.com"),
            ["503"] = ("Amir Stern", "050-1111019", "amir.s@example.com"),
            ["504"] = ("Maya Tal", "050-1111020", "maya.t@example.com"),
        };

        // Seed Units
        var units = new List<Unit>();
        for (int floor = 1; floor <= 5; floor++)
        {
            for (int apt = 1; apt <= 4; apt++)
            {
                var unitNum = $"{floor}0{apt}";
                var ownerName = tenantNames.TryGetValue(unitNum, out var tn) ? tn.fullName : null;
                units.Add(new Unit
                {
                    BuildingId = building1.Id,
                    UnitNumber = unitNum,
                    Floor = floor,
                    SizeSqm = 75 + apt * 10,
                    OwnerName = ownerName
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
            FullName = "Yossi Cohen",
            Phone = "050-1111001"
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

        // Create tenant users for each unit and link them
        var tenantUsers = new Dictionary<string, ApplicationUser>();
        foreach (var (unitNum, info) in tenantNames)
        {
            if (unitNum == "101") continue; // Already created above
            var tUser = new ApplicationUser
            {
                UserName = info.email,
                Email = info.email,
                EmailConfirmed = true,
                FullName = info.fullName,
                Phone = info.phone
            };
            await CreateUserWithRole(userManager, tUser, devPassword, AppRoles.Tenant, logger);
            tenantUsers[unitNum] = tUser;
        }

        // Link tenant users to units
        unit1.TenantUserId = tenantUser.Id;
        context.Units.Update(unit1);
        foreach (var unit in units.Where(u => u.UnitNumber != "101"))
        {
            if (tenantUsers.TryGetValue(unit.UnitNumber, out var tu))
            {
                var dbUser = await userManager.FindByEmailAsync(tu.Email!);
                if (dbUser != null)
                {
                    unit.TenantUserId = dbUser.Id;
                    context.Units.Update(unit);
                }
            }
        }

        // Link manager to building
        context.BuildingManagers.Add(new BuildingManager
        {
            UserId = managerUser.Id,
            BuildingId = building1.Id
        });
        await context.SaveChangesAsync();

        // ─── Seed Tenant Profiles ────────────────────────────
        logger.LogInformation("Seeding tenant profiles...");
        var moveInDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        foreach (var unit in units)
        {
            if (tenantNames.TryGetValue(unit.UnitNumber, out var info))
            {
                var userId = unit.UnitNumber == "101"
                    ? tenantUser.Id
                    : (tenantUsers.TryGetValue(unit.UnitNumber, out var tu) ? tu.Id : null);

                context.TenantProfiles.Add(new TenantProfile
                {
                    UnitId = unit.Id,
                    UserId = userId,
                    FullName = info.fullName,
                    Phone = info.phone,
                    Email = info.email,
                    MoveInDate = moveInDate,
                    IsActive = true,
                    Notes = "Seeded tenant",
                    CreatedBy = "seed"
                });
            }
        }
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} tenant profiles.", units.Count);

        // ─── Seed HOA Fee Plan + Charges + Payments ─────────
        await SeedHOAPaymentData(context, building1, units, tenantUser, tenantUsers, userManager, logger);

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

        // Seed demo financial ledger entries (income + expenses) for report
        if (!await context.LedgerEntries.AnyAsync())
        {
            var buildingId = (await context.Buildings.FirstAsync()).Id;
            var now = DateTime.UtcNow;
            var entries = new List<LedgerEntry>();
            decimal balance = 0;

            // 12 months of sample income (HOA collections)
            for (int m = 11; m >= 0; m--)
            {
                var date = now.AddMonths(-m);
                var income = 8500m + (m % 3) * 500m; // vary a bit
                balance += income;
                entries.Add(new LedgerEntry
                {
                    BuildingId = buildingId,
                    EntryType = LedgerEntryType.Payment,
                    Category = "HOAMonthlyFees",
                    Description = $"HOA collection {date:yyyy-MM}",
                    Debit = 0, Credit = income, BalanceAfter = balance,
                    CreatedAtUtc = new DateTime(date.Year, date.Month, 5, 10, 0, 0, DateTimeKind.Utc)
                });
            }

            // Sample expenses
            var expenses = new (string cat, decimal amount, int monthsAgo, string desc)[]
            {
                ("Cleaning", 2200, 11, "Monthly cleaning service"),
                ("Cleaning", 2200, 10, "Monthly cleaning service"),
                ("Cleaning", 2200, 9, "Monthly cleaning service"),
                ("Cleaning", 2200, 8, "Monthly cleaning service"),
                ("Cleaning", 2200, 7, "Monthly cleaning service"),
                ("Cleaning", 2200, 6, "Monthly cleaning service"),
                ("Cleaning", 2200, 5, "Monthly cleaning service"),
                ("Cleaning", 2200, 4, "Monthly cleaning service"),
                ("Cleaning", 2200, 3, "Monthly cleaning service"),
                ("Cleaning", 2200, 2, "Monthly cleaning service"),
                ("Cleaning", 2200, 1, "Monthly cleaning service"),
                ("Cleaning", 2200, 0, "Monthly cleaning service"),
                ("Gardening", 900, 10, "Gardening Q4"),
                ("Gardening", 900, 7, "Gardening Q1"),
                ("Gardening", 900, 4, "Gardening Q2"),
                ("Gardening", 900, 1, "Gardening Q3"),
                ("Electricity", 1800, 9, "Common area electricity"),
                ("Electricity", 2100, 6, "Common area electricity"),
                ("Electricity", 1600, 3, "Common area electricity"),
                ("Electricity", 1900, 0, "Common area electricity"),
                ("ElevatorMaintenance", 3500, 8, "Elevator annual inspection"),
                ("ElevatorMaintenance", 850, 2, "Elevator repair"),
                ("WaterPumps", 650, 5, "Water pump service"),
                ("Insurance", 4200, 11, "Annual building insurance"),
                ("PestControl", 500, 6, "Quarterly pest control"),
                ("PestControl", 500, 0, "Quarterly pest control"),
                ("Repairs", 1200, 4, "Lobby door repair"),
                ("BankFees", 120, 9, "Bank service fees"),
                ("BankFees", 120, 6, "Bank service fees"),
                ("BankFees", 120, 3, "Bank service fees"),
                ("BankFees", 120, 0, "Bank service fees"),
            };

            foreach (var (cat, amount, monthsAgo, desc) in expenses)
            {
                var date = now.AddMonths(-monthsAgo);
                balance -= amount;
                entries.Add(new LedgerEntry
                {
                    BuildingId = buildingId,
                    EntryType = LedgerEntryType.Expense,
                    Category = cat,
                    Description = desc,
                    Debit = amount, Credit = 0, BalanceAfter = balance,
                    CreatedAtUtc = new DateTime(date.Year, date.Month, 15, 14, 0, 0, DateTimeKind.Utc)
                });
            }

            context.LedgerEntries.AddRange(entries);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} demo financial ledger entries.", entries.Count);
        }

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

    /// <summary>
    /// Seeds HOA fee plan, unit charges for 3 months, payments, allocations,
    /// and ledger entries so the dashboard shows a realistic collection status.
    /// </summary>
    private static async Task SeedHOAPaymentData(
        AppDbContext context,
        Building building,
        List<Unit> units,
        ApplicationUser primaryTenant,
        Dictionary<string, ApplicationUser> tenantUsers,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        if (await context.HOAFeePlans.AnyAsync()) return; // already seeded

        var buildingId = building.Id;
        var now = DateTime.UtcNow;
        var currentPeriod = now.ToString("yyyy-MM");
        var prevPeriod1 = now.AddMonths(-1).ToString("yyyy-MM");
        var prevPeriod2 = now.AddMonths(-2).ToString("yyyy-MM");

        // ── 1. Create HOA Fee Plan ──────────────────────────
        var hoaPlan = new HOAFeePlan
        {
            BuildingId = buildingId,
            Name = "Monthly HOA 2026",
            CalculationMethod = HOACalculationMethod.FixedPerUnit,
            FixedAmountPerUnit = 450m,
            EffectiveFrom = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsActive = true,
            CreatedBy = "seed"
        };
        context.HOAFeePlans.Add(hoaPlan);
        await context.SaveChangesAsync();

        // ── 2. Define payment statuses per unit for current month ─
        //    Paid=8, Partial=3, Unpaid=5, Overdue=4  → 20 total
        var currentMonthStatuses = new Dictionary<string, string>
        {
            ["101"] = "Paid",    ["102"] = "Paid",
            ["103"] = "Partial", ["104"] = "Unpaid",
            ["201"] = "Paid",    ["202"] = "Paid",
            ["203"] = "Partial", ["204"] = "Unpaid",
            ["301"] = "Paid",    ["302"] = "Paid",
            ["303"] = "Partial", ["304"] = "Overdue",
            ["401"] = "Paid",    ["402"] = "Overdue",
            ["403"] = "Unpaid",  ["404"] = "Overdue",
            ["501"] = "Paid",    ["502"] = "Unpaid",
            ["503"] = "Overdue", ["504"] = "Unpaid",
        };

        // Previous months: most units paid, a few overdue (simulates aging)
        var prevMonth1Statuses = new Dictionary<string, string>
        {
            ["101"] = "Paid",  ["102"] = "Paid",  ["103"] = "Paid",   ["104"] = "Paid",
            ["201"] = "Paid",  ["202"] = "Paid",  ["203"] = "Paid",   ["204"] = "Paid",
            ["301"] = "Paid",  ["302"] = "Paid",  ["303"] = "Paid",   ["304"] = "Overdue",
            ["401"] = "Paid",  ["402"] = "Overdue",["403"] = "Paid",  ["404"] = "Overdue",
            ["501"] = "Paid",  ["502"] = "Paid",  ["503"] = "Overdue",["504"] = "Paid",
        };

        var prevMonth2Statuses = new Dictionary<string, string>
        {
            ["101"] = "Paid",  ["102"] = "Paid",  ["103"] = "Paid",  ["104"] = "Paid",
            ["201"] = "Paid",  ["202"] = "Paid",  ["203"] = "Paid",  ["204"] = "Paid",
            ["301"] = "Paid",  ["302"] = "Paid",  ["303"] = "Paid",  ["304"] = "Paid",
            ["401"] = "Paid",  ["402"] = "Paid",  ["403"] = "Paid",  ["404"] = "Overdue",
            ["501"] = "Paid",  ["502"] = "Paid",  ["503"] = "Paid",  ["504"] = "Paid",
        };

        var periods = new[]
        {
            (period: prevPeriod2, statuses: prevMonth2Statuses, monthsAgo: 2),
            (period: prevPeriod1, statuses: prevMonth1Statuses, monthsAgo: 1),
            (period: currentPeriod, statuses: currentMonthStatuses, monthsAgo: 0),
        };

        decimal amountPerUnit = 450m;
        var allCharges = new List<UnitCharge>();
        var allPayments = new List<Payment>();
        var allAllocations = new List<PaymentAllocation>();
        var ledgerEntries = new List<LedgerEntry>();
        decimal runningBalance = 0m;

        foreach (var (period, statuses, monthsAgo) in periods)
        {
            var periodDate = now.AddMonths(-monthsAgo);
            var dueDate = new DateTime(periodDate.Year, periodDate.Month, 10, 0, 0, 0, DateTimeKind.Utc);
            var chargeDate = new DateTime(periodDate.Year, periodDate.Month, 1, 8, 0, 0, DateTimeKind.Utc);

            foreach (var unit in units)
            {
                if (!statuses.TryGetValue(unit.UnitNumber, out var status)) continue;

                // ── Create UnitCharge ──
                var chargeStatus = status switch
                {
                    "Paid" => UnitChargeStatus.Paid,
                    "Partial" => UnitChargeStatus.PartiallyPaid,
                    "Overdue" => UnitChargeStatus.Overdue,
                    _ => UnitChargeStatus.Pending
                };

                // Unpaid units in current month get a future due date (25th) so they show as "Unpaid"
                // Overdue units get an early due date (5th) to ensure they're past due
                var unitDueDate = status switch
                {
                    "Unpaid" when monthsAgo == 0 => new DateTime(periodDate.Year, periodDate.Month, 25, 0, 0, 0, DateTimeKind.Utc),
                    "Overdue" => new DateTime(periodDate.Year, periodDate.Month, 5, 0, 0, 0, DateTimeKind.Utc),
                    _ => dueDate
                };

                var charge = new UnitCharge
                {
                    UnitId = unit.Id,
                    HOAFeePlanId = hoaPlan.Id,
                    Period = period,
                    AmountDue = amountPerUnit,
                    DueDate = unitDueDate,
                    Status = chargeStatus,
                    CreatedAtUtc = chargeDate
                };
                allCharges.Add(charge);

                // ── Ledger: Charge entry ──
                runningBalance += amountPerUnit;
                ledgerEntries.Add(new LedgerEntry
                {
                    BuildingId = buildingId,
                    UnitId = unit.Id,
                    EntryType = LedgerEntryType.Charge,
                    Category = "HOAMonthlyFees",
                    Description = $"HOA charge {period} - {unit.UnitNumber}",
                    Debit = amountPerUnit,
                    Credit = 0,
                    BalanceAfter = runningBalance,
                    CreatedAtUtc = chargeDate
                });
            }
        }

        context.UnitCharges.AddRange(allCharges);
        await context.SaveChangesAsync(); // need charge IDs

        // ── 3. Create Payments + Allocations for Paid/Partial ─
        foreach (var (period, statuses, monthsAgo) in periods)
        {
            var periodDate = now.AddMonths(-monthsAgo);
            var payDate = new DateTime(periodDate.Year, periodDate.Month, 8, 14, 0, 0, DateTimeKind.Utc);

            foreach (var unit in units)
            {
                if (!statuses.TryGetValue(unit.UnitNumber, out var status)) continue;
                if (status != "Paid" && status != "Partial") continue;

                var charge = allCharges.First(c => c.UnitId == unit.Id && c.Period == period);
                var payAmount = status == "Paid"
                    ? amountPerUnit
                    : Math.Round(amountPerUnit * (0.4m + (unit.Floor ?? 1) * 0.05m), 2); // 200-325 range

                // Resolve tenant user id
                string userId;
                if (unit.UnitNumber == "101")
                    userId = primaryTenant.Id;
                else if (tenantUsers.TryGetValue(unit.UnitNumber, out var tu))
                {
                    var dbUser = await userManager.FindByEmailAsync(tu.Email!);
                    userId = dbUser?.Id ?? primaryTenant.Id;
                }
                else
                    userId = primaryTenant.Id;

                var payment = new Payment
                {
                    UnitId = unit.Id,
                    UserId = userId,
                    Amount = payAmount,
                    PaymentDateUtc = payDate,
                    ProviderReference = $"FAKE-{period}-{unit.UnitNumber}",
                    Status = PaymentStatus.Succeeded,
                    CreatedAtUtc = payDate
                };
                allPayments.Add(payment);

                // Ledger: Payment entry
                runningBalance -= payAmount;
                ledgerEntries.Add(new LedgerEntry
                {
                    BuildingId = buildingId,
                    UnitId = unit.Id,
                    EntryType = LedgerEntryType.Payment,
                    Category = "HOAMonthlyFees",
                    Description = $"Payment {period} - {unit.UnitNumber}",
                    Debit = 0,
                    Credit = payAmount,
                    BalanceAfter = runningBalance,
                    CreatedAtUtc = payDate
                });
            }
        }

        context.Payments.AddRange(allPayments);
        await context.SaveChangesAsync(); // need payment IDs

        // Create allocations linking payments to charges
        foreach (var payment in allPayments)
        {
            // Find the matching charge for this unit in the same period
            var periodFromRef = payment.ProviderReference?.Split('-')[1] + "-" + payment.ProviderReference?.Split('-')[2].PadLeft(2, '0');
            // Actually, let's find by unit and amount match
            var matchingCharges = allCharges.Where(c => c.UnitId == payment.UnitId).ToList();
            var charge = matchingCharges.FirstOrDefault(c =>
            {
                var payDate = payment.PaymentDateUtc;
                var chargeDate = c.CreatedAtUtc;
                return payDate.Year == chargeDate.Year && payDate.Month == chargeDate.Month;
            });

            if (charge != null)
            {
                allAllocations.Add(new PaymentAllocation
                {
                    PaymentId = payment.Id,
                    UnitChargeId = charge.Id,
                    AllocatedAmount = payment.Amount
                });
            }
        }

        context.PaymentAllocations.AddRange(allAllocations);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Seeded HOA payment data: {Plan} plan, {Charges} charges, {Payments} payments, {Allocations} allocations across 3 months.",
            1, allCharges.Count, allPayments.Count, allAllocations.Count);

        // ── 4. Add unit-level ledger entries (merged with existing building-level) ─
        context.LedgerEntries.AddRange(ledgerEntries);
        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} unit-level ledger entries for HOA charges/payments.", ledgerEntries.Count);

        // ── 5. Seed Vendor Invoices & Payments ─────────────────────────
        if (!context.VendorInvoices.Any())
        {
            var seedBuilding = context.Buildings.First();
            var seedVendor = context.Vendors.First();

            var invoice1 = new VendorInvoice
            {
                BuildingId = seedBuilding.Id,
                VendorId = seedVendor.Id,
                Category = "Repairs",
                Description = "Monthly elevator maintenance",
                InvoiceNumber = "INV-2026-001",
                InvoiceDate = DateTime.UtcNow.AddDays(-30),
                Amount = 3500,
                DueDate = DateTime.UtcNow.AddDays(-15),
                Status = VendorInvoiceStatus.Approved,
                CreatedBy = "seeder"
            };

            var invoice2 = new VendorInvoice
            {
                BuildingId = seedBuilding.Id,
                VendorId = seedVendor.Id,
                Category = "Cleaning",
                Description = "Deep cleaning common areas",
                InvoiceNumber = "INV-2026-002",
                InvoiceDate = DateTime.UtcNow.AddDays(-10),
                Amount = 1200,
                DueDate = DateTime.UtcNow.AddDays(20),
                Status = VendorInvoiceStatus.Draft,
                CreatedBy = "seeder"
            };

            context.VendorInvoices.AddRange(invoice1, invoice2);
            await context.SaveChangesAsync();

            // Add a payment to invoice1 (partial)
            context.VendorPayments.Add(new VendorPayment
            {
                VendorInvoiceId = invoice1.Id,
                PaidAmount = 2000,
                PaidAtUtc = DateTime.UtcNow.AddDays(-10),
                PaymentMethod = VendorPaymentMethod.BankTransfer,
                Reference = "TXN-98765"
            });
            await context.SaveChangesAsync();

            // Ledger for approved invoice
            context.LedgerEntries.Add(new LedgerEntry
            {
                BuildingId = seedBuilding.Id,
                EntryType = LedgerEntryType.Expense,
                Category = "Repairs",
                Description = $"Vendor invoice INV-2026-001: Monthly elevator maintenance",
                ReferenceId = invoice1.Id,
                Debit = 3500,
                Credit = 0,
                BalanceAfter = 0,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-30)
            });
            await context.SaveChangesAsync();

            logger.LogInformation("Seeded 2 vendor invoices + 1 payment.");
        }
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
