import React, { useEffect, useState } from 'react';
import {
  Box, Typography, Button, Table, TableBody, TableCell, TableContainer,
  TableHead, TableRow, Paper, Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, IconButton, Chip, Alert, CircularProgress, Collapse,
  useMediaQuery, useTheme, Stack, Card, CardContent, CardActionArea,
} from '@mui/material';
import { Add, Edit, Delete, ExpandMore, ExpandLess } from '@mui/icons-material';
import { buildingsApi } from '../../api/services';
import type { BuildingDto, UnitDto } from '../../types';
import { useTranslation } from 'react-i18next';

const BuildingsPage: React.FC = () => {
  const { t } = useTranslation();
  const theme = useTheme();
  const isMobile = useMediaQuery(theme.breakpoints.down('md'));
  const [buildings, setBuildings] = useState<BuildingDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [buildingDialogOpen, setBuildingDialogOpen] = useState(false);
  const [unitDialogOpen, setUnitDialogOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [editingBuilding, setEditingBuilding] = useState<BuildingDto | null>(null);
  const [deletingBuilding, setDeletingBuilding] = useState<BuildingDto | null>(null);
  const [selectedBuildingId, setSelectedBuildingId] = useState<number | null>(null);
  const [units, setUnits] = useState<UnitDto[]>([]);
  const [unitsLoading, setUnitsLoading] = useState(false);
  const [formData, setFormData] = useState({ name: '', addressLine: '', city: '', postalCode: '', notes: '', issuerProfileId: '', committeeLegalName: '' });
  const [unitFormData, setUnitFormData] = useState({ unitNumber: '', floor: '', sizeSqm: '', ownerName: '' });

  const loadBuildings = () => {
    setLoading(true); setError(null);
    buildingsApi.getAll()
      .then(r => setBuildings(r.data))
      .catch(err => setError(err.response?.data?.message || t('buildings.failedLoad')))
      .finally(() => setLoading(false));
  };

  useEffect(() => { loadBuildings(); }, []);

  const loadUnits = (buildingId: number) => {
    setUnitsLoading(true);
    buildingsApi.getUnits(buildingId).then(r => setUnits(r.data)).catch(() => setUnits([])).finally(() => setUnitsLoading(false));
  };

  const handleExpandRow = (buildingId: number) => {
    if (selectedBuildingId === buildingId) { setSelectedBuildingId(null); setUnits([]); }
    else { setSelectedBuildingId(buildingId); loadUnits(buildingId); }
  };

  const handleOpenCreateBuilding = () => { setEditingBuilding(null); setFormData({ name: '', addressLine: '', city: '', postalCode: '', notes: '', issuerProfileId: '', committeeLegalName: '' }); setBuildingDialogOpen(true); };
  const handleOpenEditBuilding = (b: BuildingDto) => { setEditingBuilding(b); setFormData({ name: b.name, addressLine: b.addressLine || '', city: b.city || '', postalCode: b.postalCode || '', notes: b.notes || '', issuerProfileId: b.issuerProfileId || '', committeeLegalName: b.committeeLegalName || '' }); setBuildingDialogOpen(true); };

  const handleSaveBuilding = () => {
    if (!formData.name.trim()) return;
    const payload = { name: formData.name.trim(), addressLine: formData.addressLine.trim() || undefined, city: formData.city.trim() || undefined, postalCode: formData.postalCode.trim() || undefined, notes: formData.notes.trim() || undefined, issuerProfileId: formData.issuerProfileId.trim() || undefined, committeeLegalName: formData.committeeLegalName.trim() || undefined };
    if (editingBuilding) {
      buildingsApi.update(editingBuilding.id, payload).then(() => { setBuildingDialogOpen(false); loadBuildings(); }).catch(err => setError(err.response?.data?.message || t('buildings.failedUpdate')));
    } else {
      buildingsApi.create(payload).then(() => { setBuildingDialogOpen(false); loadBuildings(); }).catch(err => setError(err.response?.data?.message || t('buildings.failedCreate')));
    }
  };

  const handleOpenDeleteBuilding = (b: BuildingDto) => { setDeletingBuilding(b); setDeleteDialogOpen(true); };
  const handleConfirmDelete = () => {
    if (!deletingBuilding) return;
    buildingsApi.delete(deletingBuilding.id).then(() => {
      setDeleteDialogOpen(false); setDeletingBuilding(null);
      if (selectedBuildingId === deletingBuilding.id) { setSelectedBuildingId(null); setUnits([]); }
      loadBuildings();
    }).catch(err => setError(err.response?.data?.message || t('buildings.failedDelete')));
  };

  const handleOpenAddUnit = (buildingId: number) => { setSelectedBuildingId(buildingId); setUnitFormData({ unitNumber: '', floor: '', sizeSqm: '', ownerName: '' }); setUnitDialogOpen(true); loadUnits(buildingId); };
  const handleSaveUnit = () => {
    const buildingId = selectedBuildingId;
    if (!buildingId || !unitFormData.unitNumber.trim()) return;
    const payload: Partial<UnitDto> = { unitNumber: unitFormData.unitNumber.trim(), floor: unitFormData.floor ? parseInt(unitFormData.floor, 10) : undefined, sizeSqm: unitFormData.sizeSqm ? parseFloat(unitFormData.sizeSqm) : undefined, ownerName: unitFormData.ownerName.trim() || undefined };
    buildingsApi.createUnit(buildingId, payload).then(() => { setUnitDialogOpen(false); setUnitFormData({ unitNumber: '', floor: '', sizeSqm: '', ownerName: '' }); loadUnits(buildingId); loadBuildings(); }).catch(err => setError(err.response?.data?.message || t('buildings.failedCreateUnit')));
  };

  if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: 200 }}><CircularProgress /></Box>;

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2, flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h4" sx={{ fontWeight: 700, fontSize: { xs: '1.3rem', md: '2rem' } }}>{t('buildings.title')}</Typography>
        <Button variant="contained" startIcon={<Add />} onClick={handleOpenCreateBuilding}>{t('buildings.addBuilding')}</Button>
      </Box>
      {error && <Alert severity="error" onClose={() => setError(null)} sx={{ mb: 2 }}>{error}</Alert>}
      {isMobile ? (
        <Stack spacing={1.5}>
          {buildings.map(building => (
            <Card key={building.id} variant="outlined">
              <CardActionArea onClick={() => handleExpandRow(building.id)}>
                <CardContent sx={{ py: 1.5, px: 2, '&:last-child': { pb: 1.5 } }}>
                  <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <Typography variant="subtitle1" fontWeight={600}>{building.name}</Typography>
                    <Chip label={building.unitCount} size="small" color="primary" variant="outlined" />
                  </Box>
                  <Typography variant="body2" color="text.secondary">{building.addressLine || ''}{building.city ? `, ${building.city}` : ''}</Typography>
                </CardContent>
              </CardActionArea>
              <Collapse in={selectedBuildingId === building.id} timeout="auto" unmountOnExit>
                <Box sx={{ px: 2, pb: 2, bgcolor: 'action.hover' }}>
                  <Box sx={{ display: 'flex', gap: 1, mb: 1, flexWrap: 'wrap' }}>
                    <Button size="small" variant="outlined" onClick={() => handleOpenEditBuilding(building)}>{t('app.edit')}</Button>
                    <Button size="small" variant="outlined" color="error" onClick={() => handleOpenDeleteBuilding(building)}>{t('app.delete')}</Button>
                    <Button size="small" variant="outlined" startIcon={<Add />} onClick={() => handleOpenAddUnit(building.id)}>{t('buildings.addUnit')}</Button>
                  </Box>
                  {unitsLoading ? <CircularProgress size={24} /> : units.length === 0 ? (
                    <Typography variant="body2" color="text.secondary">{t('buildings.noUnits')}</Typography>
                  ) : (
                    <Stack spacing={0.5}>
                      {units.map(unit => (
                        <Typography key={unit.id} variant="body2">
                          {t('buildings.unitNumber')}: {unit.unitNumber} · {t('buildings.floor')}: {unit.floor ?? '—'} · {unit.sizeSqm ?? '—'}m² · {unit.ownerName ?? '—'}
                        </Typography>
                      ))}
                    </Stack>
                  )}
                </Box>
              </Collapse>
            </Card>
          ))}
          {buildings.length === 0 && !loading && <Typography variant="body1" color="text.secondary" sx={{ py: 4, textAlign: 'center' }}>{t('buildings.noBuildings')}</Typography>}
        </Stack>
      ) : (
        <>
          <TableContainer component={Paper}>
            <Table>
              <TableHead><TableRow>
                <TableCell width={48} /><TableCell>{t('buildings.name')}</TableCell><TableCell>{t('buildings.address')}</TableCell>
                <TableCell>{t('buildings.city')}</TableCell><TableCell align="center">{t('buildings.units')}</TableCell><TableCell align="right">{t('app.actions')}</TableCell>
              </TableRow></TableHead>
              <TableBody>
                {buildings.map(building => (
                  <React.Fragment key={building.id}>
                    <TableRow hover sx={{ cursor: 'pointer', '& > *': { borderBottom: 'unset' } }} onClick={() => handleExpandRow(building.id)}>
                      <TableCell><IconButton size="small">{selectedBuildingId === building.id ? <ExpandLess /> : <ExpandMore />}</IconButton></TableCell>
                      <TableCell><Typography fontWeight={600}>{building.name}</Typography></TableCell>
                      <TableCell>{building.addressLine || '—'}</TableCell>
                      <TableCell>{building.city || '—'}</TableCell>
                      <TableCell align="center"><Chip label={building.unitCount} size="small" color="primary" variant="outlined" /></TableCell>
                      <TableCell align="right" onClick={e => e.stopPropagation()}>
                        <IconButton size="small" color="primary" onClick={() => handleOpenEditBuilding(building)}><Edit /></IconButton>
                        <IconButton size="small" color="error" onClick={() => handleOpenDeleteBuilding(building)}><Delete /></IconButton>
                        <Button size="small" variant="outlined" startIcon={<Add />} onClick={() => handleOpenAddUnit(building.id)}>{t('buildings.addUnit')}</Button>
                      </TableCell>
                    </TableRow>
                    <TableRow>
                      <TableCell colSpan={6} sx={{ py: 0, borderBottom: 0 }}>
                        <Collapse in={selectedBuildingId === building.id} timeout="auto" unmountOnExit>
                          <Box sx={{ py: 2, pl: 6, pr: 2, bgcolor: 'action.hover' }}>
                            <Typography variant="subtitle2" gutterBottom>{t('buildings.unitsIn', { name: building.name })}</Typography>
                            {unitsLoading ? <CircularProgress size={24} /> : units.length === 0 ? (
                              <Typography variant="body2" color="text.secondary">{t('buildings.noUnits')}</Typography>
                            ) : (
                              <Table size="small">
                                <TableHead><TableRow>
                                  <TableCell>{t('buildings.unitNumber')}</TableCell><TableCell>{t('buildings.floor')}</TableCell>
                                  <TableCell>{t('buildings.sizeSqm')}</TableCell><TableCell>{t('buildings.ownerName')}</TableCell>
                                </TableRow></TableHead>
                                <TableBody>
                                  {units.map(unit => (
                                    <TableRow key={unit.id}>
                                      <TableCell>{unit.unitNumber}</TableCell><TableCell>{unit.floor ?? '—'}</TableCell>
                                      <TableCell>{unit.sizeSqm ?? '—'}</TableCell><TableCell>{unit.ownerName ?? '—'}</TableCell>
                                    </TableRow>
                                  ))}
                                </TableBody>
                              </Table>
                            )}
                          </Box>
                        </Collapse>
                      </TableCell>
                    </TableRow>
                  </React.Fragment>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
          {buildings.length === 0 && !loading && <Typography variant="body1" color="text.secondary" sx={{ py: 4, textAlign: 'center' }}>{t('buildings.noBuildings')}</Typography>}
        </>
      )}

      <Dialog open={buildingDialogOpen} onClose={() => setBuildingDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{editingBuilding ? t('buildings.editBuilding') : t('buildings.createBuilding')}</DialogTitle>
        <DialogContent>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
            <TextField label={t('buildings.name')} value={formData.name} onChange={e => setFormData(p => ({ ...p, name: e.target.value }))} required fullWidth />
            <TextField label={t('buildings.addressLine')} value={formData.addressLine} onChange={e => setFormData(p => ({ ...p, addressLine: e.target.value }))} fullWidth />
            <TextField label={t('buildings.city')} value={formData.city} onChange={e => setFormData(p => ({ ...p, city: e.target.value }))} fullWidth />
            <TextField label={t('buildings.postalCode')} value={formData.postalCode} onChange={e => setFormData(p => ({ ...p, postalCode: e.target.value }))} fullWidth />
            <TextField label={t('buildings.notes')} value={formData.notes} onChange={e => setFormData(p => ({ ...p, notes: e.target.value }))} multiline rows={3} fullWidth />
            <Typography variant="subtitle2" sx={{ mt: 1 }}>{t('buildings.accountingSettings')}</Typography>
            <TextField label={t('buildings.committeeLegalName')} value={formData.committeeLegalName} onChange={e => setFormData(p => ({ ...p, committeeLegalName: e.target.value }))} fullWidth helperText={t('buildings.committeeLegalNameHelp')} />
            <TextField label={t('buildings.issuerProfileId')} value={formData.issuerProfileId} onChange={e => setFormData(p => ({ ...p, issuerProfileId: e.target.value }))} fullWidth helperText={t('buildings.issuerProfileIdHelp')} />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setBuildingDialogOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleSaveBuilding} disabled={!formData.name.trim()}>{editingBuilding ? t('app.save') : t('app.create')}</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={unitDialogOpen} onClose={() => setUnitDialogOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>{t('buildings.addUnit')}</DialogTitle>
        <DialogContent>
          <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2, pt: 1 }}>
            <TextField label={t('buildings.unitNumber')} value={unitFormData.unitNumber} onChange={e => setUnitFormData(p => ({ ...p, unitNumber: e.target.value }))} required fullWidth placeholder={t('buildings.unitPlaceholder')} />
            <TextField label={t('buildings.floor')} type="number" value={unitFormData.floor} onChange={e => setUnitFormData(p => ({ ...p, floor: e.target.value }))} fullWidth inputProps={{ min: 0 }} />
            <TextField label={t('buildings.sizeSqm')} type="number" value={unitFormData.sizeSqm} onChange={e => setUnitFormData(p => ({ ...p, sizeSqm: e.target.value }))} fullWidth inputProps={{ min: 0, step: 0.01 }} />
            <TextField label={t('buildings.ownerName')} value={unitFormData.ownerName} onChange={e => setUnitFormData(p => ({ ...p, ownerName: e.target.value }))} fullWidth />
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setUnitDialogOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" onClick={handleSaveUnit} disabled={!unitFormData.unitNumber.trim()}>{t('buildings.addUnit')}</Button>
        </DialogActions>
      </Dialog>

      <Dialog open={deleteDialogOpen} onClose={() => setDeleteDialogOpen(false)}>
        <DialogTitle>{t('buildings.deleteBuilding')}</DialogTitle>
        <DialogContent>
          {deletingBuilding && <Typography>{t('buildings.deleteConfirm', { name: deletingBuilding.name })}</Typography>}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteDialogOpen(false)}>{t('app.cancel')}</Button>
          <Button variant="contained" color="error" onClick={handleConfirmDelete}>{t('app.delete')}</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
};

export default BuildingsPage;
