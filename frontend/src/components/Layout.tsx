import React, { useState } from 'react';
import {
  AppBar, Toolbar, Typography, Drawer, List, ListItemButton, ListItemIcon,
  ListItemText, Box, IconButton, Divider, Chip, useMediaQuery, useTheme, Avatar
} from '@mui/material';
import {
  Menu as MenuIcon, Dashboard, Business, Engineering, CleaningServices,
  Assignment, Build, Logout, WorkOutline, Schedule,
  AccountBalance, Payment, Settings, BarChart, FactCheck
} from '@mui/icons-material';
import { useNavigate, useLocation, Outlet } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { useTranslation } from 'react-i18next';

const DRAWER_WIDTH = 264;

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
  { labelKey: 'nav.incomeExpenses', path: '/income-expenses', icon: <BarChart />, roles: ['Admin', 'Manager'] },
  { labelKey: 'nav.collectionStatus', path: '/collection-status', icon: <FactCheck />, roles: ['Admin', 'Manager'] },
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

  // Get initials for avatar
  const initials = (user?.fullName || '')
    .split(' ')
    .map(n => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);

  const drawer = (
    <Box sx={{ overflow: 'auto', height: '100%', display: 'flex', flexDirection: 'column' }}>
      {/* Logo area */}
      <Box sx={{ px: 2, py: 1.5, display: 'flex', alignItems: 'center', gap: 1.5 }}>
        <Box
          component="img"
          src="/logo.png"
          alt="HomeHero"
          sx={{ height: 40, width: 'auto', objectFit: 'contain' }}
        />
      </Box>
      <Divider />

      {/* User info */}
      <Box sx={{ px: 2, py: 1.5, display: 'flex', alignItems: 'center', gap: 1.5 }}>
        <Avatar
          sx={{
            width: 36, height: 36, fontSize: '0.85rem', fontWeight: 600,
            bgcolor: 'primary.main', color: 'primary.contrastText',
          }}
        >
          {initials}
        </Avatar>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography variant="body2" fontWeight={600} noWrap>{user?.fullName}</Typography>
          <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
            {user?.roles.map(r => (
              <Chip key={r} label={r} size="small"
                sx={{ height: 20, fontSize: '0.7rem', bgcolor: 'secondary.main', color: 'secondary.contrastText' }}
              />
            ))}
          </Box>
        </Box>
      </Box>
      <Divider />

      {/* Navigation */}
      <Box sx={{ flex: 1, overflow: 'auto', py: 1 }}>
        <List disablePadding>
          {visibleItems.map(item => (
            <ListItemButton
              key={item.path}
              selected={location.pathname === item.path}
              onClick={() => { navigate(item.path); if (isMobile) setMobileOpen(false); }}
              sx={{ py: 0.75 }}
            >
              <ListItemIcon sx={{ minWidth: 40, color: location.pathname === item.path ? 'primary.main' : 'text.secondary' }}>
                {item.icon}
              </ListItemIcon>
              <ListItemText
                primary={t(item.labelKey)}
                primaryTypographyProps={{
                  variant: 'body2',
                  fontWeight: location.pathname === item.path ? 600 : 400,
                  color: location.pathname === item.path ? 'primary.main' : 'text.primary',
                }}
              />
            </ListItemButton>
          ))}
        </List>
      </Box>

      {/* Logout */}
      <Divider />
      <List disablePadding sx={{ py: 1 }}>
        <ListItemButton onClick={logout} sx={{ py: 0.75 }}>
          <ListItemIcon sx={{ minWidth: 40, color: 'text.secondary' }}><Logout /></ListItemIcon>
          <ListItemText primary={t('app.logout')} primaryTypographyProps={{ variant: 'body2' }} />
        </ListItemButton>
      </List>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
      <AppBar
        position="fixed"
        sx={{
          zIndex: theme.zIndex.drawer + 1,
          background: 'linear-gradient(90deg, #1a56a0 0%, #123d73 100%)',
        }}
      >
        <Toolbar variant="dense" sx={{ minHeight: { xs: 52, md: 56 } }}>
          {isMobile && (
            <IconButton color="inherit" onClick={() => setMobileOpen(!mobileOpen)} edge="start" sx={{ mr: 1 }}>
              <MenuIcon />
            </IconButton>
          )}
          {isMobile && (
            <Box
              component="img"
              src="/logo.png"
              alt="HomeHero"
              sx={{ height: 32, width: 'auto', filter: 'brightness(0) invert(1)', objectFit: 'contain' }}
            />
          )}
          <Box sx={{ flexGrow: 1 }} />
        </Toolbar>
      </AppBar>

      {isMobile ? (
        <Drawer variant="temporary" open={mobileOpen} onClose={() => setMobileOpen(false)}
          ModalProps={{ keepMounted: true }}
          sx={{
            '& .MuiDrawer-paper': {
              width: DRAWER_WIDTH,
              top: '52px',
              height: 'calc(100% - 52px)',
              bgcolor: 'background.paper',
            },
          }}>
          {drawer}
        </Drawer>
      ) : (
        <Drawer variant="permanent"
          sx={{
            width: DRAWER_WIDTH, flexShrink: 0,
            '& .MuiDrawer-paper': {
              width: DRAWER_WIDTH,
              boxSizing: 'border-box',
              bgcolor: 'background.paper',
              borderRight: '1px solid',
              borderColor: 'divider',
            },
          }}>
          <Toolbar variant="dense" sx={{ minHeight: { md: 56 } }} />
          {drawer}
        </Drawer>
      )}

      <Box component="main" sx={{
        flexGrow: 1,
        p: { xs: 1.5, sm: 2, md: 3 },
        mt: { xs: '52px', md: '56px' },
        width: { xs: '100%', md: `calc(100% - ${DRAWER_WIDTH}px)` },
        overflow: 'hidden',
      }}>
        <Outlet />
      </Box>
    </Box>
  );
};

export default Layout;
