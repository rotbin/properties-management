import React, { useEffect, useState, useCallback } from 'react';
import {
  Box, Typography, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, Chip, MenuItem, TextField, Button, Dialog, DialogTitle, DialogContent,
  DialogActions, CircularProgress, Alert, useMediaQuery, useTheme, Card, CardContent,
  Stack, CardActionArea, IconButton, Tooltip
} from '@mui/material';
import {
  Add, Edit, CheckCircle, Cancel, Delete, Payment, Receipt
} from '@mui/icons-material';
import { vendorInvoicesApi, buildingsApi, vendorsApi, workOrdersApi } from '../../api/services';
import type { VendorInvoiceDto, VendorPaymentDto, BuildingDto, VendorDto, WorkOrderDto } from '../../types';
import { VENDOR_INVOICE_STATUSES, VENDOR_INVOICE_CATEGORIES, VENDOR_PAYMENT_METHODS } from '../../types';
import { formatDateOnly, formatCurrency } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';

const statusColor = (s: string) => s === 'Paid' ? 'success' : s === 'Approved' ? 'info' : s === 'Cancelled' ? 'error' : 'default';

const VendorInvoicesPage: React.FC = () => {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));

  const [invoices, setInvoices] = useState<VendorInvoiceDto[]>([]);
  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [vendors, setVendors] = useState<VendorDto[]>([]);
  const [workOrders, setWorkOrders] = useState<WorkOrderDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  // Filters
  const [filterBuilding, setFilterBuilding] = useState('');
  const [filterVendor, setFilterVendor] = useState('');
  const [filterStatus, setFilterStatus] = useState('');

  // Invoice dialog
  const [formOpen, setFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<any>({
    buildingId: '', vendorId: '', workOrderId: '', category: 'Other', description: '',
    invoiceNumber: '', invoiceDate: new Date().toISOString().split('T')[0], amount: '', dueDate: '', notes: ''
  });

  // Payment dialog
  const [payOpen, setPayOpen] = useState(false);
  const [payInvoiceId, setPayInvoiceId] = useState<number | null>(null);
  const [payForm, setPayForm] = useState<any>({
    paidAmount: '', paidAtUtc: new Date().toISOString().split('T')[0], paymentMethod: 'BankTransfer', reference: '', notes: ''
  });

  // Payments list dialog
  const [paymentsOpen, setPaymentsOpen] = useState(false);
  const [paymentsInvoice, setPaymentsInvoice] = useState<VendorInvoiceDto | null>(null);
  const [payments, setPayments] = useState<VendorPaymentDto[]>([]);
  const [paymentsLoading, setPaymentsLoading] = useState(false);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      const params: any = {};
      if (filterBuilding) params.buildingId = Number(filterBuilding);
      if (filterVendor) params.vendorId = Number(filterVendor);
      if (filterStatus) params.status = filterStatus;
      const [i, b, v, wo] = await Promise.all([
        vendorInvoicesApi.getAll(params), buildingsApi.getAll(), vendorsApi.getAll(), workOrdersApi.getAll()
      ]);
      setInvoices(i.data); setBuildings(b.data); setVendors(v.data); setWorkOrders(wo.data);
    } catch { setError(t('vendorInvoices.failedLoad')); }
    finally { setLoading(false); }
  }, [filterBuilding, filterVendor, filterStatus, t]);

  useEffect(() => { load(); }, [load]);

  // ─── Invoice CRUD ─────────────────────────────────────

  const openCreate = () => {
    setEditingId(null);
    setForm({
      buildingId: filterBuilding || '', vendorId: filterVendor || '', workOrderId: '',
      category: 'Other', description: '', invoiceNumber: '',
      invoiceDate: new Date().toISOString().split('T')[0], amount: '', dueDate: '', notes: ''
    });
    setFormOpen(true);
  };

  const openEdit = (inv: VendorInvoiceDto) => {
    setEditingId(inv.id);
    setForm({
      buildingId: inv.buildingId, vendorId: inv.vendorId, workOrderId: inv.workOrderId || '',
      category: inv.category || 'Other', description: inv.description || '',
      invoiceNumber: inv.invoiceNumber || '', invoiceDate: inv.invoiceDate.split('T')[0],
      amount: inv.amount, dueDate: inv.dueDate ? inv.dueDate.split('T')[0] : '', notes: inv.notes || ''
    });
    setFormOpen(true);
  };

  const handleSave = async () => {
    try {
      if (!form.buildingId || !form.vendorId || !form.amount) { setError(t('vendorInvoices.missingFields')); return; }
      const data = { ...form, buildingId: Number(form.buildingId), vendorId: Number(form.vendorId), workOrderId: form.workOrderId ? Number(form.workOrderId) : null, amount: Number(form.amount) };
      if (editingId) await vendorInvoicesApi.update(editingId, data);
      else await vendorInvoicesApi.create(data);
      setFormOpen(false);
      setSuccess(editingId ? t('vendorInvoices.updated') : t('vendorInvoices.created'));
      load();
    } catch (err: any) { setError(err?.response?.data?.message || t('vendorInvoices.failedSave')); }
  };

  const handleApprove = async (id: number) => {
    try { await vendorInvoicesApi.approve(id); setSuccess(t('vendorInvoices.approved')); load(); }
    catch { setError(t('vendorInvoices.failedApprove')); }
  };

  const handleCancel = async (id: number) => {
    try { await vendorInvoicesApi.cancel(id); setSuccess(t('vendorInvoices.cancelled')); load(); }
    catch { setError(t('vendorInvoices.failedCancel')); }
  };

  const handleDelete = async (id: number) => {
    try { await vendorInvoicesApi.delete(id); setSuccess(t('vendorInvoices.deleted')); load(); }
    catch { setError(t('vendorInvoices.failedDelete')); }
  };

  // ─── Payments ─────────────────────────────────────────

  const openAddPayment = (inv: VendorInvoiceDto) => {
    setPayInvoiceId(inv.id);
    setPayForm({
      paidAmount: inv.balance > 0 ? inv.balance : '', paidAtUtc: new Date().toISOString().split('T')[0],
      paymentMethod: 'BankTransfer', reference: '', notes: ''
    });
    setPayOpen(true);
  };

  const handleAddPayment = async () => {
    if (!payInvoiceId || !payForm.paidAmount) return;
    try {
      await vendorInvoicesApi.addPayment(payInvoiceId, { ...payForm, paidAmount: Number(payForm.paidAmount) });
      setPayOpen(false);
      setSuccess(t('vendorInvoices.paymentAdded'));
      load();
    } catch { setError(t('vendorInvoices.failedPayment')); }
  };

  const openPaymentsList = async (inv: VendorInvoiceDto) => {
    setPaymentsInvoice(inv);
    setPaymentsOpen(true);
    setPaymentsLoading(true);
    try { const r = await vendorInvoicesApi.getPayments(inv.id); setPayments(r.data); }
    catch { setError(t('vendorInvoices.failedLoadPayments')); }
    finally { setPaymentsLoading(false); }
  };

  const handleDeletePayment = async (paymentId: number) => {
    try {
      await vendorInvoicesApi.deletePayment(paymentId);
      if (paymentsInvoice) {
        const r = await vendorInvoicesApi.getPayments(paymentsInvoice.id);
        setPayments(r.data);
      }
      load();
    } catch { setError(t('vendorInvoices.failedDeletePayment')); }
  };

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h4" sx={{ fontWeight: 700, fontSize: { xs: '1.3rem', md: '2rem' } }}>
          {t('vendorInvoices.title')}
        </Typography>
        <Button variant="contained" startIcon={<Add />} onClick={openCreate}>{t('vendorInvoices.addInvoice')}</Button>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}
      {success && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccess('')}>{success}</Alert>}

      <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
        <TextField select label={t('vendorInvoices.building')} value={filterBuilding} onChange={e => setFilterBuilding(e.target.value)} size="small" sx={{ minWidth: 200 }}>
          <MenuItem value="">{t('app.all')}</MenuItem>
          {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
        </TextField>
        <TextField select label={t('vendorInvoices.vendor')} value={filterVendor} onChange={e => setFilterVendor(e.target.value)} size="small" sx={{ minWidth: 200 }}>
          <MenuItem value="">{t('app.all')}</MenuItem>
          {vendors.map(v => <MenuItem key={v.id} value={v.id}>{v.name}</MenuItem>)}
        </TextField>
        <TextField select label={t('vendorInvoices.status')} value={filterStatus} onChange={e => setFilterStatus(e.target.value)} size="small" sx={{ minWidth: 160 }}>
          <MenuItem value="">{t('app.all')}</MenuItem>
          {VENDOR_INVOICE_STATUSES.map(s => <MenuItem key={s} value={s}>{t(`vendorInvoices.st${s}`, s)}</MenuItem>)}
        </TextField>
      </Box>

      {isMobile ? (
        <Stack spacing={1.5}>
          {invoices.map(inv => (
            <Card key={inv.id} variant="outlined">
              <CardActionArea onClick={() => openPaymentsList(inv)}>
                <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                    <Typography variant="subtitle2" fontWeight={600}>{inv.vendorName}</Typography>
                    <Chip label={t(`vendorInvoices.st${inv.status}`, inv.status)} size="small" color={statusColor(inv.status) as any} />
                  </Box>
                  <Typography variant="body2" color="text.secondary">{inv.buildingName} · {inv.category}</Typography>
                  <Typography variant="body2">{formatCurrency(inv.amount)} · {t('vendorInvoices.paid')}: {formatCurrency(inv.paidAmount)}</Typography>
                  <Typography variant="caption" color="text.secondary">{formatDateOnly(inv.invoiceDate)}</Typography>
                </CardContent>
              </CardActionArea>
            </Card>
          ))}
          {invoices.length === 0 && <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('vendorInvoices.noInvoices')}</Typography>}
        </Stack>
      ) : (
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead><TableRow>
              <TableCell>{t('vendorInvoices.vendor')}</TableCell>
              <TableCell>{t('vendorInvoices.building')}</TableCell>
              <TableCell>{t('vendorInvoices.category')}</TableCell>
              <TableCell>{t('vendorInvoices.invoiceDate')}</TableCell>
              <TableCell>{t('vendorInvoices.dueDate')}</TableCell>
              <TableCell align="right">{t('vendorInvoices.amount')}</TableCell>
              <TableCell align="right">{t('vendorInvoices.paidAmount')}</TableCell>
              <TableCell align="right">{t('vendorInvoices.balance')}</TableCell>
              <TableCell>{t('vendorInvoices.status')}</TableCell>
              <TableCell>{t('app.actions')}</TableCell>
            </TableRow></TableHead>
            <TableBody>
              {invoices.map(inv => (
                <TableRow key={inv.id} hover>
                  <TableCell>{inv.vendorName}</TableCell>
                  <TableCell>{inv.buildingName}</TableCell>
                  <TableCell>{t(`enums.finCategory.${inv.category}`, inv.category ?? '')}</TableCell>
                  <TableCell>{formatDateOnly(inv.invoiceDate)}</TableCell>
                  <TableCell>{formatDateOnly(inv.dueDate)}</TableCell>
                  <TableCell align="right">{formatCurrency(inv.amount)}</TableCell>
                  <TableCell align="right">{formatCurrency(inv.paidAmount)}</TableCell>
                  <TableCell align="right">{formatCurrency(inv.balance)}</TableCell>
                  <TableCell><Chip label={t(`vendorInvoices.st${inv.status}`, inv.status)} size="small" color={statusColor(inv.status) as any} /></TableCell>
                  <TableCell>
                    <Box sx={{ display: 'flex', gap: 0.5 }}>
                      <Tooltip title={t('app.edit')}><IconButton size="small" onClick={() => openEdit(inv)}><Edit fontSize="small" /></IconButton></Tooltip>
                      {inv.status === 'Draft' && <Tooltip title={t('vendorInvoices.approve')}><IconButton size="small" color="success" onClick={() => handleApprove(inv.id)}><CheckCircle fontSize="small" /></IconButton></Tooltip>}
                      {inv.status !== 'Cancelled' && inv.status !== 'Paid' && <Tooltip title={t('vendorInvoices.addPayment')}><IconButton size="small" color="primary" onClick={() => openAddPayment(inv)}><Payment fontSize="small" /></IconButton></Tooltip>}
                      <Tooltip title={t('vendorInvoices.viewPayments')}><IconButton size="small" onClick={() => openPaymentsList(inv)}><Receipt fontSize="small" /></IconButton></Tooltip>
                      {inv.status === 'Draft' && <Tooltip title={t('vendorInvoices.cancelInvoice')}><IconButton size="small" color="warning" onClick={() => handleCancel(inv.id)}><Cancel fontSize="small" /></IconButton></Tooltip>}
                      <Tooltip title={t('app.delete')}><IconButton size="small" color="error" onClick={() => handleDelete(inv.id)}><Delete fontSize="small" /></IconButton></Tooltip>
                    </Box>
                  </TableCell>
                </TableRow>
              ))}
              {invoices.length === 0 && <TableRow><TableCell colSpan={10} align="center">{t('vendorInvoices.noInvoices')}</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* ─── Invoice Create/Edit Dialog ────────────────── */}
      <Dialog open={formOpen} onClose={() => setFormOpen(false)} maxWidth="sm" fullWidth fullScreen={isMobile}>
        <DialogTitle>{editingId ? t('vendorInvoices.editInvoice') : t('vendorInvoices.addInvoice')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField select label={t('vendorInvoices.building')} value={form.buildingId} onChange={e => setForm({ ...form, buildingId: e.target.value })} fullWidth required>
              {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
            </TextField>
            <TextField select label={t('vendorInvoices.vendor')} value={form.vendorId} onChange={e => setForm({ ...form, vendorId: e.target.value })} fullWidth required>
              {vendors.map(v => <MenuItem key={v.id} value={v.id}>{v.name} ({v.serviceType})</MenuItem>)}
            </TextField>
            <TextField select label={t('vendorInvoices.linkedWo')} value={form.workOrderId} onChange={e => setForm({ ...form, workOrderId: e.target.value })} fullWidth>
              <MenuItem value="">{t('app.na')}</MenuItem>
              {workOrders.map(wo => <MenuItem key={wo.id} value={wo.id}>#{wo.id} - {wo.title}</MenuItem>)}
            </TextField>
            <TextField select label={t('vendorInvoices.category')} value={form.category} onChange={e => setForm({ ...form, category: e.target.value })} fullWidth>
              {VENDOR_INVOICE_CATEGORIES.map(c => <MenuItem key={c} value={c}>{t(`enums.finCategory.${c}`, c)}</MenuItem>)}
            </TextField>
            <TextField label={t('vendorInvoices.description')} value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} fullWidth multiline rows={2} />
            <TextField label={t('vendorInvoices.invoiceNumber')} value={form.invoiceNumber} onChange={e => setForm({ ...form, invoiceNumber: e.target.value })} fullWidth />
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField label={t('vendorInvoices.invoiceDate')} type="date" value={form.invoiceDate} onChange={e => setForm({ ...form, invoiceDate: e.target.value })} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
              <TextField label={t('vendorInvoices.dueDate')} type="date" value={form.dueDate} onChange={e => setForm({ ...form, dueDate: e.target.value })} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
            </Box>
            <TextField label={t('vendorInvoices.amount')} type="number" value={form.amount} onChange={e => setForm({ ...form, amount: e.target.value })} fullWidth required />
            <TextField label={t('vendorInvoices.notes')} value={form.notes} onChange={e => setForm({ ...form, notes: e.target.value })} fullWidth multiline rows={2} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setFormOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleSave}>{editingId ? t('app.save') : t('vendorInvoices.addInvoice')}</Button>
        </DialogActions>
      </Dialog>

      {/* ─── Add Payment Dialog ────────────────────────── */}
      <Dialog open={payOpen} onClose={() => setPayOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{t('vendorInvoices.addPayment')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField label={t('vendorInvoices.paidAmount')} type="number" value={payForm.paidAmount} onChange={e => setPayForm({ ...payForm, paidAmount: e.target.value })} fullWidth required />
            <TextField label={t('vendorInvoices.paidDate')} type="date" value={payForm.paidAtUtc} onChange={e => setPayForm({ ...payForm, paidAtUtc: e.target.value })} fullWidth slotProps={{ inputLabel: { shrink: true } }} />
            <TextField select label={t('vendorInvoices.paymentMethod')} value={payForm.paymentMethod} onChange={e => setPayForm({ ...payForm, paymentMethod: e.target.value })} fullWidth>
              {VENDOR_PAYMENT_METHODS.map(m => <MenuItem key={m} value={m}>{t(`vendorInvoices.pm${m}`, m)}</MenuItem>)}
            </TextField>
            <TextField label={t('vendorInvoices.reference')} value={payForm.reference} onChange={e => setPayForm({ ...payForm, reference: e.target.value })} fullWidth />
            <TextField label={t('vendorInvoices.notes')} value={payForm.notes} onChange={e => setPayForm({ ...payForm, notes: e.target.value })} fullWidth multiline rows={2} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPayOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleAddPayment}>{t('vendorInvoices.addPayment')}</Button>
        </DialogActions>
      </Dialog>

      {/* ─── Payments List Dialog ──────────────────────── */}
      <Dialog open={paymentsOpen} onClose={() => setPaymentsOpen(false)} maxWidth="md" fullWidth fullScreen={isMobile}>
        <DialogTitle>{t('vendorInvoices.paymentsFor', { vendor: paymentsInvoice?.vendorName, num: paymentsInvoice?.invoiceNumber || paymentsInvoice?.id })}</DialogTitle>
        <DialogContent>
          {paymentsLoading ? <CircularProgress /> : (
            <TableContainer>
              <Table size="small">
                <TableHead><TableRow>
                  <TableCell>{t('vendorInvoices.paidDate')}</TableCell>
                  <TableCell align="right">{t('vendorInvoices.paidAmount')}</TableCell>
                  <TableCell>{t('vendorInvoices.paymentMethod')}</TableCell>
                  <TableCell>{t('vendorInvoices.reference')}</TableCell>
                  <TableCell>{t('vendorInvoices.notes')}</TableCell>
                  <TableCell>{t('app.actions')}</TableCell>
                </TableRow></TableHead>
                <TableBody>
                  {payments.map(p => (
                    <TableRow key={p.id}>
                      <TableCell>{formatDateOnly(p.paidAtUtc)}</TableCell>
                      <TableCell align="right">{formatCurrency(p.paidAmount)}</TableCell>
                      <TableCell>{t(`vendorInvoices.pm${p.paymentMethod}`, p.paymentMethod)}</TableCell>
                      <TableCell>{p.reference}</TableCell>
                      <TableCell>{p.notes}</TableCell>
                      <TableCell>
                        <IconButton size="small" color="error" onClick={() => handleDeletePayment(p.id)}><Delete fontSize="small" /></IconButton>
                      </TableCell>
                    </TableRow>
                  ))}
                  {payments.length === 0 && <TableRow><TableCell colSpan={6} align="center">{t('vendorInvoices.noPayments')}</TableCell></TableRow>}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPaymentsOpen(false)}>{t('app.close')}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default VendorInvoicesPage;
