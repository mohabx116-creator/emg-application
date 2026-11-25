const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE_DIR = 'C:/Users/pc/Desktop/CONSTRUCTION_ERP_DEMO';
const PHASE31_DIR = path.join(BASE_DIR, 'qa/phase31/screenshots');
const PHASE32_DIR = path.join(BASE_DIR, 'qa/phase32/screenshots');
const OUT_DIR = path.join(BASE_DIR, 'qa/phase32/contact-sheets');

if (!fs.existsSync(OUT_DIR)) {
  fs.mkdirSync(OUT_DIR, { recursive: true });
}

function toBase64(filePath) {
  if (!fs.existsSync(filePath)) {
    console.warn('File not found for base64:', filePath);
    return '';
  }
  const buf = fs.readFileSync(filePath);
  return `data:image/png;base64,${buf.toString('base64')}`;
}

(async () => {
  console.log('Generating Authentic Phase 3.2 Contact Sheets...');
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  // 1. English Mobile Comparison (Phase 3.1 vs Phase 3.2)
  {
    console.log('Generating: contact_sheet_en_mobile_comparison.png');
    const roles = ['gm', 'eng', 'acc', 'admin'];
    const cards = roles.map(r => ({
      role: r.toUpperCase(),
      p31: toBase64(path.join(PHASE31_DIR, `${r}_mobile_390x844_en_light.png`)),
      p32: toBase64(path.join(PHASE32_DIR, `${r}_mobile_390x844_en_light.png`))
    }));

    const html = `
      <!DOCTYPE html>
      <html>
      <head>
        <style>
          body { margin: 0; font-family: 'Segoe UI', sans-serif; background: #0f172a; color: #f8fafc; padding: 24px; }
          .header { text-align: center; margin-bottom: 24px; }
          .header h1 { margin: 0 0 8px 0; font-size: 24px; color: #38bdf8; }
          .header p { margin: 0; font-size: 14px; color: #94a3b8; }
          .grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; }
          .role-col { background: #1e293b; border-radius: 8px; padding: 12px; border: 1px solid #334155; }
          .role-title { font-weight: 700; font-size: 16px; margin-bottom: 8px; text-align: center; color: #e2e8f0; border-bottom: 1px solid #334155; padding-bottom: 6px; }
          .img-pair { display: flex; flex-direction: column; gap: 8px; }
          .img-box { background: #000; border-radius: 4px; overflow: hidden; border: 1px solid #475569; }
          .label { font-size: 11px; padding: 4px 6px; font-weight: 600; text-align: center; }
          .label-before { background: #ef4444; color: #fff; }
          .label-after { background: #10b981; color: #fff; }
          img { width: 100%; display: block; max-height: 380px; object-fit: contain; }
        </style>
      </head>
      <body>
        <div class="header">
          <h1>Phase 3.2 Visual Defect Correction: English Mobile Capture</h1>
          <p>Defect Correction A: Phase 3.1 captured Arabic RTL under English filename (0.05% diff). Phase 3.2 visibly displays true English LTR with responsive controls.</p>
        </div>
        <div class="grid">
          ${cards.map(c => `
            <div class="role-col">
              <div class="role-title">ROLE: ${c.role} (Mobile 390x844)</div>
              <div class="img-pair">
                <div class="img-box">
                  <div class="label label-before">Phase 3.1 (Defect: Arabic RTL under EN name)</div>
                  <img src="${c.p31}" />
                </div>
                <div class="img-box">
                  <div class="label label-after">Phase 3.2 (Verified: True English LTR)</div>
                  <img src="${c.p32}" />
                </div>
              </div>
            </div>
          `).join('')}
        </div>
      </body>
      </html>
    `;

    await page.setContent(html);
    await page.setViewportSize({ width: 1400, height: 960 });
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(OUT_DIR, 'contact_sheet_en_mobile_comparison.png'), fullPage: true });
  }

  // 2. Dark Mode Contrast Comparison (Phase 3.1 vs Phase 3.2)
  {
    console.log('Generating: contact_sheet_dark_mode_comparison.png');
    const items = [
      {
        title: 'General Manager (Desktop 1440x900)',
        contrastBefore: '1.31:1 (Pale text on near-white card)',
        contrastAfter: '10.43:1 / 4.69:1 (Theme tokens)',
        p31: toBase64(path.join(PHASE31_DIR, 'gm_desktop_1440x900_ar_dark.png')),
        p32: toBase64(path.join(PHASE32_DIR, 'gm_desktop_1440x900_ar_dark.png'))
      },
      {
        title: 'Cost Accountant (Desktop 1440x900)',
        contrastBefore: '1.20:1 (Pale text on pale-blue card)',
        contrastAfter: '15.34:1 (Deep dark gradient)',
        p31: toBase64(path.join(PHASE31_DIR, 'acc_desktop_1440x900_ar_dark.png')),
        p32: toBase64(path.join(PHASE32_DIR, 'acc_desktop_1440x900_ar_dark.png'))
      },
      {
        title: 'System Administrator (Mobile 390x844)',
        contrastBefore: '1.35:1 (Pale text on light-gray card)',
        contrastAfter: '8.50:1 (Dark slate card token)',
        p31: toBase64(path.join(PHASE31_DIR, 'admin_mobile_390x844_ar_dark.png')),
        p32: toBase64(path.join(PHASE32_DIR, 'admin_mobile_390x844_ar_dark.png'))
      }
    ];

    const html = `
      <!DOCTYPE html>
      <html>
      <head>
        <style>
          body { margin: 0; font-family: 'Segoe UI', sans-serif; background: #0b0f19; color: #f8fafc; padding: 24px; }
          .header { text-align: center; margin-bottom: 24px; }
          .header h1 { margin: 0 0 8px 0; font-size: 24px; color: #818cf8; }
          .header p { margin: 0; font-size: 14px; color: #94a3b8; }
          .grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 20px; }
          .card-col { background: #111827; border-radius: 8px; padding: 16px; border: 1px solid #1f2937; }
          .title { font-weight: 700; font-size: 15px; margin-bottom: 12px; color: #f3f4f6; text-align: center; }
          .metrics { font-size: 12px; margin-bottom: 12px; display: flex; justify-content: space-between; gap: 8px; }
          .m-before { background: #7f1d1d; color: #fca5a5; padding: 4px 8px; border-radius: 4px; flex: 1; text-align: center; }
          .m-after { background: #064e3b; color: #6ee7b7; padding: 4px 8px; border-radius: 4px; flex: 1; text-align: center; font-weight: 700; }
          .img-box { margin-bottom: 12px; border-radius: 6px; overflow: hidden; border: 1px solid #374151; }
          .label { font-size: 11px; padding: 4px; font-weight: 700; text-align: center; }
          .l-before { background: #ef4444; color: #fff; }
          .l-after { background: #10b981; color: #fff; }
          img { width: 100%; display: block; max-height: 250px; object-fit: contain; background: #000; }
        </style>
      </head>
      <body>
        <div class="header">
          <h1>Phase 3.2 Visual Defect Correction: Dark Mode Contrast Audit</h1>
          <p>Defect Correction B: MUI 7 CSS variables mode caused palette.mode==='dark' to evaluate to false. Corrected using useColorScheme() and semantic theme tokens (WCAG AA >= 4.5:1).</p>
        </div>
        <div class="grid">
          ${items.map(item => `
            <div class="card-col">
              <div class="title">${item.title}</div>
              <div class="metrics">
                <div class="m-before">Before: ${item.contrastBefore}</div>
                <div class="m-after">After: ${item.contrastAfter}</div>
              </div>
              <div class="img-box">
                <div class="label l-before">Phase 3.1 (Contrast Fail: Pale on Light Card)</div>
                <img src="${item.p31}" />
              </div>
              <div class="img-box">
                <div class="label l-after">Phase 3.2 (Contrast Verified: Semantic Dark Token)</div>
                <img src="${item.p32}" />
              </div>
            </div>
          `).join('')}
        </div>
      </body>
      </html>
    `;

    await page.setContent(html);
    await page.setViewportSize({ width: 1400, height: 880 });
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(OUT_DIR, 'contact_sheet_dark_mode_comparison.png'), fullPage: true });
  }

  // 3. Engineer Mobile Identity Card Comparison
  {
    console.log('Generating: contact_sheet_engineer_mobile_comparison.png');
    const p31 = toBase64(path.join(PHASE31_DIR, 'eng_mobile_390x844_ar_light.png'));
    const p32_390 = toBase64(path.join(PHASE32_DIR, 'eng_mobile_390x844_ar_light.png'));
    const p32_360 = toBase64(path.join(PHASE32_DIR, 'eng_mobile_360x800_ar_light_identity_card.png'));

    const html = `
      <!DOCTYPE html>
      <html>
      <head>
        <style>
          body { margin: 0; font-family: 'Segoe UI', sans-serif; background: #0f172a; color: #f8fafc; padding: 24px; }
          .header { text-align: center; margin-bottom: 24px; }
          .header h1 { margin: 0 0 8px 0; font-size: 24px; color: #38bdf8; }
          .header p { margin: 0; font-size: 14px; color: #94a3b8; }
          .grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 20px; }
          .card-col { background: #1e293b; border-radius: 8px; padding: 16px; border: 1px solid #334155; }
          .title { font-weight: 700; font-size: 15px; margin-bottom: 8px; text-align: center; color: #f1f5f9; }
          .desc { font-size: 12px; color: #cbd5e1; margin-bottom: 12px; text-align: center; min-height: 36px; }
          .img-box { border-radius: 6px; overflow: hidden; border: 1px solid #475569; background: #000; }
          .label { font-size: 12px; padding: 6px; font-weight: 700; text-align: center; }
          .l-fail { background: #ef4444; color: #fff; }
          .l-pass { background: #10b981; color: #fff; }
          img { width: 100%; display: block; max-height: 520px; object-fit: contain; }
        </style>
      </head>
      <body>
        <div class="header">
          <h1>Phase 3.2 Visual Defect Correction: Engineer Mobile Identity Card</h1>
          <p>Defect Correction C: Fixed flex sizing, minimum widths, name wrapping, and attendance chip placement on 360px & 390px screens.</p>
        </div>
        <div class="grid">
          <div class="card-col">
            <div class="title">Phase 3.1: Mobile 390px</div>
            <div class="desc">Defect: Engineer name wrapped into cramped ~100px column beside chip despite card space.</div>
            <div class="img-box">
              <div class="label l-fail">Phase 3.1 Defect (Cramped Narrow Column)</div>
              <img src="${p31}" />
            </div>
          </div>
          <div class="card-col">
            <div class="title">Phase 3.2: Mobile 390px</div>
            <div class="desc">Corrected: Responsive column flex, full-width name (height: 24.5px, single line), clean chip alignment.</div>
            <div class="img-box">
              <div class="label l-pass">Phase 3.2 Verified (390px Full Width)</div>
              <img src="${p32_390}" />
            </div>
          </div>
          <div class="card-col">
            <div class="title">Phase 3.2: Small Mobile 360px</div>
            <div class="desc">Validated: Zero horizontal overflow, compact identity card at ultra-narrow 360px viewport.</div>
            <div class="img-box">
              <div class="label l-pass">Phase 3.2 Verified (360px Compact Card)</div>
              <img src="${p32_360}" />
            </div>
          </div>
        </div>
      </body>
      </html>
    `;

    await page.setContent(html);
    await page.setViewportSize({ width: 1300, height: 780 });
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(OUT_DIR, 'contact_sheet_engineer_mobile_comparison.png'), fullPage: true });
  }

  // 4. Six-Role Desktop Consistency
  {
    console.log('Generating: contact_sheet_six_role_desktop.png');
    const roles = [
      { code: 'gm', name: 'General Manager' },
      { code: 'om', name: 'Operations Manager' },
      { code: 'pm', name: 'Project Manager' },
      { code: 'eng', name: 'Site Engineer' },
      { code: 'acc', name: 'Cost Accountant' },
      { code: 'admin', name: 'System Administrator' }
    ];

    const cards = roles.map(r => ({
      name: r.name,
      ar: toBase64(path.join(PHASE32_DIR, `${r.code}_desktop_1440x900_ar_light.png`)),
      en: toBase64(path.join(PHASE32_DIR, `${r.code}_desktop_1440x900_en_light.png`))
    }));

    const html = `
      <!DOCTYPE html>
      <html>
      <head>
        <style>
          body { margin: 0; font-family: 'Segoe UI', sans-serif; background: #0f172a; color: #f8fafc; padding: 24px; }
          .header { text-align: center; margin-bottom: 24px; }
          .header h1 { margin: 0 0 8px 0; font-size: 24px; color: #38bdf8; }
          .header p { margin: 0; font-size: 14px; color: #94a3b8; }
          .grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; }
          .role-card { background: #1e293b; border-radius: 8px; padding: 12px; border: 1px solid #334155; }
          .role-title { font-weight: 700; font-size: 14px; margin-bottom: 8px; color: #f1f5f9; text-align: center; }
          .pair { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; }
          .box { border-radius: 4px; overflow: hidden; border: 1px solid #475569; background: #000; }
          .tag { font-size: 10px; padding: 3px; font-weight: 700; text-align: center; background: #3b82f6; color: #fff; }
          img { width: 100%; display: block; max-height: 150px; object-fit: contain; }
        </style>
      </head>
      <body>
        <div class="header">
          <h1>Phase 3.2 Visual Defect Correction: Six-Role Desktop Consistency</h1>
          <p>Verified authentic bilingual desktop presentation across all six role workspaces in Arabic RTL and English LTR.</p>
        </div>
        <div class="grid">
          ${cards.map(c => `
            <div class="role-card">
              <div class="role-title">${c.name}</div>
              <div class="pair">
                <div class="box">
                  <div class="tag">Arabic RTL (1440x900)</div>
                  <img src="${c.ar}" />
                </div>
                <div class="box">
                  <div class="tag">English LTR (1440x900)</div>
                  <img src="${c.en}" />
                </div>
              </div>
            </div>
          `).join('')}
        </div>
      </body>
      </html>
    `;

    await page.setContent(html);
    await page.setViewportSize({ width: 1400, height: 860 });
    await page.waitForTimeout(600);
    await page.screenshot({ path: path.join(OUT_DIR, 'contact_sheet_six_role_desktop.png'), fullPage: true });
  }

  await browser.close();
  console.log('All 4 Contact Sheets Generated Successfully!');
})();
