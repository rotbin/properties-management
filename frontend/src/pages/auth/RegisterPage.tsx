import React, { useState } from 'react';
import {
  Box, Card, CardContent, TextField, Button, Typography, Alert, CircularProgress,
  IconButton, Menu, MenuItem, ListItemIcon, ListItemText, Divider
} from '@mui/material';
import { Language, Check } from '@mui/icons-material';
import { useNavigate, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { authApi } from '../../api/services';
import { setTokens } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { validatePassword } from '../../utils/passwordValidation';

const LANGUAGES = [
  { code: 'he', label: 'עברית', flag: '🇮🇱' },
  { code: 'en', label: 'English', flag: '🇺🇸' },
];

const RegisterPage: React.FC = () => {
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const { login } = useAuth();
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [phone, setPhone] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [langAnchor, setLangAnchor] = useState<null | HTMLElement>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    const pwCheck = validatePassword(password, t);
    if (pwCheck.hasError) {
      setError(pwCheck.message);
      return;
    }

    if (password !== confirmPassword) {
      setError(t('register.passwordMismatch'));
      return;
    }

    setLoading(true);
    try {
      const response = await authApi.register({
        fullName,
        email,
        password,
        phone: phone || undefined,
      });
      // Auto-login with returned tokens
      setTokens(response.data.accessToken, response.data.refreshToken);
      // Use login to set user context (re-login with credentials)
      await login(email, password);
      navigate('/dashboard');
    } catch (err: any) {
      const msg = err.response?.data?.message || t('register.failed');
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const handleLanguageSelect = (code: string) => {
    i18n.changeLanguage(code);
    localStorage.setItem('lang', code);
    setLangAnchor(null);
  };

  return (
    <Box sx={{
      minHeight: '100vh',
      display: 'flex',
      justifyContent: 'center',
      alignItems: 'center',
      background: 'linear-gradient(135deg, #1a56a0 0%, #123d73 50%, #0d2b52 100%)',
      p: 2,
      position: 'relative',
    }}>
      <Box sx={{
        position: 'absolute', top: 0, left: 0, right: 0, height: '40%',
        background: 'linear-gradient(180deg, rgba(245,145,30,0.15) 0%, transparent 100%)',
        pointerEvents: 'none',
      }} />

      <IconButton
        onClick={e => setLangAnchor(e.currentTarget)}
        sx={{
          position: 'absolute', top: 16, right: 16,
          color: 'rgba(255,255,255,0.8)',
          '&:hover': { color: '#fff', bgcolor: 'rgba(255,255,255,0.1)' },
        }}
        size="medium"
        aria-label="Language settings"
      >
        <Language />
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

      <Card sx={{
        maxWidth: 440, width: '100%', position: 'relative', zIndex: 1,
        borderRadius: 3, overflow: 'visible',
        boxShadow: '0 20px 60px rgba(0,0,0,0.3), 0 1px 3px rgba(0,0,0,0.1)',
      }}>
        <CardContent sx={{ p: { xs: 3, sm: 4 } }}>
          <Box sx={{ display: 'flex', justifyContent: 'center', mb: 1 }}>
            <Box
              component="img"
              src="/logo.png"
              alt="HomeHero"
              sx={{ height: { xs: 64, sm: 80 }, width: 'auto', objectFit: 'contain' }}
            />
          </Box>

          <Typography variant="h6" align="center" sx={{ mb: 0.5, fontWeight: 700 }}>
            {t('register.title')}
          </Typography>
          <Typography variant="body2" align="center" color="text.secondary" sx={{ mb: 3 }}>
            {t('register.subtitle')}
          </Typography>

          {error && <Alert severity="error" sx={{ mb: 2, borderRadius: 2 }}>{error}</Alert>}

          <form onSubmit={handleSubmit}>
            <TextField
              fullWidth label={t('register.fullName')} value={fullName}
              onChange={e => setFullName(e.target.value)} required
              sx={{ mb: 2 }} size="medium"
              InputLabelProps={{ shrink: true }}
            />
            <TextField
              fullWidth label={t('register.email')} type="email" value={email}
              onChange={e => setEmail(e.target.value)} required
              sx={{ mb: 2 }} size="medium"
              InputLabelProps={{ shrink: true }}
              slotProps={{ htmlInput: { dir: 'ltr', style: { textAlign: 'left' } } }}
            />
            <TextField
              fullWidth label={t('register.phone')} value={phone}
              onChange={e => setPhone(e.target.value)}
              sx={{ mb: 2 }} size="medium"
              InputLabelProps={{ shrink: true }}
              slotProps={{ htmlInput: { dir: 'ltr', style: { textAlign: 'left' } } }}
              placeholder="050-1234567"
            />
            <TextField
              fullWidth label={t('register.password')} type="password" value={password}
              onChange={e => setPassword(e.target.value)} required
              sx={{ mb: 2 }} size="medium"
              InputLabelProps={{ shrink: true }}
              slotProps={{ htmlInput: { dir: 'ltr', style: { textAlign: 'left' } } }}
            />
            <TextField
              fullWidth label={t('register.confirmPassword')} type="password" value={confirmPassword}
              onChange={e => setConfirmPassword(e.target.value)} required
              sx={{ mb: 3 }} size="medium"
              InputLabelProps={{ shrink: true }}
              slotProps={{ htmlInput: { dir: 'ltr', style: { textAlign: 'left' } } }}
            />
            <Button
              fullWidth variant="contained" size="large" type="submit" disabled={loading}
              sx={{
                py: 1.5, fontSize: '1rem',
                background: 'linear-gradient(135deg, #1a56a0 0%, #2d6fbe 100%)',
                '&:hover': { background: 'linear-gradient(135deg, #123d73 0%, #1a56a0 100%)' },
              }}
            >
              {loading ? <CircularProgress size={24} color="inherit" /> : t('register.signUp')}
            </Button>
          </form>

          <Divider sx={{ my: 2 }} />

          <Typography variant="body2" align="center" color="text.secondary" sx={{ mb: 1 }}>
            {t('register.haveAccount')}{' '}
            <Typography component={Link} to="/login" variant="body2" color="primary" sx={{ fontWeight: 600, textDecoration: 'none' }}>
              {t('register.signIn')}
            </Typography>
          </Typography>
          <Typography variant="body2" align="center" color="text.secondary">
            <Typography component={Link} to="/register-tenant" variant="body2" color="primary" sx={{ fontWeight: 600, textDecoration: 'none' }}>
              {t('login.signUpTenant')}
            </Typography>
          </Typography>
        </CardContent>
      </Card>

      <Typography
        variant="caption"
        sx={{ position: 'absolute', bottom: 16, color: 'rgba(255,255,255,0.5)', textAlign: 'center' }}
      >
        HomeHero Property Management
      </Typography>
    </Box>
  );
};

export default RegisterPage;
