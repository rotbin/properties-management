import React, { useEffect, useState, useRef } from 'react';
import {
  Box, Typography, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, Chip, Button, Dialog, DialogTitle, DialogContent, DialogActions, TextField,
  MenuItem, CircularProgress, Alert, Divider, List, ListItem, ListItemText,
  Card, CardContent, CardActionArea, Stack, useMediaQuery, useTheme
} from '@mui/material';
import { Visibility, CloudUpload, Send } from '@mui/icons-material';
import { workOrdersApi } from '../../api/services';
import type { WorkOrderDto } from '../../types';
import { WO_STATUSES } from '../../types';
import { formatDateLocal } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';

const statusColor = (s: string) => s === 'Completed' ? 'success' : s === 'Cancelled' ? 'error' : s === 'InProgress' ? 'warning' : 'default';

const VendorWorkOrdersPage: React.FC = () => {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [workOrders, setWorkOrders] = useState<WorkOrderDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [detailOpen, setDetailOpen] = useState(false);
  const [selected, setSelected] = useState<WorkOrderDto | null>(null);
  const [statusForm, setStatusForm] = useState('');
  const [noteText, setNoteText] = useState('');
  const fileInputRef = useRef<HTMLInputElement>(null);

  const load = async () => {
    try { setLoading(true); const r = await workOrdersApi.getMy(); setWorkOrders(r.data); }
    catch { setError(t('vendorWo.failedLoad')); } finally { setLoading(false); }
  };

  useEffect(() => { load(); }, []);

  const handleStatusUpdate = async () => {
    if (!selected || !statusForm) return;
    try { await workOrdersApi.updateStatus(selected.id, { status: statusForm }); setSuccess(t('vendorWo.statusUpdated')); load(); refreshDetail(); } catch { setError(t('vendorWo.failedUpdateStatus')); }
  };

  const handleAddNote = async () => {
    if (!selected || !noteText.trim()) return;
    try { await workOrdersApi.addNote(selected.id, { noteText }); setNoteText(''); setSuccess(t('vendorWo.noteAdded')); refreshDetail(); } catch { setError(t('vendorWo.failedAddNote')); }
  };

  const handleUploadPhotos = async (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!selected || !e.target.files?.length) return;
    const files = Array.from(e.target.files);
    try { await workOrdersApi.uploadAttachments(selected.id, files); setSuccess(t('vendorWo.photosUploaded')); refreshDetail(); } catch { setError(t('vendorWo.failedUpload')); }
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const refreshDetail = async () => {
    if (!selected) return;
    try { const r = await workOrdersApi.getById(selected.id); setSelected(r.data); } catch { /* ignore */ }
  };

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  return (
    <Box>
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 2, fontSize: { xs: '1.3rem', md: '2rem' } }}>{t('vendorWo.title')}</Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}
      {success && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccess('')}>{success}</Alert>}

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
                  <Typography variant="caption" color="text.secondary">{wo.buildingName} · {formatDateLocal(wo.scheduledFor)}</Typography>
                </CardContent>
              </CardActionArea>
            </Card>
          ))}
          {workOrders.length === 0 && <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('vendorWo.noWorkOrders')}</Typography>}
        </Stack>
      ) : (
        <TableContainer component={Paper}>
          <Table>
            <TableHead><TableRow>
              <TableCell>{t('vendorWo.id')}</TableCell><TableCell>{t('vendorWo.woTitle')}</TableCell><TableCell>{t('vendorWo.building')}</TableCell>
              <TableCell>{t('vendorWo.status')}</TableCell><TableCell>{t('vendorWo.scheduled')}</TableCell><TableCell>{t('app.actions')}</TableCell>
            </TableRow></TableHead>
            <TableBody>
              {workOrders.map(wo => (
                <TableRow key={wo.id} hover>
                  <TableCell>{wo.id}</TableCell><TableCell>{wo.title}</TableCell><TableCell>{wo.buildingName}</TableCell>
                  <TableCell><Chip label={t(`enums.woStatus.${wo.status}`, wo.status)} size="small" color={statusColor(wo.status) as any} /></TableCell>
                  <TableCell>{formatDateLocal(wo.scheduledFor)}</TableCell>
                  <TableCell><Button size="small" startIcon={<Visibility />} onClick={() => { setSelected(wo); setStatusForm(wo.status); setDetailOpen(true); }}>{t('app.view')}</Button></TableCell>
                </TableRow>
              ))}
              {workOrders.length === 0 && <TableRow><TableCell colSpan={6} align="center">{t('vendorWo.noWorkOrders')}</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={detailOpen} onClose={() => setDetailOpen(false)} maxWidth="md" fullWidth fullScreen={isMobile}>
        {selected && (<>
          <DialogTitle>{t('vendorWo.detailTitle', { id: selected.id })}</DialogTitle>
          <DialogContent>
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2, mb: 2 }}>
              <Typography><strong>{t('vendorWo.woTitle')}:</strong> {selected.title}</Typography>
              <Typography><strong>{t('vendorWo.building')}:</strong> {selected.buildingName}</Typography>
              <Typography><strong>{t('vendorWo.status')}:</strong> <Chip label={t(`enums.woStatus.${selected.status}`, selected.status)} size="small" color={statusColor(selected.status) as any} /></Typography>
              <Typography><strong>{t('vendorWo.scheduled')}:</strong> {formatDateLocal(selected.scheduledFor)}</Typography>
            </Box>
            <Typography sx={{ mb: 2 }}><strong>{t('vendorWo.description')}:</strong> {selected.description || t('app.na')}</Typography>

            <Box sx={{ display: 'flex', gap: 2, mb: 2, alignItems: 'center' }}>
              <TextField select label={t('vendorWo.updateStatus')} value={statusForm} onChange={e => setStatusForm(e.target.value)} size="small" sx={{ minWidth: 180 }}>
                {WO_STATUSES.map(s => <MenuItem key={s} value={s}>{t(`enums.woStatus.${s}`, s)}</MenuItem>)}
              </TextField>
              <Button variant="contained" size="small" onClick={handleStatusUpdate}>{t('app.update')}</Button>
            </Box>

            <Divider sx={{ my: 2 }} />
            <Typography variant="subtitle2" sx={{ mb: 1 }}>{t('vendorWo.notes')}</Typography>
            {selected.notes.length > 0 && (
              <List dense sx={{ mb: 1 }}>
                {selected.notes.map(n => (<ListItem key={n.id}><ListItemText primary={n.noteText} secondary={`${n.createdByName || t('vendorWo.you')} - ${formatDateLocal(n.createdAtUtc)}`} /></ListItem>))}
              </List>
            )}
            <Box sx={{ display: 'flex', gap: 1 }}>
              <TextField fullWidth size="small" placeholder={t('vendorWo.addNote')} value={noteText} onChange={e => setNoteText(e.target.value)} />
              <Button variant="outlined" startIcon={<Send />} onClick={handleAddNote} disabled={!noteText.trim()}>{t('app.send')}</Button>
            </Box>

            <Divider sx={{ my: 2 }} />
            <Typography variant="subtitle2" sx={{ mb: 1 }}>{t('vendorWo.completionPhotos')}</Typography>
            {selected.attachments.length > 0 && (
              <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mb: 1 }}>
                {selected.attachments.map(a => (<Chip key={a.id} label={a.fileName} component="a" href={a.url} target="_blank" clickable />))}
              </Box>
            )}
            <input ref={fileInputRef} type="file" accept="image/jpeg,image/png,image/webp" multiple onChange={handleUploadPhotos} style={{ display: 'none' }} />
            <Button variant="outlined" startIcon={<CloudUpload />} onClick={() => fileInputRef.current?.click()}>{t('vendorWo.uploadPhotos')}</Button>
          </DialogContent>
          <DialogActions><Button onClick={() => setDetailOpen(false)}>{t('app.close')}</Button></DialogActions>
        </>)}
      </Dialog>
    </Box>
  );
};

export default VendorWorkOrdersPage;
