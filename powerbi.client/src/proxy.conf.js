const { env } = require('process');

const target = env.ASPNETCORE_HTTPS_PORT
  ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}`
  : env.ASPNETCORE_URLS
    ? env.ASPNETCORE_URLS.split(';')[0]
    : 'https://localhost:7201';

/** Formato objeto: melhor suporte a POST no dev server. */
module.exports = {
  '/api': {
    target,
    secure: false,
    changeOrigin: true
  },
  '/weatherforecast': {
    target,
    secure: false,
    changeOrigin: true
  }
};
