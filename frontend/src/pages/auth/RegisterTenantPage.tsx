import React, { useState, useEffect, useCallback } from 'react';
import {
  Box, Card, CardContent, TextField, Button, Typography, Alert, CircularProgress,
  IconButton, Menu, MenuItem, ListItemIcon, ListItemText, Divider,
  Stepper, Step, StepLabel, FormControlLabel, Checkbox, Autocomplete,
  FormControl, InputLabel, Select, Dialog, DialogTitle, DialogContent, DialogActions,
  Link as MuiLink
} from '@mui/material';
import { Language, Check } from '@mui/icons-material';
import { useNavigate, Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { authApi } from '../../api/services';
import { setTokens } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';

const LANGUAGES = [
  { code: 'he', label: 'עברית', flag: '🇮🇱' },
  { code: 'en', label: 'English', flag: '🇺🇸' },
];

interface BuildingOption {
  id: number;
  name: string;
  addressLine?: string;
  city?: string;
}

const PROPERTY_ROLES = [
  { value: 0, labelKey: 'registerTenant.roleOwner' },
  { value: 1, labelKey: 'registerTenant.roleLandlord' },
  { value: 2, labelKey: 'registerTenant.roleRenter' },
];

const RegisterTenantPage: React.FC = () => {
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const { login } = useAuth();

  // Step state
  const [activeStep, setActiveStep] = useState(0);

  // Screen 1: Personal details
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [marketingConsent, setMarketingConsent] = useState(false);
  const [termsAccepted, setTermsAccepted] = useState(false);
  const [termsDialogOpen, setTermsDialogOpen] = useState(false);

  // Screen 2: Address & property
  const [buildingSearch, setBuildingSearch] = useState('');
  const [buildingOptions, setBuildingOptions] = useState<BuildingOption[]>([]);
  const [selectedBuilding, setSelectedBuilding] = useState<BuildingOption | null>(null);
  const [buildingLoading, setBuildingLoading] = useState(false);
  const [floor, setFloor] = useState('');
  const [apartmentNumber, setApartmentNumber] = useState('');
  const [propertyRole, setPropertyRole] = useState(2); // Renter default
  const [isCommitteeMember, setIsCommitteeMember] = useState(false);

  // General
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [langAnchor, setLangAnchor] = useState<null | HTMLElement>(null);

  // Building autocomplete search
  const searchBuildings = useCallback(async (query: string) => {
    if (query.length < 2) {
      setBuildingOptions([]);
      return;
    }
    setBuildingLoading(true);
    try {
      const res = await authApi.searchBuildings(query);
      setBuildingOptions(res.data);
    } catch {
      setBuildingOptions([]);
    } finally {
      setBuildingLoading(false);
    }
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => {
      if (buildingSearch.length >= 2) {
        searchBuildings(buildingSearch);
      }
    }, 300);
    return () => clearTimeout(timer);
  }, [buildingSearch, searchBuildings]);

  const validateStep1 = (): boolean => {
    if (!firstName.trim() || !lastName.trim() || !email.trim() || !password) {
      setError(t('registerTenant.fillRequired'));
      return false;
    }
    if (password !== confirmPassword) {
      setError(t('register.passwordMismatch'));
      return false;
    }
    if (!termsAccepted) {
      setError(t('registerTenant.mustAcceptTerms'));
      return false;
    }
    setError('');
    return true;
  };

  const handleNext = () => {
    if (activeStep === 0 && validateStep1()) {
      setActiveStep(1);
    }
  };

  const handleBack = () => {
    setError('');
    setActiveStep(0);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (!selectedBuilding) {
      setError(t('registerTenant.selectBuilding'));
      return;
    }
    if (!apartmentNumber.trim()) {
      setError(t('registerTenant.fillRequired'));
      return;
    }

    setLoading(true);
    try {
      const response = await authApi.registerTenant({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        email: email.trim(),
        password,
        phone: phone.trim() || undefined,
        marketingConsent,
        termsAccepted,
        buildingId: selectedBuilding.id,
        floor: floor ? parseInt(floor, 10) : undefined,
        apartmentNumber: apartmentNumber.trim(),
        propertyRole,
        isCommitteeMember,
      });
      setTokens(response.data.accessToken, response.data.refreshToken);
      await login(email, password);
      navigate('/my-requests');
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

  const steps = [t('registerTenant.stepPersonal'), t('registerTenant.stepAddress')];

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
        maxWidth: 500, width: '100%', position: 'relative', zIndex: 1,
        borderRadius: 3, overflow: 'visible',
        boxShadow: '0 20px 60px rgba(0,0,0,0.3), 0 1px 3px rgba(0,0,0,0.1)',
      }}>
        <CardContent sx={{ p: { xs: 3, sm: 4 } }}>
          <Box sx={{ display: 'flex', justifyContent: 'center', mb: 1 }}>
            <Box
              component="img"
              src="/logo.png"
              alt="HomeHero"
              sx={{ height: { xs: 56, sm: 72 }, width: 'auto', objectFit: 'contain' }}
            />
          </Box>

          <Typography variant="h6" align="center" sx={{ mb: 0.5, fontWeight: 700 }}>
            {t('registerTenant.title')}
          </Typography>
          <Typography variant="body2" align="center" color="text.secondary" sx={{ mb: 2 }}>
            {t('registerTenant.subtitle')}
          </Typography>

          <Stepper activeStep={activeStep} alternativeLabel sx={{ mb: 3 }}>
            {steps.map((label) => (
              <Step key={label}>
                <StepLabel>{label}</StepLabel>
              </Step>
            ))}
          </Stepper>

          {error && <Alert severity="error" sx={{ mb: 2, borderRadius: 2 }}>{error}</Alert>}

          {/* ─── STEP 1: Personal Details ─── */}
          {activeStep === 0 && (
            <Box>
              <Box sx={{ display: 'flex', gap: 2, mb: 2 }}>
                <TextField
                  fullWidth label={t('registerTenant.firstName')} value={firstName}
                  onChange={e => setFirstName(e.target.value)} required
                  size="medium" InputLabelProps={{ shrink: true }}
                />
                <TextField
                  fullWidth label={t('registerTenant.lastName')} value={lastName}
                  onChange={e => setLastName(e.target.value)} required
                  size="medium" InputLabelProps={{ shrink: true }}
                />
              </Box>
              <TextField
                fullWidth label={t('registerTenant.phone')} value={phone}
                onChange={e => setPhone(e.target.value)}
                sx={{ mb: 2 }} size="medium"
                InputLabelProps={{ shrink: true }}
                slotProps={{ htmlInput: { dir: 'ltr', style: { textAlign: 'left' } } }}
                placeholder="050-1234567"
              />
              <TextField
                fullWidth label={t('registerTenant.email')} type="email" value={email}
                onChange={e => setEmail(e.target.value)} required
                sx={{ mb: 2 }} size="medium"
                InputLabelProps={{ shrink: true }}
                slotProps={{ htmlInput: { dir: 'ltr', style: { textAlign: 'left' } } }}
              />
              <TextField
                fullWidth label={t('registerTenant.password')} type="password" value={password}
                onChange={e => setPassword(e.target.value)} required
                sx={{ mb: 2 }} size="medium"
                InputLabelProps={{ shrink: true }}
                slotProps={{ htmlInput: { dir: 'ltr', style: { textAlign: 'left' } } }}
              />
              <TextField
                fullWidth label={t('registerTenant.confirmPassword')} type="password" value={confirmPassword}
                onChange={e => setConfirmPassword(e.target.value)} required
                sx={{ mb: 2 }} size="medium"
                InputLabelProps={{ shrink: true }}
                slotProps={{ htmlInput: { dir: 'ltr', style: { textAlign: 'left' } } }}
              />

              <FormControlLabel
                control={<Checkbox checked={marketingConsent} onChange={e => setMarketingConsent(e.target.checked)} />}
                label={t('registerTenant.marketingConsent')}
                sx={{ mb: 1, display: 'flex' }}
              />

              <FormControlLabel
                control={<Checkbox checked={termsAccepted} onChange={e => setTermsAccepted(e.target.checked)} />}
                label={
                  <Typography variant="body2">
                    {t('registerTenant.iAccept')}{' '}
                    <MuiLink
                      component="button"
                      type="button"
                      variant="body2"
                      onClick={(e) => { e.preventDefault(); setTermsDialogOpen(true); }}
                      sx={{ fontWeight: 600 }}
                    >
                      {t('registerTenant.termsOfUse')}
                    </MuiLink>
                  </Typography>
                }
                sx={{ mb: 2, display: 'flex' }}
              />

              <Button
                fullWidth variant="contained" size="large" onClick={handleNext}
                sx={{
                  py: 1.5, fontSize: '1rem',
                  background: 'linear-gradient(135deg, #1a56a0 0%, #2d6fbe 100%)',
                  '&:hover': { background: 'linear-gradient(135deg, #123d73 0%, #1a56a0 100%)' },
                }}
              >
                {t('registerTenant.next')}
              </Button>
            </Box>
          )}

          {/* ─── STEP 2: Address & Property ─── */}
          {activeStep === 1 && (
            <form onSubmit={handleSubmit}>
              <Autocomplete
                options={buildingOptions}
                getOptionLabel={(opt) => {
                  const parts = [opt.name];
                  if (opt.addressLine) parts.push(opt.addressLine);
                  if (opt.city) parts.push(opt.city);
                  return parts.join(' – ');
                }}
                value={selectedBuilding}
                onChange={(_, val) => setSelectedBuilding(val)}
                onInputChange={(_, val) => setBuildingSearch(val)}
                loading={buildingLoading}
                noOptionsText={buildingSearch.length < 2 ? t('registerTenant.typeToSearch') : t('registerTenant.noResults')}
                renderInput={(params) => (
                  <TextField
                    {...params}
                    label={t('registerTenant.address')}
                    required
                    InputLabelProps={{ shrink: true }}
                    slotProps={{
                      input: {
                        ...params.InputProps,
                        endAdornment: (
                          <>
                            {buildingLoading ? <CircularProgress color="inherit" size={20} /> : null}
                            {params.InputProps.endAdornment}
                          </>
                        ),
                      },
                    }}
                  />
                )}
                sx={{ mb: 2 }}
                isOptionEqualToValue={(opt, val) => opt.id === val.id}
              />

              <Box sx={{ display: 'flex', gap: 2, mb: 2 }}>
                <TextField
                  fullWidth label={t('registerTenant.floor')} value={floor}
                  onChange={e => setFloor(e.target.value)}
                  type="number" size="medium"
                  InputLabelProps={{ shrink: true }}
                />
                <TextField
                  fullWidth label={t('registerTenant.apartmentNumber')} value={apartmentNumber}
                  onChange={e => setApartmentNumber(e.target.value)} required
                  size="medium"
                  InputLabelProps={{ shrink: true }}
                />
              </Box>

              <FormControl fullWidth sx={{ mb: 2 }}>
                <InputLabel shrink>{t('registerTenant.propertyRole')}</InputLabel>
                <Select
                  value={propertyRole}
                  onChange={e => setPropertyRole(Number(e.target.value))}
                  label={t('registerTenant.propertyRole')}
                  notched
                >
                  {PROPERTY_ROLES.map(role => (
                    <MenuItem key={role.value} value={role.value}>
                      {t(role.labelKey)}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              <FormControlLabel
                control={<Checkbox checked={isCommitteeMember} onChange={e => setIsCommitteeMember(e.target.checked)} />}
                label={t('registerTenant.isCommitteeMember')}
                sx={{ mb: 3, display: 'flex' }}
              />

              <Box sx={{ display: 'flex', gap: 2 }}>
                <Button
                  fullWidth variant="outlined" size="large" onClick={handleBack}
                  sx={{ py: 1.5 }}
                >
                  {t('registerTenant.back')}
                </Button>
                <Button
                  fullWidth variant="contained" size="large" type="submit" disabled={loading}
                  sx={{
                    py: 1.5, fontSize: '1rem',
                    background: 'linear-gradient(135deg, #1a56a0 0%, #2d6fbe 100%)',
                    '&:hover': { background: 'linear-gradient(135deg, #123d73 0%, #1a56a0 100%)' },
                  }}
                >
                  {loading ? <CircularProgress size={24} color="inherit" /> : t('registerTenant.signUp')}
                </Button>
              </Box>
            </form>
          )}

          <Divider sx={{ my: 2 }} />

          <Typography variant="body2" align="center" color="text.secondary">
            {t('register.haveAccount')}{' '}
            <Typography component={Link} to="/login" variant="body2" color="primary" sx={{ fontWeight: 600, textDecoration: 'none' }}>
              {t('register.signIn')}
            </Typography>
          </Typography>
        </CardContent>
      </Card>

      {/* Terms of Use Dialog */}
      <TermsOfUseDialog open={termsDialogOpen} onClose={() => setTermsDialogOpen(false)} />

      <Typography
        variant="caption"
        sx={{ position: 'absolute', bottom: 16, color: 'rgba(255,255,255,0.5)', textAlign: 'center' }}
      >
        HomeHero Property Management
      </Typography>
    </Box>
  );
};

// ─── Terms of Use Dialog ───
const TermsOfUseDialog: React.FC<{ open: boolean; onClose: () => void }> = ({ open, onClose }) => {
  const { t, i18n } = useTranslation();
  const isHe = i18n.language === 'he';

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth scroll="paper">
      <DialogTitle sx={{ fontWeight: 700 }}>
        {t('registerTenant.termsOfUse')}
      </DialogTitle>
      <DialogContent dividers sx={{ direction: isHe ? 'rtl' : 'ltr' }}>
        {isHe ? <TermsContentHebrew /> : <TermsContentEnglish />}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} variant="contained">
          {t('app.close')}
        </Button>
      </DialogActions>
    </Dialog>
  );
};

