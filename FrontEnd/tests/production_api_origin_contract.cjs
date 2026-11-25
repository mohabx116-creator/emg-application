const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const axiosSource = fs.readFileSync(path.join(root, 'src/utils/axios.ts'), 'utf8');
const dockerfile = fs.readFileSync(path.join(root, 'Dockerfile'), 'utf8');
const profileSource = fs.readFileSync(
  path.join(root, 'src/views/application/users/account-profile/Profile1/Profile.tsx'),
  'utf8'
);

assert.match(axiosSource, /baseURL:\s*['"]\/['"]/);
assert.match(axiosSource, /timeout:\s*10000/);
assert.doesNotMatch(axiosSource, /localhost|127\.0\.0\.1/);
assert.match(dockerfile, /ARG VITE_APP_API_URL=\//);
assert.doesNotMatch(dockerfile, /localhost|127\.0\.0\.1/);
assert.doesNotMatch(profileSource, /VITE_APP_API_URL|localhost|127\.0\.0\.1/);

const dist = path.join(root, 'dist');
if (fs.existsSync(dist)) {
  const files = [];
  const visit = (directory) => {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      const fullPath = path.join(directory, entry.name);
      if (entry.isDirectory()) visit(fullPath);
      else files.push(fullPath);
    }
  };
  visit(dist);
  const bundle = files
    .filter((file) => /\.(?:js|html|css)$/.test(file))
    .map((file) => fs.readFileSync(file, 'utf8'))
    .join('\n');
  assert.doesNotMatch(bundle, /localhost:8080|localhost:5050|VITE_APP_API_URL/);
}

console.log('production API origin contract: PASS');
