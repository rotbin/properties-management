import React, { useMemo } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { ThemeProvider, createTheme, CssBaseline } from '@mui/material';
import { CacheProvider } from '@emotion/react';
import createCache from '@emotion/cache';
import rtlPlugin from 'stylis-plugin-rtl';
import { prefixer } from 'stylis';
import { useTranslation } from 'react-i18next';
import { AuthProvider, useAuth } from './auth/AuthContext';
import Layout from './components/Layout';
import LoginPage from './pages/auth/LoginPage';
import DashboardPage from './pages/manager/DashboardPage';
import BuildingsPage from './pages/manager/BuildingsPage';
import VendorsPage from './pages/manager/VendorsPage';
import AssetsPage from './pages/manager/AssetsPage';
import ServiceRequestsPage from './pages/manager/ServiceRequestsPage';
import WorkOrdersPage from './pages/manager/WorkOrdersPage';
import CleaningPlansPage from './pages/manager/CleaningPlansPage';
import JobsPage from './pages/manager/JobsPage';
import NewRequestPage from './pages/tenant/NewRequestPage';
import MyRequestsPage from './pages/tenant/MyRequestsPage';
import MyChargesPage from './pages/tenant/MyChargesPage';
import VendorWorkOrdersPage from './pages/vendor/VendorWorkOrdersPage';
import HOAPlansPage from './pages/manager/HOAPlansPage';
import PaymentProviderConfigPage from './pages/manager/PaymentProviderConfigPage';
import PaymentSuccessPage from './pages/payment/PaymentSuccessPage';
import PaymentCancelPage from './pages/payment/PaymentCancelPage';

// Emotion caches for RTL and LTR
const rtlCache = createCache({
  key: 'muirtl',
  stylisPlugins: [prefixer, rtlPlugin],
});
const ltrCache = createCache({
  key: 'muiltr',
  stylisPlugins: [prefixer],
});

const ProtectedRoute: React.FC<{ children: React.ReactNode; roles?: string[] }> = ({ children, roles }) => {
  const { isAuthenticated, user } = useAuth();
  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (roles && !roles.some(r => user?.roles.includes(r))) return <Navigate to="/dashboard" replace />;
  return <>{children}</>;
};

const AppRoutes: React.FC = () => {
  const { isAuthenticated, user } = useAuth();

  const getDefaultRoute = () => {
    if (!isAuthenticated) return '/login';
    if (user?.roles.includes('Vendor')) return '/my-work-orders';
    if (user?.roles.includes('Tenant')) return '/my-requests';
    return '/dashboard';
  };

  return (
    <Routes>
      <Route path="/login" element={isAuthenticated ? <Navigate to={getDefaultRoute()} /> : <LoginPage />} />
      <Route path="/" element={<ProtectedRoute><Layout /></ProtectedRoute>}>
        <Route index element={<Navigate to={getDefaultRoute()} replace />} />
        <Route path="dashboard" element={<ProtectedRoute roles={['Admin', 'Manager']}><DashboardPage /></ProtectedRoute>} />
        <Route path="buildings" element={<ProtectedRoute roles={['Admin', 'Manager']}><BuildingsPage /></ProtectedRoute>} />
        <Route path="vendors" element={<ProtectedRoute roles={['Admin', 'Manager']}><VendorsPage /></ProtectedRoute>} />
        <Route path="assets" element={<ProtectedRoute roles={['Admin', 'Manager']}><AssetsPage /></ProtectedRoute>} />
        <Route path="service-requests" element={<ProtectedRoute roles={['Admin', 'Manager']}><ServiceRequestsPage /></ProtectedRoute>} />
        <Route path="work-orders" element={<ProtectedRoute roles={['Admin', 'Manager']}><WorkOrdersPage /></ProtectedRoute>} />
        <Route path="cleaning-plans" element={<ProtectedRoute roles={['Admin', 'Manager']}><CleaningPlansPage /></ProtectedRoute>} />
        <Route path="jobs" element={<ProtectedRoute roles={['Admin', 'Manager']}><JobsPage /></ProtectedRoute>} />
        <Route path="hoa" element={<ProtectedRoute roles={['Admin', 'Manager']}><HOAPlansPage /></ProtectedRoute>} />
        <Route path="payment-config" element={<ProtectedRoute roles={['Admin', 'Manager']}><PaymentProviderConfigPage /></ProtectedRoute>} />
        <Route path="payment/success" element={<ProtectedRoute><PaymentSuccessPage /></ProtectedRoute>} />
        <Route path="payment/cancel" element={<ProtectedRoute><PaymentCancelPage /></ProtectedRoute>} />
        <Route path="new-request" element={<ProtectedRoute roles={['Tenant', 'Admin', 'Manager']}><NewRequestPage /></ProtectedRoute>} />
        <Route path="my-requests" element={<ProtectedRoute roles={['Tenant']}><MyRequestsPage /></ProtectedRoute>} />
        <Route path="my-charges" element={<ProtectedRoute roles={['Tenant']}><MyChargesPage /></ProtectedRoute>} />
        <Route path="my-work-orders" element={<ProtectedRoute roles={['Vendor']}><VendorWorkOrdersPage /></ProtectedRoute>} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
};

const App: React.FC = () => {
  const { i18n } = useTranslation();
  const isRtl = i18n.language === 'he';
  const direction = isRtl ? 'rtl' : 'ltr';

  // Update document direction and lang
  React.useEffect(() => {
    document.documentElement.dir = direction;
    document.documentElement.lang = i18n.language;
  }, [direction, i18n.language]);

  const theme = useMemo(
    () =>
      createTheme({
        direction,
        palette: {
          primary: { main: '#1976d2' },
          secondary: { main: '#9c27b0' },
          background: { default: '#f5f5f5' },
        },
        typography: {
          fontFamily: '"Inter", "Roboto", "Helvetica", "Arial", sans-serif',
        },
        components: {
          MuiButton: { styleOverrides: { root: { textTransform: 'none' } } },
        },
      }),
    [direction]
  );

  return (
    <CacheProvider value={isRtl ? rtlCache : ltrCache}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <BrowserRouter>
          <AuthProvider>
            <AppRoutes />
          </AuthProvider>
        </BrowserRouter>
      </ThemeProvider>
    </CacheProvider>
  );
};

export default App;
