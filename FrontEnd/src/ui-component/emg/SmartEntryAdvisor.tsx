import { useEffect, useState } from 'react';
import { Alert, Button, Stack, Typography } from '@mui/material';
import { suggestEmgEntry, EmgEntrySuggestion } from 'api/emg';

type SmartEntryAdvisorProps = {
  projectId?: number | null;
  description: string;
  movementType?: string | null;
  unit?: string;
  quantity?: number | null;
  unitPrice?: number | null;
  amount?: number | null;
  supplierName?: string | null;
  onApply: (suggestion: EmgEntrySuggestion) => void;
};

export default function SmartEntryAdvisor({ projectId, description, movementType, unit, quantity, unitPrice, amount, supplierName, onApply }: SmartEntryAdvisorProps) {
  const [suggestion, setSuggestion] = useState<EmgEntrySuggestion | null>(null);
  useEffect(() => {
    if (description.trim().length < 2) { setSuggestion(null); return; }
    const timer = window.setTimeout(async () => {
      try {
        setSuggestion(await suggestEmgEntry({
          projectId: projectId || null,
          description,
          movementType: movementType ?? null,
          unit: unit || null,
          supplierName: supplierName ?? null,
          quantity: quantity ?? null,
          unitPrice: unitPrice ?? null,
          amount: amount ?? (quantity != null && unitPrice != null ? quantity * unitPrice : null)
        }));
      } catch { setSuggestion(null); }
    }, 450);
    return () => window.clearTimeout(timer);
  }, [projectId, description, movementType, unit, quantity, unitPrice, amount, supplierName]);
  if (!suggestion) return null;
  return (
    <Alert severity={suggestion.requiresConfirmation ? 'warning' : 'info'} action={<Button size="small" onClick={() => onApply(suggestion)}>تطبيق</Button>}>
      <Stack spacing={0.25}>
        <Typography variant="body2" fontWeight={700}>اقتراح ذكي: {suggestion.suggestedEntryKind} · {suggestion.suggestedExpenseCategory}</Typography>
        <Typography variant="caption">الوحدة المقترحة: {suggestion.suggestedUnit} · الثقة: {Math.round(suggestion.confidence * 100)}%</Typography>
        <Typography variant="caption">{suggestion.explanation}</Typography>
      </Stack>
    </Alert>
  );
}
