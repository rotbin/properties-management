import React, { useState, useEffect } from 'react';
import {
  Typography, Box, Button, Card, CardContent, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Paper, Dialog, DialogTitle, DialogContent,
  DialogActions, TextField, MenuItem, Chip, Alert, CircularProgress,
  FormControl, InputLabel, Select, IconButton, Tooltip, Stack,
  Checkbox, FormControlLabel, useMediaQuery, useTheme, ToggleButtonGroup, ToggleButton
} from '@mui/material';
import { Send, Preview, Delete, Notifications, Sms, Email } from '@mui/icons-material';
import { buildingsApi, smsApi } from '../../api/services';
import type {
  BuildingDto, SmsTemplateDto, SmsCampaignRecipientDto, SmsCampaignDto, SendCampaignResult, ReminderChannel
} from '../../types';
import { REMINDER_CHANNELS } from '../../types';
import { useTranslation } from 'react-i18next';

const SendRemindersPage: React.FC = () => {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));

  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [templates, setTemplates] = useState<SmsTemplateDto[]>([]);
  const [selectedBuilding, setSelectedBuilding] = useState<number | ''>('');
  const [period, setPeriod] = useState(new Date().toISOString().slice(0, 7));
  const [selectedTemplate, setSelectedTemplate] = useState<number | ''>('');
  const [includePartial, setIncludePartial] = useState(true);
  const [channel, setChannel] = useState<ReminderChannel>('Sms');
  const [notes, setNotes] = useState('');

  const [loading, setLoading] = useState(false);
  const [msg, setMsg] = useState('');
  const [msgSeverity, setMsgSeverity] = useState<'info' | 'success' | 'error'>('info');

  const [campaign, setCampaign] = useState<SmsCampaignDto | null>(null);
  const [recipients, setRecipients] = useState<SmsCampaignRecipientDto[]>([]);

  const [previewOpen, setPreviewOpen] = useState(false);
  const [previewText, setPreviewText] = useState('');
  const [previewSubject, setPreviewSubject] = useState('');
  const [previewName, setPreviewName] = useState('');

  const [confirmOpen, setConfirmOpen] = useState(false);
  const [sendResult, setSendResult] = useState<SendCampaignResult | null>(null);
  const [sendResultOpen, setSendResultOpen] = useState(false);

  useEffect(() => {
    buildingsApi.getAll().then(r => { setBuildings(r.data); if (r.data.length > 0) setSelectedBuilding(r.data[0].id); });
    smsApi.getTemplates().then(r => {
      setTemplates(r.data);
      const heTemplate = r.data.find(t2 => t2.language === 'he');
      if (heTemplate) setSelectedTemplate(heTemplate.id);
      else if (r.data.length > 0) setSelectedTemplate(r.data[0].id);
    });
  }, []);

  const showMsg = (text: string, severity: 'info' | 'success' | 'error' = 'info') => {
    setMsg(text);
    setMsgSeverity(severity);
  };

  const handleGenerate = async () => {
    if (!selectedBuilding || !selectedTemplate || !period) {
      showMsg(t('reminders.errorMissingFields'), 'error');
      return;
    }
    setLoading(true);
    try {
      const r = await smsApi.createCampaign({
        buildingId: selectedBuilding as number,
        period,
        templateId: selectedTemplate as number,
        includePartial,
        channel,
        notes: notes || undefined,
      });
      setCampaign(r.data.campaign);
      setRecipients(r.data.recipients);
      showMsg(t('reminders.listGenerated'), 'success');
    } catch (err: unknown) {
      let detail = '';
      if (err && typeof err === 'object' && 'response' in err) {
        const resp = (err as { response?: { data?: { message?: string } } }).response;
        if (resp?.data?.message) detail = resp.data.message;
      }
      showMsg(t('reminders.errorGenerating') + (detail ? ` – ${detail}` : ''), 'error');
    } finally { setLoading(false); }
  };

  const handleToggleSelect = async (recipientId: number, isSelected: boolean) => {
    if (!campaign) return;
    try {
      const r = await smsApi.updateRecipients(campaign.id, {
        updates: [{ recipientId, isSelected }]
      });
      setRecipients(r.data);
    } catch { showMsg(t('reminders.errorUpdating'), 'error'); }
  };

  const handleRemoveRecipient = async (recipientId: number) => {
    if (!campaign) return;
    try {
      const r = await smsApi.updateRecipients(campaign.id, {
        removeRecipientIds: [recipientId]
      });
      setRecipients(r.data);
    } catch { showMsg(t('reminders.errorUpdating'), 'error'); }
  };

  const handlePreview = async (recipientId: number, name: string) => {
    if (!campaign) return;
    try {
      const r = await smsApi.previewMessage(campaign.id, recipientId);
      setPreviewText(r.data.message);
      setPreviewSubject(r.data.subject || '');
      setPreviewName(name);
      setPreviewOpen(true);
    } catch { showMsg(t('reminders.errorPreview'), 'error'); }
  };

  const handleSend = async () => {
    if (!campaign) return;
    setConfirmOpen(false);
    setLoading(true);
    try {
      const r = await smsApi.sendCampaign(campaign.id);
      setSendResult(r.data);
      setSendResultOpen(true);
      const updated = await smsApi.getCampaign(campaign.id);
      setCampaign(updated.data.campaign);
      setRecipients(updated.data.recipients);
      showMsg(t('reminders.sendSuccess'), 'success');
    } catch (err: unknown) {
      let detail = '';
      if (err && typeof err === 'object' && 'response' in err) {
        const resp = (err as { response?: { data?: { message?: string } } }).response;
        if (resp?.data?.message) detail = resp.data.message;
      }
      showMsg(t('reminders.errorSending') + (detail ? ` – ${detail}` : ''), 'error');
    } finally { setLoading(false); }
  };

  const selectedCount = recipients.filter(r => r.isSelected).length;
  const showPhone = !campaign || campaign.channel !== 'Email';
  const showEmail = !campaign || campaign.channel !== 'Sms';

  const statusColor = (status: string) => {
    switch (status) {
      case 'Paid': return 'success';
      case 'Overdue': return 'error';
      case 'Partial': return 'warning';
      case 'Unpaid': return 'error';
      default: return 'default';
    }
  };

  const sendStatusColor = (status: string) => {
    switch (status) {
      case 'Sent': return 'success';
      case 'Failed': return 'error';
      case 'Skipped': return 'warning';
      default: return 'default';
    }
  };

  const channelLabel = campaign
    ? t(`reminders.channel.${campaign.channel}`)
    : t(`reminders.channel.${channel}`);

  return (
    <Box>
      <Typography variant="h4" gutterBottom sx={{ fontSize: { xs: '1.3rem', md: '2rem' }, fontWeight: 700 }}>
        <Notifications sx={{ mr: 1, verticalAlign: 'middle' }} />
        {t('reminders.title')}
      </Typography>

      {msg && <Alert severity={msgSeverity} onClose={() => setMsg('')} sx={{ mb: 2 }}>{msg}</Alert>}

      {/* Setup Section */}
      <Card sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom>{t('reminders.setup')}</Typography>
          <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', alignItems: 'flex-end' }}>
            <FormControl sx={{ minWidth: 200 }}>
              <InputLabel>{t('reminders.building')}</InputLabel>
              <Select value={selectedBuilding} label={t('reminders.building')} onChange={e => setSelectedBuilding(e.target.value as number)}>
                {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
              </Select>
            </FormControl>

            <TextField label={t('reminders.period')} type="month" value={period}
              onChange={e => setPeriod(e.target.value)} InputLabelProps={{ shrink: true }} size="small" />

            <FormControl sx={{ minWidth: 250 }}>
              <InputLabel>{t('reminders.template')}</InputLabel>
              <Select value={selectedTemplate} label={t('reminders.template')} onChange={e => setSelectedTemplate(e.target.value as number)}>
                {templates.map(t2 => <MenuItem key={t2.id} value={t2.id}>{t2.name} ({t2.language})</MenuItem>)}
              </Select>
            </FormControl>

            <FormControlLabel
              control={<Checkbox checked={includePartial} onChange={e => setIncludePartial(e.target.checked)} />}
              label={t('reminders.includePartial')}
            />
          </Box>

          {/* Channel selector */}
          <Box sx={{ mt: 2 }}>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>{t('reminders.channelLabel')}</Typography>
            <ToggleButtonGroup
              value={channel}
              exclusive
              onChange={(_, v) => { if (v) setChannel(v); }}
              size="small"
            >
              {REMINDER_CHANNELS.map(ch => (
                <ToggleButton key={ch} value={ch} sx={{ px: 2 }}>
                  {ch === 'Sms' && <Sms sx={{ mr: 0.5, fontSize: 18 }} />}
                  {ch === 'Email' && <Email sx={{ mr: 0.5, fontSize: 18 }} />}
                  {ch === 'Both' && <><Sms sx={{ mr: 0.3, fontSize: 16 }} /><Email sx={{ mr: 0.5, fontSize: 16 }} /></>}
                  {t(`reminders.channel.${ch}`)}
                </ToggleButton>
              ))}
            </ToggleButtonGroup>
          </Box>

          <TextField label={t('reminders.campaignNotes')} value={notes} onChange={e => setNotes(e.target.value)}
            fullWidth sx={{ mt: 2 }} size="small" />

          <Box sx={{ mt: 2 }}>
            <Button variant="contained" onClick={handleGenerate} disabled={loading || !selectedBuilding || !selectedTemplate}>
              {loading ? <CircularProgress size={20} sx={{ mr: 1 }} /> : null}
              {t('reminders.generateList')}
            </Button>
          </Box>
        </CardContent>
      </Card>

      {/* Recipients Section */}
      {campaign && (
        <>
          {/* Summary Cards */}
          <Box sx={{ display: 'flex', gap: isMobile ? 1 : 2, mb: 2, flexWrap: 'wrap' }}>
            <Card variant="outlined" sx={{ p: 1.5, minWidth: isMobile ? 'calc(50% - 8px)' : 140, flex: isMobile ? '1 1 calc(50% - 8px)' : undefined }}>
              <Typography variant="body2" color="text.secondary">{t('reminders.totalRecipients')}</Typography>
              <Typography variant="h5">{recipients.length}</Typography>
            </Card>
            <Card variant="outlined" sx={{ p: 1.5, minWidth: isMobile ? 'calc(50% - 8px)' : 140, flex: isMobile ? '1 1 calc(50% - 8px)' : undefined }}>
              <Typography variant="body2" color="text.secondary">{t('reminders.selected')}</Typography>
              <Typography variant="h5" color="primary.main">{selectedCount}</Typography>
            </Card>
            <Card variant="outlined" sx={{ p: 1.5, minWidth: isMobile ? 'calc(50% - 8px)' : 140, flex: isMobile ? '1 1 calc(50% - 8px)' : undefined }}>
              <Typography variant="body2" color="text.secondary">{t('reminders.channelLabel')}</Typography>
              <Chip label={channelLabel} size="small" color="primary" />
            </Card>
            <Card variant="outlined" sx={{ p: 1.5, minWidth: isMobile ? 'calc(50% - 8px)' : 140, flex: isMobile ? '1 1 calc(50% - 8px)' : undefined }}>
              <Typography variant="body2" color="text.secondary">{t('reminders.campaignStatus')}</Typography>
              <Chip label={t(`reminders.status.${campaign.status}`, campaign.status)} size="small"
                color={campaign.status === 'Sent' ? 'success' : campaign.status === 'Draft' ? 'info' : 'default'} />
            </Card>
            {campaign.status === 'Sent' && (
              <Card variant="outlined" sx={{ p: 1.5, minWidth: isMobile ? '100%' : 200 }}>
                <Typography variant="body2" color="text.secondary">{t('reminders.sendResults')}</Typography>
                <Box sx={{ display: 'flex', gap: 1 }}>
                  <Chip label={`${t('reminders.sent')}: ${campaign.sentCount}`} color="success" size="small" />
                  <Chip label={`${t('reminders.failed')}: ${campaign.failedCount}`} color="error" size="small" />
                  <Chip label={`${t('reminders.skipped')}: ${campaign.skippedCount}`} color="warning" size="small" />
                </Box>
              </Card>
            )}
          </Box>

          {/* Recipients Table */}
          <Card>
            <CardContent>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2, flexWrap: 'wrap', gap: 1 }}>
                <Typography variant="h6">{t('reminders.recipientsList')}</Typography>
                {campaign.status === 'Draft' && (
                  <Button variant="contained" color="primary" startIcon={<Send />}
                    onClick={() => setConfirmOpen(true)} disabled={selectedCount === 0}>
                    {t('reminders.sendReminders')} ({selectedCount})
                  </Button>
                )}
              </Box>

              {isMobile ? (
                <Stack spacing={1.5}>
                  {recipients.map(r => (
                    <Card key={r.id} variant="outlined">
                      <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                          {campaign.status === 'Draft' && (
                            <Checkbox checked={r.isSelected} size="small"
                              onChange={e => handleToggleSelect(r.id, e.target.checked)} />
                          )}
                          <Typography variant="subtitle2" sx={{ flex: 1 }}>{r.fullNameSnapshot}</Typography>
                          <Chip label={t(`enums.chargeStatus.${r.chargeStatusSnapshot}`, r.chargeStatusSnapshot)}
                            size="small" color={statusColor(r.chargeStatusSnapshot) as any} />
                        </Box>
                        <Typography variant="body2" color="text.secondary">
                          {showPhone && (r.phoneSnapshot || '—')}
                          {showPhone && showEmail && ' · '}
                          {showEmail && (r.emailSnapshot || '—')}
                          {' · '}{t('hoa.balance')}: {r.outstandingSnapshot.toFixed(2)}
                        </Typography>
                        {r.sendStatus !== 'Pending' && (
                          <Chip label={t(`reminders.sendStatus.${r.sendStatus}`, r.sendStatus)} size="small"
                            color={sendStatusColor(r.sendStatus) as any} sx={{ mt: 0.5 }} />
                        )}
                        <Box sx={{ display: 'flex', gap: 0.5, mt: 0.5 }}>
                          <Button size="small" variant="outlined" startIcon={<Preview />}
                            onClick={() => handlePreview(r.id, r.fullNameSnapshot)}>{t('reminders.preview')}</Button>
                          {campaign.status === 'Draft' && (
                            <IconButton size="small" color="error" onClick={() => handleRemoveRecipient(r.id)}>
                              <Delete />
                            </IconButton>
                          )}
                        </Box>
                      </CardContent>
                    </Card>
                  ))}
                  {recipients.length === 0 && (
                    <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('reminders.noRecipients')}</Typography>
                  )}
                </Stack>
              ) : (
                <TableContainer component={Paper} variant="outlined">
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        {campaign.status === 'Draft' && <TableCell padding="checkbox" />}
                        <TableCell>{t('reminders.unitCol')}</TableCell>
                        <TableCell>{t('reminders.tenantName')}</TableCell>
                        {showPhone && <TableCell>{t('reminders.phone')}</TableCell>}
                        {showEmail && <TableCell>{t('reminders.email')}</TableCell>}
                        <TableCell>{t('reminders.chargeStatus')}</TableCell>
                        <TableCell align="right">{t('reminders.outstanding')}</TableCell>
                        {campaign.status === 'Sent' && <TableCell>{t('reminders.sendStatusCol')}</TableCell>}
                        <TableCell>{t('app.actions')}</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {recipients.map(r => (
                        <TableRow key={r.id} sx={r.isSelected ? { bgcolor: 'action.selected' } : undefined}>
                          {campaign.status === 'Draft' && (
                            <TableCell padding="checkbox">
                              <Checkbox checked={r.isSelected} size="small"
                                onChange={e => handleToggleSelect(r.id, e.target.checked)} />
                            </TableCell>
                          )}
                          <TableCell>{r.unitId}</TableCell>
                          <TableCell>{r.fullNameSnapshot}</TableCell>
                          {showPhone && <TableCell dir="ltr">{r.phoneSnapshot || '—'}</TableCell>}
                          {showEmail && <TableCell>{r.emailSnapshot || '—'}</TableCell>}
                          <TableCell>
                            <Chip label={t(`enums.chargeStatus.${r.chargeStatusSnapshot}`, r.chargeStatusSnapshot)}
                              size="small" color={statusColor(r.chargeStatusSnapshot) as any} />
                          </TableCell>
                          <TableCell align="right">{r.outstandingSnapshot.toFixed(2)}</TableCell>
                          {campaign.status === 'Sent' && (
                            <TableCell>
                              <Chip label={t(`reminders.sendStatus.${r.sendStatus}`, r.sendStatus)} size="small"
                                color={sendStatusColor(r.sendStatus) as any} />
                              {r.errorMessage && (
                                <Tooltip title={r.errorMessage}><Typography variant="caption" color="error"> ⚠</Typography></Tooltip>
                              )}
                            </TableCell>
                          )}
                          <TableCell>
                            <Tooltip title={t('reminders.preview')}>
                              <IconButton size="small" onClick={() => handlePreview(r.id, r.fullNameSnapshot)}>
                                <Preview />
                              </IconButton>
                            </Tooltip>
                            {campaign.status === 'Draft' && (
                              <Tooltip title={t('app.delete')}>
                                <IconButton size="small" color="error" onClick={() => handleRemoveRecipient(r.id)}>
                                  <Delete />
                                </IconButton>
                              </Tooltip>
                            )}
                          </TableCell>
                        </TableRow>
                      ))}
                      {recipients.length === 0 && (
                        <TableRow><TableCell colSpan={10} align="center">{t('reminders.noRecipients')}</TableCell></TableRow>
                      )}
                    </TableBody>
                  </Table>
                </TableContainer>
              )}
            </CardContent>
          </Card>
        </>
      )}

      {/* Preview Dialog */}
      <Dialog open={previewOpen} onClose={() => setPreviewOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{t('reminders.previewTitle', { name: previewName })}</DialogTitle>
        <DialogContent>
          {previewSubject && (
            <Typography variant="subtitle2" sx={{ mt: 1, mb: 1 }}>
              {t('reminders.emailSubject')}: {previewSubject}
            </Typography>
          )}
          <Paper variant="outlined" sx={{ p: 2, mt: 1, bgcolor: 'grey.50', direction: 'rtl' }}>
            <Typography variant="body1" sx={{ whiteSpace: 'pre-wrap' }}>{previewText}</Typography>
          </Paper>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPreviewOpen(false)}>{t('app.close')}</Button>
        </DialogActions>
      </Dialog>

      {/* Send Confirmation Dialog */}
      <Dialog open={confirmOpen} onClose={() => setConfirmOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{t('reminders.confirmSendTitle')}</DialogTitle>
        <DialogContent>
          <Typography>{t('reminders.confirmSendBody', { count: selectedCount, channel: channelLabel })}</Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setConfirmOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" color="primary" onClick={handleSend} startIcon={<Send />}>
            {t('reminders.confirmSend')}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Send Result Dialog */}
      <Dialog open={sendResultOpen} onClose={() => setSendResultOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{t('reminders.sendResultTitle')}</DialogTitle>
        <DialogContent>
          {sendResult && (
            <Stack spacing={1} sx={{ mt: 1 }}>
              <Chip label={`${t('reminders.selected')}: ${sendResult.totalSelected}`} />
              <Chip label={`${t('reminders.sent')}: ${sendResult.sentCount}`} color="success" />
              <Chip label={`${t('reminders.failed')}: ${sendResult.failedCount}`} color="error" />
              <Chip label={`${t('reminders.skipped')}: ${sendResult.skippedCount}`} color="warning" />
            </Stack>
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSendResultOpen(false)}>{t('app.close')}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default SendRemindersPage;
