export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  roles: string[];
  fullName: string;
  email: string;
  userId: string;
}

export interface User {
  id: string;
  email: string;
  fullName: string;
  phone?: string;
  vendorId?: number;
  roles: string[];
}

export interface BuildingDto {
  id: number;
  name: string;
  addressLine?: string;
  city?: string;
  postalCode?: string;
  notes?: string;
  unitCount: number;
}

export interface UnitDto {
  id: number;
  buildingId: number;
  unitNumber: string;
  floor?: number;
  sizeSqm?: number;
  ownerName?: string;
  tenantUserId?: string;
  tenantName?: string;
}

export interface VendorDto {
  id: number;
  name: string;
  serviceType: string;
  phone?: string;
  email?: string;
  contactName?: string;
  notes?: string;
}

export interface AssetDto {
  id: number;
  buildingId: number;
  buildingName?: string;
  name: string;
  assetType: string;
  locationDescription?: string;
  serialNumber?: string;
  installDate?: string;
  warrantyUntil?: string;
  vendorId?: number;
  vendorName?: string;
  notes?: string;
}

export interface PreventivePlanDto {
  id: number;
  assetId: number;
  assetName?: string;
  title: string;
  frequencyType: string;
  interval: number;
  nextDueDate: string;
  checklistText?: string;
}

export interface ServiceRequestDto {
  id: number;
  buildingId: number;
  buildingName?: string;
  unitId?: number;
  unitNumber?: string;
  submittedByUserId: string;
  submittedByName: string;
  phone?: string;
  email?: string;
  area: string;
  category: string;
  priority: string;
  isEmergency: boolean;
  description: string;
  status: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
  attachments: AttachmentDto[];
  // Linked vendor assignment
  assignedVendorId?: number;
  assignedVendorName?: string;
  linkedWorkOrderId?: number;
  linkedWorkOrderStatus?: string;
}

export interface AttachmentDto {
  id: number;
  fileName: string;
  contentType: string;
  url: string;
  uploadedAtUtc: string;
}

export interface WorkOrderDto {
  id: number;
  buildingId: number;
  buildingName?: string;
  buildingAddress?: string;
  serviceRequestId?: number;
  vendorId?: number;
  vendorName?: string;
  title: string;
  description?: string;
  scheduledFor?: string;
  status: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
  completedAtUtc?: string;
  notes: WorkOrderNoteDto[];
  attachments: AttachmentDto[];
  // SR details
  srArea?: string;
  srCategory?: string;
  srPriority?: string;
  srIsEmergency?: boolean;
  srPhone?: string;
  srSubmittedByName?: string;
  srDescription?: string;
  srAttachments?: AttachmentDto[];
}

export interface WorkOrderNoteDto {
  id: number;
  noteText: string;
  createdAtUtc: string;
  createdByUserId: string;
  createdByName?: string;
}

export interface CleaningPlanDto {
  id: number;
  buildingId: number;
  cleaningVendorId: number;
  cleaningVendorName?: string;
  stairwellsPerWeek: number;
  parkingPerWeek: number;
  corridorLobbyPerWeek: number;
  garbageRoomPerWeek: number;
  effectiveFrom: string;
}

export interface JobRunLogDto {
  id: number;
  jobName: string;
  periodKey: string;
  ranAtUtc: string;
}

export interface GenerateJobResponse {
  alreadyRan: boolean;
  periodKey: string;
  workOrdersCreated: number;
  message: string;
}

