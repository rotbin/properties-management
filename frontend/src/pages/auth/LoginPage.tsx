import React, { useState } from 'react';
import {
  Box, Card, CardContent, TextField, Button, Typography, Alert, CircularProgress,
  ToggleButtonGroup, ToggleButton
} from '@mui/material';
import { useAuth } from '../../auth/AuthContext';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';

const LoginPage: React.FC = () => {
  const { login, isLoading } = useAuth();
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');

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

  const handleLanguageChange = (_: React.MouseEvent<HTMLElement>, newLang: string | null) => {
    if (newLang) {
      i18n.changeLanguage(newLang);
      localStorage.setItem('lang', newLang);
    }
  };

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', justifyContent: 'center', alignItems: 'center', bgcolor: '#f5f5f5', p: 2 }}>
      <Card sx={{ maxWidth: 420, width: '100%' }}>
        <CardContent sx={{ p: 4 }}>
          {/* Language toggle at top of login */}
          <Box sx={{ display: 'flex', justifyContent: 'center', mb: 2 }}>
            <ToggleButtonGroup
              value={i18n.language}
              exclusive
              onChange={handleLanguageChange}
              size="small"
            >
              <ToggleButton value="he">{t('app.hebrew')}</ToggleButton>
              <ToggleButton value="en">{t('app.english')}</ToggleButton>
            </ToggleButtonGroup>
          </Box>

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
