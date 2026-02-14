import React, { useEffect, useState } from 'react';
import {
  Grid, Card, CardContent, Typography, Box, Button, CircularProgress,
  TextField, Divider, Chip, Alert
} from '@mui/material';
import {
  Business, Assignment, WorkOutline, Engineering,
  CheckCircle, Warning, Error as ErrorIcon, TrendingUp
} from '@mui/icons-material';
import { useNavigate } from 'react-router-dom';
import { buildingsApi, serviceRequestsApi, workOrdersApi, vendorsApi, reportsApi } from '../../api/services';
import type { CollectionSummaryDto } from '../../types';
import { useTranslation } from 'react-i18next';

const DashboardPage: React.FC = () => {
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const [stats, setStats] = useState({ buildings: 0, openSRs: 0, activeWOs: 0, vendors: 0 });
  const [loading, setLoading] = useState(true);
  const [collectionPeriod, setCollectionPeriod] = useState(() => {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  });
  const [collectionData, setCollectionData] = useState<CollectionSummaryDto[]>([]);
  const [collectionLoading, setCollectionLoading] = useState(false);
  const [collectionError, setCollectionError] = useState('');

  useEffect(() => {
    Promise.all([
      buildingsApi.getAll().then(r => r.data.length),
      serviceRequestsApi.getAll().then(r => r.data.filter(sr => ['New', 'InReview', 'Approved', 'InProgress'].includes(sr.status)).length),
      workOrdersApi.getAll().then(r => r.data.filter(wo => ['Draft', 'Assigned', 'Scheduled', 'InProgress'].includes(wo.status)).length),
      vendorsApi.getAll().then(r => r.data.length),
    ]).then(([buildings, openSRs, activeWOs, vendors]) => {
      setStats({ buildings, openSRs, activeWOs, vendors });
    }).catch(() => {}).finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    setCollectionLoading(true);
    setCollectionError('');
    reportsApi.dashboardCollection(collectionPeriod)
      .then(r => setCollectionData(r.data))
      .catch((err) => {
        console.error('Dashboard collection error:', err);
        setCollectionError(err?.response?.data?.message || err?.message || 'Failed to load collection data');
      })
      .finally(() => setCollectionLoading(false));
  }, [collectionPeriod]);

  const formatCurrency = (v: number) =>
    v.toLocaleString(i18n.language === 'he' ? 'he-IL' : 'en-US', { style: 'currency', currency: 'ILS', maximumFractionDigits: 0 });

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  const cards = [
    { label: t('dashboard.buildings'), value: stats.buildings, icon: <Business sx={{ fontSize: 36 }} />, color: '#1a56a0', bgLight: 'rgba(26,86,160,0.08)', path: '/buildings' },
    { label: t('dashboard.openRequests'), value: stats.openSRs, icon: <Assignment sx={{ fontSize: 36 }} />, color: '#f5911e', bgLight: 'rgba(245,145,30,0.08)', path: '/service-requests' },
    { label: t('dashboard.activeWorkOrders'), value: stats.activeWOs, icon: <WorkOutline sx={{ fontSize: 36 }} />, color: '#2e7d32', bgLight: 'rgba(46,125,50,0.08)', path: '/work-orders' },
    { label: t('dashboard.vendors'), value: stats.vendors, icon: <Engineering sx={{ fontSize: 36 }} />, color: '#7c3aed', bgLight: 'rgba(124,58,237,0.08)', path: '/vendors' },
  ];

  // Aggregate collection summary across buildings
  const totalUnpaid = collectionData.reduce((s, c) => s + c.unpaidCount, 0);
  const totalOverdue = collectionData.reduce((s, c) => s + c.overdueCount, 0);
  const totalPaidUnits = collectionData.reduce((s, c) => s + c.paidCount, 0);
  const totalOutstanding = collectionData.reduce((s, c) => s + c.totalOutstanding, 0);
  const totalDue = collectionData.reduce((s, c) => s + c.totalDue, 0);
  const totalPaidAmt = collectionData.reduce((s, c) => s + c.totalPaid, 0);
  const overallRate = totalDue > 0 ? Math.round(totalPaidAmt / totalDue * 100 * 10) / 10 : 0;

  return (
    <Box>
      <Typography variant="h4" gutterBottom sx={{ fontWeight: 700, fontSize: { xs: '1.3rem', md: '2rem' } }}>{t('dashboard.title')}</Typography>
      <Grid container spacing={2.5} sx={{ mb: 4 }}>
        {cards.map(card => (
          <Grid size={{ xs: 6, sm: 6, md: 3 }} key={card.label}>
            <Card
              sx={{
                cursor: 'pointer',
                transition: 'all 0.2s ease',
                '&:hover': { boxShadow: '0 4px 16px rgba(0,0,0,0.12)', transform: 'translateY(-2px)' },
              }}
              onClick={() => navigate(card.path)}
            >
              <CardContent sx={{ p: { xs: 2, md: 2.5 } }}>
                <Box sx={{
                  width: 48, height: 48, borderRadius: 2, display: 'flex', alignItems: 'center', justifyContent: 'center',
                  bgcolor: card.bgLight, color: card.color, mb: 1.5,
                }}>
                  {card.icon}
                </Box>
                <Typography variant="h3" sx={{ fontWeight: 700, fontSize: { xs: '1.8rem', md: '2.2rem' }, lineHeight: 1 }}>{card.value}</Typography>
                <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>{card.label}</Typography>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      {/* Collection Status Widget */}
      <Divider sx={{ mb: 3 }} />
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h6" fontWeight={600}>{t('dashboard.collectionStatus')}</Typography>
        <TextField type="month" value={collectionPeriod} onChange={e => setCollectionPeriod(e.target.value)}
          size="small" InputLabelProps={{ shrink: true }} />
      </Box>

      {collectionError && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setCollectionError('')}>{collectionError}</Alert>}
      {collectionLoading ? <CircularProgress size={24} /> : (
        <>
          <Grid container spacing={2} sx={{ mb: 2 }}>
            <Grid size={{ xs: 6, sm: 4, md: 2.4 }}>
              <Card sx={{ borderTop: '3px solid #d32f2f' }}>
                <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.5 }}>
                    <Warning sx={{ fontSize: 18, color: '#d32f2f' }} />
                    <Typography variant="caption" color="text.secondary">{t('dashboard.unpaidUnits')}</Typography>
                  </Box>
                  <Typography variant="h5" fontWeight={700} color="error.main">{totalUnpaid}</Typography>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2.4 }}>
              <Card sx={{ borderTop: '3px solid #b71c1c' }}>
                <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.5 }}>
                    <ErrorIcon sx={{ fontSize: 18, color: '#b71c1c' }} />
                    <Typography variant="caption" color="text.secondary">{t('dashboard.overdueUnits')}</Typography>
                  </Box>
                  <Typography variant="h5" fontWeight={700} sx={{ color: '#b71c1c' }}>{totalOverdue}</Typography>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2.4 }}>
              <Card sx={{ borderTop: '3px solid #2e7d32' }}>
                <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.5 }}>
                    <CheckCircle sx={{ fontSize: 18, color: '#2e7d32' }} />
                    <Typography variant="caption" color="text.secondary">{t('dashboard.paidUnits')}</Typography>
                  </Box>
                  <Typography variant="h5" fontWeight={700} color="success.main">{totalPaidUnits}</Typography>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 6, sm: 4, md: 2.4 }}>
              <Card sx={{ borderTop: '3px solid #1a56a0' }}>
                <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5, mb: 0.5 }}>
                    <TrendingUp sx={{ fontSize: 18, color: '#1a56a0' }} />
                    <Typography variant="caption" color="text.secondary">{t('dashboard.collectionRate')}</Typography>
                  </Box>
                  <Typography variant="h5" fontWeight={700} sx={{ color: '#1a56a0' }}>{overallRate}%</Typography>
                </CardContent>
              </Card>
            </Grid>
            <Grid size={{ xs: 12, sm: 4, md: 2.4 }}>
              <Card sx={{ borderTop: `3px solid ${totalOutstanding > 0 ? '#f5911e' : '#2e7d32'}` }}>
                <CardContent sx={{ p: 1.5, '&:last-child': { pb: 1.5 } }}>
                  <Typography variant="caption" color="text.secondary">{t('dashboard.outstanding')}</Typography>
                  <Typography variant="h6" fontWeight={700} sx={{ color: totalOutstanding > 0 ? '#f5911e' : '#2e7d32' }}>
                    {formatCurrency(totalOutstanding)}
                  </Typography>
                </CardContent>
              </Card>
            </Grid>
          </Grid>

          {/* Per-building mini rows */}
          {collectionData.length > 0 && collectionData.some(c => c.generatedCount > 0) && (
            <Box sx={{ mb: 2 }}>
              {collectionData.filter(c => c.generatedCount > 0).map(c => (
                <Box key={c.buildingId} sx={{
                  display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                  py: 1, px: 1.5, borderBottom: '1px solid', borderColor: 'divider',
                  '&:hover': { bgcolor: 'action.hover' }, cursor: 'pointer', flexWrap: 'wrap', gap: 0.5
                }}
                  onClick={() => navigate(`/collection-status?building=${c.buildingId}`)}
                >
                  <Typography variant="body2" fontWeight={600}>{c.buildingName}</Typography>
                  <Box sx={{ display: 'flex', gap: 1, alignItems: 'center', flexWrap: 'wrap' }}>
                    <Chip label={`${t('dashboard.paidUnits')}: ${c.paidCount}`} size="small" color="success" variant="outlined" />
                    {c.unpaidCount > 0 && <Chip label={`${t('dashboard.unpaidUnits')}: ${c.unpaidCount}`} size="small" color="error" variant="outlined" />}
                    {c.overdueCount > 0 && <Chip label={`${t('dashboard.overdueUnits')}: ${c.overdueCount}`} size="small" sx={{ borderColor: '#b71c1c', color: '#b71c1c' }} variant="outlined" />}
                    <Typography variant="caption" color="text.secondary">{c.collectionRatePercent}%</Typography>
                  </Box>
                </Box>
              ))}
            </Box>
          )}

          <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
            <Button variant="outlined" size="small" onClick={() => navigate('/collection-status')}>
              {t('dashboard.viewCollectionStatus')}
            </Button>
          </Box>
        </>
      )}

      <Divider sx={{ my: 3 }} />
      <Typography variant="h6" gutterBottom>{t('dashboard.quickActions')}</Typography>
      <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
        <Button variant="outlined" onClick={() => navigate('/service-requests')}>{t('dashboard.viewServiceRequests')}</Button>
        <Button variant="outlined" onClick={() => navigate('/work-orders')}>{t('dashboard.viewWorkOrders')}</Button>
        <Button variant="outlined" onClick={() => navigate('/cleaning-plans')}>{t('dashboard.cleaningPlans')}</Button>
        <Button variant="outlined" onClick={() => navigate('/jobs')}>{t('dashboard.runJobs')}</Button>
      </Box>
    </Box>
  );
};

export default DashboardPage;
