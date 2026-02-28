import React, { useEffect, useState, useCallback } from 'react';
import {
  Box, Typography, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, Chip, MenuItem, TextField, Button, Dialog, DialogTitle, DialogContent,
  DialogActions, CircularProgress, Alert, useMediaQuery, useTheme, Card, CardContent,
  Stack, CardActionArea, IconButton, Tooltip, Switch, FormControlLabel
} from '@mui/material';
import {
  Edit, EventBusy, Archive, Delete, History, PersonAdd, Send, Chat,
  NotificationsActive, MarkEmailRead, Circle
} from '@mui/icons-material';
import { buildingsApi, tenantsApi, tenantMessagesApi } from '../../api/services';
import type { TenantProfileDto, BuildingDto, UnitDto, CreateTenantRequest, UpdateTenantRequest, TenantMessageDto, SendTenantMessageRequest } from '../../types';
import { formatDateOnly, toInputDate } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';

type StatusFilter = '' | 'Active' | 'Inactive' | 'Archived';

const statusColor = (tp: TenantProfileDto): 'success' | 'error' | 'default' | 'warning' =>
  tp.isArchived ? 'default' : tp.isActive ? 'success' : 'warning';

const statusLabel = (tp: TenantProfileDto): string =>
  tp.isArchived ? 'Archived' : tp.isActive ? 'Active' : 'Inactive';

