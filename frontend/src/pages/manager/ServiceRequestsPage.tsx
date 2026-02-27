import React, { useEffect, useState, useRef } from 'react';
import {
  Box, Typography, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, Chip, MenuItem, TextField, Button, Dialog, DialogTitle, DialogContent,
  DialogActions, CircularProgress, Alert, useMediaQuery, useTheme, Card, CardContent,
  Stack, CardActionArea, FormControlLabel, Switch, ImageList, ImageListItem, IconButton
} from '@mui/material';
import { Visibility, Add, Delete, CloudUpload, NoteAdd, Edit, BugReport, Chat, FiberNew } from '@mui/icons-material';
import { serviceRequestsApi, buildingsApi, workOrdersApi, vendorsApi } from '../../api/services';
import type { ServiceRequestDto, BuildingDto, VendorDto, UnitDto } from '../../types';
import { SR_STATUSES, AREAS, CATEGORIES, PRIORITIES } from '../../types';
import { formatDateLocal } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../auth/AuthContext';
import TicketChat from '../../components/TicketChat';

const priorityColor = (p: string) => p === 'Critical' ? 'error' : p === 'High' ? 'warning' : p === 'Medium' ? 'info' : 'default';
const statusColor = (s: string) => s === 'New' ? 'info' : s === 'Resolved' ? 'success' : s === 'Rejected' ? 'error' : 'default';

const MAX_FILES = 5;
const MAX_FILE_SIZE = 10 * 1024 * 1024;
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

const nowLocal = () => {
  const d = new Date();
  d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
  return d.toISOString().slice(0, 16);
};

