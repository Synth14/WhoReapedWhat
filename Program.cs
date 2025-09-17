using WhoReapedWhat.Models;


internal class Program
{
    private static async Task Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Trace);
        });

        var logger = loggerFactory.CreateLogger<Program>();

        try
        {
            // Chargement de la config
            var config = Config.Load("config.json");
            logger.LogInformation("Configuration chargée avec succès.");

            // Création des loggers typés
            var emailLogger = loggerFactory.CreateLogger<EmailService>();
            var handlerLogger = loggerFactory.CreateLogger<FileDeletionHandler>();

            // Services
            var emailService = new EmailService(config, emailLogger);
            var handler = new FileDeletionHandler(config, emailService, handlerLogger);

            // FileSystemWatcher
            using var watcher = new FileSystemWatcher(config.WatchPaths.First())
            {
                IncludeSubdirectories = config.IncludeSubdirectories,
                NotifyFilter = NotifyFilters.FileName |
                               NotifyFilters.DirectoryName |
                               NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            watcher.Deleted += async (s, e) => await handler.OnFileDeleted(e.FullPath);

            watcher.Error += (s, e) =>
            {
                logger.LogError("Erreur sur le FileSystemWatcher : {err}", e.GetException().Message);
            };

            logger.LogInformation("Surveillance démarrée sur {path}", config.WatchPaths.First());

            // Keep alive
            await Task.Delay(-1);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Erreur fatale au lancement de WhoReapedWhat");
        }
    }
}
