import React, { useState, useEffect } from 'react';
import {
  Typography, Box, Card, CardContent, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Paper, Button, Chip, Alert,
  CircularProgress, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, MenuItem, FormControl, InputLabel, Select, IconButton, Tooltip
} from '@mui/material';
import { Payment, CreditCard, Add, Delete, Star, StarBorder, OpenInNew } from '@mui/icons-material';
import { hoaApi, paymentsApi } from '../../api/services';
import type { UnitChargeDto, PaymentMethodDto, PaymentDto } from '../../types';
import { formatDateLocal } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';

const MyChargesPage: React.FC = () => {
  const { t } = useTranslation();
  const [charges, setCharges] = useState<UnitChargeDto[]>([]);
  const [methods, setMethods] = useState<PaymentMethodDto[]>([]);
  const [payments, setPayments] = useState<PaymentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState('');
  const [msgSeverity, setMsgSeverity] = useState<'success' | 'error' | 'info'>('info');
  const [tab, setTab] = useState<'charges' | 'payments' | 'methods'>('charges');
  const [payDialog, setPayDialog] = useState(false);
  const [payChargeId, setPayChargeId] = useState(0);
  const [payMethodId, setPayMethodId] = useState<number | ''>('');
  const [payAmount, setPayAmount] = useState('');
  const [payMode, setPayMode] = useState<'token' | 'hosted'>('token');
  const [methodDialog, setMethodDialog] = useState(false);
  const [newMethod, setNewMethod] = useState({ methodType: 'CreditCard', cardNumber: '', expiry: '', cvv: '', isDefault: true });

  const loadAll = async () => {
    setLoading(true);
    try {
      const [c, m, p] = await Promise.all([hoaApi.getMyCharges(), paymentsApi.getMethods(), paymentsApi.getMyPayments()]);
      setCharges(c.data); setMethods(m.data); setPayments(p.data);
    } catch { setMsg(t('myCharges.errorLoading')); setMsgSeverity('error'); } finally { setLoading(false); }
  };

  useEffect(() => { loadAll(); }, []);

  const totalBalance = charges.filter(c => c.status !== 'Paid' && c.status !== 'Cancelled').reduce((sum, c) => sum + c.balance, 0);

  const handlePayWithToken = async () => {
    try {
      const r = await paymentsApi.payCharge(payChargeId, { paymentMethodId: payMethodId || undefined, amount: payAmount ? parseFloat(payAmount) : undefined });
      if (r.data.status === 'Succeeded') { setMsg(t('myCharges.paymentSuccess')); setMsgSeverity('success'); } else { setMsg(t('myCharges.paymentFailed')); setMsgSeverity('error'); }
      setPayDialog(false); loadAll();
    } catch { setMsg(t('myCharges.errorProcessing')); setMsgSeverity('error'); }
  };

  const handlePayHosted = async () => {
    try {
      const r = await paymentsApi.createSession(payChargeId);
      if (r.data.paymentUrl) { window.location.href = r.data.paymentUrl; } else { setMsg(r.data.error || t('myCharges.errorSession')); setMsgSeverity('error'); }
      setPayDialog(false);
    } catch { setMsg(t('myCharges.errorSession')); setMsgSeverity('error'); }
  };

  const handleAddCardHosted = async () => {
    try {
      const r = await paymentsApi.startTokenization({ buildingId: 0, isDefault: true });
      if (r.data.redirectUrl) { window.location.href = r.data.redirectUrl; } else if (!r.data.error) { setMsg(t('myCharges.cardAdded')); setMsgSeverity('success'); setMethodDialog(false); loadAll(); } else { setMsg(r.data.error || t('myCharges.errorTokenize')); setMsgSeverity('error'); }
    } catch { setMsg(t('myCharges.errorTokenize')); setMsgSeverity('error'); }
  };

  const handleSetupMethodDirect = async () => {
    try { await paymentsApi.setupMethod(newMethod); setMsg(t('myCharges.methodAdded')); setMsgSeverity('success'); setMethodDialog(false); setNewMethod({ methodType: 'CreditCard', cardNumber: '', expiry: '', cvv: '', isDefault: true }); loadAll(); } catch { setMsg(t('myCharges.errorAddMethod')); setMsgSeverity('error'); }
  };

  const handleDeleteMethod = async (id: number) => {
    if (!window.confirm(t('myCharges.removeConfirm'))) return;
    try { await paymentsApi.deleteMethod(id); loadAll(); } catch { setMsg(t('myCharges.errorRemove')); setMsgSeverity('error'); }
  };

  const handleSetDefault = async (id: number) => {
    try { await paymentsApi.setDefault(id); loadAll(); } catch { setMsg(t('myCharges.errorDefault')); setMsgSeverity('error'); }
  };

  if (loading) return <CircularProgress />;

  const tabLabels: Record<string, string> = { charges: t('myCharges.tabCharges'), payments: t('myCharges.tabPayments'), methods: t('myCharges.tabMethods') };

  return (
    <Box>
      <Typography variant="h4" gutterBottom>{t('myCharges.title')}</Typography>
      {msg && <Alert severity={msgSeverity} onClose={() => setMsg('')} sx={{ mb: 2 }}>{msg}</Alert>}

      <Card sx={{ mb: 3, bgcolor: totalBalance > 0 ? 'error.50' : 'success.50' }}>
        <CardContent sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <Box>
            <Typography variant="body2" color="text.secondary">{t('myCharges.outstandingBalance')}</Typography>
            <Typography variant="h4" color={totalBalance > 0 ? 'error.main' : 'success.main'}>{totalBalance.toFixed(2)} ₪</Typography>
          </Box>
          <Button variant="contained" startIcon={<CreditCard />} onClick={() => setMethodDialog(true)}>{t('myCharges.addPaymentMethod')}</Button>
        </CardContent>
      </Card>

      <Box sx={{ display: 'flex', gap: 1, mb: 2 }}>
        {(['charges', 'payments', 'methods'] as const).map(t2 => (
          <Button key={t2} variant={tab === t2 ? 'contained' : 'outlined'} size="small" onClick={() => setTab(t2)}>{tabLabels[t2]}</Button>
        ))}
      </Box>

      {tab === 'charges' && (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead><TableRow>
              <TableCell>{t('myCharges.period')}</TableCell><TableCell>{t('myCharges.unit')}</TableCell><TableCell align="right">{t('myCharges.due')}</TableCell>
              <TableCell align="right">{t('myCharges.paid')}</TableCell><TableCell align="right">{t('myCharges.balance')}</TableCell>
              <TableCell>{t('myCharges.status')}</TableCell><TableCell>{t('myCharges.dueDate')}</TableCell><TableCell>{t('myCharges.action')}</TableCell>
            </TableRow></TableHead>
            <TableBody>
              {charges.map(c => (
                <TableRow key={c.id}>
                  <TableCell>{c.period}</TableCell><TableCell>{c.unitNumber}</TableCell>
                  <TableCell align="right">{c.amountDue.toFixed(2)}</TableCell><TableCell align="right">{c.amountPaid.toFixed(2)}</TableCell>
                  <TableCell align="right" sx={{ fontWeight: 'bold', color: c.balance > 0 ? 'error.main' : 'success.main' }}>{c.balance.toFixed(2)}</TableCell>
                  <TableCell><Chip label={t(`enums.chargeStatus.${c.status}`, c.status)} size="small" color={c.status === 'Paid' ? 'success' : c.status === 'Overdue' ? 'error' : c.status === 'PartiallyPaid' ? 'warning' : 'default'} /></TableCell>
                  <TableCell>{formatDateLocal(c.dueDate)}</TableCell>
                  <TableCell>
                    {c.balance > 0 && (
                      <Box sx={{ display: 'flex', gap: 0.5 }}>
                        {methods.length > 0 && <Button size="small" startIcon={<Payment />} variant="outlined" onClick={() => { setPayChargeId(c.id); setPayAmount(c.balance.toString()); setPayMethodId(methods.find(m => m.isDefault)?.id || ''); setPayMode('token'); setPayDialog(true); }}>{t('myCharges.pay')}</Button>}
                        <Button size="small" startIcon={<OpenInNew />} variant="contained" color="secondary" onClick={() => { setPayChargeId(c.id); setPayMode('hosted'); setPayDialog(true); }}>{t('myCharges.payOnline')}</Button>
                      </Box>
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {charges.length === 0 && <TableRow><TableCell colSpan={8} align="center">{t('myCharges.noCharges')}</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {tab === 'payments' && (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead><TableRow>
              <TableCell>{t('myCharges.date')}</TableCell><TableCell>{t('myCharges.unit')}</TableCell><TableCell align="right">{t('myCharges.amount')}</TableCell>
              <TableCell>{t('myCharges.card')}</TableCell><TableCell>{t('myCharges.reference')}</TableCell><TableCell>{t('myCharges.status')}</TableCell>
            </TableRow></TableHead>
            <TableBody>
              {payments.map(p => (
                <TableRow key={p.id}>
                  <TableCell>{formatDateLocal(p.paymentDateUtc)}</TableCell><TableCell>{p.unitNumber}</TableCell>
                  <TableCell align="right">{p.amount.toFixed(2)}</TableCell>
                  <TableCell>{p.last4 ? `****${p.last4}` : '—'}</TableCell>
                  <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{p.providerReference || '—'}</TableCell>
                  <TableCell><Chip label={t(`enums.paymentStatus.${p.status}`, p.status)} size="small" color={p.status === 'Succeeded' ? 'success' : p.status === 'Failed' ? 'error' : p.status === 'Pending' ? 'warning' : 'default'} /></TableCell>
                </TableRow>
              ))}
              {payments.length === 0 && <TableRow><TableCell colSpan={6} align="center">{t('myCharges.noPayments')}</TableCell></TableRow>}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {tab === 'methods' && (
        <Box>
          <Box sx={{ display: 'flex', gap: 1, mb: 2 }}><Button startIcon={<Add />} variant="contained" onClick={() => setMethodDialog(true)}>{t('myCharges.addCardHosted')}</Button></Box>
          {methods.map(m => (
            <Card key={m.id} variant="outlined" sx={{ mb: 1 }}>
              <CardContent sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', py: 1, '&:last-child': { pb: 1 } }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                  <CreditCard />
                  <Typography>{m.cardBrand || (m.methodType === 'CreditCard' ? t('myCharges.cardBrand') : t('myCharges.bank'))} ****{m.last4Digits || '****'}</Typography>
                  {m.expiry && <Typography variant="body2" color="text.secondary">{t('myCharges.expiry')} {m.expiry}</Typography>}
                  {m.provider && <Chip label={m.provider} size="small" variant="outlined" />}
                  {m.isDefault && <Chip label={t('myCharges.default')} color="primary" size="small" />}
                </Box>
                <Box>
                  {!m.isDefault && <Tooltip title={t('myCharges.setDefault')}><IconButton size="small" onClick={() => handleSetDefault(m.id)}><StarBorder fontSize="small" /></IconButton></Tooltip>}
                  {m.isDefault && <Star fontSize="small" color="primary" sx={{ mx: 0.5 }} />}
                  <IconButton size="small" color="error" onClick={() => handleDeleteMethod(m.id)}><Delete fontSize="small" /></IconButton>
                </Box>
              </CardContent>
            </Card>
          ))}
          {methods.length === 0 && <Typography color="text.secondary">{t('myCharges.noMethods')}</Typography>}
        </Box>
      )}

      <Dialog open={payDialog} onClose={() => setPayDialog(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{payMode === 'hosted' ? t('myCharges.payOnlineTitle') : t('myCharges.payWithCard')}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
          {payMode === 'hosted' ? (
            <Alert severity="info">{t('myCharges.redirectNote')}</Alert>
          ) : (<>
            <TextField label={t('myCharges.amountIls')} type="number" value={payAmount} onChange={e => setPayAmount(e.target.value)} />
            <FormControl fullWidth>
              <InputLabel>{t('myCharges.paymentMethod')}</InputLabel>
              <Select value={payMethodId} label={t('myCharges.paymentMethod')} onChange={e => setPayMethodId(e.target.value as number)}>
                {methods.map(m => (<MenuItem key={m.id} value={m.id}>{m.cardBrand || m.methodType} ****{m.last4Digits}{m.isDefault ? ` (${t('myCharges.default')})` : ''}</MenuItem>))}
              </Select>
            </FormControl>
          </>)}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPayDialog(false)}>{t('app.cancel')}</Button>
          {payMode === 'hosted' ? (
            <Button variant="contained" color="secondary" onClick={handlePayHosted} startIcon={<OpenInNew />}>{t('myCharges.goToPayment')}</Button>
          ) : (
            <Button variant="contained" color="primary" onClick={handlePayWithToken} startIcon={<Payment />}>{t('myCharges.payNow')}</Button>
          )}
        </DialogActions>
      </Dialog>

      <Dialog open={methodDialog} onClose={() => setMethodDialog(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{t('myCharges.addMethodTitle')}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
          <Alert severity="info"><span dangerouslySetInnerHTML={{ __html: t('myCharges.hostedFlowNote') }} /></Alert>
          <Button variant="contained" color="primary" startIcon={<CreditCard />} onClick={handleAddCardHosted} sx={{ mt: 1 }}>{t('myCharges.addCardHosted')}</Button>
          <Typography variant="body2" color="text.secondary" align="center" sx={{ my: 1 }}>{t('myCharges.orFakeDemo')}</Typography>
          <FormControl fullWidth>
            <InputLabel>{t('myCharges.type')}</InputLabel>
            <Select value={newMethod.methodType} label={t('myCharges.type')} onChange={e => setNewMethod(m => ({ ...m, methodType: e.target.value }))}>
              <MenuItem value="CreditCard">{t('myCharges.creditCard')}</MenuItem>
              <MenuItem value="BankAccount">{t('myCharges.bankAccount')}</MenuItem>
            </Select>
          </FormControl>
          <TextField label={t('myCharges.cardNumber')} value={newMethod.cardNumber} onChange={e => setNewMethod(m => ({ ...m, cardNumber: e.target.value }))} placeholder="4111111111111111" helperText={t('myCharges.fakeWarning')} />
          <Box sx={{ display: 'flex', gap: 2 }}>
            <TextField label={t('myCharges.expiryField')} value={newMethod.expiry} onChange={e => setNewMethod(m => ({ ...m, expiry: e.target.value }))} placeholder="12/28" />
            <TextField label={t('myCharges.cvv')} value={newMethod.cvv} onChange={e => setNewMethod(m => ({ ...m, cvv: e.target.value }))} placeholder="123" />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setMethodDialog(false)}>{t('app.cancel')}</Button>
          <Button variant="outlined" onClick={handleSetupMethodDirect}>{t('myCharges.addDirect')}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default MyChargesPage;