const TenantsPage: React.FC = () => {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));

  // Data
  const [tenants, setTenants] = useState<TenantProfileDto[]>([]);
  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [units, setUnits] = useState<UnitDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  // Filters
  const [filterBuilding, setFilterBuilding] = useState('');
  const [filterStatus, setFilterStatus] = useState<StatusFilter>('');
  const [searchText, setSearchText] = useState('');

  // Dialogs
  const [formOpen, setFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [formData, setFormData] = useState<CreateTenantRequest>({
    unitId: 0, fullName: '', phone: '', email: '', moveInDate: '', isActive: true, notes: ''
  });
  const [formBuildingId, setFormBuildingId] = useState<number | 0>(0);

  const [endTenancyOpen, setEndTenancyOpen] = useState(false);
  const [endTenancyId, setEndTenancyId] = useState<number | null>(null);
  const [moveOutDate, setMoveOutDate] = useState('');

  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false);
  const [deleteId, setDeleteId] = useState<number | null>(null);
  const [deleteName, setDeleteName] = useState('');

  const [historyOpen, setHistoryOpen] = useState(false);
  const [, setHistoryUnitId] = useState<number | null>(null);
  const [historyUnitNumber, setHistoryUnitNumber] = useState('');
  const [historyData, setHistoryData] = useState<TenantProfileDto[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  // Messaging
  const [msgDialogOpen, setMsgDialogOpen] = useState(false);
  const [msgTenant, setMsgTenant] = useState<TenantProfileDto | null>(null);
  const [msgForm, setMsgForm] = useState<SendTenantMessageRequest>({ subject: '', body: '' });
  const [msgSending, setMsgSending] = useState(false);
  const [msgHistoryOpen, setMsgHistoryOpen] = useState(false);
  const [msgHistoryTenant, setMsgHistoryTenant] = useState<TenantProfileDto | null>(null);
  const [msgHistory, setMsgHistory] = useState<TenantMessageDto[]>([]);
  const [msgHistoryLoading, setMsgHistoryLoading] = useState(false);
  const [reminderSending, setReminderSending] = useState(false);

  // Load data
  const load = useCallback(async () => {
    try {
      setLoading(true);
      const params: Record<string, unknown> = {};
      if (filterBuilding) params.buildingId = Number(filterBuilding);
      if (filterStatus === 'Active') params.activeOnly = true;
      if (filterStatus === 'Archived') params.includeArchived = true;

      const [tRes, bRes] = await Promise.all([
        tenantsApi.getAll(params as any),
        buildingsApi.getAll()
      ]);
      let data = tRes.data;
      if (filterStatus === 'Inactive') data = data.filter(tp => !tp.isActive && !tp.isArchived);
      if (filterStatus === 'Archived') data = data.filter(tp => tp.isArchived);
      setTenants(data);
      setBuildings(bRes.data);
    } catch {
      setError(t('tenants.failedLoad'));
    } finally {
      setLoading(false);
    }
  }, [filterBuilding, filterStatus, t]);

  useEffect(() => { load(); }, [load]);

  // Load units for a building (for the form)
  const loadUnits = async (buildingId: number) => {
    if (!buildingId) { setUnits([]); return; }
    try {
      const res = await buildingsApi.getUnits(buildingId);
      setUnits(res.data);
    } catch { /* ignore */ }
  };

  // Filter locally by search text
  const filtered = tenants.filter(tp => {
    if (!searchText) return true;
    const s = searchText.toLowerCase();
    return tp.fullName.toLowerCase().includes(s)
      || (tp.phone || '').toLowerCase().includes(s)
      || (tp.email || '').toLowerCase().includes(s)
      || (tp.unitNumber || '').toLowerCase().includes(s);
  });

  // ─── Create / Edit ────────────────────────────────────

  const openCreate = () => {
    setEditingId(null);
    setFormData({ unitId: 0, fullName: '', phone: '', email: '', moveInDate: new Date().toISOString().split('T')[0], isActive: true, notes: '' });
    setFormBuildingId(filterBuilding ? Number(filterBuilding) : 0);
    if (filterBuilding) loadUnits(Number(filterBuilding));
    else setUnits([]);
    setFormOpen(true);
  };

  const openEdit = (tp: TenantProfileDto) => {
    setEditingId(tp.id);
    setFormData({
      unitId: tp.unitId,
      fullName: tp.fullName,
      phone: tp.phone || '',
      email: tp.email || '',
      moveInDate: toInputDate(tp.moveInDate),
      isActive: tp.isActive,
      notes: tp.notes || ''
    });
    setFormBuildingId(tp.buildingId);
    loadUnits(tp.buildingId);
    setFormOpen(true);
  };

  const handleSave = async () => {
    try {
      if (!formData.fullName.trim()) { setError(t('tenants.nameRequired')); return; }
      if (!editingId && !formData.unitId) { setError(t('tenants.unitRequired')); return; }

      if (editingId) {
        const updateReq: UpdateTenantRequest = {
          fullName: formData.fullName,
          phone: formData.phone || undefined,
          email: formData.email || undefined,
          moveInDate: formData.moveInDate || undefined,
          isActive: formData.isActive,
          notes: formData.notes || undefined
        };
        await tenantsApi.update(editingId, updateReq);
      } else {
        // Check if there's an active tenant for the selected unit
        const existingActive = tenants.find(tp => tp.unitId === formData.unitId && tp.isActive);
        if (existingActive && formData.isActive) {
          // The backend handles ending the previous tenant, but let's warn via the success message
        }
        await tenantsApi.create(formData);
      }
      setFormOpen(false);
      setSuccess(editingId ? t('tenants.updated') : t('tenants.created'));
      load();
    } catch (err: unknown) {
      let detail = '';
      if (err && typeof err === 'object' && 'response' in err) {
        const resp = (err as { response?: { data?: { message?: string } } }).response;
        if (resp?.data?.message) detail = resp.data.message;
      }
      setError((editingId ? t('tenants.failedUpdate') : t('tenants.failedCreate')) + (detail ? ` – ${detail}` : ''));
    }
  };

  // ─── End Tenancy ──────────────────────────────────────

  const openEndTenancy = (tp: TenantProfileDto) => {
    setEndTenancyId(tp.id);
    setMoveOutDate(new Date().toISOString().split('T')[0]);
    setEndTenancyOpen(true);
  };

  const handleEndTenancy = async () => {
    if (!endTenancyId || !moveOutDate) return;
    try {
      await tenantsApi.endTenancy(endTenancyId, { moveOutDate });
      setEndTenancyOpen(false);
      setSuccess(t('tenants.tenancyEnded'));
      load();
    } catch { setError(t('tenants.failedEndTenancy')); }
  };

  // ─── Delete ───────────────────────────────────────────

  const openDelete = (tp: TenantProfileDto) => {
    setDeleteId(tp.id);
    setDeleteName(tp.fullName);
    setDeleteConfirmOpen(true);
  };

  const handleDelete = async () => {
    if (!deleteId) return;
    try {
      const res = await tenantsApi.delete(deleteId);
      const data = res.data as { archived?: boolean; message?: string };
      setDeleteConfirmOpen(false);
      setSuccess(data.archived ? t('tenants.archived') : t('tenants.deleted'));
      load();
    } catch { setError(t('tenants.failedDelete')); }
  };

  // ─── History ──────────────────────────────────────────

  const openHistory = async (unitId: number, unitNumber: string) => {
    setHistoryUnitId(unitId);
    setHistoryUnitNumber(unitNumber);
    setHistoryOpen(true);
    setHistoryLoading(true);
    try {
      const res = await tenantsApi.unitHistory(unitId);
      setHistoryData(res.data);
    } catch { setError(t('tenants.failedLoadHistory')); }
    finally { setHistoryLoading(false); }
  };

  // ─── Messaging ──────────────────────────────────────

  const openSendMessage = (tp: TenantProfileDto) => {
    setMsgTenant(tp);
    setMsgForm({ subject: '', body: '' });
    setMsgDialogOpen(true);
  };

  const handleSendMessage = async () => {
    if (!msgTenant || !msgForm.subject.trim() || !msgForm.body.trim()) return;
    setMsgSending(true);
    try {
      await tenantMessagesApi.sendMessage(msgTenant.id, msgForm);
      setMsgDialogOpen(false);
      setSuccess(t('tenants.messageSent'));
    } catch { setError(t('tenants.failedSendMessage')); }
    finally { setMsgSending(false); }
  };

  const openMessageHistory = async (tp: TenantProfileDto) => {
    setMsgHistoryTenant(tp);
    setMsgHistoryOpen(true);
    setMsgHistoryLoading(true);
    try {
      const res = await tenantMessagesApi.getForTenant(tp.id);
      setMsgHistory(res.data);
    } catch { setError(t('tenants.failedLoadMessages')); }
    finally { setMsgHistoryLoading(false); }
  };

  const handleSendReminders = async () => {
    if (!filterBuilding) { setError(t('tenants.selectBuildingFirst')); return; }
    setReminderSending(true);
    try {
      const res = await tenantMessagesApi.sendPaymentReminders(Number(filterBuilding));
      setSuccess(res.data.message);
    } catch { setError(t('tenants.failedSendReminders')); }
    finally { setReminderSending(false); }
  };

  // ─── Render ───────────────────────────────────────────

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h4" sx={{ fontWeight: 700, fontSize: { xs: '1.3rem', md: '2rem' } }}>
          {t('tenants.title')}
        </Typography>
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button variant="outlined" color="warning" startIcon={<NotificationsActive />}
            onClick={handleSendReminders} disabled={!filterBuilding || reminderSending}>
            {reminderSending ? <CircularProgress size={20} /> : t('tenants.sendPaymentReminders')}
          </Button>
          <Button variant="contained" startIcon={<PersonAdd />} onClick={openCreate}>
            {t('tenants.addTenant')}
          </Button>
        </Box>
      </Box>

      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}
      {success && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccess('')}>{success}</Alert>}

      {/* Filters */}
      <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
        <TextField select label={t('tenants.filterBuilding')} value={filterBuilding}
          onChange={e => setFilterBuilding(e.target.value)} size="small" sx={{ minWidth: 200 }}>
          <MenuItem value="">{t('app.all')}</MenuItem>
          {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
        </TextField>
        <TextField select label={t('tenants.filterStatus')} value={filterStatus}
          onChange={e => setFilterStatus(e.target.value as StatusFilter)} size="small" sx={{ minWidth: 160 }}>
          <MenuItem value="">{t('app.all')}</MenuItem>
          <MenuItem value="Active">{t('tenants.active')}</MenuItem>
          <MenuItem value="Inactive">{t('tenants.inactive')}</MenuItem>
          <MenuItem value="Archived">{t('tenants.archivedStatus')}</MenuItem>
        </TextField>
        <TextField label={t('tenants.search')} value={searchText}
          onChange={e => setSearchText(e.target.value)} size="small" sx={{ minWidth: 200 }} />
      </Box>

      {/* Table / Cards */}
      {isMobile ? (
        <Stack spacing={1.5}>
          {filtered.map(tp => (
            <Card key={tp.id} variant="outlined">
              <CardActionArea onClick={() => openEdit(tp)}>
                <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                    <Typography variant="subtitle2" fontWeight={600}>{tp.fullName}</Typography>
                    <Chip label={t(`tenants.${statusLabel(tp).toLowerCase()}`, statusLabel(tp))} size="small" color={statusColor(tp)} />
                  </Box>
                  <Typography variant="body2" color="text.secondary">
                    {tp.buildingName} · {t('tenants.unitLabel')} {tp.unitNumber}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {tp.phone} · {tp.email}
                  </Typography>
                  <Box sx={{ display: 'flex', gap: 0.5, mt: 1, flexWrap: 'wrap' }}>
                    {tp.isActive && (
                      <Chip label={t('tenants.endTenancy')} size="small" color="warning" variant="outlined"
                        onClick={(e) => { e.stopPropagation(); openEndTenancy(tp); }} />
                    )}
                    <Chip icon={<Send sx={{ fontSize: 14 }} />} label={t('tenants.sendMessage')} size="small" color="primary" variant="outlined"
                      onClick={(e) => { e.stopPropagation(); openSendMessage(tp); }} />
                    <Chip icon={<Chat sx={{ fontSize: 14 }} />} label={t('tenants.messageHistory')} size="small" variant="outlined"
                      onClick={(e) => { e.stopPropagation(); openMessageHistory(tp); }} />
                    <Chip label={t('tenants.history')} size="small" variant="outlined"
                      onClick={(e) => { e.stopPropagation(); openHistory(tp.unitId, tp.unitNumber || ''); }} />
                    <Chip label={tp.isArchived ? t('app.delete') : t('tenants.archive')} size="small" color="error" variant="outlined"
                      onClick={(e) => { e.stopPropagation(); openDelete(tp); }} />
                  </Box>
                </CardContent>
              </CardActionArea>
            </Card>
          ))}
          {filtered.length === 0 && (
            <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('tenants.noTenants')}</Typography>
          )}
        </Stack>
      ) : (
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>{t('tenants.building')}</TableCell>
                <TableCell>{t('tenants.unitLabel')}</TableCell>
                <TableCell>{t('tenants.fullName')}</TableCell>
                <TableCell>{t('tenants.phone')}</TableCell>
                <TableCell>{t('tenants.email')}</TableCell>
                <TableCell>{t('tenants.moveIn')}</TableCell>
                <TableCell>{t('tenants.moveOut')}</TableCell>
                <TableCell>{t('tenants.status')}</TableCell>
                <TableCell>{t('app.actions')}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {filtered.map(tp => (
                <TableRow key={tp.id} hover>
                  <TableCell>{tp.buildingName}</TableCell>
                  <TableCell>{tp.unitNumber}</TableCell>
                  <TableCell>{tp.fullName}</TableCell>
                  <TableCell>{tp.phone}</TableCell>
                  <TableCell>{tp.email}</TableCell>
                  <TableCell>{formatDateOnly(tp.moveInDate)}</TableCell>
                  <TableCell>{formatDateOnly(tp.moveOutDate)}</TableCell>
                  <TableCell>
                    <Chip label={t(`tenants.${statusLabel(tp).toLowerCase()}`, statusLabel(tp))} size="small" color={statusColor(tp)} />
                  </TableCell>
                  <TableCell>
                    <Box sx={{ display: 'flex', gap: 0.5 }}>
                      <Tooltip title={t('tenants.editTenant')}>
                        <IconButton size="small" onClick={() => openEdit(tp)}><Edit fontSize="small" /></IconButton>
                      </Tooltip>
                      {tp.isActive && (
                        <Tooltip title={t('tenants.endTenancy')}>
                          <IconButton size="small" color="warning" onClick={() => openEndTenancy(tp)}>
                            <EventBusy fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                      <Tooltip title={t('tenants.sendMessage')}>
                        <IconButton size="small" color="primary" onClick={() => openSendMessage(tp)}>
                          <Send fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={t('tenants.messageHistory')}>
                        <IconButton size="small" onClick={() => openMessageHistory(tp)}>
                          <Chat fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={t('tenants.history')}>
                        <IconButton size="small" onClick={() => openHistory(tp.unitId, tp.unitNumber || '')}>
                          <History fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={t('tenants.removeTenant')}>
                        <IconButton size="small" color="error" onClick={() => openDelete(tp)}>
                          {tp.isArchived ? <Delete fontSize="small" /> : <Archive fontSize="small" />}
                        </IconButton>
                      </Tooltip>
                    </Box>
                  </TableCell>
                </TableRow>
              ))}
              {filtered.length === 0 && (
                <TableRow><TableCell colSpan={9} align="center">{t('tenants.noTenants')}</TableCell></TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* ─── Add / Edit Dialog ─────────────────────────── */}
      <Dialog open={formOpen} onClose={() => setFormOpen(false)} maxWidth="sm" fullWidth fullScreen={isMobile}>
        <DialogTitle>{editingId ? t('tenants.editTenant') : t('tenants.addTenant')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            {!editingId && (
              <>
                <TextField select label={t('tenants.building')} value={formBuildingId}
                  onChange={e => { const bid = Number(e.target.value); setFormBuildingId(bid); loadUnits(bid); setFormData({ ...formData, unitId: 0 }); }}
                  fullWidth required>
                  <MenuItem value={0}>{t('tenants.selectBuilding')}</MenuItem>
                  {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
                </TextField>
                <TextField select label={t('tenants.unitLabel')} value={formData.unitId}
                  onChange={e => setFormData({ ...formData, unitId: Number(e.target.value) })}
                  fullWidth required>
                  <MenuItem value={0}>{t('tenants.selectUnit')}</MenuItem>
                  {units.map(u => <MenuItem key={u.id} value={u.id}>{u.unitNumber} {u.ownerName ? `(${u.ownerName})` : ''}</MenuItem>)}
                </TextField>
              </>
            )}
            <TextField label={t('tenants.fullName')} value={formData.fullName}
              onChange={e => setFormData({ ...formData, fullName: e.target.value })}
              fullWidth required />
            <TextField label={t('tenants.phone')} value={formData.phone}
              onChange={e => setFormData({ ...formData, phone: e.target.value })}
              fullWidth />
            <TextField label={t('tenants.email')} value={formData.email}
              onChange={e => setFormData({ ...formData, email: e.target.value })}
              fullWidth />
            <TextField label={t('tenants.moveIn')} type="date" value={formData.moveInDate}
              onChange={e => setFormData({ ...formData, moveInDate: e.target.value })}
              fullWidth slotProps={{ inputLabel: { shrink: true } }} />
            <FormControlLabel
              control={<Switch checked={formData.isActive} onChange={e => setFormData({ ...formData, isActive: e.target.checked })} />}
              label={t('tenants.activeTenant')}
            />
            {formData.isActive && !editingId && (() => {
              const existingActive = tenants.find(tp => tp.unitId === formData.unitId && tp.isActive);
              return existingActive ? (
                <Alert severity="warning">
                  {t('tenants.existingTenantWarning', { name: existingActive.fullName })}
                </Alert>
              ) : null;
            })()}
            <TextField label={t('tenants.notes')} value={formData.notes}
              onChange={e => setFormData({ ...formData, notes: e.target.value })}
              fullWidth multiline rows={2} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setFormOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleSave}>
            {editingId ? t('app.save') : t('tenants.addTenant')}
          </Button>
        </DialogActions>
      </Dialog>

      {/* ─── End Tenancy Dialog ────────────────────────── */}
      <Dialog open={endTenancyOpen} onClose={() => setEndTenancyOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{t('tenants.endTenancy')}</DialogTitle>
        <DialogContent>
          <Typography sx={{ mb: 2 }}>{t('tenants.endTenancyConfirm')}</Typography>
          <TextField label={t('tenants.moveOut')} type="date" value={moveOutDate}
            onChange={e => setMoveOutDate(e.target.value)}
            fullWidth slotProps={{ inputLabel: { shrink: true } }} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEndTenancyOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" color="warning" onClick={handleEndTenancy}>{t('tenants.endTenancy')}</Button>
        </DialogActions>
      </Dialog>

      {/* ─── Delete / Archive Confirm Dialog ────────────── */}
      <Dialog open={deleteConfirmOpen} onClose={() => setDeleteConfirmOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{t('tenants.removeTenant')}</DialogTitle>
        <DialogContent>
          <Typography>{t('tenants.deleteConfirm', { name: deleteName })}</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
            {t('tenants.deleteNote')}
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteConfirmOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" color="error" onClick={handleDelete}>{t('tenants.removeTenant')}</Button>
        </DialogActions>
      </Dialog>

      {/* ─── Tenant History Dialog ─────────────────────── */}
      <Dialog open={historyOpen} onClose={() => setHistoryOpen(false)} maxWidth="md" fullWidth fullScreen={isMobile}>
        <DialogTitle>{t('tenants.historyTitle', { unit: historyUnitNumber })}</DialogTitle>
        <DialogContent>
          {historyLoading ? <CircularProgress /> : (
            <TableContainer>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>{t('tenants.fullName')}</TableCell>
                    <TableCell>{t('tenants.phone')}</TableCell>
                    <TableCell>{t('tenants.email')}</TableCell>
                    <TableCell>{t('tenants.moveIn')}</TableCell>
                    <TableCell>{t('tenants.moveOut')}</TableCell>
                    <TableCell>{t('tenants.status')}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {historyData.map(tp => (
                    <TableRow key={tp.id}>
                      <TableCell>{tp.fullName}</TableCell>
                      <TableCell>{tp.phone}</TableCell>
                      <TableCell>{tp.email}</TableCell>
                      <TableCell>{formatDateOnly(tp.moveInDate)}</TableCell>
                      <TableCell>{formatDateOnly(tp.moveOutDate)}</TableCell>
                      <TableCell>
                        <Chip label={t(`tenants.${statusLabel(tp).toLowerCase()}`, statusLabel(tp))} size="small" color={statusColor(tp)} />
                      </TableCell>
                    </TableRow>
                  ))}
                  {historyData.length === 0 && (
                    <TableRow><TableCell colSpan={6} align="center">{t('tenants.noHistory')}</TableCell></TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setHistoryOpen(false)}>{t('app.close')}</Button>
        </DialogActions>
      </Dialog>

      {/* ─── Send Message Dialog ───────────────────────── */}
      <Dialog open={msgDialogOpen} onClose={() => setMsgDialogOpen(false)} maxWidth="sm" fullWidth fullScreen={isMobile}>
        <DialogTitle>{t('tenants.sendMessageTo', { name: msgTenant?.fullName })}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField label={t('tenants.msgSubject')} value={msgForm.subject}
              onChange={e => setMsgForm({ ...msgForm, subject: e.target.value })}
              fullWidth required />
            <TextField label={t('tenants.msgBody')} value={msgForm.body}
              onChange={e => setMsgForm({ ...msgForm, body: e.target.value })}
              fullWidth required multiline rows={6} />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setMsgDialogOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleSendMessage} disabled={msgSending || !msgForm.subject.trim() || !msgForm.body.trim()}>
            {msgSending ? <CircularProgress size={20} /> : t('tenants.send')}
          </Button>
        </DialogActions>
      </Dialog>

      {/* ─── Message History Dialog ────────────────────── */}
      <Dialog open={msgHistoryOpen} onClose={() => setMsgHistoryOpen(false)} maxWidth="md" fullWidth fullScreen={isMobile}>
        <DialogTitle>{t('tenants.messagesFor', { name: msgHistoryTenant?.fullName })}</DialogTitle>
        <DialogContent>
          {msgHistoryLoading ? <CircularProgress /> : msgHistory.length === 0 ? (
            <Typography color="text.secondary" align="center" sx={{ py: 4 }}>{t('tenants.noMessages')}</Typography>
          ) : (
            <Stack spacing={1.5} sx={{ mt: 1 }}>
              {msgHistory.map(msg => (
                <Card key={msg.id} variant="outlined" sx={{
                  borderLeft: '4px solid',
                  borderLeftColor: msg.messageType === 'TenantReply' ? '#2e7d32' : msg.messageType === 'Warning' ? '#d32f2f' : msg.messageType === 'PaymentReminder' ? '#ed6c02' : '#1976d2',
                  ...(msg.messageType === 'TenantReply' ? { bgcolor: '#f1f8e9' } : {})
                }}>
                  <CardContent sx={{ py: 1.5, '&:last-child': { pb: 1.5 } }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                      <Typography variant="subtitle2" fontWeight={700}>
                        {msg.messageType === 'TenantReply' && '↩ '}{msg.subject}
                      </Typography>
                      <Box sx={{ display: 'flex', gap: 0.5, alignItems: 'center' }}>
                        {msg.payerCategory && (
                          <Chip label={msg.payerCategory} size="small" variant="outlined"
                            color={msg.payerCategory === 'ChronicallyLate' ? 'error' : msg.payerCategory === 'OccasionallyLate' ? 'warning' : 'success'} />
                        )}
                        <Chip label={msg.messageType} size="small" variant="outlined" />
                        {msg.isRead ? (
                          <Chip icon={<MarkEmailRead sx={{ fontSize: 14 }} />} label={t('tenants.read')} size="small" color="success" />
                        ) : (
                          <Chip icon={<Circle sx={{ fontSize: 8 }} />} label={t('tenants.unread')} size="small" color="error" />
                        )}
                      </Box>
                    </Box>
                    <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', my: 1 }}>{msg.body}</Typography>
                    <Typography variant="caption" color="text.secondary">
                      {t('tenants.sentBy')}: {msg.sentByName || 'AI Agent'} · {new Date(msg.createdAtUtc).toLocaleString()}
                      {msg.readAtUtc && ` · ${t('tenants.readAt')}: ${new Date(msg.readAtUtc).toLocaleString()}`}
                    </Typography>
                  </CardContent>
                </Card>
              ))}
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button variant="outlined" startIcon={<Send />} onClick={() => { setMsgHistoryOpen(false); if (msgHistoryTenant) openSendMessage(msgHistoryTenant); }}>
            {t('tenants.sendNew')}
          </Button>
          <Button onClick={() => setMsgHistoryOpen(false)}>{t('app.close')}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default TenantsPage;
