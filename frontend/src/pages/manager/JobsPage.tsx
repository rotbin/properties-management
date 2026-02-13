import React, { useEffect, useState } from 'react';
import {
  Box, Typography, Button, Table, TableBody, TableCell, TableContainer, TableHead,
  TableRow, Paper, Alert, CircularProgress, Card, CardContent
} from '@mui/material';
import { PlayArrow } from '@mui/icons-material';
import { jobsApi } from '../../api/services';
import type { JobRunLogDto } from '../../types';
import { formatDateLocal } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';

const JobsPage: React.FC = () => {
  const { t } = useTranslation();
  const [logs, setLogs] = useState<JobRunLogDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const loadLogs = async () => {
    try { setLoading(true); const r = await jobsApi.getLogs(); setLogs(r.data); }
    catch { setError(t('jobs.failedLoad')); }
    finally { setLoading(false); }
  };

  useEffect(() => { loadLogs(); }, []);

  const handleGeneratePreventive = async () => {
    try { const r = await jobsApi.generatePreventive(); setSuccess(r.data.message); loadLogs(); }
    catch { setError(t('jobs.failedPreventive')); }
  };

  const handleGenerateCleaning = async () => {
    try { const r = await jobsApi.generateCleaningWeek(); setSuccess(r.data.message); loadLogs(); }
    catch { setError(t('jobs.failedCleaning')); }
  };

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  return (
    <Box>
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 3 }}>{t('jobs.title')}</Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}
      {success && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccess('')}>{success}</Alert>}

      <Card sx={{ mb: 4 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom>{t('jobs.runGenerators')}</Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            {t('jobs.generatorsDesc')}
          </Typography>
          <Box sx={{ display: 'flex', gap: 2 }}>
            <Button variant="contained" color="warning" startIcon={<PlayArrow />} onClick={handleGeneratePreventive}>
              {t('jobs.generatePreventive')}
            </Button>
            <Button variant="contained" color="info" startIcon={<PlayArrow />} onClick={handleGenerateCleaning}>
              {t('jobs.generateCleaning')}
            </Button>
          </Box>
        </CardContent>
      </Card>

      <Typography variant="h6" sx={{ mb: 2 }}>{t('jobs.history')}</Typography>
      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead><TableRow>
            <TableCell>{t('jobs.id')}</TableCell><TableCell>{t('jobs.jobName')}</TableCell><TableCell>{t('jobs.periodKey')}</TableCell><TableCell>{t('jobs.ranAt')}</TableCell>
          </TableRow></TableHead>
          <TableBody>
            {logs.map(l => (
              <TableRow key={l.id}>
                <TableCell>{l.id}</TableCell><TableCell>{l.jobName}</TableCell>
                <TableCell>{l.periodKey}</TableCell><TableCell>{formatDateLocal(l.ranAtUtc)}</TableCell>
              </TableRow>
            ))}
            {logs.length === 0 && <TableRow><TableCell colSpan={4} align="center">{t('jobs.noRuns')}</TableCell></TableRow>}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
};

export default JobsPage;
