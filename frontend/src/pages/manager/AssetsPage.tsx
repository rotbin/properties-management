import React, { useEffect, useState } from 'react';
import {
  Box, Typography, Button, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Paper, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, MenuItem, Alert, CircularProgress, Chip, Divider
} from '@mui/material';
import { Add, PlayArrow } from '@mui/icons-material';
import { assetsApi, preventivePlansApi, buildingsApi, vendorsApi } from '../../api/services';
import type { AssetDto, PreventivePlanDto, BuildingDto, VendorDto } from '../../types';
import { ASSET_TYPES, FREQUENCY_TYPES } from '../../types';
import { formatDateOnly } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';

const AssetsPage: React.FC = () => {
  const { t } = useTranslation();
  const [assets, setAssets] = useState<AssetDto[]>([]);
  const [plans, setPlans] = useState<PreventivePlanDto[]>([]);
  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [vendors, setVendors] = useState<VendorDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [assetDialog, setAssetDialog] = useState(false);
  const [planDialog, setPlanDialog] = useState(false);
  const [assetForm, setAssetForm] = useState<any>({ buildingId: '', name: '', assetType: 'Other', locationDescription: '', serialNumber: '', notes: '' });
  const [planForm, setPlanForm] = useState<any>({ assetId: '', title: '', frequencyType: 'Monthly', interval: 1, nextDueDate: '', checklistText: '' });

  const load = async () => {
    try {
      setLoading(true);
      const [a, p, b, v] = await Promise.all([assetsApi.getAll(), preventivePlansApi.getAll(), buildingsApi.getAll(), vendorsApi.getAll()]);
      setAssets(a.data); setPlans(p.data); setBuildings(b.data); setVendors(v.data);
    } catch { setError(t('assets.failedLoad')); } finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []);

  const handleCreateAsset = async () => {
    try { await assetsApi.create({ ...assetForm, buildingId: Number(assetForm.buildingId) }); setAssetDialog(false); load(); } catch { setError(t('assets.failedCreateAsset')); }
  };
  const handleCreatePlan = async () => {
    try { await preventivePlansApi.create({ ...planForm, assetId: Number(planForm.assetId), interval: Number(planForm.interval) }); setPlanDialog(false); load(); } catch { setError(t('assets.failedCreatePlan')); }
  };
  const handleGeneratePreventive = async () => {
    try { const r = await preventivePlansApi.generateNow(); setSuccess(r.data.message); } catch { setError(t('assets.failedGenerate')); }
  };

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  return (
    <Box>
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 3 }}>{t('assets.title')}</Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}
      {success && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccess('')}>{success}</Alert>}

      <Box sx={{ display: 'flex', gap: 2, mb: 2 }}>
        <Button variant="contained" startIcon={<Add />} onClick={() => { setAssetForm({ buildingId: buildings[0]?.id || '', name: '', assetType: 'Other', locationDescription: '', serialNumber: '', notes: '' }); setAssetDialog(true); }}>{t('assets.addAsset')}</Button>
        <Button variant="contained" startIcon={<Add />} onClick={() => { setPlanForm({ assetId: assets[0]?.id || '', title: '', frequencyType: 'Monthly', interval: 1, nextDueDate: new Date().toISOString().split('T')[0], checklistText: '' }); setPlanDialog(true); }}>{t('assets.addPlan')}</Button>
        <Button variant="outlined" color="warning" startIcon={<PlayArrow />} onClick={handleGeneratePreventive}>{t('assets.generateNow')}</Button>
      </Box>

      <Typography variant="h6" sx={{ mt: 2, mb: 1 }}>{t('assets.assetsSection')}</Typography>
      <TableContainer component={Paper} sx={{ mb: 4 }}>
        <Table size="small">
          <TableHead><TableRow>
            <TableCell>{t('assets.name')}</TableCell><TableCell>{t('assets.type')}</TableCell><TableCell>{t('assets.building')}</TableCell>
            <TableCell>{t('assets.location')}</TableCell><TableCell>{t('assets.serial')}</TableCell><TableCell>{t('assets.warrantyUntil')}</TableCell>
          </TableRow></TableHead>
          <TableBody>
            {assets.map(a => (
              <TableRow key={a.id}>
                <TableCell>{a.name}</TableCell><TableCell><Chip label={t(`enums.assetType.${a.assetType}`, a.assetType)} size="small" /></TableCell>
                <TableCell>{a.buildingName}</TableCell><TableCell>{a.locationDescription}</TableCell>
                <TableCell>{a.serialNumber}</TableCell><TableCell>{formatDateOnly(a.warrantyUntil)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      <Typography variant="h6" sx={{ mb: 1 }}>{t('assets.plansSection')}</Typography>
      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead><TableRow>
            <TableCell>{t('assets.planTitle')}</TableCell><TableCell>{t('assets.asset')}</TableCell><TableCell>{t('assets.frequency')}</TableCell>
            <TableCell>{t('assets.interval')}</TableCell><TableCell>{t('assets.nextDue')}</TableCell>
          </TableRow></TableHead>
          <TableBody>
            {plans.map(p => (
              <TableRow key={p.id}>
                <TableCell>{p.title}</TableCell><TableCell>{p.assetName}</TableCell>
                <TableCell>{t(`enums.frequency.${p.frequencyType}`, p.frequencyType)}</TableCell><TableCell>{p.interval}</TableCell>
                <TableCell>{formatDateOnly(p.nextDueDate)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      <Dialog open={assetDialog} onClose={() => setAssetDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{t('assets.addAsset')}</DialogTitle>
        <DialogContent>
          <TextField fullWidth select label={t('assets.building')} value={assetForm.buildingId} onChange={e => setAssetForm({ ...assetForm, buildingId: e.target.value })} sx={{ mt: 1, mb: 2 }}>
            {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
          </TextField>
          <TextField fullWidth label={t('assets.name')} value={assetForm.name} onChange={e => setAssetForm({ ...assetForm, name: e.target.value })} required sx={{ mb: 2 }} />
          <TextField fullWidth select label={t('assets.assetType')} value={assetForm.assetType} onChange={e => setAssetForm({ ...assetForm, assetType: e.target.value })} sx={{ mb: 2 }}>
            {ASSET_TYPES.map(tp => <MenuItem key={tp} value={tp}>{t(`enums.assetType.${tp}`, tp)}</MenuItem>)}
          </TextField>
          <TextField fullWidth label={t('assets.location')} value={assetForm.locationDescription} onChange={e => setAssetForm({ ...assetForm, locationDescription: e.target.value })} sx={{ mb: 2 }} />
          <TextField fullWidth label={t('assets.serialNumber')} value={assetForm.serialNumber} onChange={e => setAssetForm({ ...assetForm, serialNumber: e.target.value })} sx={{ mb: 2 }} />
          <TextField fullWidth label={t('buildings.notes')} multiline rows={2} value={assetForm.notes} onChange={e => setAssetForm({ ...assetForm, notes: e.target.value })} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAssetDialog(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleCreateAsset}>{t('app.create')}</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={planDialog} onClose={() => setPlanDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{t('assets.addPlan')}</DialogTitle>
        <DialogContent>
          <TextField fullWidth select label={t('assets.asset')} value={planForm.assetId} onChange={e => setPlanForm({ ...planForm, assetId: e.target.value })} sx={{ mt: 1, mb: 2 }}>
            {assets.map(a => <MenuItem key={a.id} value={a.id}>{a.name}</MenuItem>)}
          </TextField>
          <TextField fullWidth label={t('assets.planTitle')} value={planForm.title} onChange={e => setPlanForm({ ...planForm, title: e.target.value })} required sx={{ mb: 2 }} />
          <TextField fullWidth select label={t('assets.frequency')} value={planForm.frequencyType} onChange={e => setPlanForm({ ...planForm, frequencyType: e.target.value })} sx={{ mb: 2 }}>
            {FREQUENCY_TYPES.map(tp => <MenuItem key={tp} value={tp}>{t(`enums.frequency.${tp}`, tp)}</MenuItem>)}
          </TextField>
          <TextField fullWidth type="number" label={t('assets.interval')} value={planForm.interval} onChange={e => setPlanForm({ ...planForm, interval: e.target.value })} sx={{ mb: 2 }} />
          <TextField fullWidth type="date" label={t('assets.nextDueDate')} value={planForm.nextDueDate} onChange={e => setPlanForm({ ...planForm, nextDueDate: e.target.value })} InputLabelProps={{ shrink: true }} sx={{ mb: 2 }} />
          <TextField fullWidth label={t('assets.checklist')} multiline rows={3} value={planForm.checklistText} onChange={e => setPlanForm({ ...planForm, checklistText: e.target.value })} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPlanDialog(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleCreatePlan}>{t('app.create')}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default AssetsPage;
