import type { Theme } from '@mui/material/styles';

/** Berry-based visual language for EMG cards and surfaces. */
export const emgCardSx = (theme: Theme) => ({
  border: '1px solid',
  borderColor: theme.vars.palette.divider,
  boxShadow: theme.shadows[2],
  transition: theme.transitions.create(['box-shadow', 'transform', 'border-color'], {
    duration: 220,
    easing: theme.transitions.easing.easeOut
  }),
  '&:hover': {
    borderColor: theme.vars.palette.primary.main,
    boxShadow: theme.vars.customShadows.primary,
    transform: 'translateY(-3px)'
  }
});

export const emgFeaturedCardSx = (theme: Theme) => ({
  border: '1px solid',
  borderColor: theme.vars.palette.divider,
  boxShadow: theme.shadows[4],
  transition: theme.transitions.create(['box-shadow', 'transform', 'border-color'], {
    duration: 220,
    easing: theme.transitions.easing.easeOut
  }),
  '&:hover': {
    borderColor: theme.vars.palette.secondary.main,
    boxShadow: theme.vars.customShadows.secondary,
    transform: 'translateY(-4px)'
  }
});

export const emgSurfaceSx = (theme: Theme) => ({
  border: '1px solid',
  borderColor: theme.vars.palette.divider,
  boxShadow: theme.shadows[1]
});
