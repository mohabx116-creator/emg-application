import { CSSProperties, ReactNode, Ref } from 'react';

// material-ui
import { useColorScheme } from '@mui/material/styles';
import Card, { CardProps } from '@mui/material/Card';
import CardContent, { CardContentProps } from '@mui/material/CardContent';
import CardHeader, { CardHeaderProps } from '@mui/material/CardHeader';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';

// project imports
import { ThemeMode } from 'config';

// constant
const headerStyle = {
  '& .MuiCardHeader-action': { mr: 0 }
};

// ==============================|| CUSTOM MAIN CARD ||============================== //

export interface MainCardProps {
  border?: boolean;
  boxShadow?: boolean;
  children: ReactNode | string;
  style?: CSSProperties;
  content?: boolean;
  className?: string;
  contentClass?: string;
  contentSX?: CardContentProps['sx'];
  headerSX?: CardHeaderProps['sx'];
  darkTitle?: boolean;
  sx?: CardProps['sx'];
  secondary?: CardHeaderProps['action'];
  shadow?: string;
  elevation?: number;
  title?: ReactNode | string;
  ref?: Ref<HTMLDivElement>;
}

export default function MainCard({
  border = false,
  boxShadow,
  children,
  content = true,
  contentClass = '',
  contentSX = {},
  headerSX = {},
  darkTitle,
  secondary,
  shadow,
  sx = {},
  title,
  ref,
  ...others
}: MainCardProps) {
  const { colorScheme } = useColorScheme();
  const defaultShadow = colorScheme === ThemeMode.DARK ? '0 2px 14px 0 rgb(33 150 243 / 10%)' : '0 2px 14px 0 rgb(32 40 45 / 8%)';

  return (
    <Card
      ref={ref}
      {...others}
      sx={(theme) => ({
        border: '1px solid',
        borderColor: 'divider',
        boxShadow: theme.shadows[2],
        transition: theme.transitions.create(['box-shadow', 'transform', 'border-color'], {
          duration: 220,
          easing: theme.transitions.easing.easeOut
        }),
        ':hover': {
          boxShadow: boxShadow ? shadow || defaultShadow : theme.vars.customShadows.primary
        },
        ...(typeof sx === 'function' ? sx(theme) : sx || {})
      })}
    >
      {/* card header and action */}
      {!darkTitle && title && <CardHeader sx={{ ...headerStyle, ...headerSX }} title={title} action={secondary} />}
      {darkTitle && title && (
        <CardHeader sx={{ ...headerStyle, ...headerSX }} title={<Typography variant="h3">{title}</Typography>} action={secondary} />
      )}

      {/* content & header divider */}
      {title && <Divider />}

      {/* card content */}
      {content && (
        <CardContent sx={contentSX} className={contentClass}>
          {children}
        </CardContent>
      )}
      {!content && children}
    </Card>
  );
}
