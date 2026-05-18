using website.Helpers;

namespace website.Services
{
    public class WeeklyMailBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILogger<WeeklyMailBackgroundService> _logger;

        public WeeklyMailBackgroundService(
            IServiceScopeFactory scopeFactory,
            IWebHostEnvironment env,
            IConfiguration config,
            ILogger<WeeklyMailBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _env = env;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_env.IsProduction())
            {
                _logger.LogInformation("WeeklyMailBackgroundService disabled (non-production environment).");
                return;
            }

            var dayOfWeek = Enum.TryParse<DayOfWeek>(_config["WeeklyMail:DayOfWeek"], true, out var d)
                ? d
                : DayOfWeek.Tuesday;
            var hour = _config.GetValue<int?>("WeeklyMail:HourUk") ?? 10;
            var stampPath = Path.Combine(_env.ContentRootPath, "umbraco", "Data", "weekly-mail-lastrun.txt");

            _logger.LogInformation("WeeklyMailBackgroundService started. Target slot: {Day} {Hour:00}:00 UK.", dayOfWeek, hour);

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
            try
            {
                do
                {
                    try
                    {
                        var nowUk = UkDateHelper.NowUk;
                        if (nowUk.DayOfWeek == dayOfWeek && nowUk.Hour == hour)
                        {
                            var lastRun = ReadLastRun(stampPath);
                            if (lastRun == null || (nowUk - lastRun.Value).TotalDays >= 6)
                            {
                                _logger.LogInformation("WeeklyMailBackgroundService firing send at {NowUk} UK.", nowUk);
                                using var scope = _scopeFactory.CreateScope();
                                var svc = scope.ServiceProvider.GetRequiredService<IWeeklyMailService>();
                                var result = await svc.SendAsync(stoppingToken);
                                _logger.LogInformation(
                                    "Weekly mail result: sent={Sent}, recipients={Recipients}, sentCount={SentCount}, reason={Reason}",
                                    result.Sent, result.Recipients, result.SentCount, result.Reason);
                                WriteLastRun(stampPath, nowUk);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "WeeklyMailBackgroundService tick failed.");
                    }
                } while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static DateTime? ReadLastRun(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var text = File.ReadAllText(path);
                return DateTime.TryParse(text, out var dt) ? dt : null;
            }
            catch
            {
                return null;
            }
        }

        private static void WriteLastRun(string path, DateTime when)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, when.ToString("o"));
            }
            catch
            {
            }
        }
    }
}
