import React, { useEffect, useState } from 'react';
import {
  Box, Typography, Button, TextField, MenuItem, Paper, Alert, CircularProgress, Card, CardContent, Grid
} from '@mui/material';
import { PlayArrow, Save } from '@mui/icons-material';
import { cleaningPlansApi, buildingsApi, vendorsApi } from '../../api/services';
import type { CleaningPlanDto, BuildingDto, VendorDto } from '../../types';
import { useTranslation } from 'react-i18next';

const CleaningPlansPage: React.FC = () => {
  const { t } = useTranslation();
  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [vendors, setVendors] = useState<VendorDto[]>([]);
  const [selectedBuilding, setSelectedBuilding] = useState<number | ''>('');
  const [plan, setPlan] = useState<CleaningPlanDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [form, setForm] = useState({ cleaningVendorId: '', stairwellsPerWeek: 3, parkingPerWeek: 1, corridorLobbyPerWeek: 3, garbageRoomPerWeek: 3, effectiveFrom: new Date().toISOString().split('T')[0] });

  useEffect(() => {
    Promise.all([buildingsApi.getAll(), vendorsApi.getAll()])
      .then(([b, v]) => { setBuildings(b.data); setVendors(v.data.filter(vd => vd.serviceType === 'Cleaning' || vd.serviceType === 'General')); })
      .catch(() => setError(t('cleaning.failedLoad')))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (!selectedBuilding) { setPlan(null); return; }
    cleaningPlansApi.getByBuilding(selectedBuilding).then(r => {
      setPlan(r.data);
      if (r.data) {
        setForm({
          cleaningVendorId: String(r.data.cleaningVendorId),
          stairwellsPerWeek: r.data.stairwellsPerWeek,
          parkingPerWeek: r.data.parkingPerWeek,
          corridorLobbyPerWeek: r.data.corridorLobbyPerWeek,
          garbageRoomPerWeek: r.data.garbageRoomPerWeek,
          effectiveFrom: r.data.effectiveFrom?.split('T')[0] || new Date().toISOString().split('T')[0],
        });
      }
    }).catch(() => {});
  }, [selectedBuilding]);

  const handleSave = async () => {
    if (!selectedBuilding || !form.cleaningVendorId) return;
    try {
      await cleaningPlansApi.create(selectedBuilding, { ...form, cleaningVendorId: Number(form.cleaningVendorId) });
      setSuccess(t('cleaning.saved'));
      const r = await cleaningPlansApi.getByBuilding(selectedBuilding);
      setPlan(r.data);
    } catch { setError(t('cleaning.failedSave')); }
  };

  const handleGenerate = async () => {
    if (!selectedBuilding) return;
    try { const r = await cleaningPlansApi.generateWeekly(selectedBuilding); setSuccess(r.data.message); } catch { setError(t('cleaning.failedGenerate')); }
  };

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  return (
    <Box>
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 3 }}>{t('cleaning.title')}</Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}
      {success && <Alert severity="success" sx={{ mb: 2 }} onClose={() => setSuccess('')}>{success}</Alert>}

      <TextField select label={t('cleaning.selectBuilding')} value={selectedBuilding} onChange={e => setSelectedBuilding(Number(e.target.value))} sx={{ mb: 3, minWidth: 300 }}>
        {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
      </TextField>

      {selectedBuilding && (
        <Card sx={{ maxWidth: 600 }}>
          <CardContent>
            <Typography variant="h6" gutterBottom>{t('cleaning.scheduleFor', { name: buildings.find(b => b.id === selectedBuilding)?.name })}</Typography>
            <TextField fullWidth select label={t('cleaning.cleaningVendor')} value={form.cleaningVendorId} onChange={e => setForm({ ...form, cleaningVendorId: e.target.value })} sx={{ mb: 2 }}>
              {vendors.map(v => <MenuItem key={v.id} value={v.id}>{v.name}</MenuItem>)}
            </TextField>
            <Grid container spacing={2}>
              <Grid size={{ xs: 6 }}><TextField fullWidth type="number" label={t('cleaning.stairwells')} value={form.stairwellsPerWeek} onChange={e => setForm({ ...form, stairwellsPerWeek: Number(e.target.value) })} inputProps={{ min: 0, max: 7 }} /></Grid>
              <Grid size={{ xs: 6 }}><TextField fullWidth type="number" label={t('cleaning.parking')} value={form.parkingPerWeek} onChange={e => setForm({ ...form, parkingPerWeek: Number(e.target.value) })} inputProps={{ min: 0, max: 7 }} /></Grid>
              <Grid size={{ xs: 6 }}><TextField fullWidth type="number" label={t('cleaning.corridor')} value={form.corridorLobbyPerWeek} onChange={e => setForm({ ...form, corridorLobbyPerWeek: Number(e.target.value) })} inputProps={{ min: 0, max: 7 }} /></Grid>
              <Grid size={{ xs: 6 }}><TextField fullWidth type="number" label={t('cleaning.garbage')} value={form.garbageRoomPerWeek} onChange={e => setForm({ ...form, garbageRoomPerWeek: Number(e.target.value) })} inputProps={{ min: 0, max: 7 }} /></Grid>
            </Grid>
            <TextField fullWidth type="date" label={t('cleaning.effectiveFrom')} value={form.effectiveFrom} onChange={e => setForm({ ...form, effectiveFrom: e.target.value })} InputLabelProps={{ shrink: true }} sx={{ mt: 2, mb: 2 }} />
            <Box sx={{ display: 'flex', gap: 2 }}>
              <Button variant="contained" startIcon={<Save />} onClick={handleSave}>{t('cleaning.savePlan')}</Button>
              <Button variant="outlined" color="warning" startIcon={<PlayArrow />} onClick={handleGenerate} disabled={!plan}>{t('cleaning.generateWeekly')}</Button>
            </Box>
          </CardContent>
        </Card>
      )}
    </Box>
  );
};

export default CleaningPlansPage;
