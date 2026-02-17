import React, { useState, useEffect } from 'react';
import {
  Typography, Box, Card, CardContent, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Paper, Button, Chip, Alert,
  CircularProgress, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, MenuItem, Stack, useMediaQuery, useTheme, IconButton, Tooltip
} from '@mui/material';
import { Receipt, Download, Add, Settings, Save } from '@mui/icons-material';
import { accountingApi, buildingsApi } from '../../api/services';
import type { ManagerInvoiceDto, BuildingDto } from '../../types';
import { formatDateLocal } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';

const ManagerInvoicesPage: React.FC = () => {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));

  const [invoices, setInvoices] = useState<ManagerInvoiceDto[]>([]);
  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState('');
  const [msgSeverity, setMsgSeverity] = useState<'success' | 'error'>('success');
  const [filterPeriod, setFilterPeriod] = useState('');
  const [issueDialog, setIssueDialog] = useState(false);
  const [issueBuildingId, setIssueBuildingId] = useState<number | ''>('');
  const [issuePeriod, setIssuePeriod] = useState(() => {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  });
  const [issuing, setIssuing] = useState(false);
  const [issuerProfileId, setIssuerProfileId] = useState('');
  const [issuerSaving, setIssuerSaving] = useState(false);
  const [showSettings, setShowSettings] = useState(false);

  const loadData = async () => {
    setLoading(true);
    try {
      const [inv, bld, profile] = await Promise.all([
        accountingApi.getInvoices(filterPeriod || undefined),
        buildingsApi.getAll(),
        accountingApi.getMyIssuerProfile()
      ]);
      setInvoices(inv.data);
      setBuildings(bld.data);
      setIssuerProfileId(profile.data.issuerProfileId || '');
    } catch { setMsg(t('mgrInvoices.errorLoading')); setMsgSeverity('error'); }
    finally { setLoading(false); }
  };

  useEffect(() => { loadData(); }, [filterPeriod]);

  const handleSaveIssuerProfile = async () => {
    setIssuerSaving(true);
    try {
      await accountingApi.setMyIssuerProfile(issuerProfileId);
      setMsg(t('mgrInvoices.profileSaved'));
      setMsgSeverity('success');
    } catch {
      setMsg(t('mgrInvoices.profileSaveError'));
      setMsgSeverity('error');
    } finally { setIssuerSaving(false); }
  };

  const handleIssue = async () => {
    if (!issueBuildingId || !issuePeriod) return;
    setIssuing(true);
    try {
      await accountingApi.issueInvoice({ buildingId: issueBuildingId as number, period: issuePeriod });
      setMsg(t('mgrInvoices.invoiceIssued'));
      setMsgSeverity('success');
      setIssueDialog(false);
      await loadData();
    } catch (err: any) {
      setMsg(err?.response?.data?.message || t('mgrInvoices.errorIssuing'));
      setMsgSeverity('error');
    } finally { setIssuing(false); }
  };

  const handleDownload = (pdfUrl?: string) => {
    if (pdfUrl) window.open(pdfUrl, '_blank');
  };

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>;

  return (
    <Box sx={{ maxWidth: 1100, mx: 'auto', p: { xs: 1, sm: 3 } }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h5" fontWeight={700}>{t('mgrInvoices.title')}</Typography>
        <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
          <TextField
            size="small"
            type="month"
            label={t('mgrInvoices.period')}
            value={filterPeriod}
            onChange={(e) => setFilterPeriod(e.target.value)}
            InputLabelProps={{ shrink: true }}
            sx={{ minWidth: 160 }}
          />
          <Button startIcon={<Add />} variant="contained" onClick={() => setIssueDialog(true)}>
            {t('mgrInvoices.issueInvoice')}
          </Button>
        </Box>
      </Box>

      {msg && <Alert severity={msgSeverity} onClose={() => setMsg('')} sx={{ mb: 2 }}>{msg}</Alert>}

      {/* Issuer Profile Settings */}
      <Card variant="outlined" sx={{ mb: 2 }}>
        <CardContent sx={{ py: 1.5, '&:last-child': { pb: 1.5 } }}>
          <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', cursor: 'pointer' }} onClick={() => setShowSettings(!showSettings)}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              <Settings fontSize="small" color="action" />
              <Typography variant="subtitle2">{t('mgrInvoices.issuerSettings')}</Typography>
              {!issuerProfileId && <Chip label={t('mgrInvoices.notConfigured')} size="small" color="warning" />}
              {issuerProfileId && <Chip label={t('mgrInvoices.configured')} size="small" color="success" />}
            </Box>
          </Box>
          {showSettings && (
            <Box sx={{ mt: 2, display: 'flex', gap: 1, alignItems: 'flex-end', flexWrap: 'wrap' }}>
              <TextField
                size="small"
                label={t('mgrInvoices.issuerProfileId')}
                value={issuerProfileId}
                onChange={e => setIssuerProfileId(e.target.value)}
                sx={{ minWidth: 280, flex: 1 }}
                helperText={t('mgrInvoices.issuerProfileIdHelp')}
              />
              <Button
                variant="contained"
                size="small"
                startIcon={issuerSaving ? <CircularProgress size={16} /> : <Save />}
                onClick={handleSaveIssuerProfile}
                disabled={issuerSaving}
                sx={{ mb: 2.5 }}
              >
                {t('app.save')}
              </Button>
            </Box>
          )}
        </CardContent>
      </Card>

      {isMobile ? (
        <Stack spacing={1.5}>
          {invoices.map(inv => (
            <Card key={inv.id} variant="outlined">
              <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                  <Typography variant="subtitle2">{inv.amount.toFixed(2)} ₪</Typography>
                  <Chip icon={<Receipt />} label={inv.invoiceDocNumber || t('mgrInvoices.issued')} size="small" color="success" />
                </Box>
                <Typography variant="body2">{inv.buildingName} · {inv.period}</Typography>
                {inv.issuedAtUtc && <Typography variant="caption" color="text.secondary">{formatDateLocal(inv.issuedAtUtc)}</Typography>}
                {inv.invoicePdfUrl && (
                  <Box sx={{ mt: 1 }}>
                    <Button size="small" startIcon={<Download />} variant="outlined" onClick={() => handleDownload(inv.invoicePdfUrl)}>
                      {t('mgrInvoices.download')}
                    </Button>
                  </Box>
                )}
              </CardContent>
            </Card>
          ))}
          {invoices.length === 0 && <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('mgrInvoices.noInvoices')}</Typography>}
        </Stack>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>{t('mgrInvoices.building')}</TableCell>
                <TableCell>{t('mgrInvoices.period')}</TableCell>
                <TableCell align="right">{t('mgrInvoices.amount')}</TableCell>
                <TableCell>{t('mgrInvoices.docNumber')}</TableCell>
                <TableCell>{t('mgrInvoices.issuedAt')}</TableCell>
                <TableCell></TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {invoices.map(inv => (
                <TableRow key={inv.id}>
                  <TableCell>{inv.buildingName}</TableCell>
                  <TableCell>{inv.period}</TableCell>
                  <TableCell align="right">{inv.amount.toFixed(2)} ₪</TableCell>
                  <TableCell>{inv.invoiceDocNumber || '—'}</TableCell>
                  <TableCell>{inv.issuedAtUtc ? formatDateLocal(inv.issuedAtUtc) : '—'}</TableCell>
                  <TableCell>
                    {inv.invoicePdfUrl && (
                      <Tooltip title={t('mgrInvoices.download')}>
                        <IconButton size="small" onClick={() => handleDownload(inv.invoicePdfUrl)}>
                          <Download fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {invoices.length === 0 && <TableRow><TableCell colSpan={6} align="center">{t('mgrInvoices.noInvoices')}</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {/* Issue Invoice Dialog */}
      <Dialog open={issueDialog} onClose={() => setIssueDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{t('mgrInvoices.issueInvoice')}</DialogTitle>
        <DialogContent>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
            <TextField
              select
              label={t('mgrInvoices.building')}
              value={issueBuildingId}
              onChange={e => setIssueBuildingId(Number(e.target.value))}
              fullWidth
              required
            >
              {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
            </TextField>
            <TextField
              type="month"
              label={t('mgrInvoices.period')}
              value={issuePeriod}
              onChange={e => setIssuePeriod(e.target.value)}
              InputLabelProps={{ shrink: true }}
              fullWidth
              required
            />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setIssueDialog(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleIssue} disabled={issuing || !issueBuildingId || !issuePeriod}>
            {issuing ? <CircularProgress size={20} /> : t('mgrInvoices.issueInvoice')}
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default ManagerInvoicesPage;
