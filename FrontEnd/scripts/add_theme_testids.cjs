const fs = require('fs');

// 1. ThemeMode.tsx
{
  const p = 'C:/Users/pc/Desktop/CONSTRUCTION_ERP_DEMO/src/layout/Customization/ThemeMode.tsx';
  let c = fs.readFileSync(p, 'utf8');
  c = c.replace(
    'control={<Radio value={ThemeMode.LIGHT} sx={{ display: \'none\' }} />}',
    'data-testid="theme-mode-light-btn"\n            control={<Radio value={ThemeMode.LIGHT} sx={{ display: \'none\' }} />}'
  );
  c = c.replace(
    'control={<Radio value={ThemeMode.DARK} sx={{ display: \'none\' }} />}',
    'data-testid="theme-mode-dark-btn"\n            control={<Radio value={ThemeMode.DARK} sx={{ display: \'none\' }} />}'
  );
  fs.writeFileSync(p, c, 'utf8');
  console.log('Updated ThemeMode.tsx with data-testids');
}

// 2. Customization/index.tsx
{
  const p = 'C:/Users/pc/Desktop/CONSTRUCTION_ERP_DEMO/src/layout/Customization/index.tsx';
  let c = fs.readFileSync(p, 'utf8');
  c = c.replace(
    '<IconButton sx={{ p: 0, color: \'grey.600\' }} onClick={handleToggle}>',
    '<IconButton data-testid="customization-close-btn" sx={{ p: 0, color: \'grey.600\' }} onClick={handleToggle}>'
  );
  fs.writeFileSync(p, c, 'utf8');
  console.log('Updated Customization/index.tsx with customization-close-btn');
}
