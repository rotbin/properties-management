import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
  Box, Typography, Card, CardContent, CardActionArea, Stack, Chip, CircularProgress,
  Dialog, DialogTitle, DialogContent, DialogActions, Button, useMediaQuery, useTheme,
  TextField, IconButton
} from '@mui/material';
import { MarkEmailRead, Circle, Warning, Payment, Email, Send, Reply } from '@mui/icons-material';
import { tenantMessagesApi } from '../../api/services';
import type { TenantMessageDto } from '../../types';
import { useTranslation } from 'react-i18next';
import { useNotifications } from '../../contexts/NotificationContext';

const typeIcon = (type: string) => {
  switch (type) {
    case 'Warning': return <Warning sx={{ fontSize: 18, color: '#d32f2f' }} />;
    case 'PaymentReminder': return <Payment sx={{ fontSize: 18, color: '#ed6c02' }} />;
    case 'TenantReply': return <Reply sx={{ fontSize: 18, color: '#2e7d32' }} />;
    default: return <Email sx={{ fontSize: 18, color: '#1976d2' }} />;
  }
};

const typeBorderColor = (type: string) => {
  switch (type) {
    case 'Warning': return '#d32f2f';
    case 'PaymentReminder': return '#ed6c02';
    case 'TenantReply': return '#2e7d32';
    default: return '#1976d2';
  }
};

