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
    { label: t('dashboard.buildings'), value: stats.buildings, icon: <Business sx={{ fontSize: 40 }} />, color: '#1976d2', path: '/buildings' },
    { label: t('dashboard.openRequests'), value: stats.openSRs, icon: <Assignment sx={{ fontSize: 40 }} />, color: '#ed6c02', path: '/service-requests' },
    { label: t('dashboard.activeWorkOrders'), value: stats.activeWOs, icon: <WorkOutline sx={{ fontSize: 40 }} />, color: '#2e7d32', path: '/work-orders' },
    { label: t('dashboard.vendors'), value: stats.vendors, icon: <Engineering sx={{ fontSize: 40 }} />, color: '#9c27b0', path: '/vendors' },
  ];

  return (
    <Box>
      <Typography variant="h4" gutterBottom sx={{ fontWeight: 700 }}>{t('dashboard.title')}</Typography>
      <Grid container spacing={3} sx={{ mb: 4 }}>
        {cards.map(card => (
          <Grid size={{ xs: 12, sm: 6, md: 3 }} key={card.label}>
            <Card sx={{ cursor: 'pointer', '&:hover': { boxShadow: 4 } }} onClick={() => navigate(card.path)}>
              <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
                <Box sx={{ color: card.color }}>{card.icon}</Box>
                <Box>
                  <Typography variant="h3" sx={{ fontWeight: 700 }}>{card.value}</Typography>
                  <Typography variant="body2" color="text.secondary">{card.label}</Typography>
                </Box>
              </CardContent>
            </Card>
          </Grid>
        ))}
      </Grid>

      <Typography variant="h6" gutterBottom>{t('dashboard.quickActions')}</Typography>
      <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap' }}>
        <Button variant="outlined" onClick={() => navigate('/service-requests')}>{t('dashboard.viewServiceRequests')}</Button>
        <Button variant="outlined" onClick={() => navigate('/work-orders')}>{t('dashboard.viewWorkOrders')}</Button>
        <Button variant="outlined" onClick={() => navigate('/cleaning-plans')}>{t('dashboard.cleaningPlans')}</Button>
        <Button variant="outlined" onClick={() => navigate('/jobs')}>{t('dashboard.runJobs')}</Button>
      </Box>
    </Box>
  );
};

export default DashboardPage;
