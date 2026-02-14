import React, { useState, useEffect, useCallback } from 'react';
import {
  Typography, Box, Button, Card, CardContent, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Paper, Dialog, DialogTitle, DialogContent,
  DialogActions, TextField, MenuItem, Chip, Alert, CircularProgress,
  FormControl, InputLabel, Select, IconButton, Tooltip
} from '@mui/material';
import { Add, PlayArrow, Edit, Download } from '@mui/icons-material';
import { buildingsApi, hoaApi, reportsApi } from '../../api/services';
import type { BuildingDto, HOAFeePlanDto, UnitChargeDto, CollectionStatusReport, AgingReport } from '../../types';
import { HOA_CALC_METHODS } from '../../types';
import { formatDateOnly } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';

const HOAPlansPage: React.FC = () => {
  const { t } = useTranslation();
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

  useEffect(() => { buildingsApi.getAll().then(r => setBuildings(r.data)); }, []);

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
    try {
      if (editingPlan.id) { await hoaApi.updatePlan(editingPlan.id, editingPlan); }
      else { await hoaApi.createPlan({ ...editingPlan, buildingId: selectedBuilding as number }); }
      setPlanDialog(false); setEditingPlan({}); loadPlans(); setMsg(t('hoa.planSaved'));
    } catch { setMsg(t('hoa.errorSaving')); }
  };

  const generateCharges = async (planId: number) => {
    try { const r = await hoaApi.generateCharges(planId, period); setMsg(r.data.message || `Generated ${r.data.chargesCreated} charges`); loadCharges(); } catch { setMsg(t('hoa.errorGenerating')); }
  };

  const handleAdjust = async () => {
    try { await hoaApi.adjustCharge(adjustChargeId, { newAmount: parseFloat(adjustAmount), reason: adjustReason }); setAdjustDialog(false); setMsg(t('hoa.chargeAdjusted')); loadCharges(); } catch { setMsg(t('hoa.errorAdjusting')); }
  };

  const downloadCsv = async (type: 'collection' | 'aging') => {
    try {
      const lang = localStorage.getItem('lang') || 'he';
      const r = type === 'collection'
        ? await reportsApi.collectionStatusCsv(selectedBuilding as number, period, lang)
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
      <Typography variant="h4" gutterBottom>{t('hoa.title')}</Typography>
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
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
            <Typography variant="h6">{t('hoa.feePlans')}</Typography>
            <Button startIcon={<Add />} variant="contained" onClick={() => { setEditingPlan({ calculationMethod: 'FixedPerUnit', effectiveFrom: new Date().toISOString().slice(0, 10), isActive: true }); setPlanDialog(true); }}>{t('hoa.newPlan')}</Button>
          </Box>
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
        </CardContent></Card>
      )}

      {tab === 'charges' && selectedBuilding && (
        <Card><CardContent>
          <Typography variant="h6" gutterBottom>{t('hoa.charges', { period })}</Typography>
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
                    <TableCell><Tooltip title={t('hoa.adjustAmount')}><IconButton size="small" onClick={() => { setAdjustChargeId(c.id); setAdjustAmount(c.amountDue.toString()); setAdjustReason(''); setAdjustDialog(true); }}><Edit /></IconButton></Tooltip></TableCell>
                  </TableRow>
                ))}
                {charges.length === 0 && <TableRow><TableCell colSpan={8} align="center">{t('hoa.noCharges')}</TableCell></TableRow>}
              </TableBody>
            </Table>
          </TableContainer>
        </CardContent></Card>
      )}

      {tab === 'collection' && selectedBuilding && collectionReport && (
        <Card><CardContent>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
            <Typography variant="h6">{t('hoa.collectionStatus', { period: collectionReport.period })}</Typography>
            <Button startIcon={<Download />} variant="outlined" size="small" onClick={() => downloadCsv('collection')}>{t('app.exportCsv')}</Button>
          </Box>
          <Box sx={{ display: 'flex', gap: 3, mb: 2, flexWrap: 'wrap' }}>
            <Card variant="outlined" sx={{ p: 2, minWidth: 140 }}><Typography variant="body2" color="text.secondary">{t('hoa.expected')}</Typography><Typography variant="h5">{collectionReport.totalExpected.toFixed(2)}</Typography></Card>
            <Card variant="outlined" sx={{ p: 2, minWidth: 140 }}><Typography variant="body2" color="text.secondary">{t('hoa.collected')}</Typography><Typography variant="h5" color="success.main">{collectionReport.totalCollected.toFixed(2)}</Typography></Card>
            <Card variant="outlined" sx={{ p: 2, minWidth: 140 }}><Typography variant="body2" color="text.secondary">{t('hoa.collectionRate')}</Typography><Typography variant="h5" color={collectionReport.collectionRate >= 80 ? 'success.main' : 'warning.main'}>{collectionReport.collectionRate}%</Typography></Card>
          </Box>
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
                    <TableCell>{r.unitNumber}</TableCell><TableCell>{r.floor}</TableCell><TableCell>{r.residentName}</TableCell>
                    <TableCell align="right">{r.amountDue.toFixed(2)}</TableCell><TableCell align="right">{r.amountPaid.toFixed(2)}</TableCell>
                    <TableCell align="right" sx={{ fontWeight: 'bold', color: r.balance > 0 ? 'error.main' : 'success.main' }}>{r.balance.toFixed(2)}</TableCell>
                    <TableCell><Chip label={t(`enums.chargeStatus.${r.status}`, r.status)} size="small" color={r.status === 'Paid' ? 'success' : r.status === 'Overdue' ? 'error' : 'default'} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </CardContent></Card>
      )}

      {tab === 'aging' && selectedBuilding && agingReport && (
        <Card><CardContent>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
            <Typography variant="h6">{t('hoa.agingReport', { name: agingReport.buildingName })}</Typography>
            <Button startIcon={<Download />} variant="outlined" size="small" onClick={() => downloadCsv('aging')}>{t('app.exportCsv')}</Button>
          </Box>
          <Typography variant="subtitle1" color="text.secondary" gutterBottom>{t('hoa.grandTotal')} <b>{agingReport.grandTotal.toFixed(2)}</b></Typography>
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
        </CardContent></Card>
      )}

      {!selectedBuilding && <Typography color="text.secondary" sx={{ mt: 2 }}>{t('hoa.selectBuilding')}</Typography>}

      <Dialog open={planDialog} onClose={() => setPlanDialog(false)} maxWidth="sm" fullWidth>
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
    </Box>
  );
};

export default HOAPlansPage;
