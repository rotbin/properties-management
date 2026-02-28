import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
  Box, Typography, Card, CardContent, CardActionArea, Stack, Chip, CircularProgress,
  Dialog, DialogTitle, DialogContent, Button, useMediaQuery, useTheme,
  TextField, IconButton, Avatar
} from '@mui/material';
import {
  MarkEmailRead, Circle, Warning, Payment, Email, Send, Reply, Chat, Close
} from '@mui/icons-material';
import { tenantMessagesApi } from '../../api/services';
import type { TenantMessageDto } from '../../types';
import { useTranslation } from 'react-i18next';
import { useNotifications } from '../../contexts/NotificationContext';
import { useAuth } from '../../auth/AuthContext';

const typeIcon = (type: string) => {
  switch (type) {
    case 'Warning': return <Warning sx={{ fontSize: 18, color: '#d32f2f' }} />;
    case 'PaymentReminder': return <Payment sx={{ fontSize: 18, color: '#ed6c02' }} />;
    case 'TenantReply': return <Reply sx={{ fontSize: 18, color: '#2e7d32' }} />;
    case 'ManagerReply': return <Email sx={{ fontSize: 18, color: '#1565c0' }} />;
    default: return <Email sx={{ fontSize: 18, color: '#1976d2' }} />;
  }
};

const typeBorderColor = (type: string) => {
  switch (type) {
    case 'Warning': return '#d32f2f';
    case 'PaymentReminder': return '#ed6c02';
    case 'TenantReply': return '#2e7d32';
    case 'ManagerReply': return '#1565c0';
    default: return '#1976d2';
  }
};