const TermsContentHebrew: React.FC = () => (
  <Box sx={{ fontSize: '0.9rem', lineHeight: 1.8 }}>
    <Typography variant="h6" gutterBottom sx={{ fontWeight: 700 }}>תקנון שימוש באפליקציה / אתר HomeHero</Typography>

    <Typography paragraph>
      אנו מודים לך על שבחרת להיכנס לאפליקציית או אתר HomeHero (להלן - "האפליקציה" או "האתר").
      השימוש באתר ובאפליקציה מוצע בכפוף לתקנון להלן וכן, השימוש באפליקציה מהווה הסכמתך לתנאים אלו.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>1. מבוא והגדרות</Typography>
    <Typography paragraph>
      "הבניין" – בית משותף או נכס מסוים.{' '}
      "הועד" – נציגות הדיירים/בעלי הדירות של הבניין.{' '}
      "חברת הניהול" – חברה ו/או עוסק ו/או כל גוף אחר המשמש כמתחזק של הבניין.{' '}
      "האפליקציה" – התוכנה המופעלת על טלפון נייד או אתר אינטרנט.{' '}
      "דייר" – אדם או תאגיד המחזיק בדירה או יחידה בבניין.{' '}
      "גולש" – כל מי שמשתמש באפליקציה או באתר.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>2. מהות השימוש באפליקציה</Typography>
    <Typography paragraph>
      האפליקציה הינה פלטפורמה לניהול אחזקת מבנים עבור חברות ניהול בניינים וועדי בתים.
      האפליקציה מאפשרת לועד הבית ו/או לחברת הניהול לנהל בצורה מקצועית וקלה את הבית ובכלל זה תשלומים, ניהול תקלות וביקורת אחזקה.
      האפליקציה מאפשרת לדייר לדווח על תקלות, לשלם את דמי ועד הבית השוטפים ותשלומים נוספים.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>3. הרשאה לשימוש</Typography>
    <Typography paragraph>
      בכדי להשתמש באפליקציה, יהא על הגולש להזין את פרטיו האישיים: שם פרטי, שם משפחה, כתובת דואר-אלקטרוני, מספר טלפון נייד, כתובת.
      בהזנת פרטים אלו נותן הגולש אישור לשמור נתונים אלו ולהעבירם לצד שלישי לצורך מתן השירותים.
      מסירת פרטים אישיים כוזבים עלולה להוות עבירה פלילית.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>4. תשלומים</Typography>
    <Typography paragraph>
      המערכת מאפשרת לשלם תשלומים שונים לוועד הבית ו/או לחברת הניהול באמצעות האפליקציה.
      שיעור סכומי התשלום נמסרים על ידי הגורם המוסמך ולא תשמע כל טענה ביחס לסכומים הללו.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>5. אחריות</Typography>
    <Typography paragraph>
      השימוש באפליקציה הינו באחריות הגולש בלבד.
      האפליקציה אינה אחראית לכל נזק ו/או הוצאה ו/או הפסד ו/או פגיעה ישירים ו/או עקיפים שייגרמו לגולשים.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>6. קניין רוחני</Typography>
    <Typography paragraph>
      זכויות היוצרים, סימני המסחר וזכויות הקניין הרוחני באפליקציה ובאתר שייכים בלעדית ל-HomeHero.
      אין להעתיק, לשנות, לפרסם, או לעשות שימוש מסחרי ללא הסכמה מפורשת מראש ובכתב.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>7. מדיניות פרטיות</Typography>
    <Typography paragraph>
      במסירת המידע הנדרש, הגולש מסכים לאיסוף, שימוש וחשיפת המידע בהתאם לתנאי תקנון זה.
      המידע שנאסף עשוי להישמר במאגר מידע וישמש לצורך אישור שימוש, יצירת קשר, שיפור השירות והתאמת תוכן.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>8. דין ומקום שיפוט</Typography>
    <Typography paragraph>
      על השימוש באתר ו/או באפליקציה יחולו דיני מדינת ישראל בלבד.
    </Typography>
  </Box>
);

