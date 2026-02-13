import React from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { Box, Paper, Typography, Button, Chip, CircularProgress } from '@mui/material';
import { CheckCircle, HourglassEmpty } from '@mui/icons-material';
import { useTranslation } from 'react-i18next';

const PaymentSuccessPage: React.FC = () => {
  const { t } = useTranslation();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const type = params.get('type');
  const status = params.get('status');
  const providerRef = params.get('provider_ref');
  const isTokenize = type === 'tokenize';
  const isPending = !status || status === 'pending';

  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '80vh' }}>
      <Paper sx={{ p: 4, maxWidth: 500, textAlign: 'center' }}>
        {isPending ? (
          <>
            <HourglassEmpty sx={{ fontSize: 64, color: 'warning.main', mb: 2 }} />
            <Typography variant="h5" gutterBottom>
              {isTokenize ? t('payment.cardPendingTitle') : t('payment.pendingTitle')}
            </Typography>
            <Typography color="text.secondary" sx={{ mb: 2 }}>
              {t('payment.pendingMsg')}
            </Typography>
            <CircularProgress size={24} sx={{ mb: 2 }} />
          </>
        ) : (
          <>
            <CheckCircle sx={{ fontSize: 64, color: 'success.main', mb: 2 }} />
            <Typography variant="h5" gutterBottom>
              {isTokenize ? t('payment.cardAddedTitle') : t('payment.successTitle')}
            </Typography>
            <Typography color="text.secondary" sx={{ mb: 2 }}>
              {isTokenize ? t('payment.successCardMsg') : t('payment.successPayMsg')}
            </Typography>
            {providerRef && <Chip label={t('payment.reference', { ref: providerRef })} variant="outlined" sx={{ mb: 2 }} />}
          </>
        )}
        <Box sx={{ mt: 3, display: 'flex', gap: 2, justifyContent: 'center' }}>
          <Button variant="contained" onClick={() => navigate('/my-charges')}>{t('payment.myCharges')}</Button>
          <Button variant="outlined" onClick={() => navigate('/dashboard')}>{t('payment.dashboard')}</Button>
        </Box>
      </Paper>
    </Box>
  );
};

export default PaymentSuccessPage;
