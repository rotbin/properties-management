import React, { useEffect, useState } from 'react';
import {
  Box, Typography, Paper, Table, TableHead, TableRow, TableCell, TableBody,
  Button, Dialog, DialogTitle, DialogContent, DialogActions, TextField,
  MenuItem, Select, FormControl, InputLabel, Chip, IconButton, Alert,
  FormGroup, FormControlLabel, Checkbox
} from '@mui/material';
import { Add, Edit, Delete, Security } from '@mui/icons-material';
import { paymentConfigApi, buildingsApi } from '../../api/services';
import type { PaymentProviderConfigDto, BuildingDto } from '../../types';
import { PAYMENT_PROVIDERS, PROVIDER_FEATURES } from '../../types';
import { useTranslation } from 'react-i18next';

const FEATURE_LIST = Object.entries(PROVIDER_FEATURES) as [string, number][];

const PaymentProviderConfigPage: React.FC = () => {
  const { t } = useTranslation();
  const [configs, setConfigs] = useState<PaymentProviderConfigDto[]>([]);
  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editId, setEditId] = useState<number | null>(null);
  const [form, setForm] = useState({ buildingId: '' as string, providerType: 'Fake', merchantIdRef: '', terminalIdRef: '', apiUserRef: '', apiPasswordRef: '', webhookSecretRef: '', supportedFeatures: 0, currency: 'ILS', baseUrl: '' });
  const [error, setError] = useState('');

  const load = async () => { const [c, b] = await Promise.all([paymentConfigApi.getAll(), buildingsApi.getAll()]); setConfigs(c.data); setBuildings(b.data); };
  useEffect(() => { load(); }, []);

  const resetForm = () => { setForm({ buildingId: '', providerType: 'Fake', merchantIdRef: '', terminalIdRef: '', apiUserRef: '', apiPasswordRef: '', webhookSecretRef: '', supportedFeatures: 0, currency: 'ILS', baseUrl: '' }); setEditId(null); setError(''); };
  const openCreate = () => { resetForm(); setDialogOpen(true); };
  const openEdit = (c: PaymentProviderConfigDto) => { setForm({ buildingId: c.buildingId?.toString() ?? '', providerType: c.providerType, merchantIdRef: c.merchantIdRef ?? '', terminalIdRef: c.terminalIdRef ?? '', apiUserRef: c.apiUserRef ?? '', apiPasswordRef: c.apiPasswordRef ?? '', webhookSecretRef: c.webhookSecretRef ?? '', supportedFeatures: c.supportedFeatures, currency: c.currency, baseUrl: c.baseUrl ?? '' }); setEditId(c.id); setDialogOpen(true); };

  const handleSave = async () => {
    try {
      const data = { buildingId: form.buildingId ? parseInt(form.buildingId) : undefined, providerType: form.providerType, merchantIdRef: form.merchantIdRef || undefined, terminalIdRef: form.terminalIdRef || undefined, apiUserRef: form.apiUserRef || undefined, apiPasswordRef: form.apiPasswordRef || undefined, webhookSecretRef: form.webhookSecretRef || undefined, supportedFeatures: form.supportedFeatures, currency: form.currency, baseUrl: form.baseUrl || undefined };
      if (editId) await paymentConfigApi.update(editId, data); else await paymentConfigApi.create(data);
      setDialogOpen(false); resetForm(); load();
    } catch (e: any) { setError(e?.response?.data?.message || t('paymentConfig.failedSave')); }
  };

  const handleDelete = async (id: number) => { if (!confirm(t('paymentConfig.deleteConfirm'))) return; await paymentConfigApi.delete(id); load(); };
  const toggleFeature = (feat: number) => { setForm(f => ({ ...f, supportedFeatures: f.supportedFeatures ^ feat })); };
  const providerColor = (p: string) => { switch (p) { case 'Fake': return 'default'; case 'Meshulam': return 'primary'; case 'Pelecard': return 'secondary'; case 'Tranzila': return 'warning'; default: return 'default'; } };
  const isRealProvider = form.providerType !== 'Fake';

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h5">{t('paymentConfig.title')}</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={openCreate}>{t('paymentConfig.addProvider')}</Button>
      </Box>

      <Alert severity="info" sx={{ mb: 2 }} icon={<Security />}>
        <span dangerouslySetInnerHTML={{ __html: t('paymentConfig.securityNote') }} />
      </Alert>

      <Paper>
        <Table>
          <TableHead><TableRow>
            <TableCell>{t('paymentConfig.building')}</TableCell><TableCell>{t('paymentConfig.provider')}</TableCell>
            <TableCell>{t('paymentConfig.features')}</TableCell><TableCell>{t('paymentConfig.currency')}</TableCell>
            <TableCell>{t('paymentConfig.merchantRef')}</TableCell><TableCell>{t('app.actions')}</TableCell>
          </TableRow></TableHead>
          <TableBody>
            {configs.map(c => (
              <TableRow key={c.id}>
                <TableCell>{c.buildingName || t('paymentConfig.globalDefault')}</TableCell>
                <TableCell><Chip label={c.providerType} color={providerColor(c.providerType) as any} size="small" /></TableCell>
                <TableCell>{FEATURE_LIST.filter(([, v]) => (c.supportedFeatures & v) !== 0).map(([k]) => (<Chip key={k} label={t(`enums.providerFeature.${k}`, k)} size="small" variant="outlined" sx={{ mr: 0.5, mb: 0.5 }} />))}</TableCell>
                <TableCell>{c.currency}</TableCell>
                <TableCell>{c.merchantIdRef || '—'}</TableCell>
                <TableCell>
                  <IconButton size="small" onClick={() => openEdit(c)}><Edit fontSize="small" /></IconButton>
                  <IconButton size="small" onClick={() => handleDelete(c.id)}><Delete fontSize="small" /></IconButton>
                </TableCell>
              </TableRow>
            ))}
            {configs.length === 0 && <TableRow><TableCell colSpan={6} align="center">{t('paymentConfig.noConfigs')}</TableCell></TableRow>}
          </TableBody>
        </Table>
      </Paper>

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{editId ? t('paymentConfig.editProvider') : t('paymentConfig.createProvider')}</DialogTitle>
        <DialogContent>
          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
          <FormControl fullWidth sx={{ mt: 2 }}>
            <InputLabel>{t('paymentConfig.building')}</InputLabel>
            <Select value={form.buildingId} onChange={(e: any) => setForm(f => ({ ...f, buildingId: e.target.value }))} label={t('paymentConfig.building')}>
              <MenuItem value="">{t('paymentConfig.globalDefault')}</MenuItem>
              {buildings.map(b => <MenuItem key={b.id} value={b.id.toString()}>{b.name}</MenuItem>)}
            </Select>
          </FormControl>
          <FormControl fullWidth sx={{ mt: 2 }}>
            <InputLabel>{t('paymentConfig.provider')}</InputLabel>
            <Select value={form.providerType} onChange={(e: any) => setForm(f => ({ ...f, providerType: e.target.value }))} label={t('paymentConfig.provider')}>
              {PAYMENT_PROVIDERS.map(p => <MenuItem key={p} value={p}>{p}</MenuItem>)}
            </Select>
          </FormControl>

          {isRealProvider && (<>
            <Typography variant="subtitle2" sx={{ mt: 2, mb: 1, color: 'text.secondary' }}>{t('paymentConfig.kvSecretRefs')}</Typography>
            <TextField fullWidth label={t('paymentConfig.merchantIdRef')} value={form.merchantIdRef} onChange={e => setForm(f => ({ ...f, merchantIdRef: e.target.value }))} sx={{ mt: 1 }} />
            <TextField fullWidth label={t('paymentConfig.terminalIdRef')} value={form.terminalIdRef} onChange={e => setForm(f => ({ ...f, terminalIdRef: e.target.value }))} sx={{ mt: 1 }} />
            <TextField fullWidth label={t('paymentConfig.apiUserRef')} value={form.apiUserRef} onChange={e => setForm(f => ({ ...f, apiUserRef: e.target.value }))} sx={{ mt: 1 }} />
            <TextField fullWidth label={t('paymentConfig.apiPasswordRef')} value={form.apiPasswordRef} onChange={e => setForm(f => ({ ...f, apiPasswordRef: e.target.value }))} sx={{ mt: 1 }} />
            <TextField fullWidth label={t('paymentConfig.webhookSecretRef')} value={form.webhookSecretRef} onChange={e => setForm(f => ({ ...f, webhookSecretRef: e.target.value }))} sx={{ mt: 1 }} />
            <TextField fullWidth label={t('paymentConfig.baseUrl')} value={form.baseUrl} onChange={e => setForm(f => ({ ...f, baseUrl: e.target.value }))} sx={{ mt: 1 }} helperText={t('paymentConfig.baseUrlHelp')} />
          </>)}

          <TextField fullWidth label={t('paymentConfig.currency')} value={form.currency} onChange={e => setForm(f => ({ ...f, currency: e.target.value }))} sx={{ mt: 2 }} />
          <Typography variant="subtitle2" sx={{ mt: 2, mb: 1 }}>{t('paymentConfig.supportedFeatures')}</Typography>
          <FormGroup row>
            {FEATURE_LIST.map(([name, val]) => (
              <FormControlLabel key={name} label={t(`enums.providerFeature.${name}`, name)}
                control={<Checkbox checked={(form.supportedFeatures & val) !== 0} onChange={() => toggleFeature(val)} />} />
            ))}
          </FormGroup>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleSave}>{editId ? t('app.update') : t('app.create')}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default PaymentProviderConfigPage;
