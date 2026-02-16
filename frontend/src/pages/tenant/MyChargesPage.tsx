import React, { useState, useEffect } from 'react';
import {
  Typography, Box, Card, CardContent, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Paper, Button, Chip, Alert,
  CircularProgress, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, MenuItem, FormControl, InputLabel, Select, IconButton, Tooltip,
  Stack, useMediaQuery, useTheme
} from '@mui/material';
import { Payment, CreditCard, Add, Delete, Star, StarBorder, OpenInNew, Repeat, Pause, PlayArrow, Cancel } from '@mui/icons-material';
import { hoaApi, paymentsApi } from '../../api/services';
import type { UnitChargeDto, PaymentMethodDto, PaymentDto, StandingOrderDto } from '../../types';
import { formatDateLocal } from '../../utils/dateUtils';
import { useTranslation } from 'react-i18next';

const MyChargesPage: React.FC = () => {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [charges, setCharges] = useState<UnitChargeDto[]>([]);
  const [methods, setMethods] = useState<PaymentMethodDto[]>([]);
  const [payments, setPayments] = useState<PaymentDto[]>([]);
  const [standingOrders, setStandingOrders] = useState<StandingOrderDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState('');
  const [msgSeverity, setMsgSeverity] = useState<'success' | 'error' | 'info'>('info');
  const [tab, setTab] = useState<'charges' | 'payments' | 'methods' | 'standingOrders'>('charges');
  const [soDialog, setSoDialog] = useState(false);
  const [soAmount, setSoAmount] = useState('');
  const [payDialog, setPayDialog] = useState(false);
  const [payChargeId, setPayChargeId] = useState(0);
  const [payMethodId, setPayMethodId] = useState<number | ''>('');
  const [payAmount, setPayAmount] = useState('');
  const [payMode, setPayMode] = useState<'token' | 'hosted'>('token');
  const [methodDialog, setMethodDialog] = useState(false);
  const [newMethod, setNewMethod] = useState({ methodType: 'CreditCard', cardNumber: '', expiry: '', cvv: '', bankName: '', branchNumber: '', accountNumber: '', accountHolder: '', isDefault: true });

  const loadAll = async () => {
    setLoading(true);
    try {
      const [c, m, p, so] = await Promise.all([hoaApi.getMyCharges(), paymentsApi.getMethods(), paymentsApi.getMyPayments(), paymentsApi.getStandingOrders()]);
      setCharges(c.data); setMethods(m.data); setPayments(p.data); setStandingOrders(so.data);
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
    try { await paymentsApi.setupMethod(newMethod); setMsg(t('myCharges.methodAdded')); setMsgSeverity('success'); setMethodDialog(false); setNewMethod({ methodType: 'CreditCard', cardNumber: '', expiry: '', cvv: '', bankName: '', branchNumber: '', accountNumber: '', accountHolder: '', isDefault: true }); loadAll(); } catch { setMsg(t('myCharges.errorAddMethod')); setMsgSeverity('error'); }
  };

  const handleDeleteMethod = async (id: number) => {
    if (!window.confirm(t('myCharges.removeConfirm'))) return;
    try { await paymentsApi.deleteMethod(id); loadAll(); } catch { setMsg(t('myCharges.errorRemove')); setMsgSeverity('error'); }
  };

  const handleSetDefault = async (id: number) => {
    try { await paymentsApi.setDefault(id); loadAll(); } catch { setMsg(t('myCharges.errorDefault')); setMsgSeverity('error'); }
  };

  if (loading) return <CircularProgress />;

  const tabLabels: Record<string, string> = { charges: t('myCharges.tabCharges'), payments: t('myCharges.tabPayments'), methods: t('myCharges.tabMethods'), standingOrders: t('myCharges.tabStandingOrders') };

  const handleCreateStandingOrder = async () => {
    if (!charges.length) return;
    const firstCharge = charges.find(c => c.balance > 0);
    if (!firstCharge) { setMsg(t('myCharges.noCharges')); setMsgSeverity('error'); return; }
    try {
      const r = await paymentsApi.createStandingOrder({
        buildingId: firstCharge.unitId, // will be resolved from unit
        unitId: firstCharge.unitId,
        amount: parseFloat(soAmount) || firstCharge.amountDue,
      });
      if (r.data.approvalUrl) {
        window.location.href = r.data.approvalUrl;
      } else if (r.data.error) {
        setMsg(r.data.error); setMsgSeverity('error');
      } else {
        setMsg(t('myCharges.soCreated')); setMsgSeverity('success');
        setSoDialog(false); loadAll();
      }
    } catch (e: any) {
      setMsg(e?.response?.data?.message || t('myCharges.soError')); setMsgSeverity('error');
    }
  };

  const handleCancelSO = async (id: number) => {
    if (!window.confirm(t('myCharges.soCancelConfirm'))) return;
    try { await paymentsApi.cancelStandingOrder(id); setMsg(t('myCharges.soCancelled')); setMsgSeverity('success'); loadAll(); }
    catch { setMsg(t('myCharges.soError')); setMsgSeverity('error'); }
  };

  const handlePauseSO = async (id: number) => {
    try { await paymentsApi.pauseStandingOrder(id); setMsg(t('myCharges.soPaused')); setMsgSeverity('success'); loadAll(); }
    catch { setMsg(t('myCharges.soError')); setMsgSeverity('error'); }
  };

  const handleResumeSO = async (id: number) => {
    try { await paymentsApi.resumeStandingOrder(id); setMsg(t('myCharges.soResumed')); setMsgSeverity('success'); loadAll(); }
    catch { setMsg(t('myCharges.soError')); setMsgSeverity('error'); }
  };

  const soStatusColor = (s: string) => {
    switch (s) {
      case 'Active': return 'success';
      case 'Paused': return 'warning';
      case 'Cancelled': return 'default';
      case 'PaymentFailed': return 'error';
      default: return 'default';
    }
  };

  return (
    <Box>
      <Typography variant="h4" gutterBottom sx={{ fontSize: { xs: '1.3rem', md: '2rem' }, fontWeight: 700 }}>{t('myCharges.title')}</Typography>
      {msg && <Alert severity={msgSeverity} onClose={() => setMsg('')} sx={{ mb: 2 }}>{msg}</Alert>}

      <Card sx={{ mb: 3, bgcolor: totalBalance > 0 ? 'error.50' : 'success.50' }}>
        <CardContent sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 1 }}>
          <Box>
            <Typography variant="body2" color="text.secondary">{t('myCharges.outstandingBalance')}</Typography>
            <Typography variant={isMobile ? 'h5' : 'h4'} color={totalBalance > 0 ? 'error.main' : 'success.main'}>{totalBalance.toFixed(2)} ₪</Typography>
          </Box>
          <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
            <Button variant="contained" startIcon={<CreditCard />} onClick={() => setMethodDialog(true)} size={isMobile ? 'small' : 'medium'}>{t('myCharges.addPaymentMethod')}</Button>
            <Button variant="outlined" startIcon={<Repeat />} onClick={() => setSoDialog(true)} size={isMobile ? 'small' : 'medium'}>{t('myCharges.setupStandingOrder')}</Button>
          </Box>
        </CardContent>
      </Card>

      <Box sx={{ display: 'flex', gap: 1, mb: 2, flexWrap: 'wrap' }}>
        {(['charges', 'payments', 'methods', 'standingOrders'] as const).map(t2 => (
          <Button key={t2} variant={tab === t2 ? 'contained' : 'outlined'} size="small" onClick={() => setTab(t2)}>{tabLabels[t2]}</Button>
        ))}
      </Box>

      {tab === 'charges' && (isMobile ? (
        <Stack spacing={1.5}>
          {charges.map(c => (
            <Card key={c.id} variant="outlined">
              <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                  <Typography variant="subtitle2">{c.period} · {c.unitNumber}</Typography>
                  <Chip label={t(`enums.chargeStatus.${c.status}`, c.status)} size="small" color={c.status === 'Paid' ? 'success' : c.status === 'Overdue' ? 'error' : c.status === 'PartiallyPaid' ? 'warning' : 'default'} />
                </Box>
                <Box sx={{ display: 'flex', justifyContent: 'space-between' }}>
                  <Typography variant="body2">{t('myCharges.due')}: {c.amountDue.toFixed(2)}</Typography>
                  <Typography variant="body2">{t('myCharges.paid')}: {c.amountPaid.toFixed(2)}</Typography>
                  <Typography variant="body2" fontWeight="bold" color={c.balance > 0 ? 'error.main' : 'success.main'}>{c.balance.toFixed(2)}</Typography>
                </Box>
                <Typography variant="caption" color="text.secondary">{t('myCharges.dueDate')}: {formatDateLocal(c.dueDate)}</Typography>
                {c.balance > 0 && (
                  <Box sx={{ display: 'flex', gap: 0.5, mt: 1 }}>
                    {methods.length > 0 && <Button size="small" startIcon={<Payment />} variant="outlined" onClick={() => { setPayChargeId(c.id); setPayAmount(c.balance.toString()); setPayMethodId(methods.find(m => m.isDefault)?.id || ''); setPayMode('token'); setPayDialog(true); }}>{t('myCharges.pay')}</Button>}
                    <Button size="small" startIcon={<OpenInNew />} variant="contained" color="secondary" onClick={() => { setPayChargeId(c.id); setPayMode('hosted'); setPayDialog(true); }}>{t('myCharges.payOnline')}</Button>
                  </Box>
                )}
              </CardContent>
            </Card>
          ))}
          {charges.length === 0 && <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('myCharges.noCharges')}</Typography>}
        </Stack>
      ) : (
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
      ))}

      {tab === 'payments' && (isMobile ? (
        <Stack spacing={1.5}>
          {payments.map(p => (
            <Card key={p.id} variant="outlined">
              <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 0.5 }}>
                  <Typography variant="subtitle2">{p.amount.toFixed(2)} ₪</Typography>
                  <Chip label={t(`enums.paymentStatus.${p.status}`, p.status)} size="small" color={p.status === 'Succeeded' ? 'success' : p.status === 'Failed' ? 'error' : p.status === 'Pending' ? 'warning' : 'default'} />
                </Box>
                <Typography variant="body2">{p.unitNumber} · {p.last4 ? `****${p.last4}` : '—'}</Typography>
                <Typography variant="caption" color="text.secondary">{formatDateLocal(p.paymentDateUtc)}</Typography>
              </CardContent>
            </Card>
          ))}
          {payments.length === 0 && <Typography align="center" color="text.secondary" sx={{ py: 4 }}>{t('myCharges.noPayments')}</Typography>}
        </Stack>
      ) : (
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
      ))}

      {tab === 'methods' && (
        <Box>
          <Box sx={{ display: 'flex', gap: 1, mb: 2 }}><Button startIcon={<Add />} variant="contained" onClick={() => setMethodDialog(true)}>{t('myCharges.addCardHosted')}</Button></Box>
          {methods.map(m => (
            <Card key={m.id} variant="outlined" sx={{ mb: 1 }}>
              <CardContent sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', py: 1, '&:last-child': { pb: 1 }, flexWrap: 'wrap', gap: 0.5 }}>
                <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap', minWidth: 0 }}>
                  <CreditCard />
                  <Typography noWrap>{m.cardBrand || (m.methodType === 'CreditCard' ? t('myCharges.cardBrand') : t('myCharges.bank'))} ****{m.last4Digits || '****'}</Typography>
                  {m.expiry && <Typography variant="body2" color="text.secondary" noWrap>{t('myCharges.expiry')} {m.expiry}</Typography>}
                  {m.provider && <Chip label={m.provider} size="small" variant="outlined" />}
                  {m.isDefault && <Chip label={t('myCharges.default')} color="primary" size="small" />}
                </Box>
                <Box sx={{ display: 'flex', alignItems: 'center' }}>
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

      {tab === 'standingOrders' && (
        <Box>
          <Box sx={{ display: 'flex', gap: 1, mb: 2 }}>
            <Button startIcon={<Repeat />} variant="contained" onClick={() => setSoDialog(true)}>{t('myCharges.setupStandingOrder')}</Button>
          </Box>
          {standingOrders.length === 0 ? (
            <Alert severity="info">{t('myCharges.noStandingOrders')}</Alert>
          ) : (
            <Stack spacing={1.5}>
              {standingOrders.map(so => (
                <Card key={so.id} variant="outlined">
                  <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <Repeat color="primary" />
                        <Typography variant="subtitle1" fontWeight="bold">{so.amount.toFixed(2)} {so.currency}</Typography>
                        <Typography variant="body2" color="text.secondary">/ {t(`myCharges.freq${so.frequency}`, so.frequency)}</Typography>
                      </Box>
                      <Chip label={t(`myCharges.soStatus${so.status}`, so.status)} size="small" color={soStatusColor(so.status) as any} />
                    </Box>
                    <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', mb: 1 }}>
                      <Typography variant="body2">{t('myCharges.unit')}: {so.unitNumber || so.unitId}</Typography>
                      <Typography variant="body2">{t('myCharges.soProvider')}: {so.providerType}</Typography>
                      {so.nextChargeDate && <Typography variant="body2">{t('myCharges.soNextCharge')}: {formatDateLocal(so.nextChargeDate)}</Typography>}
                      {so.lastChargedAtUtc && <Typography variant="body2">{t('myCharges.soLastCharged')}: {formatDateLocal(so.lastChargedAtUtc)}</Typography>}
                    </Box>
                    <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
                      <Typography variant="body2" color="text.secondary">
                        {t('myCharges.soChargeCount', { success: so.successfulCharges, failed: so.failedCharges })}
                      </Typography>
                    </Box>
                    {so.approvalUrl && so.status !== 'Cancelled' && (
                      <Button size="small" variant="outlined" color="primary" startIcon={<OpenInNew />} href={so.approvalUrl} target="_blank" sx={{ mt: 1 }}>
                        {t('myCharges.soApprove')}
                      </Button>
                    )}
                    <Box sx={{ display: 'flex', gap: 0.5, mt: 1 }}>
                      {so.status === 'Active' && (
                        <Button size="small" startIcon={<Pause />} variant="outlined" onClick={() => handlePauseSO(so.id)}>{t('myCharges.soPause')}</Button>
                      )}
                      {so.status === 'Paused' && (
                        <Button size="small" startIcon={<PlayArrow />} variant="outlined" color="success" onClick={() => handleResumeSO(so.id)}>{t('myCharges.soResume')}</Button>
                      )}
                      {(so.status === 'Active' || so.status === 'Paused') && (
                        <Button size="small" startIcon={<Cancel />} variant="outlined" color="error" onClick={() => handleCancelSO(so.id)}>{t('myCharges.soCancel')}</Button>
                      )}
                    </Box>
                  </CardContent>
                </Card>
              ))}
            </Stack>
          )}
        </Box>
      )}

      {/* Standing Order Setup Dialog */}
      <Dialog open={soDialog} onClose={() => setSoDialog(false)} maxWidth="xs" fullWidth>
        <DialogTitle>{t('myCharges.soSetupTitle')}</DialogTitle>
        <DialogContent sx={{ display: 'flex', flexDirection: 'column', gap: 2, mt: 1 }}>
          <Alert severity="info">{t('myCharges.soSetupNote')}</Alert>
          <TextField label={t('myCharges.soAmountMonthly')} type="number" value={soAmount} onChange={e => setSoAmount(e.target.value)} placeholder={charges.length > 0 ? charges[0].amountDue.toString() : '500'} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSoDialog(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" color="primary" startIcon={<Repeat />} onClick={handleCreateStandingOrder}>{t('myCharges.soSetup')}</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={payDialog} onClose={() => setPayDialog(false)} maxWidth="xs" fullWidth fullScreen={isMobile}>
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

      <Dialog open={methodDialog} onClose={() => setMethodDialog(false)} maxWidth="sm" fullWidth fullScreen={isMobile}>
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
          {newMethod.methodType === 'CreditCard' ? (<>
            <TextField label={t('myCharges.cardNumber')} value={newMethod.cardNumber} onChange={e => setNewMethod(m => ({ ...m, cardNumber: e.target.value }))} placeholder="4111111111111111" helperText={t('myCharges.fakeWarning')} />
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField label={t('myCharges.expiryField')} value={newMethod.expiry} onChange={e => setNewMethod(m => ({ ...m, expiry: e.target.value }))} placeholder="12/28" />
              <TextField label={t('myCharges.cvv')} value={newMethod.cvv} onChange={e => setNewMethod(m => ({ ...m, cvv: e.target.value }))} placeholder="123" />
            </Box>
          </>) : (<>
            <TextField label={t('myCharges.accountHolder')} value={newMethod.accountHolder} onChange={e => setNewMethod(m => ({ ...m, accountHolder: e.target.value }))} placeholder={t('myCharges.accountHolderPlaceholder')} />
            <TextField label={t('myCharges.bankName')} value={newMethod.bankName} onChange={e => setNewMethod(m => ({ ...m, bankName: e.target.value }))} placeholder={t('myCharges.bankNamePlaceholder')} />
            <Box sx={{ display: 'flex', gap: 2 }}>
              <TextField label={t('myCharges.branchNumber')} value={newMethod.branchNumber} onChange={e => setNewMethod(m => ({ ...m, branchNumber: e.target.value }))} placeholder="123" />
              <TextField label={t('myCharges.accountNumber')} value={newMethod.accountNumber} onChange={e => setNewMethod(m => ({ ...m, accountNumber: e.target.value }))} placeholder="12345678" />
            </Box>
            <Alert severity="warning" sx={{ mt: 1 }}>{t('myCharges.bankFakeWarning')}</Alert>
          </>)}
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
