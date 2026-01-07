const data = require('./coverage/coverage-final.json');

const byDir = {};

Object.keys(data).forEach(file => {
  if (!file.includes('__tests__') && file.includes('/components/')) {
    const match = file.match(/components\/([^\/]+)/);
    const dir = match ? match[1] : 'root';
    const cov = data[file];
    const lineCov = cov.s
      ? (Object.values(cov.s).filter(v => v > 0).length / Object.keys(cov.s).length) * 100
      : 100;

    if (!byDir[dir]) {
      byDir[dir] = { files: 0, totalCov: 0, uncovered: [] };
    }

    byDir[dir].files++;
    byDir[dir].totalCov += lineCov;

    if (lineCov < 70) {
      byDir[dir].uncovered.push({
        file: file.split('/').pop(),
        path: file,
        cov: lineCov.toFixed(1),
      });
    }
  }
});

// Sort and display
Object.keys(byDir)
  .sort()
  .forEach(dir => {
    const avg = (byDir[dir].totalCov / byDir[dir].files).toFixed(1);
    console.log(
      `${dir}: ${avg}% (${byDir[dir].files} files) - Uncovered: ${byDir[dir].uncovered.length}`
    );

    if (byDir[dir].uncovered.length > 0 && byDir[dir].uncovered.length <= 10) {
      byDir[dir].uncovered.forEach(u => {
        console.log(`  - ${u.file} (${u.cov}%)`);
      });
    }
  });

// Summary of highest priority gaps
console.log('\n=== HIGH PRIORITY GAPS (<50% coverage) ===');
Object.keys(byDir).forEach(dir => {
  byDir[dir].uncovered
    .filter(u => parseFloat(u.cov) < 50)
    .forEach(u => {
      console.log(`${dir}/${u.file}: ${u.cov}%`);
    });
});