const MyMessagesPage: React.FC = () => {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const { tenantMessageTick } = useNotifications();
  const { user } = useAuth();
  const [messages, setMessages] = useState<TenantMessageDto[]>([]);
  const [loading, setLoading] = useState(true);

  // Chat dialog state
  const [chatOpen, setChatOpen] = useState(false);
  const [chatThread, setChatThread] = useState<TenantMessageDto[]>([]);
  const [chatRoot, setChatRoot] = useState<TenantMessageDto | null>(null);
  const [chatLoading, setChatLoading] = useState(false);
  const [replyText, setReplyText] = useState('');
  const [replySending, setReplySending] = useState(false);
  const chatEndRef = useRef<HTMLDivElement | null>(null);

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

  const scrollToBottom = () => {
    setTimeout(() => chatEndRef.current?.scrollIntoView({ behavior: 'smooth' }), 100);
  };

  const openChat = async (msg: TenantMessageDto) => {
    setChatRoot(msg);
    setChatOpen(true);
    setChatLoading(true);
    try {
      const res = await tenantMessagesApi.getThread(msg.id);
      setChatThread(res.data);
      await tenantMessagesApi.markThreadRead(msg.id);
      setMessages(prev => prev.map(m => m.id === msg.id ? { ...m, isRead: true } : m));
      scrollToBottom();
    } catch { /* ignore */ }
    finally { setChatLoading(false); }
  };

  const refreshThread = async (rootId: number) => {
    try {
      const res = await tenantMessagesApi.getThread(rootId);
      setChatThread(res.data);
      await tenantMessagesApi.markThreadRead(rootId);
      scrollToBottom();
    } catch { /* ignore */ }
  };

  useEffect(() => {
    if (chatOpen && chatRoot) {
      refreshThread(chatRoot.id);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tenantMessageTick]);

  const handleReply = async () => {
    if (!replyText.trim() || !chatRoot) return;
    setReplySending(true);
    try {
      const subject = chatRoot.subject.startsWith('Re: ') ? chatRoot.subject : `Re: ${chatRoot.subject}`;
      const res = await tenantMessagesApi.reply({ subject, body: replyText.trim(), parentMessageId: chatRoot.id });
      setChatThread(prev => [...prev, res.data]);
      setReplyText('');
      scrollToBottom();
      load();
    } catch { /* ignore */ }
    finally { setReplySending(false); }
  };

  const handleMarkAllRead = async () => {
    try {
      await tenantMessagesApi.markAllRead();
      setMessages(prev => prev.map(m => ({ ...m, isRead: true, readAtUtc: m.readAtUtc || new Date().toISOString() })));
    } catch { /* ignore */ }
  };

  const handleCloseChat = () => {
    setChatOpen(false);
    setReplyText('');
    setChatThread([]);
    setChatRoot(null);
    load();
  };

  const unreadCount = messages.filter(m => !m.isRead).length;

  const isMine = (msg: TenantMessageDto) =>
    msg.sentByUserId === user?.id || msg.messageType === 'TenantReply';

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
              <CardActionArea onClick={() => openChat(msg)}>
                <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                      {typeIcon(msg.messageType)}
                      <Typography variant="subtitle2" fontWeight={msg.isRead ? 400 : 700}>
                        {msg.subject}
                      </Typography>
                    </Box>
                    <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                      {msg.replyCount > 0 && (
                        <Chip icon={<Chat sx={{ fontSize: 14 }} />}
                          label={msg.replyCount}
                          size="small" variant="outlined"
                          sx={{ height: 22, '& .MuiChip-label': { px: 0.5 } }} />
                      )}
                      {!msg.isRead && <Circle sx={{ fontSize: 10, color: '#d32f2f' }} />}
                    </Box>
                  </Box>
                  <Typography variant="body2" color="text.secondary" noWrap sx={{ mb: 0.5 }}>
                    {msg.body.substring(0, 120)}{msg.body.length > 120 ? '...' : ''}
                  </Typography>
                  <Box sx={{ display: 'flex', gap: 0.5, alignItems: 'center' }}>
                    <Typography variant="caption" color="text.secondary">
                      {msg.sentByName || 'AI Agent'} · {new Date(msg.lastReplyAtUtc || msg.createdAtUtc).toLocaleString()}
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

      {/* ─── Chat Conversation Dialog ─── */}
      <Dialog open={chatOpen} onClose={handleCloseChat} maxWidth="sm" fullWidth fullScreen={isMobile}
        PaperProps={{ sx: { display: 'flex', flexDirection: 'column', height: isMobile ? '100%' : '80vh' } }}>
        {chatRoot && (
          <>
            <DialogTitle sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', pb: 1 }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
                {typeIcon(chatRoot.messageType)}
                <Typography variant="h6" noWrap sx={{ fontSize: '1rem' }}>{chatRoot.subject}</Typography>
              </Box>
              <IconButton size="small" onClick={handleCloseChat}><Close /></IconButton>
            </DialogTitle>

            <DialogContent sx={{ flex: 1, overflowY: 'auto', px: 2, py: 1, display: 'flex', flexDirection: 'column' }}>
              {chatLoading ? (
                <Box sx={{ display: 'flex', justifyContent: 'center', flex: 1, alignItems: 'center' }}><CircularProgress /></Box>
              ) : (
                <Stack spacing={1.5} sx={{ py: 1 }}>
                  {chatThread.map(msg => {
                    const mine = isMine(msg);
                    return (
                      <Box key={msg.id} sx={{ display: 'flex', justifyContent: mine ? 'flex-end' : 'flex-start' }}>
                        <Box sx={{
                          maxWidth: '80%',
                          display: 'flex',
                          flexDirection: mine ? 'row-reverse' : 'row',
                          gap: 1, alignItems: 'flex-end'
                        }}>
                          <Avatar sx={{
                            width: 30, height: 30, fontSize: '0.75rem',
                            bgcolor: mine ? '#2e7d32' : '#1976d2'
                          }}>
                            {(msg.sentByName || 'AI')[0]}
                          </Avatar>
                          <Box sx={{
                            bgcolor: mine ? '#e8f5e9' : '#e3f2fd',
                            borderRadius: 2,
                            px: 2, py: 1,
                            borderBottomRightRadius: mine ? 4 : 16,
                            borderBottomLeftRadius: mine ? 16 : 4
                          }}>
                            <Typography variant="caption" color="text.secondary" fontWeight={600}>
                              {msg.sentByName || 'AI Agent'}
                            </Typography>
                            <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', mt: 0.25 }}>
                              {msg.body}
                            </Typography>
                            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5, textAlign: mine ? 'right' : 'left', fontSize: '0.65rem' }}>
                              {new Date(msg.createdAtUtc).toLocaleString()}
                            </Typography>
                          </Box>
                        </Box>
                      </Box>
                    );
                  })}
                  <div ref={chatEndRef} />
                </Stack>
              )}
            </DialogContent>

            <Box sx={{ px: 2, py: 1.5, borderTop: '1px solid', borderColor: 'divider', display: 'flex', gap: 1, alignItems: 'flex-end' }}>
              <TextField
                fullWidth
                multiline
                minRows={1}
                maxRows={4}
                size="small"
                placeholder={t('myMessages.replyPlaceholder')}
                value={replyText}
                onChange={(e) => setReplyText(e.target.value)}
                disabled={replySending}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    handleReply();
                  }
                }}
              />
              <IconButton
                color="primary"
                onClick={handleReply}
                disabled={!replyText.trim() || replySending}
              >
                {replySending ? <CircularProgress size={20} /> : <Send />}
              </IconButton>
            </Box>
          </>
        )}
      </Dialog>
    </Box>
  );
};

export default MyMessagesPage;
