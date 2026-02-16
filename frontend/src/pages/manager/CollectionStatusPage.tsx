import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
  Typography, Box, Card, CardContent, TextField, MenuItem, Button, Alert,
  CircularProgress, Table, TableBody, TableCell, TableContainer, TableHead,
  TableRow, Paper, Stack, useMediaQuery, useTheme, FormControl, InputLabel,
  Select, Chip, FormControlLabel, Switch, InputAdornment
} from '@mui/material';
import {
  Download, CheckCircle, Warning, Error as ErrorIcon,
  HourglassEmpty, HelpOutline, Search, Phone
} from '@mui/icons-material';
import { buildingsApi, reportsApi } from '../../api/services';
import type { BuildingDto, CollectionStatusReport } from '../../types';
import { useTranslation } from 'react-i18next';

const STATUS_COLORS: Record<string, string> = {
  Paid: '#2e7d32',
  Partial: '#f5911e',
  Unpaid: '#d32f2f',
  Overdue: '#b71c1c',
  NotGenerated: '#757575',
};

const STATUS_ICONS: Record<string, React.ReactNode> = {
  Paid: <CheckCircle fontSize="small" />,
  Partial: <HourglassEmpty fontSize="small" />,
  Unpaid: <Warning fontSize="small" />,
  Overdue: <ErrorIcon fontSize="small" />,
  NotGenerated: <HelpOutline fontSize="small" />,
};

const CollectionStatusPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));

  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [selectedBuilding, setSelectedBuilding] = useState<number | ''>('');
  const [period, setPeriod] = useState(() => {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  });
  const [includeNotGenerated, setIncludeNotGenerated] = useState(false);
  const [report, setReport] = useState<CollectionStatusReport | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');

  useEffect(() => { buildingsApi.getAll().then(r => { setBuildings(r.data); if (r.data.length > 0) setSelectedBuilding(r.data[0].id); }); }, []);

  const loadReport = useCallback(async () => {
    if (!selectedBuilding) return;
    setLoading(true); setError('');
    try {
      const r = await reportsApi.collectionStatus(selectedBuilding as number, period, includeNotGenerated);
      setReport(r.data);
    } catch { setError(t('collection.errorLoading')); }
    finally { setLoading(false); }
  }, [selectedBuilding, period, includeNotGenerated, t]);

  useEffect(() => { if (selectedBuilding) loadReport(); }, [selectedBuilding, period, includeNotGenerated, loadReport]);

  const handleExportCsv = async () => {
    if (!selectedBuilding) return;
    try {
      const lang = localStorage.getItem('lang') || 'he';
      const r = await reportsApi.collectionStatusCsv(selectedBuilding as number, period, includeNotGenerated, lang);
      const url = window.URL.createObjectURL(new Blob([r.data]));
      const a = document.createElement('a'); a.href = url;
      a.download = `collection-status-${selectedBuilding}-${period}.csv`; a.click();
    } catch { setError(t('collection.errorExport')); }
  };

  const filteredRows = useMemo(() => {
    if (!report) return [];
    let rows = report.rows;
    if (statusFilter !== 'all') {
      rows = rows.filter(r => r.status === statusFilter);
    }
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      rows = rows.filter(r =>
        r.unitNumber.toLowerCase().includes(q) ||
        (r.payerDisplayName ?? '').toLowerCase().includes(q) ||
        (r.payerPhone ?? '').includes(q)
      );
    }
    return rows;
  }, [report, statusFilter, searchQuery]);

  const formatCurrency = (v: number) =>
    v.toLocaleString(i18n.language === 'he' ? 'he-IL' : 'en-US', { style: 'currency', currency: 'ILS', maximumFractionDigits: 0 });

  const formatDate = (d?: string) => {
    if (!d) return '—';
    return new Date(d).toLocaleDateString(i18n.language === 'he' ? 'he-IL' : 'en-US');
  };

  const s = report?.summary;

  return (
    <Box>
      <Typography variant="h4" gutterBottom sx={{ fontWeight: 700, fontSize: { xs: '1.3rem', md: '2rem' } }}>
        {t('collection.title')}
      </Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}

      {/* Filters */}
      <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap', alignItems: 'center' }}>
        <FormControl sx={{ minWidth: 200 }} size="small">
          <InputLabel>{t('collection.building')}</InputLabel>
          <Select value={selectedBuilding} label={t('collection.building')}
            onChange={e => setSelectedBuilding(e.target.value as number)}>
            {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
          </Select>
        </FormControl>
        <TextField label={t('collection.period')} type="month" value={period}
          onChange={e => setPeriod(e.target.value)} size="small" InputLabelProps={{ shrink: true }} />
        <FormControlLabel
          control={<Switch checked={includeNotGenerated} onChange={e => setIncludeNotGenerated(e.target.checked)} size="small" />}
          label={<Typography variant="body2">{t('collection.includeNotGenerated')}</Typography>}
        />
        <Button variant="outlined" startIcon={<Download />} onClick={handleExportCsv}
          disabled={!selectedBuilding} size="small">{t('app.exportCsv')}</Button>
      </Box>

      {!selectedBuilding && <Typography color="text.secondary">{t('collection.selectBuilding')}</Typography>}
      {loading && <CircularProgress sx={{ mb: 2 }} />}

      {s && !loading && (
        <>
          {/* Summary Cards */}
          <Box sx={{ display: 'flex', gap: isMobile ? 1 : 2, mb: 3, flexWrap: 'wrap' }}>
            {[
              { label: t('collection.paidUnits'), value: s.paidCount, color: '#2e7d32', bg: 'rgba(46,125,50,0.08)' },
              { label: t('collection.partialUnits'), value: s.partialCount, color: '#f5911e', bg: 'rgba(245,145,30,0.08)' },
              { label: t('collection.unpaidUnits'), value: s.unpaidCount, color: '#d32f2f', bg: 'rgba(211,47,47,0.08)' },
              { label: t('collection.overdueUnits'), value: s.overdueCount, color: '#b71c1c', bg: 'rgba(183,28,28,0.08)' },
              { label: t('collection.collectionRate'), value: `${s.collectionRatePercent}%`, color: '#1a56a0', bg: 'rgba(26,86,160,0.08)' },
            ].map((card) => (
              <Card key={card.label} sx={{ flex: isMobile ? '1 1 45%' : '1 1 0', minWidth: isMobile ? 0 : 120 }}>
                <CardContent sx={{ p: isMobile ? 1.5 : 2, '&:last-child': { pb: isMobile ? 1.5 : 2 } }}>
                  <Typography variant={isMobile ? 'h5' : 'h4'} fontWeight={700} sx={{ color: card.color }}>{card.value}</Typography>
                  <Typography variant="caption" color="text.secondary">{card.label}</Typography>
                </CardContent>
              </Card>
            ))}
          </Box>

          {/* Totals row */}
          <Box sx={{ display: 'flex', gap: 2, mb: 2, flexWrap: 'wrap' }}>
            <Typography variant="body2"><strong>{t('collection.totalDue')}:</strong> {formatCurrency(s.totalDue)}</Typography>
            <Typography variant="body2"><strong>{t('collection.totalPaid')}:</strong> {formatCurrency(s.totalPaid)}</Typography>
            <Typography variant="body2" sx={{ color: s.totalOutstanding > 0 ? 'error.main' : 'success.main' }}>
              <strong>{t('collection.outstanding')}:</strong> {formatCurrency(s.totalOutstanding)}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {t('collection.generatedOf', { generated: s.generatedCount, total: s.totalUnits })}
            </Typography>
          </Box>

          {/* Search & Status Filter */}
          <Box sx={{ display: 'flex', gap: 2, mb: 2, flexWrap: 'wrap', alignItems: 'center' }}>
            <TextField size="small" placeholder={t('collection.search')} value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              InputProps={{ startAdornment: <InputAdornment position="start"><Search /></InputAdornment> }}
              sx={{ minWidth: 220 }} />
            <FormControl size="small" sx={{ minWidth: 160 }}>
              <InputLabel>{t('collection.statusFilter')}</InputLabel>
              <Select value={statusFilter} label={t('collection.statusFilter')}
                onChange={e => setStatusFilter(e.target.value)}>
                <MenuItem value="all">{t('collection.allStatuses')}</MenuItem>
                <MenuItem value="Paid">{t('collection.statusPaid')}</MenuItem>
                <MenuItem value="Partial">{t('collection.statusPartial')}</MenuItem>
                <MenuItem value="Unpaid">{t('collection.statusUnpaid')}</MenuItem>
                <MenuItem value="Overdue">{t('collection.statusOverdue')}</MenuItem>
                {includeNotGenerated && <MenuItem value="NotGenerated">{t('collection.statusNotGenerated')}</MenuItem>}
              </Select>
            </FormControl>
          </Box>

          {/* Data */}
          {isMobile ? (
            <Stack spacing={1}>
              {filteredRows.map((r) => (
                <Card key={r.unitId} variant="outlined">
                  <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                      <Typography variant="subtitle2" fontWeight={700}>{r.unitNumber}</Typography>
                      <StatusChip status={r.status} t={t} />
                    </Box>
                    <Typography variant="body2" color="text.secondary">{r.payerDisplayName}</Typography>
                    {r.payerPhone && (
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mt: 0.3 }}>
                        <Phone sx={{ fontSize: 14, color: 'text.secondary' }} />
                        <Typography variant="caption" color="text.secondary">{r.payerPhone}</Typography>
                      </Box>
                    )}
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', mt: 1 }}>
                      <Box>
                        <Typography variant="caption" color="text.secondary">{t('collection.due')}</Typography>
                        <Typography variant="body2" fontWeight={600}>{formatCurrency(r.amountDue)}</Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" color="text.secondary">{t('collection.paid')}</Typography>
                        <Typography variant="body2" fontWeight={600} color="success.main">{formatCurrency(r.amountPaid)}</Typography>
                      </Box>
                      <Box>
                        <Typography variant="caption" color="text.secondary">{t('collection.outstanding')}</Typography>
                        <Typography variant="body2" fontWeight={600} color={r.outstanding > 0 ? 'error.main' : 'success.main'}>
                          {formatCurrency(r.outstanding)}
                        </Typography>
                      </Box>
                    </Box>
                  </CardContent>
                </Card>
              ))}
            </Stack>
          ) : (
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>{t('collection.unit')}</TableCell>
                    <TableCell>{t('collection.payer')}</TableCell>
                    <TableCell>{t('collection.phone')}</TableCell>
                    <TableCell align="right">{t('collection.due')}</TableCell>
                    <TableCell align="right">{t('collection.paid')}</TableCell>
                    <TableCell align="right">{t('collection.outstanding')}</TableCell>
                    <TableCell>{t('collection.dueDate')}</TableCell>
                    <TableCell>{t('collection.status')}</TableCell>
                    <TableCell>{t('collection.lastPayment')}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {filteredRows.map((r) => (
                    <TableRow key={r.unitId} sx={{
                      bgcolor: r.status === 'Overdue' ? 'rgba(183,28,28,0.04)' :
                        r.status === 'Unpaid' ? 'rgba(211,47,47,0.02)' : undefined
                    }}>
                      <TableCell><strong>{r.unitNumber}</strong>{r.floor != null ? ` (${t('collection.floor')} ${r.floor})` : ''}</TableCell>
                      <TableCell>{r.payerDisplayName}</TableCell>
                      <TableCell>{r.payerPhone || '—'}</TableCell>
                      <TableCell align="right">{r.amountDue > 0 ? formatCurrency(r.amountDue) : '—'}</TableCell>
                      <TableCell align="right" sx={{ color: 'success.main' }}>{r.amountPaid > 0 ? formatCurrency(r.amountPaid) : '—'}</TableCell>
                      <TableCell align="right" sx={{ color: r.outstanding > 0 ? 'error.main' : 'inherit', fontWeight: r.outstanding > 0 ? 600 : 400 }}>
                        {r.outstanding > 0 ? formatCurrency(r.outstanding) : '—'}
                      </TableCell>
                      <TableCell>{formatDate(r.dueDate)}</TableCell>
                      <TableCell><StatusChip status={r.status} t={t} /></TableCell>
                      <TableCell>{formatDate(r.lastPaymentDateUtc)}</TableCell>
                    </TableRow>
                  ))}
                  {filteredRows.length === 0 && (
                    <TableRow><TableCell colSpan={9} align="center">{t('collection.noData')}</TableCell></TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </>
      )}
    </Box>
  );
};

const StatusChip: React.FC<{ status: string; t: (key: string) => string }> = ({ status, t }) => (
  <Chip
    icon={<>{STATUS_ICONS[status]}</>}
    label={t(`collection.status${status}`)}
    size="small"
    sx={{
      bgcolor: `${STATUS_COLORS[status]}15`,
      color: STATUS_COLORS[status],
      fontWeight: 600,
      '& .MuiChip-icon': { color: STATUS_COLORS[status] }
    }}
  />
);

export default CollectionStatusPage;
