import React, { useState } from 'react';
import {
  AppBar, Toolbar, Typography, Drawer, List, ListItemButton, ListItemIcon,
  ListItemText, Box, IconButton, Divider, Chip, useMediaQuery, useTheme
} from '@mui/material';
import {
  Menu as MenuIcon, Dashboard, Business, Engineering, CleaningServices,
  Assignment, Build, Logout, Home, WorkOutline, Schedule,
  AccountBalance, Payment, Settings
} from '@mui/icons-material';
import { useNavigate, useLocation, Outlet } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { useTranslation } from 'react-i18next';

const DRAWER_WIDTH = 260;

interface NavItem {
  labelKey: string;
  path: string;
  icon: React.ReactNode;
  roles: string[];
}

const navItems: NavItem[] = [
  { labelKey: 'nav.dashboard', path: '/dashboard', icon: <Dashboard />, roles: ['Admin', 'Manager'] },
  { labelKey: 'nav.buildings', path: '/buildings', icon: <Business />, roles: ['Admin', 'Manager'] },
  { labelKey: 'nav.vendors', path: '/vendors', icon: <Engineering />, roles: ['Admin', 'Manager'] },
  { labelKey: 'nav.assets', path: '/assets', icon: <Build />, roles: ['Admin', 'Manager'] },
  { labelKey: 'nav.serviceRequests', path: '/service-requests', icon: <Assignment />, roles: ['Admin', 'Manager'] },
  { labelKey: 'nav.workOrders', path: '/work-orders', icon: <WorkOutline />, roles: ['Admin', 'Manager'] },
  { labelKey: 'nav.cleaningPlans', path: '/cleaning-plans', icon: <CleaningServices />, roles: ['Admin', 'Manager'] },
  { labelKey: 'nav.jobs', path: '/jobs', icon: <Schedule />, roles: ['Admin', 'Manager'] },
  { labelKey: 'nav.hoaFinance', path: '/hoa', icon: <AccountBalance />, roles: ['Admin', 'Manager'] },
  { labelKey: 'nav.paymentProviders', path: '/payment-config', icon: <Settings />, roles: ['Admin', 'Manager'] },
  { labelKey: 'nav.myRequests', path: '/my-requests', icon: <Assignment />, roles: ['Tenant'] },
  { labelKey: 'nav.newRequest', path: '/new-request', icon: <Assignment />, roles: ['Tenant'] },
  { labelKey: 'nav.myCharges', path: '/my-charges', icon: <Payment />, roles: ['Tenant'] },
  { labelKey: 'nav.myWorkOrders', path: '/my-work-orders', icon: <WorkOutline />, roles: ['Vendor'] },
];

const Layout: React.FC = () => {
  const { user, logout, hasAnyRole } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [mobileOpen, setMobileOpen] = useState(false);
  const { t } = useTranslation();

  const visibleItems = navItems.filter(item => hasAnyRole(...item.roles));

  const drawer = (
    <Box sx={{ overflow: 'auto' }}>
      <Box sx={{ p: 2, display: 'flex', alignItems: 'center', gap: 1 }}>
        <Home color="primary" />
        <Typography variant="h6" noWrap sx={{ fontWeight: 700 }}>
          {t('app.brand')}
        </Typography>
      </Box>
      <Divider />
      <Box sx={{ p: 2 }}>
        <Typography variant="body2" color="text.secondary">{user?.fullName}</Typography>
        <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap', mt: 0.5 }}>
          {user?.roles.map(r => (
            <Chip key={r} label={r} size="small" color="primary" variant="outlined" />
          ))}
        </Box>
      </Box>
      <Divider />
      <List>
        {visibleItems.map(item => (
          <ListItemButton
            key={item.path}
            selected={location.pathname === item.path}
            onClick={() => { navigate(item.path); if (isMobile) setMobileOpen(false); }}
          >
            <ListItemIcon>{item.icon}</ListItemIcon>
            <ListItemText primary={t(item.labelKey)} />
          </ListItemButton>
        ))}
      </List>
      <Divider />
      <List>
        <ListItemButton onClick={logout}>
          <ListItemIcon><Logout /></ListItemIcon>
          <ListItemText primary={t('app.logout')} />
        </ListItemButton>
      </List>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar position="fixed" sx={{ zIndex: theme.zIndex.drawer + 1 }}>
        <Toolbar>
          {isMobile && (
            <IconButton color="inherit" onClick={() => setMobileOpen(!mobileOpen)} sx={{ mr: 2 }}>
              <MenuIcon />
            </IconButton>
          )}
          <Typography variant="h6" noWrap sx={{ flexGrow: 1 }}>
            {t('app.title')}
          </Typography>
        </Toolbar>
      </AppBar>

      {isMobile ? (
        <Drawer variant="temporary" open={mobileOpen} onClose={() => setMobileOpen(false)}
          ModalProps={{ keepMounted: true }}
          sx={{ '& .MuiDrawer-paper': { width: DRAWER_WIDTH, top: '56px' } }}>
          {drawer}
        </Drawer>
      ) : (
        <Drawer variant="permanent"
          sx={{ width: DRAWER_WIDTH, flexShrink: 0, '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box' } }}>
          <Toolbar />
          {drawer}
        </Drawer>
      )}

      <Box component="main" sx={{
        flexGrow: 1,
        p: { xs: 1.5, sm: 2, md: 3 },
        mt: { xs: 7, md: 8 },
        width: { xs: '100%', md: `calc(100% - ${DRAWER_WIDTH}px)` },
        overflow: 'hidden',
      }}>
        <Outlet />
      </Box>
    </Box>
  );
};

export default Layout;