export const AREAS = ['Stairwell', 'Parking', 'Lobby', 'Corridor', 'GarbageRoom', 'Garden', 'Roof', 'Other'] as const;
export const CATEGORIES = ['Plumbing', 'Electrical', 'HVAC', 'Cleaning', 'Pest', 'Structural', 'Elevator', 'Security', 'General'] as const;
export const PRIORITIES = ['Low', 'Medium', 'High', 'Critical'] as const;
export const SR_STATUSES = ['New', 'InReview', 'Approved', 'Assigned', 'InProgress', 'Resolved', 'Closed', 'Rejected'] as const;
export const WO_STATUSES = ['Draft', 'Assigned', 'Scheduled', 'InProgress', 'Completed', 'Cancelled', 'OnHold'] as const;
export const VENDOR_SERVICE_TYPES = ['Cleaning', 'Plumbing', 'Electrical', 'Elevator', 'HVAC', 'Gardening', 'PestControl', 'Security', 'General'] as const;
export const ASSET_TYPES = ['Elevator', 'Generator', 'WaterPump', 'FireSystem', 'HVAC', 'Boiler', 'Intercom', 'Gate', 'SolarPanel', 'Other'] as const;
export const FREQUENCY_TYPES = ['Daily', 'Weekly', 'BiWeekly', 'Monthly', 'Quarterly', 'Yearly'] as const;

// ─── Finance Types ──────────────────────────────────────

export interface HOAFeePlanDto {
  id: number;
  buildingId: number;
  buildingName?: string;
  name: string;
  calculationMethod: string;
  amountPerSqm?: number;
  fixedAmountPerUnit?: number;
  effectiveFrom: string;
  isActive: boolean;
}

export interface UnitChargeDto {
  id: number;
  unitId: number;
  unitNumber?: string;
  floor?: number;
  tenantName?: string;
  hoaFeePlanId: number;
  period: string;
  amountDue: number;
  amountPaid: number;
  balance: number;
  dueDate: string;
  status: string;
  createdAtUtc: string;
}

export interface PaymentMethodDto {
  id: number;
  methodType: string;
  provider?: string;
  last4Digits?: string;
  expiry?: string;
  cardBrand?: string;
  isDefault: boolean;
  isActive: boolean;
}

export interface PaymentProviderConfigDto {
  id: number;
  buildingId?: number;
  buildingName?: string;
  providerType: string;
  isActive: boolean;
  merchantIdRef?: string;
  terminalIdRef?: string;
  apiUserRef?: string;
  apiPasswordRef?: string;
  webhookSecretRef?: string;
  supportedFeatures: number;
  currency: string;
  baseUrl?: string;
}

export interface PaymentSessionResponse {
  paymentUrl?: string;
  sessionId?: string;
  paymentId?: number;
  error?: string;
}

export interface TokenizationResponse {
  redirectUrl?: string;
  error?: string;
}

export interface PaymentDto {
  id: number;
  unitId: number;
  unitNumber?: string;
  userId: string;
  userName?: string;
  amount: number;
  paymentDateUtc: string;
  paymentMethodId?: number;
  last4?: string;
  providerReference?: string;
  status: string;
  createdAtUtc: string;
}

export interface CollectionRowDto {
  unitId: number;
  unitNumber: string;
  floor?: number;
  sizeSqm?: number;
  payerDisplayName?: string;
  payerPhone?: string;
  amountDue: number;
  amountPaid: number;
  outstanding: number;
  dueDate?: string;
  status: string; // Paid | Partial | Unpaid | Overdue | NotGenerated
  lastPaymentDateUtc?: string;
}

export interface CollectionSummaryDto {
  buildingId: number;
  buildingName?: string;
  period: string;
  totalUnits: number;
  generatedCount: number;
  paidCount: number;
  partialCount: number;
  unpaidCount: number;
  overdueCount: number;
  totalDue: number;
  totalPaid: number;
  totalOutstanding: number;
  collectionRatePercent: number;
}

export interface CollectionStatusReport {
  summary: CollectionSummaryDto;
  rows: CollectionRowDto[];
}

// Legacy alias kept for backward compat
export interface CollectionStatusRow {
  unitId: number;
  unitNumber: string;
  floor?: number;
  residentName?: string;
  amountDue: number;
  amountPaid: number;
  balance: number;
  status: string;
}

export interface AgingBucket {
  unitId: number;
  unitNumber: string;
  residentName?: string;
  current: number;
  days1to30: number;
  days31to60: number;
  days61to90: number;
  days90Plus: number;
  total: number;
}

export interface AgingReport {
  buildingId: number;
  buildingName?: string;
  buckets: AgingBucket[];
  grandTotal: number;
}

