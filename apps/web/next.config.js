/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  output: 'standalone', // Enable Docker-optimized output

  // Turbopack configuration for Next.js 16
  turbopack: {
    // Empty config to silence Turbopack warning
    // Note: PDF.js aliases not needed in Turbopack (handled differently)
  },

  // Webpack config for backward compatibility (use --webpack flag if needed)
  webpack: (config) => {
    // PDF.js worker configuration - only used with --webpack flag
    config.resolve.alias.canvas = false;
    config.resolve.alias.encoding = false;
    return config;
  },
};
module.exports = nextConfig;
