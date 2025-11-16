import winston from 'winston';

// Get logging configuration from environment or use defaults
const logLevel = process.env.LOG_LEVEL || 'info';
const logFormat = process.env.NODE_ENV === 'production' ? 'json' : 'simple';
const isProduction = process.env.NODE_ENV === 'production';

// Create logger instance
const logger = winston.createLogger({
  level: logLevel,
  format: logFormat === 'json'
    ? winston.format.combine(
        winston.format.timestamp(),
        winston.format.errors({ stack: true }),
        winston.format.json()
      )
    : winston.format.combine(
        winston.format.timestamp(),
        winston.format.errors({ stack: true }),
        winston.format.simple()
      ),
  transports: [
    new winston.transports.Console(),
    // Add file transport in production
    ...(isProduction ? [
      new winston.transports.File({ filename: 'error.log', level: 'error' }),
      new winston.transports.File({ filename: 'combined.log' })
    ] : [])
  ]
});

export default logger;
