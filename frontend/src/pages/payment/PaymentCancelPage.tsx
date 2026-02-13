import React from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { Box, Paper, Typography, Button } from '@mui/material';
import { Cancel } from '@mui/icons-material';
import { useTranslation } from 'react-i18next';

const PaymentCancelPage: React.FC = () => {
  const { t } = useTranslation();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const isTokenize = params.get('type') === 'tokenize';

  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '80vh' }}>
      <Paper sx={{ p: 4, maxWidth: 500, textAlign: 'center' }}>
        <Cancel sx={{ fontSize: 64, color: 'error.main', mb: 2 }} />
        <Typography variant="h5" gutterBottom>
          {isTokenize ? t('payment.cardCancelTitle') : t('payment.cancelTitle')}
        </Typography>
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          {isTokenize ? t('payment.cancelCardMsg') : t('payment.cancelPayMsg')}
        </Typography>
        <Box sx={{ mt: 3, display: 'flex', gap: 2, justifyContent: 'center' }}>
          <Button variant="contained" onClick={() => navigate('/my-charges')}>{t('app.tryAgain')}</Button>
          <Button variant="outlined" onClick={() => navigate('/dashboard')}>{t('payment.dashboard')}</Button>
        </Box>
      </Paper>
    </Box>
  );
};

export default PaymentCancelPage;
