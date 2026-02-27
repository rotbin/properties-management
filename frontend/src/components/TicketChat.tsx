import React, { useEffect, useState, useRef, useCallback } from 'react';
import {
  Box, Typography, TextField, IconButton, CircularProgress, Avatar, Chip
} from '@mui/material';
import { Send, SmartToy, Person, SupportAgent } from '@mui/icons-material';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { ticketMessagesApi } from '../api/services';
import type { TicketMessageDto } from '../types';
import { useAuth } from '../auth/AuthContext';
import { useTranslation } from 'react-i18next';

interface TicketFieldUpdate {
  ticketId: number;
  area?: string;
  category?: string;
  priority?: string;
  isEmergency?: boolean;
  description?: string;
}

interface TicketChatProps {
  ticketId: number;
  incidentGroupId?: number;
  incidentGroupTitle?: string;
  incidentTicketCount?: number;
  onTicketUpdated?: (update: TicketFieldUpdate) => void;
}

const TicketChat: React.FC<TicketChatProps> = ({ ticketId, incidentGroupId, incidentTicketCount, onTicketUpdated }) => {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [messages, setMessages] = useState<TicketMessageDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [text, setText] = useState('');
  const [sending, setSending] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const connectionRef = useRef<HubConnection | null>(null);

  const getToken = useCallback(() => {
    return localStorage.getItem('token') || '';
  }, []);

  // Initial fetch + SignalR connection
  useEffect(() => {
    let cancelled = false;

    const init = async () => {
      // Fetch existing messages
      try {
        const res = await ticketMessagesApi.getMessages(ticketId);
        if (!cancelled) setMessages(res.data);
      } catch { /* ignore */ }
      finally { if (!cancelled) setLoading(false); }

      // Mark as read
      try { await ticketMessagesApi.markRead(ticketId); } catch { /* ignore */ }

      // Set up SignalR
      const baseUrl = import.meta.env.VITE_API_URL || window.location.origin;
      const connection = new HubConnectionBuilder()
        .withUrl(`${baseUrl}/hubs/ticket-chat`, {
          accessTokenFactory: () => getToken(),
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      connection.on('NewMessage', (msg: TicketMessageDto) => {
        setMessages(prev => {
          if (prev.some(m => m.id === msg.id)) return prev;
          return [...prev, msg];
        });
        ticketMessagesApi.markRead(ticketId).catch(() => {});
      });

      connection.on('TicketUpdated', (update: TicketFieldUpdate) => {
        onTicketUpdated?.(update);
      });

      try {
        await connection.start();
        await connection.invoke('JoinTicket', ticketId);
        connectionRef.current = connection;
      } catch (err) {
        console.warn('SignalR connection failed, falling back to polling', err);
        // Fallback: poll every 10s
        if (!cancelled) {
          const interval = setInterval(async () => {
            try {
              const res = await ticketMessagesApi.getMessages(ticketId);
              setMessages(res.data);
            } catch { /* ignore */ }
          }, 10_000);
          return () => clearInterval(interval);
        }
      }
    };

    init();

    return () => {
      cancelled = true;
      const conn = connectionRef.current;
      if (conn) {
        conn.invoke('LeaveTicket', ticketId).catch(() => {});
        conn.stop().catch(() => {});
        connectionRef.current = null;
      }
    };
  }, [ticketId, getToken]);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const handleSend = async () => {
    if (!text.trim() || sending) return;
    setSending(true);
    try {
      await ticketMessagesApi.postMessage(ticketId, text.trim());
      setText('');
    } catch { /* ignore */ }
    finally { setSending(false); }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSend(); }
  };

  const senderAvatar = (senderType: string) => {
    switch (senderType) {
      case 'Agent': return <Avatar sx={{ bgcolor: '#7c4dff', width: 32, height: 32 }}><SmartToy fontSize="small" /></Avatar>;
      case 'Manager': return <Avatar sx={{ bgcolor: '#1a56a0', width: 32, height: 32 }}><SupportAgent fontSize="small" /></Avatar>;
      default: return <Avatar sx={{ bgcolor: '#43a047', width: 32, height: 32 }}><Person fontSize="small" /></Avatar>;
    }
  };

  const isOwnMessage = (msg: TicketMessageDto) => msg.senderUserId === user?.id;

  return (
    <Box sx={{ mt: 2, border: '1px solid', borderColor: 'divider', borderRadius: 2, display: 'flex', flexDirection: 'column', maxHeight: 400 }}>
      <Box sx={{ px: 2, py: 1, borderBottom: '1px solid', borderColor: 'divider', bgcolor: 'grey.50', display: 'flex', alignItems: 'center', gap: 1 }}>
        <SmartToy fontSize="small" color="primary" />
        <Typography variant="subtitle2">{t('ticketChat.title')}</Typography>
        {incidentGroupId && (
          <Chip
            size="small"
            color="warning"
            variant="outlined"
            label={t('ticketChat.incidentLinked', { id: incidentGroupId, count: incidentTicketCount || 0 })}
            sx={{ ml: 'auto' }}
          />
        )}
      </Box>

      <Box sx={{ flex: 1, overflow: 'auto', px: 2, py: 1.5, minHeight: 150, maxHeight: 280, bgcolor: '#fafafa' }}>
        {loading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}><CircularProgress size={24} /></Box>
        ) : messages.length === 0 ? (
          <Typography variant="body2" color="text.secondary" align="center" sx={{ py: 3 }}>
            {t('ticketChat.noMessages')}
          </Typography>
        ) : (
          messages.map(msg => {
            const own = isOwnMessage(msg);
            const isAgent = msg.senderType === 'Agent';
            return (
              <Box key={msg.id} sx={{
                display: 'flex',
                flexDirection: own ? 'row-reverse' : 'row',
                gap: 1,
                mb: 1.5,
                alignItems: 'flex-start'
              }}>
                {senderAvatar(msg.senderType)}
                <Box sx={{
                  maxWidth: '75%',
                  bgcolor: isAgent ? '#f3e5f5' : own ? '#e3f2fd' : '#fff',
                  border: '1px solid',
                  borderColor: isAgent ? '#ce93d8' : own ? '#90caf9' : 'divider',
                  borderRadius: 2,
                  px: 1.5,
                  py: 1,
                }}>
                  <Typography variant="caption" color="text.secondary" fontWeight={600}>
                    {isAgent ? t('ticketChat.aiAssistant') : msg.senderName || msg.senderType}
                  </Typography>
                  <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap', mt: 0.25 }}>
                    {msg.text}
                  </Typography>
                  <Typography variant="caption" color="text.disabled" sx={{ display: 'block', textAlign: own ? 'left' : 'right', mt: 0.5 }}>
                    {new Date(msg.createdAtUtc).toLocaleString()}
                  </Typography>
                </Box>
              </Box>
            );
          })
        )}
        <div ref={messagesEndRef} />
      </Box>

      <Box sx={{ display: 'flex', gap: 1, px: 2, py: 1, borderTop: '1px solid', borderColor: 'divider' }}>
        <TextField
          fullWidth
          size="small"
          placeholder={t('ticketChat.placeholder')}
          value={text}
          onChange={e => setText(e.target.value)}
          onKeyDown={handleKeyDown}
          multiline
          maxRows={3}
          disabled={sending}
        />
        <IconButton color="primary" onClick={handleSend} disabled={!text.trim() || sending}>
          {sending ? <CircularProgress size={20} /> : <Send />}
        </IconButton>
      </Box>
    </Box>
  );
};

export default TicketChat;
