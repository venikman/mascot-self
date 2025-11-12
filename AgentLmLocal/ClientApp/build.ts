#!/usr/bin/env bun

// Bun build script for production
import { $ } from 'bun';

console.log('🔨 Building with Bun...');

// Clean output directory
console.log('📁 Cleaning output directory...');
await $`rm -rf ../wwwroot/*`;

// Copy index.html to wwwroot
console.log('📄 Copying index.html...');
await $`cp index.html ../wwwroot/index.html`;

// Build with Bun's bundler
console.log('📦 Bundling JavaScript...');
const result = await Bun.build({
  entrypoints: ['./src/main.tsx'],
  outdir: '../wwwroot',
  target: 'browser',
  format: 'esm',
  splitting: true,
  minify: true,
  sourcemap: 'external',
  naming: {
    entry: '[name].[hash].[ext]',
    chunk: '[name].[hash].[ext]',
    asset: '[name].[hash].[ext]',
  },
  external: [],
});

if (!result.success) {
  console.error('❌ Build failed:', result.logs);
  process.exit(1);
}

console.log('✅ Build successful!');
console.log(`📦 Generated ${result.outputs.length} files`);

// Update index.html with hashed file names
const mainOutput = result.outputs.find(output =>
  output.path.includes('main') && output.kind === 'entry-point'
);

if (mainOutput) {
  const filename = mainOutput.path.split('/').pop();
  console.log(`📝 Main bundle: ${filename}`);

  // Read index.html
  const indexPath = '../wwwroot/index.html';
  const indexHtml = await Bun.file(indexPath).text();

  // Replace the script src
  const updatedHtml = indexHtml.replace(
    '/src/main.tsx',
    `/${filename}`
  );

  // Write back
  await Bun.write(indexPath, updatedHtml);
  console.log('✅ Updated index.html with hashed filename');
}

console.log('\n✨ Build complete! Output: ../wwwroot/');
