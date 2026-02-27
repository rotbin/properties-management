import React, { useEffect, useState } from 'react';
import {
  Box, Typography, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  Paper, Chip, Button, Dialog, DialogTitle, DialogContent, DialogActions, CircularProgress,
  Card, CardContent, CardActionArea, Stack, useMediaQuery, useTheme
} from '@mui/material';
import { Visibility, Add, BugReport, Chat, FiberNew } from '@mui/icons-material';
import { useNavigate } from 'react-router-dom';
import { serviceRequestsApi } from '../../api/services';
import type { ServiceRequestDto } from '../../types';
import { formatDateLocal } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';
import TicketChat from '../../components/TicketChat';

const priorityColor = (p: string) => p === 'Critical' ? 'error' : p === 'High' ? 'warning' : p === 'Medium' ? 'info' : 'default';
const statusColor = (s: string) => s === 'New' ? 'info' : s === 'Resolved' ? 'success' : s === 'Rejected' ? 'error' : 'default';

const MyRequestsPage: React.FC = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [requests, setRequests] = useState<ServiceRequestDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<ServiceRequestDto | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);

  useEffect(() => { serviceRequestsApi.getMy().then(r => setRequests(r.data)).catch(() => {}).finally(() => setLoading(false)); }, []);

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h4" sx={{ fontWeight: 700, fontSize: { xs: '1.3rem', md: '2rem' } }}>{t('myRequests.title')}</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={() => navigate('/new-request')} size={isMobile ? 'small' : 'medium'}>{t('myRequests.newRequest')}</Button>
      </Box>

      {isMobile ? (
        <Stack spacing={1.5}>
          {requests.map(sr => (
            <Card key={sr.id} variant="outlined">
              <CardActionArea onClick={() => { setSelected(sr); setDetailOpen(true); }}>
                <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                    <Typography variant="subtitle2">#{sr.id}</Typography>
                    <Box sx={{ display: 'flex', gap: 0.5 }}>
                      <Chip label={t(`enums.priority.${sr.priority}`, sr.priority)} size="small" color={priorityColor(sr.priority) as any} />
                      <Chip label={t(`enums.srStatus.${sr.status}`, sr.status)} size="small" color={statusColor(sr.status) as any} />
                    </Box>
                  </Box>
                  <Typography variant="body2" fontWeight={600} noWrap>{sr.buildingName} – {t(`enums.area.${sr.area}`, sr.area)} – {t(`enums.category.${sr.category}`, sr.category)}</Typography>
                  <Box sx={{ display: 'flex', gap: 0.5, alignItems: 'center' }}>
                    <Typography variant="caption" color="text.secondary">{formatDateLocal(sr.createdAtUtc)}</Typography>
                    {sr.hasUnreadMessages && <Chip icon={<FiberNew sx={{ fontSize: 14 }} />} label={t('ticketChat.unread')} size="small" color="error" sx={{ height: 20, '& .MuiChip-label': { px: 0.5, fontSize: '0.65rem' } }} />}
                    {sr.incidentGroupId && <Chip icon={<BugReport sx={{ fontSize: 14 }} />} label={`#${sr.incidentGroupId}`} size="small" color="warning" variant="outlined" sx={{ height: 20, '& .MuiChip-label': { px: 0.5, fontSize: '0.65rem' } }} />}
                    {(sr.messageCount ?? 0) > 0 && <Chip icon={<Chat sx={{ fontSize: 14 }} />} label={sr.messageCount} size="small" variant="outlined" sx={{ height: 20, '& .MuiChip-label': { px: 0.5, fontSize: '0.65rem' } }} />}
                  </Box>
                </CardContent>
              </CardActionArea>
            </Card>
          ))}
          {requests.length === 0 && <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('myRequests.noRequests')}</Typography>}
        </Stack>
      ) : (
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
      )}

      <Dialog open={detailOpen} onClose={() => setDetailOpen(false)} maxWidth="md" fullWidth fullScreen={isMobile}>
        {selected && (<>
          <DialogTitle>
            {selected.buildingName} – {t(`enums.area.${selected.area}`, selected.area)} – {t(`enums.category.${selected.category}`, selected.category)}
            {selected.isEmergency && <Chip label={t('serviceRequests.emergency')} color="error" size="small" sx={{ ml: 1 }} />}
          </DialogTitle>
          <DialogContent>
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2, mb: 2 }}>
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

            <TicketChat
              ticketId={selected.id}
              incidentGroupId={selected.incidentGroupId}
              incidentGroupTitle={selected.incidentGroupTitle}
              incidentTicketCount={selected.incidentTicketCount}
            />
          </DialogContent>
          <DialogActions><Button onClick={() => setDetailOpen(false)}>{t('app.close')}</Button></DialogActions>
        </>)}
      </Dialog>
    </Box>
  );
};

export default MyRequestsPage;
