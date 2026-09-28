using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceAudit360.Persistence.Seed;

/// <summary>
/// Applies pending migrations and seeds the reference data the application cannot start without:
/// roles, the bootstrap administrator, the Indian bank catalogue, categories and settings.
/// </summary>
public sealed class DatabaseInitializer(
    ApplicationDbContext context,
    IPasswordHasher passwordHasher,
    ILogger<DatabaseInitializer> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        if (context.Database.IsRelational())
        {
            logger.LogInformation("Applying database migrations...");
            await context.Database.MigrateAsync(cancellationToken);
        }
    }

    public async Task SeedAsync(string adminPassword, CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(cancellationToken);
        await SyncSystemRolePermissionsAsync(cancellationToken);
        await SeedAdminAsync(adminPassword, cancellationToken);
        await SeedBanksAsync(cancellationToken);
        await SeedCategoriesAsync(cancellationToken);
        await SeedSettingsAsync(cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed data verified.");
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        if (await context.Roles.AnyAsync(cancellationToken))
        {
            return;
        }

        var administrator = Role.Create(Roles.Administrator, "Full access to every module.", isSystemRole: true);
        administrator.ReplacePermissions(Permissions.All);

        var auditor = Role.Create(Roles.Auditor, "Can import statements and maintain transactions.", isSystemRole: true);
        auditor.ReplacePermissions(Permissions.Auditor);

        var viewer = Role.Create(Roles.Viewer, "Read-only access.", isSystemRole: true);
        viewer.ReplacePermissions(Permissions.ReadOnly);

        await context.Roles.AddRangeAsync([administrator, auditor, viewer], cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Grants each system role any permission added to the application since the database was first seeded.
    /// <see cref="SeedRolesAsync"/> only runs on an empty database, so without this a new permission would
    /// never reach an existing Administrator. Grant-only by design: permissions added to a system role by
    /// hand are left alone.
    /// </summary>
    private async Task SyncSystemRolePermissionsAsync(CancellationToken cancellationToken)
    {
        var canonical = new Dictionary<string, IReadOnlyList<string>>
        {
            [Roles.Administrator] = Permissions.All,
            [Roles.Auditor] = Permissions.Auditor,
            [Roles.Viewer] = Permissions.ReadOnly
        };

        var roles = await context.Roles
            .Include(r => r.Permissions)
            .Where(r => r.IsSystemRole)
            .ToListAsync(cancellationToken);

        var granted = 0;

        foreach (var role in roles)
        {
            if (!canonical.TryGetValue(role.Name, out var expected))
            {
                continue;
            }

            var existing = role.Permissions.Select(p => p.Permission).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var permission in expected.Where(p => !existing.Contains(p)))
            {
                role.GrantPermission(permission);
                granted++;
                logger.LogInformation("Granted '{Permission}' to the {Role} role.", permission, role.Name);
            }
        }

        if (granted > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedAdminAsync(string adminPassword, CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var adminRole = await context.Roles.FirstAsync(r => r.Name == Roles.Administrator, cancellationToken);

        var admin = User.Create(
            "admin",
            "admin@financeaudit360.local",
            "System Administrator",
            passwordHasher.Hash(adminPassword));

        admin.AssignRole(adminRole.Id);

        await context.Users.AddAsync(admin, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Bootstrap administrator 'admin' created. Change the password immediately after first login.");
    }

    private async Task SeedBanksAsync(CancellationToken cancellationToken)
    {
        var banks = new[]
        {
            Bank.Create("HDFC Bank", "HDFC", BankCode.Hdfc, "HDFC BANK|HDFCBANK|HDFC Bank Limited|hdfcbank.com"),
            Bank.Create("ICICI Bank", "ICICI", BankCode.Icici, "ICICI BANK|ICICIBANK|ICICI Bank Limited|icicibank.com"),
            Bank.Create("State Bank of India", "SBI", BankCode.Sbi, "STATE BANK OF INDIA|SBI CARD|SBICARD|onlinesbi"),
            Bank.Create("Axis Bank", "AXIS", BankCode.Axis, "AXIS BANK|AXISBANK|Axis Bank Ltd|axisbank.com"),
            Bank.Create("Kotak Mahindra Bank", "KOTAK", BankCode.Kotak, "KOTAK MAHINDRA|KOTAK BANK|kotak.com"),
            Bank.Create("IndusInd Bank", "INDUSIND", BankCode.IndusInd, "INDUSIND BANK|INDUSIND|indusind.com"),
            Bank.Create("Standard Chartered Bank", "SC", BankCode.StandardChartered, "STANDARD CHARTERED|SCB|sc.com"),
            Bank.Create("American Express", "AMEX", BankCode.AmericanExpress, "AMERICAN EXPRESS|AMEX|americanexpress.com"),
            Bank.Create("Citibank", "CITI", BankCode.Citi, "CITIBANK|CITI BANK|citibank.co.in"),
            Bank.Create("Yes Bank", "YES", BankCode.Yes, "YES BANK|YESBANK"),
            Bank.Create("RBL Bank", "RBL", BankCode.Rbl, "RBL BANK|RATNAKAR"),
            Bank.Create("IDFC FIRST Bank", "IDFC", BankCode.IdfcFirst, "IDFC FIRST|IDFC BANK"),
            Bank.Create("South Indian Bank", "SIB", BankCode.SouthIndian, "SOUTH INDIAN BANK|ONECARD|ONE CARD"),
            Bank.Create("Uni Card", "UNI", BankCode.UniCard, "UNI CARD|UNI CARDS|uni.cards"),
            Bank.Create("Slice Card", "SLICE", BankCode.Slice, "SLICE CARD|SLICE CREDIT CARD|sliceit.com"),
            Bank.Create("Punjab National Bank", "PNB", BankCode.PunjabNational, "PUNJAB NATIONAL BANK|PNB|pnbindia.in"),
            Bank.Create("Uttar Pradesh Gramin Bank", "UPGB", BankCode.UttarPradeshGramin, "UTTAR PRADESH GRAMIN BANK|UP GRAMIN BANK|UPGB"),
            Bank.Create("Airtel Payments Bank", "AIRTEL", BankCode.AirtelPayments, "AIRTEL PAYMENTS BANK|AIRTEL PAYMENT BANK|AIRTEL BANK"),
            Bank.Create("Paytm PostPaid", "PAYTM", BankCode.PaytmPostPaid, "PAYTM POSTPAID|PAYTM POST PAID|POSTPAID BY PAYTM")
        };

        var existingCodes = await context.Banks
            .Select(bank => bank.Code)
            .ToHashSetAsync(cancellationToken);
        var missingBanks = banks.Where(bank => !existingCodes.Contains(bank.Code)).ToArray();
        if (missingBanks.Length == 0)
        {
            return;
        }

        await context.Banks.AddRangeAsync(missingBanks, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedCategoriesAsync(CancellationToken cancellationToken)
    {
        if (await context.Categories.AnyAsync(cancellationToken))
        {
            return;
        }

        var seeds = new (string Name, string Code, string Keywords, string Color, int Order)[]
        {
            ("Food & Dining", "FOOD", "SWIGGY|ZOMATO|RESTAURANT|CAFE|DOMINO|MCDONALD|STARBUCKS|EATFIT", "#f97316", 1),
            ("Groceries", "GROCERY", "BIGBASKET|BLINKIT|DMART|ZEPTO|GROFERS|RELIANCE FRESH|MORE SUPERMARKET", "#16a34a", 2),
            ("Shopping", "SHOP", "AMAZON|FLIPKART|MYNTRA|AJIO|MEESHO|NYKAA|TATA CLIQ", "#8b5cf6", 3),
            ("Travel", "TRAVEL", "IRCTC|MAKEMYTRIP|GOIBIBO|INDIGO|AIR INDIA|VISTARA|YATRA|CLEARTRIP", "#0ea5e9", 4),
            ("Transport", "TRANSPORT", "UBER|OLA|RAPIDO|FASTAG|PETROL|HP PETROL|INDIAN OIL|BHARAT PETROLEUM", "#14b8a6", 5),
            ("Utilities", "UTIL", "ELECTRICITY|BSES|TATA POWER|GAS|WATER BILL|BROADBAND|AIRTEL|JIO|VODAFONE|ACT FIBERNET", "#eab308", 6),
            ("Entertainment", "ENTERTAIN", "NETFLIX|PRIME VIDEO|HOTSTAR|SPOTIFY|BOOKMYSHOW|PVR|INOX|YOUTUBE", "#ec4899", 7),
            ("Healthcare", "HEALTH", "APOLLO|PHARMEASY|1MG|NETMEDS|HOSPITAL|CLINIC|DIAGNOSTIC|MEDPLUS", "#ef4444", 8),
            ("Education", "EDU", "UDEMY|COURSERA|BYJU|UNACADEMY|SCHOOL FEE|COLLEGE|TUITION", "#6366f1", 9),
            ("Insurance", "INSURE", "LIC|INSURANCE|POLICYBAZAAR|HDFC LIFE|ICICI PRU|STAR HEALTH", "#64748b", 10),
            ("Investments", "INVEST", "ZERODHA|GROWW|UPSTOX|MUTUAL FUND|SIP|NPS|PPF", "#22c55e", 11),
            ("Card Payment", "CARDPAY", "PAYMENT RECEIVED|AUTO DEBIT|NEFT CR|CREDIT CARD PAYMENT|THANK YOU", "#0f766e", 12),
            ("Fees & Charges", "FEES", "ANNUAL FEE|LATE FEE|FINANCE CHARGE|GST|INTEREST|SURCHARGE|CONVENIENCE FEE", "#b91c1c", 13),
            ("Cash Withdrawal", "CASH", "ATM|CASH WDL|CASH WITHDRAWAL", "#a16207", 14),
            ("Transfer", "TRANSFER", "UPI|IMPS|NEFT|RTGS|FUND TRANSFER", "#3b82f6", 15),
            ("Rewards", "REWARD", "CASHBACK|REWARD POINTS|REDEMPTION", "#84cc16", 16),
            ("Rent", "RENT", "RENT|LEASE|MAINTENANCE", "#7c3aed", 17),
            ("Uncategorised", "OTHER", "", "#94a3b8", 99)
        };

        var categories = seeds
            .Select(s =>
            {
                var category = TransactionCategory.Create(s.Name, s.Code, null, s.Keywords, isSystemCategory: true);
                category.Update(s.Name, s.Code, null, s.Color, null, null, s.Keywords, s.Order);
                return category;
            })
            .ToList();

        await context.Categories.AddRangeAsync(categories, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSettingsAsync(CancellationToken cancellationToken)
    {
        var defaults = new (string Key, string? Value, SettingDataType Type, string Category, string Description)[]
        {
            ("App.DefaultCurrency", "INR", SettingDataType.String, "General", "Currency used when a statement does not declare one."),
            ("App.DateFormat", "dd MMM yyyy", SettingDataType.String, "General", "Display date format."),
            ("Import.AutoMatchVendors", "true", SettingDataType.Boolean, "Import", "Match merchant text to vendors during import."),
            ("Import.AutoMatchCategories", "true", SettingDataType.Boolean, "Import", "Apply category keywords during import."),
            ("Import.AutoMatchPersons", "true", SettingDataType.Boolean, "Import", "Link transactions to persons using their keywords."),
            ("Import.SkipDuplicates", "true", SettingDataType.Boolean, "Import", "Skip transaction lines already present."),
            ("Import.RetentionDays", "365", SettingDataType.Number, "Import", "Days to retain uploaded PDF files."),
            ("Security.AccessTokenMinutes", "30", SettingDataType.Number, "Security", "Access token lifetime in minutes."),
            ("Security.RefreshTokenDays", "14", SettingDataType.Number, "Security", "Refresh token lifetime in days."),
            ("Dashboard.DefaultPeriod", "Last30Days", SettingDataType.String, "Dashboard", "Default dashboard period."),
            ("Grid.DefaultPageSize", "25", SettingDataType.Number, "Grid", "Default rows per page in grids.")
        };

        var existingKeys = await context.Settings.Select(s => s.Key).ToListAsync(cancellationToken);

        var missing = defaults
            .Where(d => !existingKeys.Contains(d.Key))
            .Select(d => Setting.Create(d.Key, d.Value, d.Type, d.Category, d.Description, isSystem: true))
            .ToList();

        if (missing.Count > 0)
        {
            await context.Settings.AddRangeAsync(missing, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