// ─── Income vs Expenses Report ─────────────────────────

export interface CategoryAmount {
  category: string;
  amount: number;
}

export interface MonthlyBreakdown {
  month: string;
  income: number;
  expenses: number;
  net: number;
}

export interface IncomeExpensesReport {
  buildingId: number;
  buildingName?: string;
  fromDate: string;
  toDate: string;
  totalIncome: number;
  totalExpenses: number;
  netBalance: number;
  incomeByCategory: CategoryAmount[];
  expensesByCategory: CategoryAmount[];
  monthlyBreakdown: MonthlyBreakdown[];
}

export const EXPENSE_CATEGORIES = [
  'Cleaning', 'Gardening', 'Electricity', 'ElevatorMaintenance', 'WaterPumps',
  'FireSystems', 'PestControl', 'Insurance', 'BankFees', 'Repairs', 'Projects', 'Other'
] as const;

export const INCOME_CATEGORIES = [
  'HOAMonthlyFees', 'SpecialAssessment', 'LateFees', 'OtherIncome'
] as const;

// ─── Tenant Management ─────────────────────────────────

export interface TenantProfileDto {
  id: number;
  unitId: number;
  unitNumber?: string;
  floor?: number;
  buildingId: number;
  buildingName?: string;
  userId?: string;
  fullName: string;
  phone?: string;
  email?: string;
  moveInDate?: string;
  moveOutDate?: string;
  isActive: boolean;
  isArchived: boolean;
  notes?: string;
  createdAtUtc: string;
}

export interface CreateTenantRequest {
  unitId: number;
  fullName: string;
  phone?: string;
  email?: string;
  moveInDate?: string;
  isActive: boolean;
  notes?: string;
  userId?: string;
}

export interface UpdateTenantRequest {
  fullName: string;
  phone?: string;
  email?: string;
  moveInDate?: string;
  moveOutDate?: string;
  isActive: boolean;
  notes?: string;
}

export interface EndTenancyRequest {
  moveOutDate: string;
}

export const TENANT_STATUSES = ['Active', 'Inactive', 'Archived'] as const;

export const HOA_CALC_METHODS = ['BySqm', 'FixedPerUnit', 'ManualPerUnit'] as const;
export const CHARGE_STATUSES = ['Pending', 'Paid', 'PartiallyPaid', 'Overdue', 'Cancelled'] as const;
export const PAYMENT_STATUSES = ['Pending', 'Succeeded', 'Failed', 'Refunded'] as const;
export const PAYMENT_PROVIDERS = ['Fake', 'Meshulam', 'Pelecard', 'Tranzila'] as const;
export const PROVIDER_FEATURES = {
  HostedPaymentPage: 1,
  Tokenization: 2,
  RecurringCharges: 4,
  Refunds: 8,
  Webhooks: 16,
} as const;

// ─── Vendor Invoices & Payments ─────────────────────────

export interface VendorInvoiceDto {
  id: number;
  buildingId: number;
  buildingName?: string;
  vendorId: number;
  vendorName?: string;
  workOrderId?: number;
  serviceRequestId?: number;
  category?: string;
  description?: string;
  invoiceNumber?: string;
  invoiceDate: string;
  amount: number;
  paidAmount: number;
  balance: number;
  dueDate?: string;
  status: string;
  notes?: string;
  createdAtUtc: string;
}

export interface VendorPaymentDto {
  id: number;
  vendorInvoiceId: number;
  paidAmount: number;
  paidAtUtc: string;
  paymentMethod: string;
  reference?: string;
  notes?: string;
  createdAtUtc: string;
}

export const VENDOR_INVOICE_STATUSES = ['Draft', 'Approved', 'Paid', 'Cancelled'] as const;
export const VENDOR_PAYMENT_METHODS = ['BankTransfer', 'CreditCard', 'Cash', 'Check', 'Other'] as const;
export const VENDOR_INVOICE_CATEGORIES = [
  'Cleaning', 'Gardening', 'PestControl', 'Repairs', 'Elevator', 'Electricity', 'Other'
] as const;
