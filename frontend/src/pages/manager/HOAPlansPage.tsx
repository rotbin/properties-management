import React, { useState, useEffect, useCallback } from 'react';
import {
  Typography, Box, Button, Card, CardContent, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Paper, Dialog, DialogTitle, DialogContent,
  DialogActions, TextField, MenuItem, Chip, Alert, CircularProgress,
  FormControl, InputLabel, Select, IconButton, Tooltip, Stack,
  useMediaQuery, useTheme, List, ListItem, ListItemText, Divider
} from '@mui/material';
import { Add, PlayArrow, Edit, Download, Payment, Visibility, Delete } from '@mui/icons-material';
import { buildingsApi, hoaApi, reportsApi } from '../../api/services';
import type { BuildingDto, HOAFeePlanDto, UnitChargeDto, CollectionStatusReport, AgingReport, ChargePaymentDto } from '../../types';
import { HOA_CALC_METHODS, MANUAL_PAYMENT_METHODS } from '../../types';
import { formatDateOnly } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';

const HOAPlansPage: React.FC = () => {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [selectedBuilding, setSelectedBuilding] = useState<number | ''>('');
  const [plans, setPlans] = useState<HOAFeePlanDto[]>([]);
  const [charges, setCharges] = useState<UnitChargeDto[]>([]);
  const [period, setPeriod] = useState(new Date().toISOString().slice(0, 7));
  const [collectionReport, setCollectionReport] = useState<CollectionStatusReport | null>(null);
  const [agingReport, setAgingReport] = useState<AgingReport | null>(null);
  const [loading, setLoading] = useState(false);
  const [msg, setMsg] = useState('');
  const [tab, setTab] = useState<'plans' | 'charges' | 'collection' | 'aging'>('plans');
  const [planDialog, setPlanDialog] = useState(false);
  const [editingPlan, setEditingPlan] = useState<Partial<HOAFeePlanDto>>({});
  const [adjustDialog, setAdjustDialog] = useState(false);
  const [adjustChargeId, setAdjustChargeId] = useState(0);
  const [adjustAmount, setAdjustAmount] = useState('');
  const [adjustReason, setAdjustReason] = useState('');

  // Manual payment state
  const [manualPayDialog, setManualPayDialog] = useState(false);
  const [manualPayChargeId, setManualPayChargeId] = useState(0);
  const [manualPayCharge, setManualPayCharge] = useState<UnitChargeDto | null>(null);
  const [manualPayForm, setManualPayForm] = useState({ paidAmount: '', paidAt: '', method: 'BankTransfer' as string, reference: '', notes: '' });

  // Payments list state
  const [paymentsDialog, setPaymentsDialog] = useState(false);
  const [paymentsChargeId, setPaymentsChargeId] = useState(0);
  const [chargePayments, setChargePayments] = useState<ChargePaymentDto[]>([]);
  const [paymentsLoading, setPaymentsLoading] = useState(false);

  // Edit manual payment state
  const [editPayDialog, setEditPayDialog] = useState(false);
  const [editPayId, setEditPayId] = useState(0);
  const [editPayForm, setEditPayForm] = useState({ paidAmount: '', paidAt: '', method: 'BankTransfer' as string, reference: '', notes: '' });

  useEffect(() => { buildingsApi.getAll().then(r => { setBuildings(r.data); if (r.data.length > 0) setSelectedBuilding(r.data[0].id); }); }, []);

  const loadPlans = useCallback(async () => { if (!selectedBuilding) return; setLoading(true); try { const r = await hoaApi.getPlans(selectedBuilding as number); setPlans(r.data); } finally { setLoading(false); } }, [selectedBuilding]);
  const loadCharges = useCallback(async () => { if (!selectedBuilding) return; setLoading(true); try { const r = await hoaApi.getCharges({ buildingId: selectedBuilding as number, period }); setCharges(r.data); } finally { setLoading(false); } }, [selectedBuilding, period]);
  const loadCollectionReport = useCallback(async () => { if (!selectedBuilding) return; setLoading(true); try { const r = await reportsApi.collectionStatus(selectedBuilding as number, period); setCollectionReport(r.data); } finally { setLoading(false); } }, [selectedBuilding, period]);
  const loadAgingReport = useCallback(async () => { if (!selectedBuilding) return; setLoading(true); try { const r = await reportsApi.aging(selectedBuilding as number); setAgingReport(r.data); } finally { setLoading(false); } }, [selectedBuilding]);

  useEffect(() => {
    if (tab === 'plans') loadPlans();
    else if (tab === 'charges') loadCharges();
    else if (tab === 'collection') loadCollectionReport();
    else if (tab === 'aging') loadAgingReport();
  }, [tab, loadPlans, loadCharges, loadCollectionReport, loadAgingReport]);

  const savePlan = async () => {
    if (!editingPlan.name?.trim()) { setMsg(t('hoa.errorSaving') + ' – Name is required'); return; }
    if (!selectedBuilding && !editingPlan.buildingId) { setMsg(t('hoa.errorSaving') + ' – Building is required'); return; }
    try {
      if (editingPlan.id) { await hoaApi.updatePlan(editingPlan.id, editingPlan); }
      else { await hoaApi.createPlan({ ...editingPlan, buildingId: selectedBuilding as number }); }
      setPlanDialog(false); setEditingPlan({}); loadPlans(); setMsg(t('hoa.planSaved'));
    } catch (err: unknown) {
      let detail = '';
      if (err && typeof err === 'object' && 'response' in err) {
        const resp = (err as { response?: { data?: { message?: string; title?: string; errors?: Record<string, string[]> } } }).response;
        if (resp?.data?.message) detail = resp.data.message;
        else if (resp?.data?.title) detail = resp.data.title;
        else if (resp?.data?.errors) detail = Object.values(resp.data.errors).flat().join('; ');
      }
      setMsg(t('hoa.errorSaving') + (detail ? ` – ${detail}` : ''));
    }
  };

  const generateCharges = async (planId: number) => {
    try { const r = await hoaApi.generateCharges(planId, period); setMsg(r.data.message || `Generated ${r.data.chargesCreated} charges`); loadCharges(); } catch { setMsg(t('hoa.errorGenerating')); }
  };

  const handleAdjust = async () => {
    try { await hoaApi.adjustCharge(adjustChargeId, { newAmount: parseFloat(adjustAmount), reason: adjustReason }); setAdjustDialog(false); setMsg(t('hoa.chargeAdjusted')); loadCharges(); } catch { setMsg(t('hoa.errorAdjusting')); }
  };

  const openManualPayment = (charge: UnitChargeDto) => {
    setManualPayChargeId(charge.id);
    setManualPayCharge(charge);
    setManualPayForm({ paidAmount: charge.balance > 0 ? charge.balance.toFixed(2) : '', paidAt: new Date().toISOString().slice(0, 16), method: 'BankTransfer', reference: '', notes: '' });
    setManualPayDialog(true);
  };

  const handleManualPayment = async () => {
    const amount = parseFloat(manualPayForm.paidAmount);
    if (!amount || amount <= 0) { setMsg(t('hoa.errorAmountRequired')); return; }
    try {
      await hoaApi.addManualPayment(manualPayChargeId, {
        paidAmount: amount,
        paidAt: manualPayForm.paidAt || undefined,
        method: manualPayForm.method,
        reference: manualPayForm.reference || undefined,
        notes: manualPayForm.notes || undefined,
      });
      setManualPayDialog(false);
      setMsg(t('hoa.manualPaymentSaved'));
      loadCharges();
    } catch (err: unknown) {
      let detail = '';
      if (err && typeof err === 'object' && 'response' in err) {
        const resp = (err as { response?: { data?: { message?: string } } }).response;
        if (resp?.data?.message) detail = resp.data.message;
      }
      setMsg(t('hoa.errorManualPayment') + (detail ? ` – ${detail}` : ''));
    }
  };

  const openChargePayments = async (chargeId: number) => {
    setPaymentsChargeId(chargeId);
    setPaymentsDialog(true);
    setPaymentsLoading(true);
    try {
      const r = await hoaApi.getChargePayments(chargeId);
      setChargePayments(r.data);
    } catch { setChargePayments([]); }
    finally { setPaymentsLoading(false); }
  };

  const openEditPayment = (p: ChargePaymentDto) => {
    setEditPayId(p.id);
    setEditPayForm({
      paidAmount: p.amount.toFixed(2),
      paidAt: p.paymentDateUtc.slice(0, 16),
      method: p.manualMethodType || 'BankTransfer',
      reference: p.providerReference || '',
      notes: p.notes || '',
    });
    setEditPayDialog(true);
  };

  const handleEditPayment = async () => {
    const amount = parseFloat(editPayForm.paidAmount);
    if (!amount || amount <= 0) { setMsg(t('hoa.errorAmountRequired')); return; }
    try {
      await hoaApi.editManualPayment(editPayId, {
        paidAmount: amount,
        paidAt: editPayForm.paidAt || undefined,
        method: editPayForm.method,
        reference: editPayForm.reference || undefined,
        notes: editPayForm.notes || undefined,
      });
      setEditPayDialog(false);
      setMsg(t('hoa.manualPaymentUpdated'));
      openChargePayments(paymentsChargeId); // refresh list
      loadCharges();
    } catch (err: unknown) {
      let detail = '';
      if (err && typeof err === 'object' && 'response' in err) {
        const resp = (err as { response?: { data?: { message?: string } } }).response;
        if (resp?.data?.message) detail = resp.data.message;
      }
      setMsg(t('hoa.errorManualPayment') + (detail ? ` – ${detail}` : ''));
    }
  };

  const handleDeletePayment = async (paymentId: number) => {
    if (!window.confirm(t('hoa.confirmDeletePayment'))) return;
    try {
      await hoaApi.deleteManualPayment(paymentId);
      setMsg(t('hoa.manualPaymentDeleted'));
      openChargePayments(paymentsChargeId); // refresh list
      loadCharges();
    } catch { setMsg(t('hoa.errorManualPayment')); }
  };

  const downloadCsv = async (type: 'collection' | 'aging') => {
    try {
      const lang = localStorage.getItem('lang') || 'he';
      const r = type === 'collection'
        ? await reportsApi.collectionStatusCsv(selectedBuilding as number, period, false, lang)
        : await reportsApi.agingCsv(selectedBuilding as number, lang);
      const url = window.URL.createObjectURL(new Blob([r.data]));
      const a = document.createElement('a'); a.href = url; a.download = `${type}-report-${selectedBuilding}.csv`; a.click();
    } catch { setMsg(t('hoa.errorCsv')); }
  };

  const tabLabels: Record<string, string> = {
    plans: t('hoa.tabPlans'),
    charges: t('hoa.tabCharges'),
    collection: t('hoa.tabCollection'),
    aging: t('hoa.tabAging'),
  };

  return (
    <Box>
      <Typography variant="h4" gutterBottom sx={{ fontSize: { xs: '1.3rem', md: '2rem' }, fontWeight: 700 }}>{t('hoa.title')}</Typography>
      {msg && <Alert severity="info" onClose={() => setMsg('')} sx={{ mb: 2 }}>{msg}</Alert>}

      <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap', alignItems: 'center' }}>
        <FormControl sx={{ minWidth: 200 }}>
          <InputLabel>{t('hoa.building')}</InputLabel>
          <Select value={selectedBuilding} label={t('hoa.building')} onChange={e => setSelectedBuilding(e.target.value as number)}>
            {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
          </Select>
        </FormControl>
        <TextField label={t('hoa.period')} type="month" value={period} onChange={e => setPeriod(e.target.value)} InputLabelProps={{ shrink: true }} size="small" />
        <Box sx={{ display: 'flex', gap: 1 }}>
          {(['plans', 'charges', 'collection', 'aging'] as const).map(t2 => (
            <Button key={t2} variant={tab === t2 ? 'contained' : 'outlined'} size="small" onClick={() => setTab(t2)}>{tabLabels[t2]}</Button>
          ))}
        </Box>
      </Box>

      {loading && <CircularProgress sx={{ mb: 2 }} />}

      {tab === 'plans' && selectedBuilding && (
        <Card><CardContent>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2, flexWrap: 'wrap', gap: 1 }}>
            <Typography variant="h6">{t('hoa.feePlans')}</Typography>
            <Button startIcon={<Add />} variant="contained" size={isMobile ? 'small' : 'medium'} onClick={() => { setEditingPlan({ calculationMethod: 'FixedPerUnit', effectiveFrom: new Date().toISOString().slice(0, 10), isActive: true }); setPlanDialog(true); }}>{t('hoa.newPlan')}</Button>
          </Box>
          {isMobile ? (
            <Stack spacing={1.5}>
              {plans.map(p => (
                <Card key={p.id} variant="outlined">
                  <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                      <Typography variant="subtitle1" fontWeight={600}>{p.name}</Typography>
                      <Chip label={p.isActive ? t('hoa.active') : t('hoa.inactive')} color={p.isActive ? 'success' : 'default'} size="small" />
                    </Box>
                    <Typography variant="body2">
                      <Chip label={t(`enums.calcMethod.${p.calculationMethod}`, p.calculationMethod)} size="small" sx={{ mr: 0.5 }} />
                      {p.calculationMethod === 'BySqm' && `${p.amountPerSqm} /m²`}
                      {p.calculationMethod === 'FixedPerUnit' && `${p.fixedAmountPerUnit} / ${t('buildings.units').toLowerCase()}`}
                      {p.calculationMethod === 'ManualPerUnit' && t('hoa.manual')}
                    </Typography>
                    <Typography variant="caption" color="text.secondary">{t('hoa.effectiveFrom')}: {formatDateOnly(p.effectiveFrom)}</Typography>
                    <Box sx={{ display: 'flex', gap: 1, mt: 1 }}>
                      <Button size="small" variant="outlined" startIcon={<Edit />} onClick={() => { setEditingPlan(p); setPlanDialog(true); }}>{t('app.edit')}</Button>
                      <Button size="small" variant="outlined" color="primary" startIcon={<PlayArrow />} onClick={() => generateCharges(p.id)}>{t('hoa.generateCharges')}</Button>
                    </Box>
                  </CardContent>
                </Card>
              ))}
              {plans.length === 0 && <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('hoa.noPlans')}</Typography>}
            </Stack>
          ) : (
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead><TableRow>
                  <TableCell>{t('buildings.name')}</TableCell><TableCell>{t('hoa.calcMethod')}</TableCell><TableCell>{t('hoa.amount')}</TableCell>
                  <TableCell>{t('hoa.effectiveFrom')}</TableCell><TableCell>{t('hoa.active')}</TableCell><TableCell>{t('app.actions')}</TableCell>
                </TableRow></TableHead>
                <TableBody>
                  {plans.map(p => (
                    <TableRow key={p.id}>
                      <TableCell>{p.name}</TableCell>
                      <TableCell><Chip label={t(`enums.calcMethod.${p.calculationMethod}`, p.calculationMethod)} size="small" /></TableCell>
                      <TableCell>
                        {p.calculationMethod === 'BySqm' && `${p.amountPerSqm} /m²`}
                        {p.calculationMethod === 'FixedPerUnit' && `${p.fixedAmountPerUnit} / ${t('buildings.units').toLowerCase()}`}
                        {p.calculationMethod === 'ManualPerUnit' && t('hoa.manual')}
                      </TableCell>
                      <TableCell>{formatDateOnly(p.effectiveFrom)}</TableCell>
                      <TableCell><Chip label={p.isActive ? t('hoa.active') : t('hoa.inactive')} color={p.isActive ? 'success' : 'default'} size="small" /></TableCell>
                      <TableCell>
                        <Tooltip title={t('app.edit')}><IconButton size="small" onClick={() => { setEditingPlan(p); setPlanDialog(true); }}><Edit /></IconButton></Tooltip>
                        <Tooltip title={t('hoa.generateCharges')}><IconButton size="small" color="primary" onClick={() => generateCharges(p.id)}><PlayArrow /></IconButton></Tooltip>
                      </TableCell>
                    </TableRow>
                  ))}
                  {plans.length === 0 && <TableRow><TableCell colSpan={6} align="center">{t('hoa.noPlans')}</TableCell></TableRow>}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </CardContent></Card>
      )}

      {tab === 'charges' && selectedBuilding && (
        <Card><CardContent>
          <Typography variant="h6" gutterBottom>{t('hoa.charges', { period })}</Typography>
          {isMobile ? (
            <Stack spacing={1.5}>
              {charges.map(c => (
                <Card key={c.id} variant="outlined">
                  <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                      <Typography variant="subtitle2">{c.unitNumber} · {c.tenantName || '—'}</Typography>
                      <Chip label={t(`enums.chargeStatus.${c.status}`, c.status)} size="small" color={c.status === 'Paid' ? 'success' : c.status === 'Overdue' ? 'error' : c.status === 'PartiallyPaid' ? 'warning' : 'default'} />
                    </Box>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                      <Typography variant="body2">{t('hoa.due')}: {c.amountDue.toFixed(2)}</Typography>
                      <Typography variant="body2">{t('hoa.paid')}: {c.amountPaid.toFixed(2)}</Typography>
                      <Typography variant="body2" fontWeight="bold" color={c.balance > 0 ? 'error.main' : 'success.main'}>{t('hoa.balance')}: {c.balance.toFixed(2)}</Typography>
                    </Box>
                    <Box sx={{ mt: 0.5, display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
                      <Button size="small" variant="outlined" startIcon={<Edit />} onClick={() => { setAdjustChargeId(c.id); setAdjustAmount(c.amountDue.toString()); setAdjustReason(''); setAdjustDialog(true); }}>{t('hoa.adjustAmount')}</Button>
                      {c.balance > 0 && <Button size="small" variant="contained" color="primary" startIcon={<Payment />} onClick={() => openManualPayment(c)}>{t('hoa.manualPayment')}</Button>}
                      <Button size="small" variant="outlined" color="info" startIcon={<Visibility />} onClick={() => openChargePayments(c.id)}>{t('hoa.viewPayments')}</Button>
                    </Box>
                  </CardContent>
                </Card>
              ))}
              {charges.length === 0 && <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('hoa.noCharges')}</Typography>}
            </Stack>
          ) : (
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead><TableRow>
                  <TableCell>{t('hoa.unitCol')}</TableCell><TableCell>{t('hoa.floorCol')}</TableCell><TableCell>{t('hoa.tenant')}</TableCell>
                  <TableCell align="right">{t('hoa.due')}</TableCell><TableCell align="right">{t('hoa.paid')}</TableCell>
                  <TableCell align="right">{t('hoa.balance')}</TableCell><TableCell>{t('hoa.statusCol')}</TableCell><TableCell>{t('app.actions')}</TableCell>
                </TableRow></TableHead>
                <TableBody>
                  {charges.map(c => (
                    <TableRow key={c.id}>
                      <TableCell>{c.unitNumber}</TableCell><TableCell>{c.floor}</TableCell><TableCell>{c.tenantName || '—'}</TableCell>
                      <TableCell align="right">{c.amountDue.toFixed(2)}</TableCell><TableCell align="right">{c.amountPaid.toFixed(2)}</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 'bold', color: c.balance > 0 ? 'error.main' : 'success.main' }}>{c.balance.toFixed(2)}</TableCell>
                      <TableCell><Chip label={t(`enums.chargeStatus.${c.status}`, c.status)} size="small" color={c.status === 'Paid' ? 'success' : c.status === 'Overdue' ? 'error' : c.status === 'PartiallyPaid' ? 'warning' : 'default'} /></TableCell>
                      <TableCell>
                        <Tooltip title={t('hoa.adjustAmount')}><IconButton size="small" onClick={() => { setAdjustChargeId(c.id); setAdjustAmount(c.amountDue.toString()); setAdjustReason(''); setAdjustDialog(true); }}><Edit /></IconButton></Tooltip>
                        {c.balance > 0 && <Tooltip title={t('hoa.manualPayment')}><IconButton size="small" color="primary" onClick={() => openManualPayment(c)}><Payment /></IconButton></Tooltip>}
                        <Tooltip title={t('hoa.viewPayments')}><IconButton size="small" color="info" onClick={() => openChargePayments(c.id)}><Visibility /></IconButton></Tooltip>
                      </TableCell>
                    </TableRow>
                  ))}
                  {charges.length === 0 && <TableRow><TableCell colSpan={8} align="center">{t('hoa.noCharges')}</TableCell></TableRow>}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </CardContent></Card>
      )}

      {tab === 'collection' && selectedBuilding && collectionReport && (
        <Card><CardContent>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2, flexWrap: 'wrap', gap: 1 }}>
            <Typography variant="h6">{t('hoa.collectionStatus', { period: collectionReport.summary.period })}</Typography>
            <Button startIcon={<Download />} variant="outlined" size="small" onClick={() => downloadCsv('collection')}>{t('app.exportCsv')}</Button>
          </Box>
          <Box sx={{ display: 'flex', gap: isMobile ? 1 : 3, mb: 2, flexWrap: 'wrap' }}>
            <Card variant="outlined" sx={{ p: isMobile ? 1.5 : 2, minWidth: isMobile ? 'calc(50% - 8px)' : 140, flex: isMobile ? '1 1 calc(50% - 8px)' : undefined }}><Typography variant="body2" color="text.secondary">{t('hoa.expected')}</Typography><Typography variant={isMobile ? 'h6' : 'h5'}>{collectionReport.summary.totalDue.toFixed(2)}</Typography></Card>
            <Card variant="outlined" sx={{ p: isMobile ? 1.5 : 2, minWidth: isMobile ? 'calc(50% - 8px)' : 140, flex: isMobile ? '1 1 calc(50% - 8px)' : undefined }}><Typography variant="body2" color="text.secondary">{t('hoa.collected')}</Typography><Typography variant={isMobile ? 'h6' : 'h5'} color="success.main">{collectionReport.summary.totalPaid.toFixed(2)}</Typography></Card>
            <Card variant="outlined" sx={{ p: isMobile ? 1.5 : 2, minWidth: isMobile ? '100%' : 140 }}><Typography variant="body2" color="text.secondary">{t('hoa.collectionRate')}</Typography><Typography variant={isMobile ? 'h6' : 'h5'} color={collectionReport.summary.collectionRatePercent >= 80 ? 'success.main' : 'warning.main'}>{collectionReport.summary.collectionRatePercent}%</Typography></Card>
          </Box>
          {isMobile ? (
            <Stack spacing={1}>
              {collectionReport.rows.map(r => (
                <Card key={r.unitId} variant="outlined">
                  <CardContent sx={{ py: 1, px: 2, '&:last-child': { pb: 1 } }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <Typography variant="subtitle2">{r.unitNumber} · {r.payerDisplayName}</Typography>
                      <Chip label={t(`enums.chargeStatus.${r.status}`, r.status)} size="small" color={r.status === 'Paid' ? 'success' : r.status === 'Overdue' ? 'error' : 'default'} />
                    </Box>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', mt: 0.5 }}>
                      <Typography variant="caption">{t('hoa.due')}: {r.amountDue.toFixed(2)}</Typography>
                      <Typography variant="caption">{t('hoa.paid')}: {r.amountPaid.toFixed(2)}</Typography>
                      <Typography variant="caption" fontWeight="bold" color={r.outstanding > 0 ? 'error.main' : 'success.main'}>{t('hoa.balance')}: {r.outstanding.toFixed(2)}</Typography>
                    </Box>
                  </CardContent>
                </Card>
              ))}
            </Stack>
          ) : (
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead><TableRow>
                  <TableCell>{t('hoa.unitCol')}</TableCell><TableCell>{t('hoa.floorCol')}</TableCell><TableCell>{t('hoa.resident')}</TableCell>
                  <TableCell align="right">{t('hoa.due')}</TableCell><TableCell align="right">{t('hoa.paid')}</TableCell>
                  <TableCell align="right">{t('hoa.balance')}</TableCell><TableCell>{t('hoa.statusCol')}</TableCell>
                </TableRow></TableHead>
                <TableBody>
                  {collectionReport.rows.map(r => (
                    <TableRow key={r.unitId}>
                      <TableCell>{r.unitNumber}</TableCell><TableCell>{r.floor}</TableCell><TableCell>{r.payerDisplayName}</TableCell>
                      <TableCell align="right">{r.amountDue.toFixed(2)}</TableCell><TableCell align="right">{r.amountPaid.toFixed(2)}</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 'bold', color: r.outstanding > 0 ? 'error.main' : 'success.main' }}>{r.outstanding.toFixed(2)}</TableCell>
                      <TableCell><Chip label={t(`enums.chargeStatus.${r.status}`, r.status)} size="small" color={r.status === 'Paid' ? 'success' : r.status === 'Overdue' ? 'error' : 'default'} /></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </CardContent></Card>
      )}

      {tab === 'aging' && selectedBuilding && agingReport && (
        <Card><CardContent>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2, flexWrap: 'wrap', gap: 1 }}>
            <Typography variant="h6">{t('hoa.agingReport', { name: agingReport.buildingName })}</Typography>
            <Button startIcon={<Download />} variant="outlined" size="small" onClick={() => downloadCsv('aging')}>{t('app.exportCsv')}</Button>
          </Box>
          <Typography variant="subtitle1" color="text.secondary" gutterBottom>{t('hoa.grandTotal')} <b>{agingReport.grandTotal.toFixed(2)}</b></Typography>
          {isMobile ? (
            <Stack spacing={1}>
              {agingReport.buckets.map(b => (
                <Card key={b.unitId} variant="outlined">
                  <CardContent sx={{ py: 1, px: 2, '&:last-child': { pb: 1 } }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                      <Typography variant="subtitle2">{b.unitNumber} · {b.residentName}</Typography>
                      <Typography variant="subtitle2" fontWeight="bold">{b.total.toFixed(2)}</Typography>
                    </Box>
                    <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                      <Chip label={`${t('hoa.current')}: ${b.current.toFixed(0)}`} size="small" variant="outlined" />
                      <Chip label={`1-30: ${b.days1to30.toFixed(0)}`} size="small" variant="outlined" />
                      <Chip label={`31-60: ${b.days31to60.toFixed(0)}`} size="small" variant="outlined" />
                      <Chip label={`61-90: ${b.days61to90.toFixed(0)}`} size="small" variant="outlined" />
                      {b.days90Plus > 0 && <Chip label={`90+: ${b.days90Plus.toFixed(0)}`} size="small" color="error" />}
                    </Box>
                  </CardContent>
                </Card>
              ))}
              {agingReport.buckets.length === 0 && <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('hoa.noOutstanding')}</Typography>}
            </Stack>
          ) : (
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead><TableRow>
                  <TableCell>{t('hoa.unitCol')}</TableCell><TableCell>{t('hoa.resident')}</TableCell>
                  <TableCell align="right">{t('hoa.current')}</TableCell><TableCell align="right">{t('hoa.days1to30')}</TableCell>
                  <TableCell align="right">{t('hoa.days31to60')}</TableCell><TableCell align="right">{t('hoa.days61to90')}</TableCell>
                  <TableCell align="right">{t('hoa.days90plus')}</TableCell><TableCell align="right">{t('hoa.total')}</TableCell>
                </TableRow></TableHead>
                <TableBody>
                  {agingReport.buckets.map(b => (
                    <TableRow key={b.unitId}>
                      <TableCell>{b.unitNumber}</TableCell><TableCell>{b.residentName}</TableCell>
                      <TableCell align="right">{b.current.toFixed(2)}</TableCell><TableCell align="right">{b.days1to30.toFixed(2)}</TableCell>
                      <TableCell align="right">{b.days31to60.toFixed(2)}</TableCell><TableCell align="right">{b.days61to90.toFixed(2)}</TableCell>
                      <TableCell align="right" sx={{ color: b.days90Plus > 0 ? 'error.main' : 'inherit' }}>{b.days90Plus.toFixed(2)}</TableCell>
                      <TableCell align="right" sx={{ fontWeight: 'bold' }}>{b.total.toFixed(2)}</TableCell>
                    </TableRow>
                  ))}
                  {agingReport.buckets.length === 0 && <TableRow><TableCell colSpan={8} align="center">{t('hoa.noOutstanding')}</TableCell></TableRow>}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </CardContent></Card>
      )}

      {!selectedBuilding && <Typography color="text.secondary" sx={{ mt: 2 }}>{t('hoa.selectBuilding')}</Typography>}

      <Dialog open={planDialog} onClose={() => setPlanDialog(false)} maxWidth="sm" fullWidth fullScreen={isMobile}>
        <DialogTitle>{editingPlan.id ? t('hoa.editPlan') : t('hoa.createPlan')}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
          <TextField label={t('hoa.planName')} value={editingPlan.name || ''} onChange={e => setEditingPlan(p => ({ ...p, name: e.target.value }))} fullWidth />
          <FormControl fullWidth>
            <InputLabel>{t('hoa.calcMethod')}</InputLabel>
            <Select value={editingPlan.calculationMethod || ''} label={t('hoa.calcMethod')} onChange={e => setEditingPlan(p => ({ ...p, calculationMethod: e.target.value }))}>
              {HOA_CALC_METHODS.map(m => <MenuItem key={m} value={m}>{t(`enums.calcMethod.${m}`, m)}</MenuItem>)}
            </Select>
          </FormControl>
          {editingPlan.calculationMethod === 'BySqm' && <TextField label={t('hoa.amountPerSqm')} type="number" value={editingPlan.amountPerSqm ?? ''} onChange={e => setEditingPlan(p => ({ ...p, amountPerSqm: parseFloat(e.target.value) }))} />}
          {editingPlan.calculationMethod === 'FixedPerUnit' && <TextField label={t('hoa.fixedPerUnit')} type="number" value={editingPlan.fixedAmountPerUnit ?? ''} onChange={e => setEditingPlan(p => ({ ...p, fixedAmountPerUnit: parseFloat(e.target.value) }))} />}
          <TextField label={t('hoa.effectiveFrom')} type="date" value={(editingPlan.effectiveFrom || '').slice(0, 10)} onChange={e => setEditingPlan(p => ({ ...p, effectiveFrom: e.target.value }))} InputLabelProps={{ shrink: true }} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPlanDialog(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={savePlan}>{t('app.save')}</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={adjustDialog} onClose={() => setAdjustDialog(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{t('hoa.adjustAmount')}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
          <TextField label={t('hoa.newAmount')} type="number" value={adjustAmount} onChange={e => setAdjustAmount(e.target.value)} />
          <TextField label={t('hoa.reason')} value={adjustReason} onChange={e => setAdjustReason(e.target.value)} multiline rows={2} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAdjustDialog(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleAdjust}>{t('app.save')}</Button>
        </DialogActions>
      </Dialog>

      {/* Manual Payment Dialog */}
      <Dialog open={manualPayDialog} onClose={() => setManualPayDialog(false)} maxWidth="sm" fullWidth fullScreen={isMobile}>
        <DialogTitle>{t('hoa.manualPayment')}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
          {manualPayCharge && (
            <Alert severity="info" sx={{ mb: 1 }}>
              {t('hoa.unitCol')}: {manualPayCharge.unitNumber} · {manualPayCharge.tenantName || '—'} — {t('hoa.balance')}: <b>{manualPayCharge.balance.toFixed(2)}</b>
            </Alert>
          )}
          <TextField label={t('hoa.paidAmount')} type="number" value={manualPayForm.paidAmount}
            onChange={e => setManualPayForm(f => ({ ...f, paidAmount: e.target.value }))} required
            inputProps={{ min: 0.01, step: 0.01 }} />
          <TextField label={t('hoa.paymentDate')} type="datetime-local" value={manualPayForm.paidAt}
            onChange={e => setManualPayForm(f => ({ ...f, paidAt: e.target.value }))}
            InputLabelProps={{ shrink: true }} />
          <FormControl fullWidth>
            <InputLabel>{t('hoa.paymentMethod')}</InputLabel>
            <Select value={manualPayForm.method} label={t('hoa.paymentMethod')}
              onChange={e => setManualPayForm(f => ({ ...f, method: e.target.value }))}>
              {MANUAL_PAYMENT_METHODS.map(m => <MenuItem key={m} value={m}>{t(`enums.manualPayMethod.${m}`, m)}</MenuItem>)}
            </Select>
          </FormControl>
          <TextField label={t('hoa.reference')} value={manualPayForm.reference}
            onChange={e => setManualPayForm(f => ({ ...f, reference: e.target.value }))}
            placeholder={t('hoa.referencePlaceholder')} />
          <TextField label={t('hoa.notes')} value={manualPayForm.notes}
            onChange={e => setManualPayForm(f => ({ ...f, notes: e.target.value }))}
            multiline rows={2} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setManualPayDialog(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleManualPayment}>{t('app.save')}</Button>
        </DialogActions>
      </Dialog>

      {/* Payments List Dialog */}
      <Dialog open={paymentsDialog} onClose={() => setPaymentsDialog(false)} maxWidth="md" fullWidth fullScreen={isMobile}>
        <DialogTitle>{t('hoa.chargePayments')}</DialogTitle>
        <DialogContent>
          {paymentsLoading ? <CircularProgress /> : chargePayments.length === 0 ? (
            <Typography color="text.secondary" sx={{ py: 3 }} align="center">{t('hoa.noPayments')}</Typography>
          ) : (
            <List disablePadding>
              {chargePayments.map((p, idx) => (
                <React.Fragment key={p.id}>
                  {idx > 0 && <Divider />}
                  <ListItem
                    secondaryAction={p.isManual && p.status !== 'Cancelled' ? (
                      <Box>
                        <Tooltip title={t('app.edit')}><IconButton size="small" onClick={() => openEditPayment(p)}><Edit /></IconButton></Tooltip>
                        <Tooltip title={t('app.delete')}><IconButton size="small" color="error" onClick={() => handleDeletePayment(p.id)}><Delete /></IconButton></Tooltip>
                      </Box>
                    ) : undefined}
                  >
                    <ListItemText
                      primary={
                        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
                          <Typography variant="subtitle2">{p.amount.toFixed(2)} ILS</Typography>
                          <Chip label={p.isManual ? t(`enums.manualPayMethod.${p.manualMethodType}`, p.manualMethodType || 'Manual') : t('hoa.card')} size="small" variant="outlined" />
                          <Chip label={t(`enums.paymentStatus.${p.status}`, p.status)} size="small"
                            color={p.status === 'Succeeded' ? 'success' : p.status === 'Cancelled' ? 'error' : 'default'} />
                        </Box>
                      }
                      secondary={
                        <>
                          {formatDateOnly(p.paymentDateUtc)}
                          {p.providerReference && ` · ${t('hoa.reference')}: ${p.providerReference}`}
                          {p.notes && ` · ${p.notes}`}
                          {p.enteredByName && ` · ${t('hoa.enteredBy')}: ${p.enteredByName}`}
                        </>
                      }
                    />
                  </ListItem>
                </React.Fragment>
              ))}
            </List>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPaymentsDialog(false)}>{t('app.close')}</Button>
        </DialogActions>
      </Dialog>

      {/* Edit Manual Payment Dialog */}
      <Dialog open={editPayDialog} onClose={() => setEditPayDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{t('hoa.editManualPayment')}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
          <TextField label={t('hoa.paidAmount')} type="number" value={editPayForm.paidAmount}
            onChange={e => setEditPayForm(f => ({ ...f, paidAmount: e.target.value }))} required
            inputProps={{ min: 0.01, step: 0.01 }} />
          <TextField label={t('hoa.paymentDate')} type="datetime-local" value={editPayForm.paidAt}
            onChange={e => setEditPayForm(f => ({ ...f, paidAt: e.target.value }))}
            InputLabelProps={{ shrink: true }} />
          <FormControl fullWidth>
            <InputLabel>{t('hoa.paymentMethod')}</InputLabel>
            <Select value={editPayForm.method} label={t('hoa.paymentMethod')}
              onChange={e => setEditPayForm(f => ({ ...f, method: e.target.value }))}>
              {MANUAL_PAYMENT_METHODS.map(m => <MenuItem key={m} value={m}>{t(`enums.manualPayMethod.${m}`, m)}</MenuItem>)}
            </Select>
          </FormControl>
          <TextField label={t('hoa.reference')} value={editPayForm.reference}
            onChange={e => setEditPayForm(f => ({ ...f, reference: e.target.value }))} />
          <TextField label={t('hoa.notes')} value={editPayForm.notes}
            onChange={e => setEditPayForm(f => ({ ...f, notes: e.target.value }))}
            multiline rows={2} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditPayDialog(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleEditPayment}>{t('app.save')}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default HOAPlansPage;
