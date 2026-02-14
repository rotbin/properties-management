import React, { useEffect, useState } from 'react';
import {
  Box, Typography, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, Chip, MenuItem, TextField, Button, Dialog, DialogTitle, DialogContent,
  DialogActions, CircularProgress, Alert, Divider, List, ListItem, ListItemText,
  useMediaQuery, useTheme, Card, CardContent, Stack, CardActionArea
} from '@mui/material';
import { Visibility, PersonAdd } from '@mui/icons-material';
import { workOrdersApi, vendorsApi, buildingsApi } from '../../api/services';
import type { WorkOrderDto, VendorDto, BuildingDto } from '../../types';
import { WO_STATUSES } from '../../types';
import { formatDateLocal } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';

const statusColor = (s: string) => s === 'Completed' ? 'success' : s === 'Cancelled' ? 'error' : s === 'InProgress' ? 'warning' : 'default';

const WorkOrdersPage: React.FC = () => {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [workOrders, setWorkOrders] = useState<WorkOrderDto[]>([]);
  const [vendors, setVendors] = useState<VendorDto[]>([]);
  const [_buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [filterStatus, setFilterStatus] = useState('');
  const [filterVendor, setFilterVendor] = useState('');
  const [detailOpen, setDetailOpen] = useState(false);
  const [selected, setSelected] = useState<WorkOrderDto | null>(null);
  const [assignDialog, setAssignDialog] = useState(false);
  const [assignForm, setAssignForm] = useState({ vendorId: '', scheduledFor: '' });
  const [statusForm, setStatusForm] = useState('');

  const load = async () => {
    try {
      setLoading(true);
      const params: any = {};
      if (filterStatus) params.status = filterStatus;
      if (filterVendor) params.vendorId = Number(filterVendor);
      const [w, v, b] = await Promise.all([workOrdersApi.getAll(params), vendorsApi.getAll(), buildingsApi.getAll()]);
      setWorkOrders(w.data); setVendors(v.data); setBuildings(b.data);
    } catch { setError(t('workOrders.failedLoad')); } finally { setLoading(false); }
  };

  useEffect(() => { load(); }, [filterStatus, filterVendor]);

  const handleAssign = async () => {
    if (!selected || !assignForm.vendorId) return;
    try {
      await workOrdersApi.assign(selected.id, { vendorId: Number(assignForm.vendorId), scheduledFor: assignForm.scheduledFor || undefined });
      setSuccess(t('workOrders.vendorAssigned')); setAssignDialog(false); load();
    } catch { setError(t('workOrders.failedAssign')); }
  };

  const handleStatusUpdate = async () => {
    if (!selected || !statusForm) return;
    try { await workOrdersApi.updateStatus(selected.id, { status: statusForm }); setSuccess(t('workOrders.statusUpdated')); setDetailOpen(false); load(); } catch { setError(t('workOrders.failedUpdate')); }
  };

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  return (
    <Box>
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 2, fontSize: { xs: '1.3rem', md: '2rem' } }}>{t('workOrders.title')}</Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}
      {success && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccess('')}>{success}</Alert>}

      <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
        <TextField select label={t('workOrders.status')} value={filterStatus} onChange={e => setFilterStatus(e.target.value)} size="small" sx={{ minWidth: 160 }}>
          <MenuItem value="">{t('app.all')}</MenuItem>
          {WO_STATUSES.map(s => <MenuItem key={s} value={s}>{t(`enums.woStatus.${s}`, s)}</MenuItem>)}
        </TextField>
        <TextField select label={t('workOrders.vendor')} value={filterVendor} onChange={e => setFilterVendor(e.target.value)} size="small" sx={{ minWidth: 200 }}>
          <MenuItem value="">{t('workOrders.allVendors')}</MenuItem>
          {vendors.map(v => <MenuItem key={v.id} value={v.id}>{v.name}</MenuItem>)}
        </TextField>
      </Box>

      {isMobile ? (
        <Stack spacing={1.5}>
          {workOrders.map(wo => (
            <Card key={wo.id} variant="outlined">
              <CardActionArea onClick={() => { setSelected(wo); setStatusForm(wo.status); setDetailOpen(true); }}>
                <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                    <Typography variant="subtitle2">#{wo.id}</Typography>
                    <Chip label={t(`enums.woStatus.${wo.status}`, wo.status)} size="small" color={statusColor(wo.status) as any} />
                  </Box>
                  <Typography variant="body2" fontWeight={600} noWrap>{wo.title}</Typography>
                  <Typography variant="caption" color="text.secondary">{wo.buildingName} · {wo.vendorName || t('workOrders.unassigned')} · {formatDateLocal(wo.createdAtUtc)}</Typography>
                </CardContent>
              </CardActionArea>
            </Card>
          ))}
          {workOrders.length === 0 && <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('workOrders.noWorkOrders')}</Typography>}
        </Stack>
      ) : (
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead><TableRow>
              <TableCell>{t('workOrders.id')}</TableCell><TableCell>{t('workOrders.woTitle')}</TableCell><TableCell>{t('workOrders.building')}</TableCell>
              <TableCell>{t('workOrders.vendor')}</TableCell><TableCell>{t('workOrders.status')}</TableCell><TableCell>{t('workOrders.scheduled')}</TableCell>
              <TableCell>{t('workOrders.created')}</TableCell><TableCell>{t('app.actions')}</TableCell>
            </TableRow></TableHead>
            <TableBody>
              {workOrders.map(wo => (
                <TableRow key={wo.id} hover>
                  <TableCell>{wo.id}</TableCell>
                  <TableCell>{wo.title}</TableCell>
                  <TableCell>{wo.buildingName}</TableCell>
                  <TableCell>{wo.vendorName || '—'}</TableCell>
                  <TableCell><Chip label={t(`enums.woStatus.${wo.status}`, wo.status)} size="small" color={statusColor(wo.status) as any} /></TableCell>
                  <TableCell>{formatDateLocal(wo.scheduledFor)}</TableCell>
                  <TableCell>{formatDateLocal(wo.createdAtUtc)}</TableCell>
                  <TableCell>
                    <Button size="small" startIcon={<Visibility />} onClick={() => { setSelected(wo); setStatusForm(wo.status); setDetailOpen(true); }}>{t('app.view')}</Button>
                  </TableCell>
                </TableRow>
              ))}
              {workOrders.length === 0 && <TableRow><TableCell colSpan={8} align="center">{t('workOrders.noWorkOrders')}</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={detailOpen} onClose={() => setDetailOpen(false)} maxWidth="md" fullWidth fullScreen={isMobile}>
        {selected && (<>
          <DialogTitle>{t('vendorWo.detailTitle', { id: selected.id })}</DialogTitle>
          <DialogContent>
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2, mb: 2 }}>
              <Typography><strong>{t('workOrders.woTitle')}:</strong> {selected.title}</Typography>
              <Typography><strong>{t('workOrders.building')}:</strong> {selected.buildingName}</Typography>
              <Typography><strong>{t('workOrders.vendor')}:</strong> {selected.vendorName || t('workOrders.unassigned')}</Typography>
              <Typography><strong>{t('workOrders.status')}:</strong> {t(`enums.woStatus.${selected.status}`, selected.status)}</Typography>
              <Typography><strong>{t('workOrders.scheduled')}:</strong> {formatDateLocal(selected.scheduledFor)}</Typography>
              <Typography><strong>{t('workOrders.created')}:</strong> {formatDateLocal(selected.createdAtUtc)}</Typography>
            </Box>
            <Typography sx={{ mb: 2 }}><strong>{t('workOrders.description')}:</strong> {selected.description || t('app.na')}</Typography>
            {selected.notes.length > 0 && (<>
              <Divider sx={{ my: 1 }} />
              <Typography variant="subtitle2">{t('workOrders.notes')}:</Typography>
              <List dense>
                {selected.notes.map(n => (
                  <ListItem key={n.id}><ListItemText primary={n.noteText} secondary={`${n.createdByName || 'Unknown'} - ${formatDateLocal(n.createdAtUtc)}`} /></ListItem>
                ))}
              </List>
            </>)}
            <Box sx={{ display: 'flex', gap: 2, mt: 2, alignItems: 'center' }}>
              <TextField select label={t('workOrders.changeStatus')} value={statusForm} onChange={e => setStatusForm(e.target.value)} size="small" sx={{ minWidth: 160 }}>
                {WO_STATUSES.map(s => <MenuItem key={s} value={s}>{t(`enums.woStatus.${s}`, s)}</MenuItem>)}
              </TextField>
              <Button variant="contained" size="small" onClick={handleStatusUpdate}>{t('workOrders.updateStatus')}</Button>
              <Button variant="outlined" startIcon={<PersonAdd />} onClick={() => { setAssignForm({ vendorId: String(selected.vendorId || ''), scheduledFor: '' }); setAssignDialog(true); }}>{t('workOrders.assignVendor')}</Button>
            </Box>
          </DialogContent>
          <DialogActions><Button onClick={() => setDetailOpen(false)}>{t('app.close')}</Button></DialogActions>
        </>)}
      </Dialog>

      <Dialog open={assignDialog} onClose={() => setAssignDialog(false)} maxWidth="sm" fullWidth fullScreen={isMobile}>
        <DialogTitle>{t('workOrders.assignVendorTo', { id: selected?.id })}</DialogTitle>
        <DialogContent>
          <TextField fullWidth select label={t('workOrders.vendor')} value={assignForm.vendorId} onChange={e => setAssignForm({ ...assignForm, vendorId: e.target.value })} sx={{ mt: 1, mb: 2 }}>
            {vendors.map(v => <MenuItem key={v.id} value={v.id}>{v.name} ({v.serviceType})</MenuItem>)}
          </TextField>
          <TextField fullWidth type="datetime-local" label={t('workOrders.scheduleFor')} value={assignForm.scheduledFor} onChange={e => setAssignForm({ ...assignForm, scheduledFor: e.target.value })} InputLabelProps={{ shrink: true }} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAssignDialog(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleAssign}>{t('workOrders.assign')}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default WorkOrdersPage;