const ServiceRequestsPage: React.FC = () => {
  const { t } = useTranslation();
  const { user } = useAuth();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [requests, setRequests] = useState<ServiceRequestDto[]>([]);
  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [vendors, setVendors] = useState<VendorDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [filterStatus, setFilterStatus] = useState('');
  const [filterBuilding, setFilterBuilding] = useState('');
  const [detailOpen, setDetailOpen] = useState(false);
  const [selected, setSelected] = useState<ServiceRequestDto | null>(null);
  const [woDialogOpen, setWoDialogOpen] = useState(false);
  const [woForm, setWoForm] = useState({ title: '', description: '', vendorId: '', scheduledFor: '' });
  const [statusForm, setStatusForm] = useState('');

  // Create SR dialog
  const [createOpen, setCreateOpen] = useState(false);
  const [createUnits, setCreateUnits] = useState<UnitDto[]>([]);
  const [createFiles, setCreateFiles] = useState<File[]>([]);
  const [createPreviews, setCreatePreviews] = useState<string[]>([]);
  const [createSubmitting, setCreateSubmitting] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [createForm, setCreateForm] = useState({
    buildingId: '' as any, unitId: '' as any, phone: user?.phone || '',
    area: 'Other', category: 'General', priority: 'Medium', isEmergency: false, description: ''
  });

  const load = async () => {
    try {
      setLoading(true);
      const params: any = {};
      if (filterStatus && filterStatus !== '__unassigned') params.status = filterStatus;
      if (filterBuilding) params.buildingId = Number(filterBuilding);
      const [r, b, v] = await Promise.all([serviceRequestsApi.getAll(params), buildingsApi.getAll(), vendorsApi.getAll()]);
      let data = r.data;
      if (filterStatus === '__unassigned') {
        data = data.filter(sr => !sr.assignedVendorId);
      }
      setRequests(data); setBuildings(b.data); setVendors(v.data);
    } catch { setError(t('serviceRequests.failedLoad')); } finally { setLoading(false); }
  };

  useEffect(() => { load(); }, [filterStatus, filterBuilding]);

  const handleTicketUpdated = (update: { ticketId: number; area?: string; category?: string; priority?: string; isEmergency?: boolean; description?: string; status?: string }) => {
    const patch = (sr: ServiceRequestDto) => ({
      ...sr,
      area: update.area ?? sr.area,
      category: update.category ?? sr.category,
      priority: update.priority ?? sr.priority,
      isEmergency: update.isEmergency ?? sr.isEmergency,
      description: update.description ?? sr.description,
      status: update.status ?? sr.status,
    });
    setSelected(prev => prev ? patch(prev) : prev);
    setRequests(prev => prev.map(sr => sr.id === update.ticketId ? patch(sr) : sr));
  };

  const loadCreateUnits = async (buildingId: number) => {
    if (!buildingId) { setCreateUnits([]); return; }
    try { const r = await buildingsApi.getUnits(buildingId); setCreateUnits(r.data); } catch { /* ignore */ }
  };

  const handleCreateFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = Array.from(e.target.files || []);
    const validFiles: File[] = [];
    for (const file of selected) {
      if (createFiles.length + validFiles.length >= MAX_FILES) break;
      if (file.size > MAX_FILE_SIZE || !ALLOWED_TYPES.includes(file.type)) continue;
      validFiles.push(file);
    }
    const newFiles = [...createFiles, ...validFiles];
    setCreateFiles(newFiles);
    setCreatePreviews(newFiles.map(f => URL.createObjectURL(f)));
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const removeCreateFile = (index: number) => {
    const nf = createFiles.filter((_, i) => i !== index);
    setCreateFiles(nf); setCreatePreviews(nf.map(f => URL.createObjectURL(f)));
  };

  const handleCreateSR = async () => {
    if (!createForm.buildingId || !createForm.description.trim()) {
      setError(t('serviceRequests.missingFields')); return;
    }
    if (!createForm.phone.trim()) {
      setError(t('newRequest.phoneRequired')); return;
    }
    setCreateSubmitting(true);
    try {
      const sr = await serviceRequestsApi.create({
        buildingId: Number(createForm.buildingId),
        unitId: createForm.unitId ? Number(createForm.unitId) : undefined,
        phone: createForm.phone,
        area: createForm.area,
        category: createForm.category,
        priority: createForm.priority,
        isEmergency: createForm.isEmergency,
        description: createForm.description,
      });
      if (createFiles.length > 0) await serviceRequestsApi.uploadAttachments(sr.data.id, createFiles);
      setCreateOpen(false);
      setCreateForm({ buildingId: '', unitId: '', phone: user?.phone || '', area: 'Other', category: 'General', priority: 'Medium', isEmergency: false, description: '' });
      setCreateFiles([]); setCreatePreviews([]);
      setSuccess(t('serviceRequests.srCreated'));
      load();
    } catch (err: any) { setError(err?.response?.data?.message || t('serviceRequests.failedCreateSr')); }
    finally { setCreateSubmitting(false); }
  };

  const handleStatusUpdate = async () => {
    if (!selected || !statusForm) return;
    try { await serviceRequestsApi.updateStatus(selected.id, { status: statusForm }); setSuccess(t('serviceRequests.statusUpdated')); setDetailOpen(false); load(); } catch { setError(t('serviceRequests.failedUpdateStatus')); }
  };

  // Unified: Create WO or Edit WO (includes vendor assignment)
  const openWorkOrderDialog = (sr: ServiceRequestDto) => {
    setSelected(sr);
    const bld = buildings.find(b => b.id === sr.buildingId);
    const defaultTitle = `${bld?.name || ''} – ${t(`enums.area.${sr.area}`, sr.area)} – ${t(`enums.category.${sr.category}`, sr.category)}`;
    setWoForm({
      title: defaultTitle,
      description: sr.description,
      vendorId: sr.assignedVendorId?.toString() || '',
      scheduledFor: nowLocal(),
    });
    setWoDialogOpen(true);
  };

  const handleCreateOrEditWO = async () => {
    if (!selected) return;
    if (!woForm.vendorId) { setError(t('serviceRequests.vendorRequired')); return; }

    try {
      if (selected.linkedWorkOrderId) {
        // Edit existing WO: re-assign vendor
        await workOrdersApi.assign(selected.linkedWorkOrderId, {
          vendorId: Number(woForm.vendorId),
          scheduledFor: woForm.scheduledFor || undefined,
        });
        setSuccess(t('serviceRequests.woUpdated'));
      } else {
        // Create new WO with vendor
        await workOrdersApi.create({
          buildingId: selected.buildingId,
          serviceRequestId: selected.id,
          title: woForm.title,
          description: woForm.description,
          vendorId: Number(woForm.vendorId),
          scheduledFor: woForm.scheduledFor || undefined,
        });
        setSuccess(t('serviceRequests.woCreated'));
      }
      setWoDialogOpen(false);
      setDetailOpen(false);
      load();
    } catch (err: any) {
      setError(err?.response?.data?.message || t('serviceRequests.failedCreateWo'));
    }
  };

  // Ticket display title: Building – Area – Category
  const srTitle = (sr: ServiceRequestDto) => {
    return `${sr.buildingName || ''} – ${t(`enums.area.${sr.area}`, sr.area)} – ${t(`enums.category.${sr.category}`, sr.category)}`;
  };

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h4" sx={{ fontWeight: 700, fontSize: { xs: '1.3rem', md: '2rem' } }}>{t('serviceRequests.title')}</Typography>
        <Button variant="contained" startIcon={<NoteAdd />} onClick={() => { setCreateOpen(true); setCreateForm(f => ({ ...f, buildingId: filterBuilding || '' })); if (filterBuilding) loadCreateUnits(Number(filterBuilding)); }}>
          {t('serviceRequests.createSr')}
        </Button>
      </Box>
      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}
      {success && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccess('')}>{success}</Alert>}

      <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
        <TextField select label={t('serviceRequests.filterStatus')} value={filterStatus} onChange={e => setFilterStatus(e.target.value)} size="small" sx={{ minWidth: 160 }}>
          <MenuItem value="">{t('app.all')}</MenuItem>
          <MenuItem value="__unassigned">{t('serviceRequests.unassignedOnly')}</MenuItem>
          {SR_STATUSES.map(s => <MenuItem key={s} value={s}>{t(`enums.srStatus.${s}`, s)}</MenuItem>)}
        </TextField>
        <TextField select label={t('serviceRequests.filterBuilding')} value={filterBuilding} onChange={e => setFilterBuilding(e.target.value)} size="small" sx={{ minWidth: 200 }}>
          <MenuItem value="">{t('serviceRequests.allBuildings')}</MenuItem>
          {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
        </TextField>
      </Box>

      {isMobile ? (
        <Stack spacing={1.5}>
          {requests.map(sr => (
            <Card key={sr.id} variant="outlined">
              <CardActionArea onClick={() => { setSelected(sr); setStatusForm(sr.status); setDetailOpen(true); }}>
                <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                    <Typography variant="subtitle2">#{sr.id}</Typography>
                    <Box sx={{ display: 'flex', gap: 0.5 }}>
                      <Chip label={t(`enums.priority.${sr.priority}`, sr.priority)} size="small" color={priorityColor(sr.priority) as any} />
                      <Chip label={t(`enums.srStatus.${sr.status}`, sr.status)} size="small" color={statusColor(sr.status) as any} />
                    </Box>
                  </Box>
                  <Typography variant="body2" fontWeight={600} noWrap>{srTitle(sr)}</Typography>
                  <Box sx={{ display: 'flex', gap: 0.5, alignItems: 'center' }}>
                    <Typography variant="caption" color="text.secondary">{sr.submittedByName} · {formatDateLocal(sr.createdAtUtc)}</Typography>
                    {sr.hasUnreadMessages && <Chip icon={<FiberNew sx={{ fontSize: 14 }} />} label={t('ticketChat.unread')} size="small" color="error" sx={{ height: 20, '& .MuiChip-label': { px: 0.5, fontSize: '0.65rem' } }} />}
                    {sr.incidentGroupId && <Chip icon={<BugReport sx={{ fontSize: 14 }} />} label={`#${sr.incidentGroupId}`} size="small" color="warning" variant="outlined" sx={{ height: 20, '& .MuiChip-label': { px: 0.5, fontSize: '0.65rem' } }} />}
                    {(sr.messageCount ?? 0) > 0 && <Chip icon={<Chat sx={{ fontSize: 14 }} />} label={sr.messageCount} size="small" variant="outlined" sx={{ height: 20, '& .MuiChip-label': { px: 0.5, fontSize: '0.65rem' } }} />}
                  </Box>
                </CardContent>
              </CardActionArea>
            </Card>
          ))}
          {requests.length === 0 && <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('serviceRequests.noRequests')}</Typography>}
        </Stack>
      ) : (
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead><TableRow>
              <TableCell>{t('serviceRequests.id')}</TableCell><TableCell>{t('serviceRequests.ticketTitle')}</TableCell>
              <TableCell>{t('serviceRequests.priority')}</TableCell><TableCell>{t('serviceRequests.status')}</TableCell>
              <TableCell>{t('serviceRequests.submittedBy')}</TableCell><TableCell>{t('serviceRequests.date')}</TableCell><TableCell>{t('app.actions')}</TableCell>
            </TableRow></TableHead>
            <TableBody>
              {requests.map(sr => (
                <TableRow key={sr.id} hover>
                  <TableCell>{sr.id}</TableCell>
                  <TableCell>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                      {srTitle(sr)}
                      {sr.hasUnreadMessages && <Chip icon={<FiberNew sx={{ fontSize: 14 }} />} label={t('ticketChat.unread')} size="small" color="error" sx={{ height: 20, '& .MuiChip-label': { px: 0.5, fontSize: '0.65rem' } }} />}
                      {sr.incidentGroupId && <Chip icon={<BugReport sx={{ fontSize: 14 }} />} label={`#${sr.incidentGroupId}`} size="small" color="warning" variant="outlined" sx={{ height: 20, '& .MuiChip-label': { px: 0.5, fontSize: '0.65rem' } }} />}
                      {(sr.messageCount ?? 0) > 0 && <Chip icon={<Chat sx={{ fontSize: 14 }} />} label={sr.messageCount} size="small" variant="outlined" sx={{ height: 20, '& .MuiChip-label': { px: 0.5, fontSize: '0.65rem' } }} />}
                    </Box>
                  </TableCell>
                  <TableCell><Chip label={t(`enums.priority.${sr.priority}`, sr.priority)} size="small" color={priorityColor(sr.priority) as any} /></TableCell>
                  <TableCell><Chip label={t(`enums.srStatus.${sr.status}`, sr.status)} size="small" color={statusColor(sr.status) as any} /></TableCell>
                  <TableCell>{sr.submittedByName}</TableCell>
                  <TableCell>{formatDateLocal(sr.createdAtUtc)}</TableCell>
                  <TableCell>
                    <Button size="small" startIcon={<Visibility />} onClick={() => { setSelected(sr); setStatusForm(sr.status); setDetailOpen(true); }}>{t('app.view')}</Button>
                  </TableCell>
                </TableRow>
              ))}
              {requests.length === 0 && <TableRow><TableCell colSpan={7} align="center">{t('serviceRequests.noRequests')}</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Detail Dialog */}
      <Dialog open={detailOpen} onClose={() => { setDetailOpen(false); load(); }} maxWidth="md" fullWidth fullScreen={isMobile}>
        {selected && (<>
          <DialogTitle>
            {srTitle(selected)}
            {selected.isEmergency && <Chip label={t('serviceRequests.emergency')} color="error" size="small" sx={{ ml: 1 }} />}
          </DialogTitle>
          <DialogContent>
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2, mb: 2 }}>
              <Typography><strong>{t('serviceRequests.building')}:</strong> {selected.buildingName}</Typography>
              <Typography><strong>{t('serviceRequests.unit')}:</strong> {selected.unitNumber || t('app.na')}</Typography>
              <Typography><strong>{t('serviceRequests.submittedBy')}:</strong> {selected.submittedByName}</Typography>
              <Typography><strong>{t('serviceRequests.phone')}:</strong> {selected.phone || t('app.na')}</Typography>
              <Typography><strong>{t('serviceRequests.area')}:</strong> {t(`enums.area.${selected.area}`, selected.area)}</Typography>
              <Typography><strong>{t('serviceRequests.category')}:</strong> {t(`enums.category.${selected.category}`, selected.category)}</Typography>
              <Typography><strong>{t('serviceRequests.priority')}:</strong> {t(`enums.priority.${selected.priority}`, selected.priority)}</Typography>
              <Typography><strong>{t('serviceRequests.date')}:</strong> {formatDateLocal(selected.createdAtUtc)}</Typography>
            </Box>
            <Typography sx={{ mb: 2 }}><strong>{t('serviceRequests.description')}:</strong> {selected.description}</Typography>

            {selected.assignedVendorName && (
              <Box sx={{ mb: 2, p: 1.5, bgcolor: 'rgba(26,86,160,0.06)', borderRadius: 2, border: '1px solid rgba(26,86,160,0.12)' }}>
                <Typography variant="body2"><strong>{t('serviceRequests.assignedVendor')}:</strong> {selected.assignedVendorName}</Typography>
                {selected.linkedWorkOrderId && (
                  <Typography variant="body2"><strong>{t('serviceRequests.linkedWo')}:</strong> #{selected.linkedWorkOrderId} — {t(`enums.woStatus.${selected.linkedWorkOrderStatus}`, selected.linkedWorkOrderStatus ?? '')}</Typography>
                )}
              </Box>
            )}

            {selected.attachments.length > 0 && (
              <Box sx={{ mb: 2 }}>
                <Typography variant="subtitle2">{t('serviceRequests.attachments')}:</Typography>
                {selected.attachments.map(a => (
                  <Chip key={a.id} label={a.fileName} component="a" href={a.url} target="_blank" clickable sx={{ mr: 1 }} />
                ))}
              </Box>
            )}
            <Box sx={{ display: 'flex', gap: 2, alignItems: 'center', flexWrap: 'wrap' }}>
              <TextField select label={t('serviceRequests.changeStatus')} value={statusForm} onChange={e => setStatusForm(e.target.value)} size="small" sx={{ minWidth: 160 }}>
                {SR_STATUSES.map(s => <MenuItem key={s} value={s}>{t(`enums.srStatus.${s}`, s)}</MenuItem>)}
              </TextField>
              <Button variant="contained" size="small" onClick={handleStatusUpdate}>{t('serviceRequests.updateStatus')}</Button>
              {/* Single action: Create or Edit Work Order (includes vendor assignment) */}
              <Button variant="outlined" color="primary" startIcon={selected.linkedWorkOrderId ? <Edit /> : <Add />}
                onClick={() => openWorkOrderDialog(selected)}>
                {selected.linkedWorkOrderId ? t('serviceRequests.editWorkOrder') : t('serviceRequests.createWorkOrder')}
              </Button>
            </Box>

            <TicketChat
              ticketId={selected.id}
              incidentGroupId={selected.incidentGroupId}
              incidentGroupTitle={selected.incidentGroupTitle}
              incidentTicketCount={selected.incidentTicketCount}
              onTicketUpdated={handleTicketUpdated}
            />
          </DialogContent>
          <DialogActions><Button onClick={() => { setDetailOpen(false); load(); }}>{t('app.close')}</Button></DialogActions>
        </>)}
      </Dialog>

      {/* Create / Edit Work Order Dialog (includes vendor assignment) */}
      <Dialog open={woDialogOpen} onClose={() => setWoDialogOpen(false)} maxWidth="sm" fullWidth fullScreen={isMobile}>
        <DialogTitle>
          {selected?.linkedWorkOrderId
            ? t('serviceRequests.editWoTitle', { id: selected.linkedWorkOrderId })
            : t('serviceRequests.createWoFrom', { id: selected?.id })}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {!selected?.linkedWorkOrderId && (
              <>
                <TextField fullWidth label={t('serviceRequests.woTitle')} value={woForm.title}
                  onChange={e => setWoForm({ ...woForm, title: e.target.value })} />
                <TextField fullWidth multiline rows={2} label={t('serviceRequests.woDescription')} value={woForm.description}
                  onChange={e => setWoForm({ ...woForm, description: e.target.value })} />
              </>
            )}
            <TextField fullWidth select label={t('serviceRequests.vendor')} value={woForm.vendorId}
              onChange={e => setWoForm({ ...woForm, vendorId: e.target.value })} required>
              {vendors.map(v => <MenuItem key={v.id} value={v.id}>{v.name} ({v.serviceType})</MenuItem>)}
            </TextField>
            <TextField fullWidth type="datetime-local" label={t('serviceRequests.scheduleFor')} value={woForm.scheduledFor}
              onChange={e => setWoForm({ ...woForm, scheduledFor: e.target.value })} slotProps={{ inputLabel: { shrink: true } }} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setWoDialogOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleCreateOrEditWO}>
            {selected?.linkedWorkOrderId ? t('app.update') : t('serviceRequests.createWorkOrder')}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Create Service Request Dialog */}
      <Dialog open={createOpen} onClose={() => setCreateOpen(false)} maxWidth="sm" fullWidth fullScreen={isMobile}>
        <DialogTitle>{t('serviceRequests.createSr')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField fullWidth select label={t('newRequest.building')} value={createForm.buildingId}
              onChange={e => { setCreateForm({ ...createForm, buildingId: e.target.value, unitId: '' }); loadCreateUnits(Number(e.target.value)); }} required>
              {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
            </TextField>
            {createUnits.length > 0 && (
              <TextField fullWidth select label={t('newRequest.unit')} value={createForm.unitId}
                onChange={e => setCreateForm({ ...createForm, unitId: e.target.value })}>
                <MenuItem value="">{t('newRequest.noUnit')}</MenuItem>
                {createUnits.map(u => <MenuItem key={u.id} value={u.id}>{u.unitNumber} {u.ownerName ? `(${u.ownerName})` : ''}</MenuItem>)}
              </TextField>
            )}
            <TextField fullWidth label={t('newRequest.phone')} value={createForm.phone}
              onChange={e => setCreateForm({ ...createForm, phone: e.target.value })} required
              error={!createForm.phone.trim()} helperText={!createForm.phone.trim() ? t('newRequest.phoneRequired') : ''} />
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField fullWidth select label={t('newRequest.area')} value={createForm.area}
                onChange={e => setCreateForm({ ...createForm, area: e.target.value })}>
                {AREAS.map(a => <MenuItem key={a} value={a}>{t(`enums.area.${a}`, a)}</MenuItem>)}
              </TextField>
              <TextField fullWidth select label={t('newRequest.category')} value={createForm.category}
                onChange={e => setCreateForm({ ...createForm, category: e.target.value })}>
                {CATEGORIES.map(c => <MenuItem key={c} value={c}>{t(`enums.category.${c}`, c)}</MenuItem>)}
              </TextField>
            </Box>
            <Box sx={{ display: 'flex', gap: 2, alignItems: 'center' }}>
              <TextField fullWidth select label={t('newRequest.priority')} value={createForm.priority}
                onChange={e => setCreateForm({ ...createForm, priority: e.target.value })}>
                {PRIORITIES.map(p => <MenuItem key={p} value={p}>{t(`enums.priority.${p}`, p)}</MenuItem>)}
              </TextField>
              <FormControlLabel control={<Switch checked={createForm.isEmergency} onChange={e => setCreateForm({ ...createForm, isEmergency: e.target.checked })} color="error" />} label={t('newRequest.emergency')} />
            </Box>
            <TextField fullWidth multiline rows={3} label={t('newRequest.description')} value={createForm.description}
              onChange={e => setCreateForm({ ...createForm, description: e.target.value })} required placeholder={t('newRequest.descPlaceholder')} />
            <Box>
              <input ref={fileInputRef} type="file" accept="image/jpeg,image/png,image/webp" multiple onChange={handleCreateFileSelect} style={{ display: 'none' }} />
              <Button variant="outlined" size="small" startIcon={<CloudUpload />} onClick={() => fileInputRef.current?.click()} disabled={createFiles.length >= MAX_FILES}>
                {t('newRequest.addPhotos', { count: createFiles.length, max: MAX_FILES })}
              </Button>
              {createPreviews.length > 0 && (
                <ImageList cols={3} rowHeight={80} sx={{ mt: 1 }}>
                  {createPreviews.map((src, i) => (
                    <ImageListItem key={i} sx={{ position: 'relative' }}>
                      <img src={src} alt={`Preview ${i + 1}`} style={{ objectFit: 'cover', height: 80, borderRadius: 4 }} />
                      <IconButton size="small" onClick={() => removeCreateFile(i)} sx={{ position: 'absolute', top: 2, right: 2, bgcolor: 'rgba(255,255,255,0.8)' }}><Delete fontSize="small" /></IconButton>
                    </ImageListItem>
                  ))}
                </ImageList>
              )}
            </Box>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleCreateSR} disabled={createSubmitting}>
            {createSubmitting ? <CircularProgress size={20} /> : t('serviceRequests.createSr')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default ServiceRequestsPage;
