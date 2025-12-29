using WhoReapedWhat.Models;

namespace WhoReapedWhat.Services;

public class FileDeletionHandler
{
    private readonly Config _config;
    private readonly EmailService _emailService;
    private readonly ILogger<FileDeletionHandler> _logger;

    public FileDeletionHandler(Config config, EmailService emailService, ILogger<FileDeletionHandler> logger)
    {
        _config = config;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task OnFileDeleted(string path)
    {
        // Filtrage par extension
        if (!_config.WatchAllFileTypes)
        {
            var ext = Path.GetExtension(path)?.TrimStart('.').ToLowerInvariant();
            if (!_config.FileTypesToWatch.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)))
                return; // Ignore ce fichier
        }

        _logger.LogWarning("Fichier supprimé : {file}", path);

        var deletion = new DeletionEvent
        {
            FilePath = path,
            FileName = Path.GetFileName(path),
            User = Environment.UserName,
            DeletedAt = DateTime.Now
        };

        await _emailService.AddDeletion(deletion);
    }
}