const MyMessagesPage: React.FC = () => {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const { tenantMessageTick } = useNotifications();
  const [messages, setMessages] = useState<TenantMessageDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState<TenantMessageDto | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);
  const [replyText, setReplyText] = useState('');
  const [replySending, setReplySending] = useState(false);

  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const load = useCallback(() => {
    tenantMessagesApi.getMyMessages()
      .then(r => setMessages(r.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => { load(); }, [load, tenantMessageTick]);

  useEffect(() => {
    pollRef.current = setInterval(load, 8000);
    return () => { if (pollRef.current) clearInterval(pollRef.current); };
  }, [load]);

  const handleOpen = async (msg: TenantMessageDto) => {
    setSelected(msg);
    setDetailOpen(true);
    if (!msg.isRead) {
      try {
        await tenantMessagesApi.markRead(msg.id);
        setMessages(prev => prev.map(m => m.id === msg.id ? { ...m, isRead: true, readAtUtc: new Date().toISOString() } : m));
      } catch { /* ignore */ }
    }
  };

  const handleMarkAllRead = async () => {
    try {
      await tenantMessagesApi.markAllRead();
      setMessages(prev => prev.map(m => ({ ...m, isRead: true, readAtUtc: m.readAtUtc || new Date().toISOString() })));
    } catch { /* ignore */ }
  };

  const handleReply = async () => {
    if (!replyText.trim() || !selected) return;
    setReplySending(true);
    try {
      const subject = selected.subject.startsWith('Re: ') ? selected.subject : `Re: ${selected.subject}`;
      const res = await tenantMessagesApi.reply({ subject, body: replyText.trim() });
      setMessages(prev => [res.data, ...prev]);
      setReplyText('');
    } catch { /* ignore */ }
    finally { setReplySending(false); }
  };

  const unreadCount = messages.filter(m => !m.isRead).length;

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}><CircularProgress /></Box>;

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <Typography variant="h4" sx={{ fontWeight: 700, fontSize: { xs: '1.3rem', md: '2rem' } }}>
            {t('myMessages.title')}
          </Typography>
          {unreadCount > 0 && (
            <Chip label={t('myMessages.unreadCount', { count: unreadCount })} color="error" size="small" />
          )}
        </Box>
        {unreadCount > 0 && (
          <Button variant="outlined" size="small" startIcon={<MarkEmailRead />} onClick={handleMarkAllRead}>
            {t('myMessages.markAllRead')}
          </Button>
        )}
      </Box>

      {messages.length === 0 ? (
        <Typography color="text.secondary" align="center" sx={{ py: 6 }}>{t('myMessages.noMessages')}</Typography>
      ) : (
        <Stack spacing={1.5}>
          {messages.map(msg => (
            <Card key={msg.id} variant="outlined" sx={{
              borderLeft: '5px solid',
              borderLeftColor: typeBorderColor(msg.messageType),
              ...(msg.isRead ? {} : { bgcolor: '#fff8e1', boxShadow: '0 2px 8px rgba(0,0,0,0.08)' })
            }}>
              <CardActionArea onClick={() => handleOpen(msg)}>
                <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                      {typeIcon(msg.messageType)}
                      <Typography variant="subtitle2" fontWeight={msg.isRead ? 400 : 700}>
                        {msg.subject}
                      </Typography>
                    </Box>
                    {!msg.isRead && <Circle sx={{ fontSize: 10, color: '#d32f2f' }} />}
                  </Box>
                  <Typography variant="body2" color="text.secondary" noWrap sx={{ mb: 0.5 }}>
                    {msg.body.substring(0, 120)}{msg.body.length > 120 ? '...' : ''}
                  </Typography>
                  <Box sx={{ display: 'flex', gap: 0.5, alignItems: 'center' }}>
                    <Typography variant="caption" color="text.secondary">
                      {msg.sentByName || 'AI Agent'} · {new Date(msg.createdAtUtc).toLocaleString()}
                    </Typography>
                    {msg.messageType === 'Warning' && (
                      <Chip label={t('myMessages.urgent')} size="small" color="error" sx={{ height: 18, '& .MuiChip-label': { px: 0.5, fontSize: '0.6rem' } }} />
                    )}
                  </Box>
                </CardContent>
              </CardActionArea>
            </Card>
          ))}
        </Stack>
      )}

      <Dialog open={detailOpen} onClose={() => setDetailOpen(false)} maxWidth="sm" fullWidth fullScreen={isMobile}>
        {selected && (
          <>
            <DialogTitle sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
              {typeIcon(selected.messageType)}
              {selected.subject}
            </DialogTitle>
            <DialogContent>
              <Box sx={{ display: 'flex', gap: 0.5, mb: 2 }}>
                <Chip label={selected.messageType} size="small" variant="outlined" />
                {selected.payerCategory && (
                  <Chip label={selected.payerCategory} size="small" variant="outlined"
                    color={selected.payerCategory === 'ChronicallyLate' ? 'error' : selected.payerCategory === 'OccasionallyLate' ? 'warning' : 'success'} />
                )}
              </Box>
              <Typography variant="body1" sx={{ whiteSpace: 'pre-wrap', mb: 2 }}>{selected.body}</Typography>
              <Typography variant="caption" color="text.secondary">
                {t('myMessages.from')}: {selected.sentByName || 'AI Agent'} · {new Date(selected.createdAtUtc).toLocaleString()}
              </Typography>

              {selected.messageType !== 'TenantReply' && (
                <Box sx={{ mt: 3, display: 'flex', gap: 1, alignItems: 'flex-end' }}>
                  <TextField
                    fullWidth
                    multiline
                    minRows={2}
                    maxRows={5}
                    size="small"
                    placeholder={t('myMessages.replyPlaceholder')}
                    value={replyText}
                    onChange={(e) => setReplyText(e.target.value)}
                    disabled={replySending}
                  />
                  <IconButton
                    color="primary"
                    onClick={handleReply}
                    disabled={!replyText.trim() || replySending}
                    sx={{ mb: 0.5 }}
                  >
                    {replySending ? <CircularProgress size={20} /> : <Send />}
                  </IconButton>
                </Box>
              )}
            </DialogContent>
            <DialogActions>
              <Button onClick={() => { setDetailOpen(false); setReplyText(''); }}>{t('app.close')}</Button>
            </DialogActions>
          </>
        )}
      </Dialog>
    </Box>
  );
};

export default MyMessagesPage;
