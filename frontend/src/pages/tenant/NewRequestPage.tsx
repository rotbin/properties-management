import React, { useEffect, useState, useRef } from 'react';
import {
  Box, Typography, Button, TextField, MenuItem, FormControlLabel, Switch,
  Alert, CircularProgress, Card, CardContent, ImageList, ImageListItem, IconButton
} from '@mui/material';
import { Delete, CloudUpload } from '@mui/icons-material';
import { useNavigate } from 'react-router-dom';
import { serviceRequestsApi, buildingsApi, tenantsApi } from '../../api/services';
import { useAuth } from '../../auth/AuthContext';
import type { BuildingDto, UnitDto } from '../../types';
import { AREAS, CATEGORIES, PRIORITIES } from '../../types';
import { useTranslation } from 'react-i18next';

const MAX_FILES = 5;
const MAX_FILE_SIZE = 10 * 1024 * 1024;
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

const NewRequestPage: React.FC = () => {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [units, setUnits] = useState<UnitDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [files, setFiles] = useState<File[]>([]);
  const [previews, setPreviews] = useState<string[]>([]);
  const [form, setForm] = useState({ buildingId: '' as any, unitId: '' as any, phone: '', area: 'Other', category: 'General', priority: 'Medium', isEmergency: false, description: '' });

  useEffect(() => {
    const init = async () => {
      try {
        // Load buildings (API already filters for tenant's buildings)
        const bRes = await buildingsApi.getAll();
        setBuildings(bRes.data);
        if (bRes.data.length === 1) setForm(f => ({ ...f, buildingId: bRes.data[0].id }));

        // Load tenant profile to get phone
        try {
          const profile = await tenantsApi.getMyProfile();
          if (profile.data.phone) setForm(f => ({ ...f, phone: f.phone || profile.data.phone || '' }));
        } catch {
          // Fallback to user phone from auth
          if (user?.phone) setForm(f => ({ ...f, phone: f.phone || user.phone || '' }));
        }
      } catch {
        setError(t('newRequest.failedLoadBuildings'));
      } finally {
        setLoading(false);
      }
    };
    init();
  }, []);

  useEffect(() => {
    if (form.buildingId) {
      buildingsApi.getUnits(Number(form.buildingId)).then(r => { setUnits(r.data); const myUnit = r.data.find(u => u.tenantUserId === user?.id); if (myUnit) setForm(f => ({ ...f, unitId: myUnit.id })); });
    }
  }, [form.buildingId, user]);

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = Array.from(e.target.files || []);
    const validFiles: File[] = [];
    for (const file of selected) {
      if (files.length + validFiles.length >= MAX_FILES) { setError(t('newRequest.maxFiles', { max: MAX_FILES })); break; }
      if (file.size > MAX_FILE_SIZE) { setError(t('newRequest.fileTooBig', { name: file.name })); continue; }
      if (!ALLOWED_TYPES.includes(file.type)) { setError(t('newRequest.invalidType', { name: file.name })); continue; }
      validFiles.push(file);
    }
    const newFiles = [...files, ...validFiles]; setFiles(newFiles); setPreviews(newFiles.map(f => URL.createObjectURL(f)));
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const removeFile = (index: number) => { const newFiles = files.filter((_, i) => i !== index); setFiles(newFiles); setPreviews(newFiles.map(f => URL.createObjectURL(f))); };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.buildingId || !form.description.trim()) { setError(t('newRequest.requiredFields')); return; }
    if (!form.phone.trim()) { setError(t('newRequest.phoneRequired')); return; }
    setSubmitting(true); setError('');
    try {
      const sr = await serviceRequestsApi.create({ buildingId: Number(form.buildingId), unitId: form.unitId ? Number(form.unitId) : undefined, phone: form.phone, area: form.area, category: form.category, priority: form.priority, isEmergency: form.isEmergency, description: form.description });
      if (files.length > 0) await serviceRequestsApi.uploadAttachments(sr.data.id, files);
      navigate('/my-requests');
    } catch (err: any) { setError(err.response?.data?.message || t('newRequest.failedSubmit')); } finally { setSubmitting(false); }
  };

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  return (
    <Box sx={{ maxWidth: 700, mx: 'auto' }}>
      <Typography variant="h4" sx={{ fontWeight: 700, mb: 3 }}>{t('newRequest.title')}</Typography>
      {error && <Alert severity="error" sx={{ mb: 2 }} onClose={() => setError('')}>{error}</Alert>}
      <Card><CardContent>
        <form onSubmit={handleSubmit}>
          <TextField fullWidth select label={t('newRequest.building')} value={form.buildingId} onChange={e => setForm({ ...form, buildingId: e.target.value, unitId: '' })} required sx={{ mb: 2 }}>
            {buildings.map(b => <MenuItem key={b.id} value={b.id}>{b.name}</MenuItem>)}
          </TextField>
          {units.length > 0 && (
            <TextField fullWidth select label={t('newRequest.unit')} value={form.unitId} onChange={e => setForm({ ...form, unitId: e.target.value })} sx={{ mb: 2 }}>
              <MenuItem value="">{t('newRequest.noUnit')}</MenuItem>
              {units.map(u => <MenuItem key={u.id} value={u.id}>{u.unitNumber} ({t('newRequest.unitFloor', { floor: u.floor })})</MenuItem>)}
            </TextField>
          )}
          <TextField fullWidth label={t('newRequest.phone')} value={form.phone} onChange={e => setForm({ ...form, phone: e.target.value })} required
            error={!form.phone.trim()} helperText={!form.phone.trim() ? t('newRequest.phoneRequired') : ''} sx={{ mb: 2 }} />
          <Box sx={{ display: 'flex', gap: 2, mb: 2 }}>
            <TextField fullWidth select label={t('newRequest.area')} value={form.area} onChange={e => setForm({ ...form, area: e.target.value })}>
              {AREAS.map(a => <MenuItem key={a} value={a}>{t(`enums.area.${a}`, a)}</MenuItem>)}
            </TextField>
            <TextField fullWidth select label={t('newRequest.category')} value={form.category} onChange={e => setForm({ ...form, category: e.target.value })}>
              {CATEGORIES.map(c => <MenuItem key={c} value={c}>{t(`enums.category.${c}`, c)}</MenuItem>)}
            </TextField>
          </Box>
          <Box sx={{ display: 'flex', gap: 2, mb: 2, alignItems: 'center' }}>
            <TextField fullWidth select label={t('newRequest.priority')} value={form.priority} onChange={e => setForm({ ...form, priority: e.target.value })}>
              {PRIORITIES.map(p => <MenuItem key={p} value={p}>{t(`enums.priority.${p}`, p)}</MenuItem>)}
            </TextField>
            <FormControlLabel control={<Switch checked={form.isEmergency} onChange={e => setForm({ ...form, isEmergency: e.target.checked })} color="error" />} label={t('newRequest.emergency')} />
          </Box>
          <TextField fullWidth multiline rows={4} label={t('newRequest.description')} value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} required sx={{ mb: 2 }} placeholder={t('newRequest.descPlaceholder')} />

          <Box sx={{ mb: 2 }}>
            <Typography variant="subtitle2" sx={{ mb: 1 }}>{t('newRequest.photos', { max: MAX_FILES })}</Typography>
            <input ref={fileInputRef} type="file" accept="image/jpeg,image/png,image/webp" multiple onChange={handleFileSelect} style={{ display: 'none' }} />
            <Button variant="outlined" startIcon={<CloudUpload />} onClick={() => fileInputRef.current?.click()} disabled={files.length >= MAX_FILES}>
              {t('newRequest.addPhotos', { count: files.length, max: MAX_FILES })}
            </Button>
            {previews.length > 0 && (
              <ImageList cols={3} rowHeight={120} sx={{ mt: 1 }}>
                {previews.map((src, i) => (
                  <ImageListItem key={i} sx={{ position: 'relative' }}>
                    <img src={src} alt={`Preview ${i + 1}`} style={{ objectFit: 'cover', height: 120, borderRadius: 4 }} />
                    <IconButton size="small" onClick={() => removeFile(i)} sx={{ position: 'absolute', top: 2, right: 2, bgcolor: 'rgba(255,255,255,0.8)' }}><Delete fontSize="small" /></IconButton>
                  </ImageListItem>
                ))}
              </ImageList>
            )}
          </Box>

          <Button fullWidth variant="contained" size="large" type="submit" disabled={submitting}>
            {submitting ? <CircularProgress size={24} /> : t('newRequest.submit')}
          </Button>
        </form>
      </CardContent></Card>
    </Box>
  );
};

export default NewRequestPage;
