import apiClient from './client';
import type {
  LoginRequest, LoginResponse, BuildingDto, UnitDto, VendorDto,
  AssetDto, PreventivePlanDto, ServiceRequestDto, WorkOrderDto,
  CleaningPlanDto, GenerateJobResponse, JobRunLogDto, WorkOrderNoteDto,
  AttachmentDto, HOAFeePlanDto, UnitChargeDto, PaymentMethodDto,
  PaymentDto, CollectionStatusReport, AgingReport,
  PaymentProviderConfigDto, PaymentSessionResponse, TokenizationResponse,
  TenantProfileDto, CreateTenantRequest, UpdateTenantRequest, EndTenancyRequest,
  VendorInvoiceDto, VendorPaymentDto,
  SmsTemplateDto, SmsCampaignDto, CreateCampaignResult, SendCampaignResult, SmsCampaignRecipientDto
} from '../types';

// Auth
export const authApi = {
  login: (data: LoginRequest) => apiClient.post<LoginResponse>('/api/auth/login', data),
  refresh: (refreshToken: string) => apiClient.post<LoginResponse>('/api/auth/refresh', { refreshToken }),
  logout: (refreshToken?: string) => apiClient.post('/api/auth/logout', { refreshToken }),
  me: () => apiClient.get('/api/auth/me'),
};

// Buildings
export const buildingsApi = {
  getAll: () => apiClient.get<BuildingDto[]>('/api/buildings'),
  getById: (id: number) => apiClient.get<BuildingDto>(`/api/buildings/${id}`),
  create: (data: Partial<BuildingDto>) => apiClient.post<BuildingDto>('/api/buildings', data),
  update: (id: number, data: Partial<BuildingDto>) => apiClient.put(`/api/buildings/${id}`, data),
  delete: (id: number) => apiClient.delete(`/api/buildings/${id}`),
  getUnits: (id: number) => apiClient.get<UnitDto[]>(`/api/buildings/${id}/units`),
  createUnit: (buildingId: number, data: Partial<UnitDto>) => apiClient.post<UnitDto>(`/api/buildings/${buildingId}/units`, data),
};

// Vendors
export const vendorsApi = {
  getAll: () => apiClient.get<VendorDto[]>('/api/vendors'),
  getById: (id: number) => apiClient.get<VendorDto>(`/api/vendors/${id}`),
  create: (data: Partial<VendorDto>) => apiClient.post<VendorDto>('/api/vendors', data),
  update: (id: number, data: Partial<VendorDto>) => apiClient.put(`/api/vendors/${id}`, data),
};

// Assets
export const assetsApi = {
  getAll: (buildingId?: number) => apiClient.get<AssetDto[]>('/api/assets', { params: { buildingId } }),
  create: (data: Partial<AssetDto>) => apiClient.post<AssetDto>('/api/assets', data),
};

// Preventive Plans
export const preventivePlansApi = {
  getAll: (assetId?: number) => apiClient.get<PreventivePlanDto[]>('/api/preventiveplans', { params: { assetId } }),
  create: (data: Partial<PreventivePlanDto>) => apiClient.post<PreventivePlanDto>('/api/preventiveplans', data),
  generateNow: () => apiClient.post<GenerateJobResponse>('/api/preventiveplans/generate-now'),
};

