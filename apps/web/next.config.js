/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  output: 'standalone', // Enable Docker-optimized output
  eslint: {
    // Disable ESLint during builds - run separately to avoid ESLint 9 circular structure issues
    ignoreDuringBuilds: true,
  },
  webpack: (config) => {
    // PDF.js worker configuration
    config.resolve.alias.canvas = false;
    config.resolve.alias.encoding = false;
    return config;
  },
};
module.exports = nextConfig;
