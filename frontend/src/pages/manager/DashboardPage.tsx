import React, { useEffect, useState } from 'react';
import { Grid, Card, CardContent, Typography, Box, Button, CircularProgress } from '@mui/material';
import { Business, Assignment, WorkOutline, Engineering } from '@mui/icons-material';
import { useNavigate } from 'react-router-dom';
import { buildingsApi, serviceRequestsApi, workOrdersApi, vendorsApi } from '../../api/services';
import { useTranslation } from 'react-i18next';

const DashboardPage: React.FC = () => {
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [stats, setStats] = useState({ buildings: 0, openSRs: 0, activeWOs: 0, vendors: 0 });
  const [loading, setLoading] = useState(true);

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

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  const cards = [
    { label: t('dashboard.buildings'), value: stats.buildings, icon: <Business sx={{ fontSize: 36 }} />, color: '#1a56a0', bgLight: 'rgba(26,86,160,0.08)', path: '/buildings' },
    { label: t('dashboard.openRequests'), value: stats.openSRs, icon: <Assignment sx={{ fontSize: 36 }} />, color: '#f5911e', bgLight: 'rgba(245,145,30,0.08)', path: '/service-requests' },
    { label: t('dashboard.activeWorkOrders'), value: stats.activeWOs, icon: <WorkOutline sx={{ fontSize: 36 }} />, color: '#2e7d32', bgLight: 'rgba(46,125,50,0.08)', path: '/work-orders' },
    { label: t('dashboard.vendors'), value: stats.vendors, icon: <Engineering sx={{ fontSize: 36 }} />, color: '#7c3aed', bgLight: 'rgba(124,58,237,0.08)', path: '/vendors' },
  ];

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
