import React, { useState } from 'react';
import {
  Box, Card, CardContent, TextField, Button, Typography, Alert, CircularProgress,
  IconButton, Menu, MenuItem, ListItemIcon, ListItemText
} from '@mui/material';
import { Settings, Check } from '@mui/icons-material';
import { useAuth } from '../../auth/AuthContext';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

const LANGUAGES = [
  { code: 'he', label: 'עברית', flag: '🇮🇱' },
  { code: 'en', label: 'English', flag: '🇺🇸' },
];

const LoginPage: React.FC = () => {
  const { login, isLoading } = useAuth();
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [langAnchor, setLangAnchor] = useState<null | HTMLElement>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    try {
      await login(email, password);
      navigate('/dashboard');
    } catch (err: any) {
      setError(err.response?.data?.message || t('login.failed'));
    }
  };

  const handleLanguageSelect = (code: string) => {
    i18n.changeLanguage(code);
    localStorage.setItem('lang', code);
    setLangAnchor(null);
  };

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', justifyContent: 'center', alignItems: 'center', bgcolor: '#f5f5f5', p: 2, position: 'relative' }}>
      {/* Language settings button - top corner */}
      <IconButton
        onClick={e => setLangAnchor(e.currentTarget)}
        sx={{
          position: 'absolute',
          top: 16,
          right: 16,
          bgcolor: 'background.paper',
          boxShadow: 1,
          '&:hover': { bgcolor: 'action.hover' },
        }}
        size="medium"
        aria-label="Language settings"
      >
        <Settings />
      </IconButton>
      <Menu
        anchorEl={langAnchor}
        open={Boolean(langAnchor)}
        onClose={() => setLangAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        {LANGUAGES.map(lang => (
          <MenuItem key={lang.code} onClick={() => handleLanguageSelect(lang.code)} selected={i18n.language === lang.code}>
            <ListItemIcon sx={{ fontSize: '1.2rem', minWidth: 32 }}>{lang.flag}</ListItemIcon>
            <ListItemText>{lang.label}</ListItemText>
            {i18n.language === lang.code && <Check fontSize="small" sx={{ ml: 1 }} />}
          </MenuItem>
        ))}
      </Menu>

      <Card sx={{ maxWidth: 420, width: '100%' }}>
        <CardContent sx={{ p: 4 }}>
          <Typography variant="h4" align="center" gutterBottom sx={{ fontWeight: 700 }}>
            {t('login.title')}
          </Typography>
          <Typography variant="body2" align="center" color="text.secondary" sx={{ mb: 3 }}>
            {t('login.subtitle')}
          </Typography>

          {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

          <form onSubmit={handleSubmit}>
            <TextField
              fullWidth label={t('login.email')} type="email" value={email}
              onChange={e => setEmail(e.target.value)} required sx={{ mb: 2 }}
            />
            <TextField
              fullWidth label={t('login.password')} type="password" value={password}
              onChange={e => setPassword(e.target.value)} required sx={{ mb: 3 }}
            />
            <Button fullWidth variant="contained" size="large" type="submit" disabled={isLoading}>
              {isLoading ? <CircularProgress size={24} /> : t('login.signIn')}
            </Button>
          </form>

          <Box sx={{ mt: 3, p: 2, bgcolor: '#f0f4ff', borderRadius: 1 }}>
            <Typography variant="caption" color="text.secondary">
              <strong>{t('login.demoTitle')}</strong><br />
              admin@example.com / Demo@123!<br />
              manager@example.com / Demo@123!<br />
              tenant@example.com / Demo@123!<br />
              vendor@example.com / Demo@123!
            </Typography>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
};

export default LoginPage;
