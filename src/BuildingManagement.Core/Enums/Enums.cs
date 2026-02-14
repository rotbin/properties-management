namespace BuildingManagement.Core.Enums;

public enum Area
{
    Stairwell = 0,
    Parking = 1,
    Lobby = 2,
    Corridor = 3,
    GarbageRoom = 4,
    Garden = 5,
    Roof = 6,
    Other = 99
}

public enum ServiceRequestCategory
{
    Plumbing = 0,
    Electrical = 1,
    HVAC = 2,
    Cleaning = 3,
    Pest = 4,
    Structural = 5,
    Elevator = 6,
    Security = 7,
    General = 99
}

public enum Priority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum ServiceRequestStatus
{
    New = 0,
    InReview = 1,
    Approved = 2,
    InProgress = 3,
    Resolved = 4,
    Closed = 5,
    Rejected = 6
}

public enum WorkOrderStatus
{
    Draft = 0,
    Assigned = 1,
    Scheduled = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5,
    OnHold = 6
}

public enum FrequencyType
{
    Daily = 0,
    Weekly = 1,
    BiWeekly = 2,
    Monthly = 3,
    Quarterly = 4,
    Yearly = 5
}

public enum AssetType
{
    Elevator = 0,
    Generator = 1,
    WaterPump = 2,
    FireSystem = 3,
    HVAC = 4,
    Boiler = 5,
    Intercom = 6,
    Gate = 7,
    SolarPanel = 8,
    Other = 99
}

public enum VendorServiceType
{
    Cleaning = 0,
    Plumbing = 1,
    Electrical = 2,
    Elevator = 3,
    HVAC = 4,
    Gardening = 5,
    PestControl = 6,
    Security = 7,
    General = 99
}

// ─── Finance Enums ───────────────────────────────────

public enum HOACalculationMethod
{
    BySqm = 0,
    FixedPerUnit = 1,
    ManualPerUnit = 2
}

public enum UnitChargeStatus
{
    Pending = 0,
    Paid = 1,
    PartiallyPaid = 2,
    Overdue = 3,
    Cancelled = 4
}

public enum PaymentMethodType
{
    CreditCard = 0,
    BankAccount = 1
}

public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Refunded = 3
}

public enum LedgerEntryType
{
    Charge = 0,
    Payment = 1,
    Adjustment = 2,
    Expense = 3
}

public enum ExpenseCategory
{
    Cleaning = 0,
    Gardening = 1,
    Electricity = 2,
    ElevatorMaintenance = 3,
    WaterPumps = 4,
    FireSystems = 5,
    PestControl = 6,
    Insurance = 7,
    BankFees = 8,
    Repairs = 9,
    Projects = 10,
    Other = 99
}

public enum IncomeCategory
{
    HOAMonthlyFees = 0,
    SpecialAssessment = 1,
    LateFees = 2,
    OtherIncome = 99
}

public enum PaymentProviderType
{
    Fake = 0,
    Meshulam = 1,
    Pelecard = 2,
    Tranzila = 3
}

[Flags]
public enum ProviderFeatures
{
    None = 0,
    HostedPaymentPage = 1,
    Tokenization = 2,
    RecurringCharges = 4,
    Refunds = 8,
    Webhooks = 16
}

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Tenant = "Tenant";
    public const string Vendor = "Vendor";

    public static readonly string[] All = [Admin, Manager, Tenant, Vendor];
}
