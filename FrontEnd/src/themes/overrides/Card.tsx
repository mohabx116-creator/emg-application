import { Theme } from '@mui/material/styles';

// EMG/Berry data-surface treatment: every card gets a quiet permanent boundary
// and a stronger themed shadow when it becomes interactive.
export default function Card(theme: Theme) {
  return {
    MuiCard: {
      styleOverrides: {
        root: {
          border: '1px solid',
          borderColor: theme.vars.palette.divider,
          boxShadow: theme.shadows[2],
          transition: theme.transitions.create(['box-shadow', 'transform', 'border-color'], {
            duration: 220,
            easing: theme.transitions.easing.easeOut
          }),
          '&:hover': {
            borderColor: theme.vars.palette.primary.main,
            boxShadow: theme.vars.customShadows.primary
          }
        }
      }
    }
  };
}
