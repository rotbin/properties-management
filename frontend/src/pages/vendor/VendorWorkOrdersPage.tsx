import React, { useEffect, useState, useRef } from 'react';
import {
  Box, Typography, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, Chip, Button, Dialog, DialogTitle, DialogContent, DialogActions, TextField,
  MenuItem, CircularProgress, Alert, Divider, List, ListItem, ListItemText,
  Card, CardContent, CardActionArea, Stack, useMediaQuery, useTheme
} from '@mui/material';
import { Visibility, CloudUpload, Send, Warning, Phone } from '@mui/icons-material';
import { workOrdersApi } from '../../api/services';
import type { WorkOrderDto } from '../../types';
import { WO_STATUSES } from '../../types';
import { formatDateLocal } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';

const statusColor = (s: string) => s === 'Completed' ? 'success' : s === 'Cancelled' ? 'error' : s === 'InProgress' ? 'warning' : 'default';
const priorityColor = (p?: string) => p === 'Critical' ? 'error' : p === 'High' ? 'warning' : p === 'Medium' ? 'info' : 'default';

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

  const openCount = workOrders.filter(wo => ['Assigned', 'Scheduled', 'InProgress'].includes(wo.status)).length;
  const emergencyCount = workOrders.filter(wo => wo.srIsEmergency && wo.status !== 'Completed' && wo.status !== 'Cancelled').length;

  return (
    <Box>
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 1, fontSize: { xs: '1.3rem', md: '2rem' } }}>{t('vendorWo.title')}</Typography>
      <Box sx={{ display: 'flex', gap: 2, mb: 2, flexWrap: 'wrap' }}>
        <Chip label={`${t('vendorWo.activeTasks')}: ${openCount}`} color="primary" variant="outlined" />
        {emergencyCount > 0 && <Chip icon={<Warning />} label={`${t('vendorWo.emergencies')}: ${emergencyCount}`} color="error" />}
      </Box>
      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}
      {success && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccess('')}>{success}</Alert>}

      {isMobile ? (
        <Stack spacing={1.5}>
          {workOrders.map(wo => (
            <Card key={wo.id} variant="outlined" sx={wo.srIsEmergency ? { borderColor: 'error.main', borderWidth: 2 } : {}}>
              <CardActionArea onClick={() => { setSelected(wo); setStatusForm(wo.status); setDetailOpen(true); }}>
                <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                    <Box sx={{ display: 'flex', gap: 0.5, alignItems: 'center' }}>
                      <Typography variant="subtitle2">#{wo.id}</Typography>
                      {wo.srIsEmergency && <Chip label={t('vendorWo.emergency')} color="error" size="small" />}
                    </Box>
                    <Box sx={{ display: 'flex', gap: 0.5 }}>
                      {wo.srPriority && <Chip label={t(`enums.priority.${wo.srPriority}`, wo.srPriority)} size="small" color={priorityColor(wo.srPriority) as any} />}
                      <Chip label={t(`enums.woStatus.${wo.status}`, wo.status)} size="small" color={statusColor(wo.status) as any} />
                    </Box>
                  </Box>
                  <Typography variant="body2" fontWeight={600} noWrap>{wo.title}</Typography>
                  <Typography variant="caption" color="text.secondary">{wo.buildingName} · {wo.srCategory ? t(`enums.category.${wo.srCategory}`, wo.srCategory) : ''} · {formatDateLocal(wo.scheduledFor)}</Typography>
                  {wo.srPhone && <Typography variant="caption" display="block" color="text.secondary"><Phone sx={{ fontSize: 12, mr: 0.5, verticalAlign: 'middle' }} />{wo.srPhone}</Typography>}
                </CardContent>
              </CardActionArea>
            </Card>
          ))}
          {workOrders.length === 0 && <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('vendorWo.noWorkOrders')}</Typography>}
        </Stack>
      ) : (
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead><TableRow>
              <TableCell>{t('vendorWo.id')}</TableCell>
              <TableCell>{t('vendorWo.woTitle')}</TableCell>
              <TableCell>{t('vendorWo.building')}</TableCell>
              <TableCell>{t('vendorWo.area')}</TableCell>
              <TableCell>{t('vendorWo.priority')}</TableCell>
              <TableCell>{t('vendorWo.status')}</TableCell>
              <TableCell>{t('vendorWo.scheduled')}</TableCell>
              <TableCell>{t('vendorWo.phone')}</TableCell>
              <TableCell>{t('app.actions')}</TableCell>
            </TableRow></TableHead>
            <TableBody>
              {workOrders.map(wo => (
                <TableRow key={wo.id} hover sx={wo.srIsEmergency ? { bgcolor: 'rgba(211,47,47,0.04)' } : {}}>
                  <TableCell>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                      {wo.id}
                      {wo.srIsEmergency && <Chip label={t('vendorWo.emergency')} color="error" size="small" />}
                    </Box>
                  </TableCell>
                  <TableCell>{wo.title}</TableCell>
                  <TableCell>{wo.buildingName}</TableCell>
                  <TableCell>{wo.srArea ? t(`enums.area.${wo.srArea}`, wo.srArea) : ''} {wo.srCategory ? `/ ${t(`enums.category.${wo.srCategory}`, wo.srCategory)}` : ''}</TableCell>
                  <TableCell>{wo.srPriority && <Chip label={t(`enums.priority.${wo.srPriority}`, wo.srPriority)} size="small" color={priorityColor(wo.srPriority) as any} />}</TableCell>
                  <TableCell><Chip label={t(`enums.woStatus.${wo.status}`, wo.status)} size="small" color={statusColor(wo.status) as any} /></TableCell>
                  <TableCell>{formatDateLocal(wo.scheduledFor)}</TableCell>
                  <TableCell>{wo.srPhone || '—'}</TableCell>
                  <TableCell><Button size="small" startIcon={<Visibility />} onClick={() => { setSelected(wo); setStatusForm(wo.status); setDetailOpen(true); }}>{t('app.view')}</Button></TableCell>
                </TableRow>
              ))}
              {workOrders.length === 0 && <TableRow><TableCell colSpan={9} align="center">{t('vendorWo.noWorkOrders')}</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={detailOpen} onClose={() => setDetailOpen(false)} maxWidth="md" fullWidth fullScreen={isMobile}>
        {selected && (<>
          <DialogTitle>{t('vendorWo.detailTitle', { id: selected.id })}</DialogTitle>
          <DialogContent>
            {selected.srIsEmergency && (
              <Alert severity="error" sx={{ mb: 2 }}>{t('vendorWo.emergencyAlert')}</Alert>
            )}
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2, mb: 2 }}>
              <Typography><strong>{t('vendorWo.woTitle')}:</strong> {selected.title}</Typography>
              <Typography><strong>{t('vendorWo.building')}:</strong> {selected.buildingName}</Typography>
              {selected.buildingAddress && <Typography><strong>{t('vendorWo.address')}:</strong> {selected.buildingAddress}</Typography>}
              <Typography><strong>{t('vendorWo.status')}:</strong> <Chip label={t(`enums.woStatus.${selected.status}`, selected.status)} size="small" color={statusColor(selected.status) as any} /></Typography>
              <Typography><strong>{t('vendorWo.scheduled')}:</strong> {formatDateLocal(selected.scheduledFor)}</Typography>
              {selected.srPriority && <Typography><strong>{t('vendorWo.priority')}:</strong> <Chip label={t(`enums.priority.${selected.srPriority}`, selected.srPriority)} size="small" color={priorityColor(selected.srPriority) as any} /></Typography>}
              {selected.srArea && <Typography><strong>{t('vendorWo.area')}:</strong> {t(`enums.area.${selected.srArea}`, selected.srArea)}</Typography>}
              {selected.srCategory && <Typography><strong>{t('vendorWo.category')}:</strong> {t(`enums.category.${selected.srCategory}`, selected.srCategory)}</Typography>}
              {selected.srPhone && <Typography><strong>{t('vendorWo.callbackPhone')}:</strong> <a href={`tel:${selected.srPhone}`}>{selected.srPhone}</a></Typography>}
              {selected.srSubmittedByName && <Typography><strong>{t('vendorWo.tenant')}:</strong> {selected.srSubmittedByName}</Typography>}
            </Box>
            <Typography sx={{ mb: 1 }}><strong>{t('vendorWo.description')}:</strong> {selected.description || t('app.na')}</Typography>
            {selected.srDescription && selected.srDescription !== selected.description && (
              <Typography sx={{ mb: 2 }} variant="body2" color="text.secondary"><strong>{t('vendorWo.srDescription')}:</strong> {selected.srDescription}</Typography>
            )}
            {(selected.srAttachments && selected.srAttachments.length > 0) && (
              <Box sx={{ mb: 2 }}>
                <Typography variant="subtitle2">{t('vendorWo.srPhotos')}:</Typography>
                <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                  {selected.srAttachments.map(a => (<Chip key={a.id} label={a.fileName} component="a" href={a.url} target="_blank" clickable />))}
                </Box>
              </Box>
            )}

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
