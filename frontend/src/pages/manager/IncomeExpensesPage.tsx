import React, { useState, useEffect, useCallback } from 'react';
import {
  Typography, Box, Card, CardContent, TextField, MenuItem, Button, Alert,
  CircularProgress, Table, TableBody, TableCell, TableContainer, TableHead,
  TableRow, Paper, Stack, useMediaQuery, useTheme, FormControl, InputLabel, Select
} from '@mui/material';
import { Download, TrendingUp, TrendingDown, AccountBalance } from '@mui/icons-material';
import {
  PieChart, Pie, Cell, ResponsiveContainer, BarChart, Bar, XAxis, YAxis,
  CartesianGrid, Tooltip, Legend
} from 'recharts';
import { buildingsApi, reportsApi } from '../../api/services';
import type { BuildingDto, IncomeExpensesReport } from '../../types';
import { useTranslation } from 'react-i18next';

const INCOME_COLORS = ['#1a56a0', '#2d6fbe', '#4a8fd4', '#7bb3e8'];
const EXPENSE_COLORS = ['#f5911e', '#e07316', '#c45a10', '#d4763a', '#b8520e', '#e89850', '#cd6d1f', '#f0a94e', '#a34d0a', '#d88a3c', '#c0731e', '#cc8040'];

const IncomeExpensesPage: React.FC = () => {
  const { t, i18n } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));

  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [selectedBuilding, setSelectedBuilding] = useState<number | ''>('');
  const [fromDate, setFromDate] = useState(() => {
    const d = new Date(); d.setMonth(d.getMonth() - 12);
    return d.toISOString().slice(0, 10);
  });
  const [toDate, setToDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<IncomeExpensesReport | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => { buildingsApi.getAll().then(r => { setBuildings(r.data); if (r.data.length > 0) setSelectedBuilding(r.data[0].id); }); }, []);

  const loadReport = useCallback(async () => {
    if (!selectedBuilding) return;
    setLoading(true); setError('');
    try {
      const r = await reportsApi.incomeExpenses(selectedBuilding as number, fromDate, toDate);
      setReport(r.data);
    } catch { setError(t('incomeExpenses.errorLoading')); }
    finally { setLoading(false); }
  }, [selectedBuilding, fromDate, toDate, t]);

  useEffect(() => { if (selectedBuilding) loadReport(); }, [selectedBuilding, loadReport]);

  const handleExportCsv = async () => {
    if (!selectedBuilding) return;
    try {
      const lang = localStorage.getItem('lang') || 'he';
      const r = await reportsApi.incomeExpensesCsv(selectedBuilding as number, fromDate, toDate, lang);
      const url = window.URL.createObjectURL(new Blob([r.data]));
      const a = document.createElement('a'); a.href = url;
      a.download = `income-expenses-${selectedBuilding}.csv`; a.click();
    } catch { setError(t('incomeExpenses.errorExport')); }
  };

  const translateCategory = (cat: string) => t(`enums.finCategory.${cat}`, cat);

  // Combine income + expense by category for table
  const allCategories = [
    ...(report?.incomeByCategory || []).map(c => ({ ...c, type: 'income' as const })),
    ...(report?.expensesByCategory || []).map(c => ({ ...c, type: 'expense' as const })),
  ];

  return (
    <Box>
      <Typography variant="h4" gutterBottom sx={{ fontWeight: 700, fontSize: { xs: '1.3rem', md: '2rem' } }}>
        {t('incomeExpenses.title')}
      </Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}

      {/* Filters */}
      <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap', alignItems: 'center' }}>
        <FormControl sx={{ minWidth: 200 }} size="small">
          <InputLabel>{t('incomeExpenses.building')}</InputLabel>
          <Select value={selectedBuilding} label={t('incomeExpenses.building')}
            onChange={e => setSelectedBuilding(e.target.value as number)}>
            {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
          </Select>
        </FormControl>
        <TextField label={t('incomeExpenses.from')} type="date" value={fromDate}
          onChange={e => setFromDate(e.target.value)} size="small" InputLabelProps={{ shrink: true }} />
        <TextField label={t('incomeExpenses.to')} type="date" value={toDate}
          onChange={e => setToDate(e.target.value)} size="small" InputLabelProps={{ shrink: true }} />
        <Button variant="outlined" startIcon={<Download />} onClick={handleExportCsv}
          disabled={!selectedBuilding} size="small">{t('app.exportCsv')}</Button>
      </Box>

      {!selectedBuilding && <Typography color="text.secondary">{t('incomeExpenses.selectBuilding')}</Typography>}
      {loading && <CircularProgress sx={{ mb: 2 }} />}

      {report && !loading && (
        <>
          {/* Summary Cards */}
          <Box sx={{ display: 'flex', gap: isMobile ? 1 : 2, mb: 3, flexWrap: 'wrap' }}>
            <Card sx={{
              flex: isMobile ? '1 1 100%' : '1 1 0',
              background: 'linear-gradient(135deg, #1a56a0 0%, #2d6fbe 100%)', color: '#fff',
            }}>
              <CardContent sx={{ p: isMobile ? 2 : 2.5 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
                  <TrendingUp /> <Typography variant="body2" sx={{ opacity: 0.9 }}>{t('incomeExpenses.totalIncome')}</Typography>
                </Box>
                <Typography variant={isMobile ? 'h5' : 'h4'} fontWeight={700}>
                  {report.totalIncome.toLocaleString(i18n.language === 'he' ? 'he-IL' : 'en-US', { style: 'currency', currency: 'ILS', maximumFractionDigits: 0 })}
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{
              flex: isMobile ? '1 1 100%' : '1 1 0',
              background: 'linear-gradient(135deg, #f5911e 0%, #e07316 100%)', color: '#fff',
            }}>
              <CardContent sx={{ p: isMobile ? 2 : 2.5 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
                  <TrendingDown /> <Typography variant="body2" sx={{ opacity: 0.9 }}>{t('incomeExpenses.totalExpenses')}</Typography>
                </Box>
                <Typography variant={isMobile ? 'h5' : 'h4'} fontWeight={700}>
                  {report.totalExpenses.toLocaleString(i18n.language === 'he' ? 'he-IL' : 'en-US', { style: 'currency', currency: 'ILS', maximumFractionDigits: 0 })}
                </Typography>
              </CardContent>
            </Card>
            <Card sx={{
              flex: isMobile ? '1 1 100%' : '1 1 0',
              background: report.netBalance >= 0
                ? 'linear-gradient(135deg, #2e7d32 0%, #43a047 100%)'
                : 'linear-gradient(135deg, #c62828 0%, #e53935 100%)',
              color: '#fff',
            }}>
              <CardContent sx={{ p: isMobile ? 2 : 2.5 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
                  <AccountBalance /> <Typography variant="body2" sx={{ opacity: 0.9 }}>{t('incomeExpenses.netBalance')}</Typography>
                </Box>
                <Typography variant={isMobile ? 'h5' : 'h4'} fontWeight={700}>
                  {report.netBalance.toLocaleString(i18n.language === 'he' ? 'he-IL' : 'en-US', { style: 'currency', currency: 'ILS', maximumFractionDigits: 0 })}
                </Typography>
              </CardContent>
            </Card>
          </Box>

          {/* Charts Row */}
          <Box sx={{ display: 'flex', gap: 2, mb: 3, flexWrap: 'wrap' }}>
            {/* Income Pie */}
            {report.incomeByCategory.length > 0 && (
              <Card sx={{ flex: isMobile ? '1 1 100%' : '1 1 0', minWidth: 0 }}>
                <CardContent>
                  <Typography variant="subtitle1" fontWeight={600} gutterBottom>{t('incomeExpenses.incomeByCategory')}</Typography>
                  <ResponsiveContainer width="100%" height={220}>
                    <PieChart>
                      <Pie data={report.incomeByCategory.map(c => ({ name: translateCategory(c.category), value: c.amount }))}
                        cx="50%" cy="50%" outerRadius={80} dataKey="value" label={({ name, percent }: { name?: string; percent?: number }) => `${name ?? ''} ${((percent ?? 0) * 100).toFixed(0)}%`}
                        labelLine={false} fontSize={11}>
                        {report.incomeByCategory.map((_, i) => <Cell key={i} fill={INCOME_COLORS[i % INCOME_COLORS.length]} />)}
                      </Pie>
                      <Tooltip formatter={(v: number | undefined) => (v ?? 0).toFixed(2)} />
                    </PieChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            )}

            {/* Expenses Pie */}
            {report.expensesByCategory.length > 0 && (
              <Card sx={{ flex: isMobile ? '1 1 100%' : '1 1 0', minWidth: 0 }}>
                <CardContent>
                  <Typography variant="subtitle1" fontWeight={600} gutterBottom>{t('incomeExpenses.expensesByCategory')}</Typography>
                  <ResponsiveContainer width="100%" height={220}>
                    <PieChart>
                      <Pie data={report.expensesByCategory.map(c => ({ name: translateCategory(c.category), value: c.amount }))}
                        cx="50%" cy="50%" outerRadius={80} dataKey="value" label={({ name, percent }: { name?: string; percent?: number }) => `${name ?? ''} ${((percent ?? 0) * 100).toFixed(0)}%`}
                        labelLine={false} fontSize={11}>
                        {report.expensesByCategory.map((_, i) => <Cell key={i} fill={EXPENSE_COLORS[i % EXPENSE_COLORS.length]} />)}
                      </Pie>
                      <Tooltip formatter={(v: number | undefined) => (v ?? 0).toFixed(2)} />
                    </PieChart>
                  </ResponsiveContainer>
                </CardContent>
              </Card>
            )}
          </Box>

          {/* Monthly Bar Chart */}
          {report.monthlyBreakdown.length > 0 && (
            <Card sx={{ mb: 3 }}>
              <CardContent>
                <Typography variant="subtitle1" fontWeight={600} gutterBottom>{t('incomeExpenses.monthlyBreakdown')}</Typography>
                <ResponsiveContainer width="100%" height={300}>
                  <BarChart data={report.monthlyBreakdown}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="month" fontSize={12} />
                    <YAxis fontSize={12} />
                    <Tooltip formatter={(v: number | undefined) => (v ?? 0).toFixed(2)} />
                    <Legend />
                    <Bar dataKey="income" name={t('incomeExpenses.income')} fill="#1a56a0" radius={[4, 4, 0, 0]} />
                    <Bar dataKey="expenses" name={t('incomeExpenses.expenses')} fill="#f5911e" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </CardContent>
            </Card>
          )}

          {/* Category Breakdown Table */}
          <Card>
            <CardContent>
              <Typography variant="subtitle1" fontWeight={600} gutterBottom>{t('incomeExpenses.categoryBreakdown')}</Typography>
              {isMobile ? (
                <Stack spacing={1}>
                  {allCategories.map((c, i) => (
                    <Card key={i} variant="outlined">
                      <CardContent sx={{ py: 1, px: 2, '&:last-child': { pb: 1 } }}>
                        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                          <Typography variant="body2" fontWeight={600}>{translateCategory(c.category)}</Typography>
                          <Typography variant="body2" fontWeight={600}
                            color={c.type === 'income' ? 'primary.main' : 'secondary.main'}>
                            {c.type === 'income' ? '+' : '-'}{c.amount.toFixed(2)}
                          </Typography>
                        </Box>
                        <Typography variant="caption" color="text.secondary">
                          {c.type === 'income' ? t('incomeExpenses.income') : t('incomeExpenses.expenses')}
                        </Typography>
                      </CardContent>
                    </Card>
                  ))}
                </Stack>
              ) : (
                <TableContainer component={Paper} variant="outlined">
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell>{t('incomeExpenses.type')}</TableCell>
                        <TableCell>{t('incomeExpenses.category')}</TableCell>
                        <TableCell align="right">{t('incomeExpenses.amount')}</TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {report.incomeByCategory.map((c, i) => (
                        <TableRow key={`inc-${i}`}>
                          <TableCell>{t('incomeExpenses.income')}</TableCell>
                          <TableCell>{translateCategory(c.category)}</TableCell>
                          <TableCell align="right" sx={{ color: 'primary.main', fontWeight: 600 }}>+{c.amount.toFixed(2)}</TableCell>
                        </TableRow>
                      ))}
                      {report.expensesByCategory.map((c, i) => (
                        <TableRow key={`exp-${i}`}>
                          <TableCell>{t('incomeExpenses.expenses')}</TableCell>
                          <TableCell>{translateCategory(c.category)}</TableCell>
                          <TableCell align="right" sx={{ color: 'secondary.main', fontWeight: 600 }}>-{c.amount.toFixed(2)}</TableCell>
                        </TableRow>
                      ))}
                      <TableRow sx={{ bgcolor: 'action.hover' }}>
                        <TableCell colSpan={2}><strong>{t('incomeExpenses.netBalance')}</strong></TableCell>
                        <TableCell align="right" sx={{ fontWeight: 700, color: report.netBalance >= 0 ? 'success.main' : 'error.main' }}>
                          {report.netBalance.toFixed(2)}
                        </TableCell>
                      </TableRow>
                    </TableBody>
                  </Table>
                </TableContainer>
              )}
            </CardContent>
          </Card>
        </>
      )}
    </Box>
  );
};

export default IncomeExpensesPage;
