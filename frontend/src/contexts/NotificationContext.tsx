import React, { createContext, useContext, useState, useEffect, useCallback, useRef } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { ticketMessagesApi } from '../api/services';
import { getAccessToken } from '../api/client';
import { useAuth } from '../auth/AuthContext';

interface NotificationContextType {
  unreadCount: number;
  refreshUnreadCount: () => void;
}

const NotificationContext = createContext<NotificationContextType>({ unreadCount: 0, refreshUnreadCount: () => {} });

export const NotificationProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const { isAuthenticated } = useAuth();
  const [unreadCount, setUnreadCount] = useState(0);
  const connectionRef = useRef<HubConnection | null>(null);

  const refreshUnreadCount = useCallback(async () => {
    try {
      const res = await ticketMessagesApi.getUnreadCount();
      setUnreadCount(res.data);
    } catch { /* ignore */ }
  }, []);

  useEffect(() => {
    if (!isAuthenticated) {
      setUnreadCount(0);
      return;
    }

    refreshUnreadCount();

    const baseUrl = import.meta.env.VITE_API_URL || window.location.origin;
    const connection = new HubConnectionBuilder()
      .withUrl(`${baseUrl}/hubs/ticket-chat`, {
        accessTokenFactory: () => getAccessToken() || '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('UnreadCountChanged', () => {
      refreshUnreadCount();
    });

    connection.start().then(() => {
      connectionRef.current = connection;
    }).catch(err => {
      console.warn('Global SignalR connection failed', err);
    });

    return () => {
      connection.stop().catch(() => {});
      connectionRef.current = null;
    };
  }, [isAuthenticated, refreshUnreadCount]);

  return (
    <NotificationContext.Provider value={{ unreadCount, refreshUnreadCount }}>
      {children}
    </NotificationContext.Provider>
  );
};

export const useNotifications = () => useContext(NotificationContext);
