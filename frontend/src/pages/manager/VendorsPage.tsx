import React, { useEffect, useState } from 'react';
import {
  Box, Typography, Button, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Paper, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, MenuItem, Alert, CircularProgress, Card, CardContent, CardActionArea,
  Stack, Chip, useMediaQuery, useTheme
} from '@mui/material';
import { Add, Edit } from '@mui/icons-material';
import { vendorsApi } from '../../api/services';
import type { VendorDto } from '../../types';
import { VENDOR_SERVICE_TYPES } from '../../types';
import { useTranslation } from 'react-i18next';

const VendorsPage: React.FC = () => {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [vendors, setVendors] = useState<VendorDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<VendorDto | null>(null);
  const [form, setForm] = useState({ name: '', serviceType: 'General', phone: '', email: '', contactName: '', notes: '' });

  const load = async () => {
    try { setLoading(true); const r = await vendorsApi.getAll(); setVendors(r.data); }
    catch { setError(t('vendors.failedLoad')); }
    finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []);

  const openCreate = () => { setEditing(null); setForm({ name: '', serviceType: 'General', phone: '', email: '', contactName: '', notes: '' }); setDialogOpen(true); };
  const openEdit = (v: VendorDto) => { setEditing(v); setForm({ name: v.name, serviceType: v.serviceType, phone: v.phone || '', email: v.email || '', contactName: v.contactName || '', notes: v.notes || '' }); setDialogOpen(true); };

  const handleSave = async () => {
    try {
      if (editing) { await vendorsApi.update(editing.id, form); }
      else { await vendorsApi.create(form); }
      setDialogOpen(false); load();
    } catch { setError(t('vendors.failedSave')); }
  };

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h4" sx={{ fontWeight: 700, fontSize: { xs: '1.3rem', md: '2rem' } }}>{t('vendors.title')}</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={openCreate} size={isMobile ? 'small' : 'medium'}>{t('vendors.addVendor')}</Button>
      </Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {isMobile ? (
        <Stack spacing={1.5}>
          {vendors.map(v => (
            <Card key={v.id} variant="outlined">
              <CardActionArea onClick={() => openEdit(v)}>
                <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                    <Typography variant="subtitle1" fontWeight={600}>{v.name}</Typography>
                    <Chip label={t(`enums.vendorServiceType.${v.serviceType}`, v.serviceType)} size="small" />
                  </Box>
                  {v.contactName && <Typography variant="body2">{v.contactName}</Typography>}
                  <Typography variant="caption" color="text.secondary">
                    {[v.phone, v.email].filter(Boolean).join(' · ') || '—'}
                  </Typography>
                </CardContent>
              </CardActionArea>
            </Card>
          ))}
          {vendors.length === 0 && <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('vendors.noVendors')}</Typography>}
        </Stack>
      ) : (
        <TableContainer component={Paper}>
          <Table>
            <TableHead><TableRow>
              <TableCell>{t('vendors.name')}</TableCell><TableCell>{t('vendors.serviceType')}</TableCell><TableCell>{t('vendors.phone')}</TableCell>
              <TableCell>{t('vendors.email')}</TableCell><TableCell>{t('vendors.contact')}</TableCell><TableCell>{t('app.actions')}</TableCell>
            </TableRow></TableHead>
            <TableBody>
              {vendors.map(v => (
                <TableRow key={v.id}>
                  <TableCell>{v.name}</TableCell><TableCell>{t(`enums.vendorServiceType.${v.serviceType}`, v.serviceType)}</TableCell>
                  <TableCell>{v.phone}</TableCell><TableCell>{v.email}</TableCell>
                  <TableCell>{v.contactName}</TableCell>
                  <TableCell><Button size="small" startIcon={<Edit />} onClick={() => openEdit(v)}>{t('app.edit')}</Button></TableCell>
                </TableRow>
              ))}
              {vendors.length === 0 && <TableRow><TableCell colSpan={6} align="center">{t('vendors.noVendors')}</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="sm" fullWidth fullScreen={isMobile}>
        <DialogTitle>{editing ? t('vendors.editVendor') : t('vendors.addVendor')}</DialogTitle>
        <DialogContent>
          <TextField fullWidth label={t('vendors.name')} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} required sx={{ mt: 1, mb: 2 }} />
          <TextField fullWidth select label={t('vendors.serviceType')} value={form.serviceType} onChange={e => setForm({ ...form, serviceType: e.target.value })} sx={{ mb: 2 }}>
            {VENDOR_SERVICE_TYPES.map(tp => <MenuItem key={tp} value={tp}>{t(`enums.vendorServiceType.${tp}`, tp)}</MenuItem>)}
          </TextField>
          <TextField fullWidth label={t('vendors.phone')} value={form.phone} onChange={e => setForm({ ...form, phone: e.target.value })} sx={{ mb: 2 }} />
          <TextField fullWidth label={t('vendors.email')} type="email" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} sx={{ mb: 2 }} />
          <TextField fullWidth label={t('vendors.contactName')} value={form.contactName} onChange={e => setForm({ ...form, contactName: e.target.value })} sx={{ mb: 2 }} />
          <TextField fullWidth label={t('vendors.notes')} multiline rows={2} value={form.notes} onChange={e => setForm({ ...form, notes: e.target.value })} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleSave}>{t('app.save')}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default VendorsPage;
