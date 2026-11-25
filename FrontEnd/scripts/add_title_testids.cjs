const fs = require('fs');

function addTestId(filePath, targetStr, replacementStr) {
  const fullPath = 'C:/Users/pc/Desktop/CONSTRUCTION_ERP_DEMO/' + filePath;
  const content = fs.readFileSync(fullPath, 'utf8');
  if (content.includes(targetStr)) {
    fs.writeFileSync(fullPath, content.replace(targetStr, replacementStr), 'utf8');
    console.log('Added testid to:', filePath);
  }
}

addTestId(
  'src/modules/role-dashboards/ExecutiveDashboard.tsx',
  '<Typography variant="h3" sx={{ fontWeight: 800 }}>',
  '<Typography variant="h3" data-testid="executive-dashboard-title" sx={{ fontWeight: 800 }}>'
);

addTestId(
  'src/modules/role-dashboards/OperationsDashboard.tsx',
  `<Typography variant="h3" sx={{ fontWeight: 800, color: isDark ? '#5eead4' : '#0f766e' }}>`,
  `<Typography variant="h3" data-testid="operations-workspace-title" sx={{ fontWeight: 800, color: isDark ? '#5eead4' : '#0f766e' }}>`
);

addTestId(
  'src/modules/role-dashboards/AccountingWorkspace.tsx',
  '<Typography variant="h2" sx={{ fontWeight: 800 }}>',
  '<Typography variant="h2" data-testid="accounting-workspace-title" sx={{ fontWeight: 800 }}>'
);
