import { Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import { Link } from 'react-router-dom';
import type { HistoricalSettlement, HistoricalWork } from 'api/historicalWork';

type Row = HistoricalWork | HistoricalSettlement;
const number = new Intl.NumberFormat('ar-EG-u-nu-latn', { maximumFractionDigits: 8 });

export default function HistoricalWorkTable({ rows }: { rows: Row[] }) {
  if (!rows.length) return null;
  return (
    <TableContainer sx={{ overflowX: 'auto' }}>
      <Table size="small" aria-label="تفاصيل السجلات التاريخية">
        <TableHead>
          <TableRow>
            {['التاريخ', 'البند والبيان', 'الطرف', 'الكمية والوحدة', 'السعر', 'المبلغ', 'المصدر'].map(label =>
              <TableCell key={label} sx={{ fontWeight: 800 }}>{label}</TableCell>
            )}
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map(row => {
            const work = 'workDate' in row ? row : null;
            const date = 'settlementDate' in row ? row.settlementDate : row.workDate;
            return (
              <TableRow key={row.id}>
                <TableCell>{date?.slice(0, 10) || '—'}</TableCell>
                <TableCell>
                  <Typography fontWeight={700}>{'narrative' in row ? row.narrative || '—' : row.description || '—'}</Typography>
                  <Typography variant="caption">{row.category} · {row.projectName}</Typography>
                </TableCell>
                <TableCell>{row.party || '—'}</TableCell>
                <TableCell>{work?.quantity != null ? number.format(work.quantity) : '—'} {work?.unit}</TableCell>
                <TableCell>{work?.rate != null ? number.format(work.rate) : '—'}</TableCell>
                <TableCell sx={{ whiteSpace: 'nowrap' }}>{number.format(row.amount)} {row.currency}</TableCell>
                <TableCell>
                  <Typography variant="body2">{row.sourceFileName}</Typography>
                  <bdi>{row.sheet}!{row.cell}</bdi>
                  {work?.operationalId != null && (
                    <Typography variant="caption" display="block">
                      {work.operationalTable === 'SiteMovement'
                        ? <Link to={`/emg/site-movements/${work.operationalId}`}>سجل حركة الموقع #{work.operationalId}</Link>
                        : `السجل المرتبط #${work.operationalId}`}
                    </Typography>
                  )}
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </TableContainer>
  );
}