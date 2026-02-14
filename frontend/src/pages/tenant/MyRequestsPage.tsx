import React, { useEffect, useState } from 'react';
import {
  Box, Typography, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, Chip, Button, Dialog, DialogTitle, DialogContent, DialogActions, CircularProgress
} from '@mui/material';
import { Visibility, Add } from '@mui/icons-material';
import { useNavigate } from 'react-router-dom';
import { serviceRequestsApi } from '../../api/services';
import type { ServiceRequestDto } from '../../types';
import { formatDateLocal } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';

const priorityColor = (p: string) => p === 'Critical' ? 'error' : p === 'High' ? 'warning' : p === 'Medium' ? 'info' : 'default';
const statusColor = (s: string) => s === 'New' ? 'info' : s === 'Resolved' ? 'success' : s === 'Rejected' ? 'error' : 'default';

const MyRequestsPage: React.FC = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [requests, setRequests] = useState<ServiceRequestDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<ServiceRequestDto | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);

  useEffect(() => { serviceRequestsApi.getMy().then(r => setRequests(r.data)).catch(() => {}).finally(() => setLoading(false)); }, []);

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4" sx={{ fontWeight: 700 }}>{t('myRequests.title')}</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={() => navigate('/new-request')}>{t('myRequests.newRequest')}</Button>
      </Box>
      <TableContainer component={Paper}>
        <Table>
          <TableHead><TableRow>
            <TableCell>{t('myRequests.id')}</TableCell><TableCell>{t('myRequests.building')}</TableCell><TableCell>{t('myRequests.area')}</TableCell>
            <TableCell>{t('myRequests.priority')}</TableCell><TableCell>{t('myRequests.status')}</TableCell><TableCell>{t('myRequests.date')}</TableCell><TableCell>{t('app.actions')}</TableCell>
          </TableRow></TableHead>
          <TableBody>
            {requests.map(sr => (
              <TableRow key={sr.id} hover>
                <TableCell>{sr.id}</TableCell><TableCell>{sr.buildingName}</TableCell>
                <TableCell>{t(`enums.area.${sr.area}`, sr.area)}</TableCell>
                <TableCell><Chip label={t(`enums.priority.${sr.priority}`, sr.priority)} size="small" color={priorityColor(sr.priority) as any} /></TableCell>
                <TableCell><Chip label={t(`enums.srStatus.${sr.status}`, sr.status)} size="small" color={statusColor(sr.status) as any} /></TableCell>
                <TableCell>{formatDateLocal(sr.createdAtUtc)}</TableCell>
                <TableCell><Button size="small" startIcon={<Visibility />} onClick={() => { setSelected(sr); setDetailOpen(true); }}>{t('app.view')}</Button></TableCell>
              </TableRow>
            ))}
            {requests.length === 0 && (
              <TableRow><TableCell colSpan={7} align="center"><Typography color="text.secondary" sx={{ py: 2 }}>{t('myRequests.noRequests')}</Typography></TableCell></TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Dialog open={detailOpen} onClose={() => setDetailOpen(false)} maxWidth="md" fullWidth>
        {selected && (<>
          <DialogTitle>
            {t('myRequests.detailTitle', { id: selected.id })}
            {selected.isEmergency && <Chip label={t('serviceRequests.emergency')} color="error" size="small" sx={{ ml: 1 }} />}
          </DialogTitle>
          <DialogContent>
            <Box sx={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 2, mb: 2 }}>
              <Typography><strong>{t('myRequests.building')}:</strong> {selected.buildingName}</Typography>
              <Typography><strong>{t('myRequests.unit')}:</strong> {selected.unitNumber || t('app.na')}</Typography>
              <Typography><strong>{t('myRequests.area')}:</strong> {t(`enums.area.${selected.area}`, selected.area)}</Typography>
              <Typography><strong>{t('myRequests.category')}:</strong> {t(`enums.category.${selected.category}`, selected.category)}</Typography>
              <Typography><strong>{t('myRequests.priority')}:</strong> {t(`enums.priority.${selected.priority}`, selected.priority)}</Typography>
              <Typography><strong>{t('myRequests.status')}:</strong> <Chip label={t(`enums.srStatus.${selected.status}`, selected.status)} size="small" color={statusColor(selected.status) as any} /></Typography>
              <Typography><strong>{t('myRequests.created')}:</strong> {formatDateLocal(selected.createdAtUtc)}</Typography>
              {selected.updatedAtUtc && <Typography><strong>{t('myRequests.updated')}:</strong> {formatDateLocal(selected.updatedAtUtc)}</Typography>}
            </Box>
            <Typography sx={{ mb: 2 }}><strong>{t('myRequests.description')}:</strong> {selected.description}</Typography>
            {selected.attachments.length > 0 && (
              <Box><Typography variant="subtitle2">{t('myRequests.attachments')}:</Typography>
                <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap', mt: 1 }}>
                  {selected.attachments.map(a => (<Chip key={a.id} label={a.fileName} component="a" href={a.url} target="_blank" clickable />))}
                </Box>
              </Box>
            )}
          </DialogContent>
          <DialogActions><Button onClick={() => setDetailOpen(false)}>{t('app.close')}</Button></DialogActions>
        </>)}
      </Dialog>
    </Box>
  );
};

export default MyRequestsPage;