// Service Requests
export const serviceRequestsApi = {
  getAll: (params?: { buildingId?: number; unitId?: number; status?: string }) =>
    apiClient.get<ServiceRequestDto[]>('/api/servicerequests', { params }),
  getMy: () => apiClient.get<ServiceRequestDto[]>('/api/servicerequests/my'),
  getById: (id: number) => apiClient.get<ServiceRequestDto>(`/api/servicerequests/${id}`),
  create: (data: any) => apiClient.post<ServiceRequestDto>('/api/servicerequests', data),
  updateStatus: (id: number, data: { status: string; note?: string }) =>
    apiClient.put(`/api/servicerequests/${id}/status`, data),
  assignVendor: (id: number, data: { vendorId: number; scheduledFor?: string; title?: string; notes?: string }) =>
    apiClient.put(`/api/servicerequests/${id}/assign-vendor`, data),
  uploadAttachments: (id: number, files: File[]) => {
    const formData = new FormData();
    files.forEach(f => formData.append('files', f));
    return apiClient.post<AttachmentDto[]>(`/api/servicerequests/${id}/attachments`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
};

// Work Orders
export const workOrdersApi = {
  getAll: (params?: { buildingId?: number; vendorId?: number; status?: string }) =>
    apiClient.get<WorkOrderDto[]>('/api/workorders', { params }),
  getMy: () => apiClient.get<WorkOrderDto[]>('/api/workorders/my'),
  getById: (id: number) => apiClient.get<WorkOrderDto>(`/api/workorders/${id}`),
  create: (data: any) => apiClient.post<WorkOrderDto>('/api/workorders', data),
  assign: (id: number, data: { vendorId: number; scheduledFor?: string }) =>
    apiClient.put(`/api/workorders/${id}/assign`, data),
  updateStatus: (id: number, data: { status: string }) =>
    apiClient.put(`/api/workorders/${id}/status`, data),
  addNote: (id: number, data: { noteText: string }) =>
    apiClient.post<WorkOrderNoteDto>(`/api/workorders/${id}/notes`, data),
  uploadAttachments: (id: number, files: File[]) => {
    const formData = new FormData();
    files.forEach(f => formData.append('files', f));
    return apiClient.post<AttachmentDto[]>(`/api/workorders/${id}/attachments`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
};

// Cleaning Plans
export const cleaningPlansApi = {
  getByBuilding: (buildingId: number) => apiClient.get<CleaningPlanDto | null>(`/api/cleaningplans/${buildingId}`),
  create: (buildingId: number, data: any) => apiClient.post<CleaningPlanDto>(`/api/cleaningplans/${buildingId}`, data),
  generateWeekly: (buildingId: number) => apiClient.post<GenerateJobResponse>(`/api/cleaningplans/${buildingId}/generate-weekly`),
};

// Jobs
export const jobsApi = {
  generatePreventive: () => apiClient.post<GenerateJobResponse>('/api/jobs/generate-preventive'),
  generateCleaningWeek: () => apiClient.post<GenerateJobResponse>('/api/jobs/generate-cleaning-week'),
  getLogs: () => apiClient.get<JobRunLogDto[]>('/api/jobs/logs'),
};

// ─── Finance / HOA ─────────────────────────────────────

export const hoaApi = {
  getPlans: (buildingId: number) => apiClient.get<HOAFeePlanDto[]>(`/api/hoa/plans/${buildingId}`),
  createPlan: (data: Partial<HOAFeePlanDto>) => apiClient.post<HOAFeePlanDto>('/api/hoa/plans', data),
  updatePlan: (id: number, data: Partial<HOAFeePlanDto>) => apiClient.put(`/api/hoa/plans/${id}`, data),
  generateCharges: (planId: number, period: string) =>
    apiClient.post(`/api/hoa/plans/${planId}/generate/${period}`),
  getCharges: (params?: { buildingId?: number; period?: string }) =>
    apiClient.get<UnitChargeDto[]>('/api/hoa/charges', { params }),
  getMyCharges: () => apiClient.get<UnitChargeDto[]>('/api/hoa/charges/my'),
  getChargesForUnit: (unitId: number) => apiClient.get<UnitChargeDto[]>(`/api/hoa/charges/unit/${unitId}`),
  adjustCharge: (id: number, data: { newAmount: number; reason?: string }) =>
    apiClient.put(`/api/hoa/charges/${id}/adjust`, data),
  // Manual payments
  addManualPayment: (unitChargeId: number, data: { paidAmount: number; paidAt?: string; method: string; reference?: string; notes?: string }) =>
    apiClient.post(`/api/hoa/charges/${unitChargeId}/manual-payment`, data),
  editManualPayment: (paymentId: number, data: { paidAmount: number; paidAt?: string; method: string; reference?: string; notes?: string }) =>
    apiClient.put(`/api/hoa/manual-payments/${paymentId}`, data),
  deleteManualPayment: (paymentId: number) =>
    apiClient.delete(`/api/hoa/manual-payments/${paymentId}`),
  getChargePayments: (unitChargeId: number) =>
    apiClient.get(`/api/hoa/charges/${unitChargeId}/payments`),
};

export const paymentsApi = {
  // Legacy direct tokenization (Fake provider only)
  setupMethod: (data: { methodType: string; cardNumber?: string; expiry?: string; cvv?: string; isDefault: boolean; provider?: string }) =>
    apiClient.post<PaymentMethodDto>('/api/payments/setup-method', data),
  // Hosted tokenization flow (all providers)
  startTokenization: (data: { buildingId: number; isDefault: boolean }) =>
    apiClient.post<TokenizationResponse>('/api/payments/tokenize', data),
  getMethods: () => apiClient.get<PaymentMethodDto[]>('/api/payments/methods'),
  setDefault: (id: number) => apiClient.put(`/api/payments/methods/${id}/default`),
  deleteMethod: (id: number) => apiClient.delete(`/api/payments/methods/${id}`),
  // Direct charge via saved token
  payCharge: (unitChargeId: number, data: { paymentMethodId?: number; amount?: number }) =>
    apiClient.post<PaymentDto>(`/api/payments/pay/${unitChargeId}`, data),
  // Hosted payment session (redirects user to provider)
  createSession: (unitChargeId: number) =>
    apiClient.post<PaymentSessionResponse>(`/api/payments/session/${unitChargeId}`),
  getMyPayments: () => apiClient.get<PaymentDto[]>('/api/payments/my'),
  getPaymentsForUnit: (unitId: number) => apiClient.get<PaymentDto[]>(`/api/payments/unit/${unitId}`),
};

// ─── Payment Provider Config ───────────────────────────

export const paymentConfigApi = {
  getAll: (buildingId?: number) =>
    apiClient.get<PaymentProviderConfigDto[]>('/api/payment-config', { params: buildingId ? { buildingId } : undefined }),
  getById: (id: number) => apiClient.get<PaymentProviderConfigDto>(`/api/payment-config/${id}`),
  create: (data: Partial<PaymentProviderConfigDto>) => apiClient.post<PaymentProviderConfigDto>('/api/payment-config', data),
  update: (id: number, data: Partial<PaymentProviderConfigDto>) => apiClient.put(`/api/payment-config/${id}`, data),
  delete: (id: number) => apiClient.delete(`/api/payment-config/${id}`),
  getProviders: () => apiClient.get<string[]>('/api/payment-config/providers'),
};

// ─── Tenants ──────────────────────────────────────────

export const tenantsApi = {
  getAll: (params?: { buildingId?: number; unitId?: number; activeOnly?: boolean; includeArchived?: boolean }) =>
    apiClient.get<TenantProfileDto[]>('/api/tenants', { params }),
  getById: (id: number) => apiClient.get<TenantProfileDto>(`/api/tenants/${id}`),
  create: (data: CreateTenantRequest) => apiClient.post<TenantProfileDto>('/api/tenants', data),
  update: (id: number, data: UpdateTenantRequest) => apiClient.put(`/api/tenants/${id}`, data),
  endTenancy: (id: number, data: EndTenancyRequest) => apiClient.post(`/api/tenants/${id}/end-tenancy`, data),
  delete: (id: number) => apiClient.delete(`/api/tenants/${id}`),
  unitHistory: (unitId: number) => apiClient.get<TenantProfileDto[]>(`/api/tenants/unit/${unitId}/history`),
};

// ─── Vendor Invoices ──────────────────────────────────

export const vendorInvoicesApi = {
  getAll: (params?: { buildingId?: number; vendorId?: number; status?: string; from?: string; to?: string }) =>
    apiClient.get<VendorInvoiceDto[]>('/api/vendor-invoices', { params }),
  getById: (id: number) => apiClient.get<VendorInvoiceDto>(`/api/vendor-invoices/${id}`),
  create: (data: any) => apiClient.post<VendorInvoiceDto>('/api/vendor-invoices', data),
  update: (id: number, data: any) => apiClient.put(`/api/vendor-invoices/${id}`, data),
  approve: (id: number) => apiClient.post(`/api/vendor-invoices/${id}/approve`),
  cancel: (id: number) => apiClient.post(`/api/vendor-invoices/${id}/cancel`),
  delete: (id: number) => apiClient.delete(`/api/vendor-invoices/${id}`),
  getPayments: (invoiceId: number) => apiClient.get<VendorPaymentDto[]>(`/api/vendor-invoices/${invoiceId}/payments`),
  addPayment: (invoiceId: number, data: any) => apiClient.post<VendorPaymentDto>(`/api/vendor-invoices/${invoiceId}/payments`, data),
  updatePayment: (paymentId: number, data: any) => apiClient.put(`/api/vendor-invoices/payments/${paymentId}`, data),
  deletePayment: (paymentId: number) => apiClient.delete(`/api/vendor-invoices/payments/${paymentId}`),
};

export const reportsApi = {
  collectionStatus: (buildingId: number, period?: string, includeNotGenerated?: boolean) =>
    apiClient.get<CollectionStatusReport>(`/api/reports/collection-status/${buildingId}`, { params: { period, includeNotGenerated } }),
  collectionUnitDetail: (buildingId: number, unitId: number, period?: string) =>
    apiClient.get(`/api/reports/collection-status/${buildingId}/unit/${unitId}`, { params: { period } }),
  aging: (buildingId: number) =>
    apiClient.get<AgingReport>(`/api/reports/aging/${buildingId}`),
  collectionStatusCsv: (buildingId: number, period?: string, includeNotGenerated?: boolean, lang?: string) =>
    apiClient.get(`/api/reports/collection-status/${buildingId}/csv`, { params: { period, includeNotGenerated, lang }, responseType: 'blob' }),
  agingCsv: (buildingId: number, lang?: string) =>
    apiClient.get(`/api/reports/aging/${buildingId}/csv`, { params: { lang }, responseType: 'blob' }),
  incomeExpenses: (buildingId: number, from?: string, to?: string) =>
    apiClient.get<import('../types').IncomeExpensesReport>(`/api/reports/income-expenses/${buildingId}`, { params: { from, to } }),
  incomeExpensesCsv: (buildingId: number, from?: string, to?: string, lang?: string) =>
    apiClient.get(`/api/reports/income-expenses/${buildingId}/csv`, { params: { from, to, lang }, responseType: 'blob' }),
  dashboardCollection: (period?: string) =>
    apiClient.get<import('../types').CollectionSummaryDto[]>(`/api/reports/dashboard/collection`, { params: { period } }),
};

// ─── SMS Notifications ──────────────────────────────────

export const smsApi = {
  getTemplates: (lang?: string) =>
    apiClient.get<SmsTemplateDto[]>('/api/notifications/sms/templates', { params: lang ? { lang } : undefined }),
  getCampaigns: (params?: { buildingId?: number; period?: string }) =>
    apiClient.get<SmsCampaignDto[]>('/api/notifications/sms/campaigns', { params }),
  getCampaign: (id: number) =>
    apiClient.get<CreateCampaignResult>(`/api/notifications/sms/campaigns/${id}`),
  createCampaign: (data: { buildingId: number; period: string; templateId: number; includePartial: boolean; notes?: string }) =>
    apiClient.post<CreateCampaignResult>('/api/notifications/sms/hoa-nonpayment/campaigns', data),
  updateRecipients: (campaignId: number, data: { updates?: { recipientId: number; isSelected: boolean }[]; addUnitIds?: number[]; removeRecipientIds?: number[] }) =>
    apiClient.put<SmsCampaignRecipientDto[]>(`/api/notifications/sms/campaigns/${campaignId}/recipients`, data),
  previewMessage: (campaignId: number, recipientId: number) =>
    apiClient.get<{ message: string }>(`/api/notifications/sms/campaigns/${campaignId}/recipients/${recipientId}/preview`),
  sendCampaign: (campaignId: number) =>
    apiClient.post<SendCampaignResult>(`/api/notifications/sms/campaigns/${campaignId}/send`, { confirm: true }),
};
