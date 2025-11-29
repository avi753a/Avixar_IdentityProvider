using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace Avixar.Infrastructure.Logging
{
    public class FileLogger : ILogger
    {
        private readonly string _path;
        private readonly string _categoryName;
        private static readonly object _lock = new object();

        public FileLogger(string path, string categoryName)
        {
            _path = path;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default!;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            var logRecord = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] [{_categoryName}] {message}";
            if (exception != null)
            {
                logRecord += Environment.NewLine + exception.ToString();
            }

            try
            {
                lock (_lock)
                {
                    File.AppendAllText(_path, logRecord + Environment.NewLine);
                }
            }
            catch
            {
                // Suppress logging errors to avoid infinite loops
            }
        }
    }

    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _path;

        public FileLoggerProvider(string path)
        {
            _path = path;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(_path, categoryName);
        }

        public void Dispose() { }
    }
}