const TermsContentEnglish: React.FC = () => (
  <Box sx={{ fontSize: '0.9rem', lineHeight: 1.8 }}>
    <Typography variant="h6" gutterBottom sx={{ fontWeight: 700 }}>HomeHero Application / Website Terms of Use</Typography>

    <Typography paragraph>
      Thank you for choosing to use the HomeHero application or website (the "Application" or "Website").
      Use of the website and application is offered subject to the following terms, and use of the application constitutes your agreement to these terms.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>1. Introduction and Definitions</Typography>
    <Typography paragraph>
      "Building" – a condominium or specific property.{' '}
      "Committee" – the residents' / owners' representative body.{' '}
      "Management Company" – any entity serving as the building's maintainer.{' '}
      "Application" – software operated on a mobile phone or website.{' '}
      "Resident" – a person or entity holding an apartment or unit in a building.{' '}
      "User" – anyone who uses the application or website.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>2. Purpose of Use</Typography>
    <Typography paragraph>
      The application is a platform for building maintenance management for building management companies and house committees.
      It enables the committee and/or management company to professionally manage the building including payments, fault management, and maintenance inspections.
      Residents can report faults, pay monthly HOA fees, and make additional payments.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>3. Authorization for Use</Typography>
    <Typography paragraph>
      To use the application, the user must enter their personal details: first name, last name, email address, mobile phone number, and address.
      By entering these details, the user authorizes storing this data and transferring it to third parties for the purpose of providing services.
      Providing false personal information may constitute a criminal offense.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>4. Payments</Typography>
    <Typography paragraph>
      The system allows making various payments to the house committee and/or management company through the application.
      Payment amounts are provided by the authorized party and no claim shall be made regarding these amounts.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>5. Liability</Typography>
    <Typography paragraph>
      Use of the application is at the user's sole responsibility.
      The application is not responsible for any direct or indirect damage, expense, loss, or harm caused to users.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>6. Intellectual Property</Typography>
    <Typography paragraph>
      Copyrights, trademarks, and intellectual property rights in the application and website belong exclusively to HomeHero.
      Copying, modifying, publishing, or making commercial use without prior written consent is prohibited.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>7. Privacy Policy</Typography>
    <Typography paragraph>
      By providing the required information, the user agrees to the collection, use, and disclosure of information in accordance with these terms.
      Collected information may be stored in a database and used for verification, contact, service improvement, and content personalization.
    </Typography>

    <Typography variant="subtitle1" gutterBottom sx={{ fontWeight: 600 }}>8. Governing Law</Typography>
    <Typography paragraph>
      The use of the website and/or application shall be governed exclusively by the laws of the State of Israel.
    </Typography>
  </Box>
);

export default RegisterTenantPage;
