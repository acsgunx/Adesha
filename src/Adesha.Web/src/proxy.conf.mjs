const target = process.env.ADESHA_API_URL || 'http://localhost:5157';

export default {
  '/api': {
    target,
    secure: false,
    changeOrigin: true,
    logLevel: 'debug',
  },
};
