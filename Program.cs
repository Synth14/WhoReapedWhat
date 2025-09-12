#if WINDOWS
using System.Diagnostics.Eventing.Reader;
#endif
namespace WhoReapedWhat
{
    class Program
    {
        private static Config? config;
        private static List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        private static readonly string CONFIG_FILE = "config.json";

        static async Task Main(string[] args)
        {

            Console.WriteLine("[FOLDER] WhoReapedWhat - Surveillance des suppressions");
            Console.WriteLine("===============================================\n");

            if (!LoadConfig())
            {
                Console.WriteLine("[ERROR] Impossible de charger la configuration. Arrêt du programme.");
                Environment.Exit(1);
            }

            // Affichage de la configuration
            Console.WriteLine($"[DIR] Surveillance de {config!.WatchPaths.Count} dossier(s):");
            foreach (var path in config.WatchPaths)
            {
                Console.WriteLine($"   • {path}");
            }

            if (config.WatchAllFileTypes)
            {
                Console.WriteLine("[SCAN] Surveillance: TOUS les types de fichiers");
            }
            else
            {
                Console.WriteLine($"[SCAN] Surveillance: {config.FileTypesToWatch.Count} types de fichiers");
                Console.WriteLine($"   Types: {string.Join(", ", config.FileTypesToWatch.Take(10))}...");
            }

            Console.WriteLine($"[EMAIL] Notifications envoyées à: {config.EmailTo}");
            Console.WriteLine("[START] Service démarré.\n");

            if (!StartWatching())
            {
                Console.WriteLine("[ERROR] Impossible de démarrer la surveillance.");
                Environment.Exit(1);
            }

            Console.WriteLine("[SLEEP] Service actif - Surveillance en cours...");

            while (true)
            {
                await Task.Delay(10000);
            }
        }

        private static bool LoadConfig()
        {
            try
            {
                if (!File.Exists(CONFIG_FILE))
                {
                    Console.WriteLine($"⚠️  Fichier {CONFIG_FILE} introuvable. Création du fichier exemple...");
                    CreateDefaultConfig();
                    Console.WriteLine($"✅ Fichier {CONFIG_FILE} créé avec des valeurs par défaut.");
                    Console.WriteLine("🔧 Modifiez ce fichier avec vos paramètres puis relancez le programme.");
                    return false;
                }

                var jsonString = File.ReadAllText(CONFIG_FILE);
                config = JsonSerializer.Deserialize<Config>(jsonString);

                // Vérification des chemins
                var validPaths = new List<string>();
                foreach (var path in config!.WatchPaths)
                {
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    {
                        validPaths.Add(path);
                    }
                    else
                    {
                        Console.WriteLine($"⚠️  Dossier ignoré (inexistant): {path}");
                    }
                }

                if (!validPaths.Any())
                {
                    Console.WriteLine("❌ Aucun dossier valide à surveiller.");
                    return false;
                }

                config.WatchPaths = validPaths;

                if (string.IsNullOrEmpty(config.EmailFrom) || string.IsNullOrEmpty(config.EmailTo))
                {
                    Console.WriteLine("❌ Adresses email manquantes dans la configuration.");
                    return false;
                }

                Console.WriteLine("✅ Configuration chargée avec succès.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur lors du chargement de la configuration: {ex.Message}");
                return false;
            }
        }

        private static void CreateDefaultConfig()
        {
            var defaultConfig = new Config();
            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonString = JsonSerializer.Serialize(defaultConfig, options);
            File.WriteAllText(CONFIG_FILE, jsonString);
        }

        private static bool StartWatching()
        {
            try
            {
                foreach (var watchPath in config!.WatchPaths)
                {
                    var watcher = new FileSystemWatcher();
                    watcher.Path = watchPath;
                    watcher.IncludeSubdirectories = true;
                    watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;

                    watcher.Deleted += OnDeleted;
                    watcher.Error += OnError;

                    watcher.EnableRaisingEvents = true;
                    watchers.Add(watcher);

                    Console.WriteLine($"✅ Surveillance active sur: {watchPath}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur démarrage surveillance: {ex.Message}");
                return false;
            }
        }

        private static async void OnDeleted(object sender, FileSystemEventArgs e)
        {
            // Vérifier si le fichier correspond aux types surveillés
            if (!ShouldWatchFile(e.FullPath))
            {
                return; // Ignorer silencieusement
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var fileName = Path.GetFileName(e.FullPath);
            var fileType = "Fichier";
            var extension = Path.GetExtension(e.FullPath)?.TrimStart('.');

            var whoDeleted = await GetWhoDeleted(e.FullPath);

            Console.WriteLine($"[DEL]  [{timestamp}] {fileType} supprimé: {fileName} (.{extension}) par {whoDeleted}");

            _ = Task.Run(() => SendEmailAlert(e.FullPath, timestamp, fileType, whoDeleted, extension));
            await LogDeletion(e.FullPath, timestamp, fileType, whoDeleted, extension);
        }

        private static bool ShouldWatchFile(string filePath)
        {
            // Si on surveille tous les types
            if (config!.WatchAllFileTypes)
            {
                return true;
            }

            // Vérifier l'extension
            var extension = Path.GetExtension(filePath)?.TrimStart('.').ToLowerInvariant();

            if (string.IsNullOrEmpty(extension))
            {
                return false; // Pas d'extension = pas de surveillance
            }

            return config.FileTypesToWatch.Any(ft => ft.ToLowerInvariant() == extension);
        }

        private static async Task<string> GetWhoDeleted(string filePath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await GetLinuxProcessInfo();
            }

            return await GetWindowsAuditInfo(filePath);
        }

        private static async Task<string> GetWindowsAuditInfo(string filePath)
        {
            try
            {
                var currentUser = Environment.UserName;
                var domain = Environment.UserDomainName;

                var processes = System.Diagnostics.Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.ProcessName))
                    .Where(p => p.ProcessName.ToLower().Contains("explorer") ||
                               p.ProcessName.ToLower().Contains("plex") ||
                               p.ProcessName.ToLower().Contains("cmd") ||
                               p.ProcessName.ToLower().Contains("powershell"))
                    .Take(3)
                    .Select(p => p.ProcessName)
                    .ToList();

                var processInfo = processes.Any() ? $" - Processus actifs: {string.Join(", ", processes)}" : "";

                return $"{domain}\\{currentUser}{processInfo}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Erreur info utilisateur: {ex.Message}");
                return Environment.UserName ?? "Utilisateur inconnu";
            }
        }

        private static async Task<string> GetLinuxProcessInfo()
        {
            try
            {
                await Task.Delay(100);

                var currentUser = Environment.UserName;

                var processes = System.Diagnostics.Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.ProcessName))
                    .Where(p => p.ProcessName.Contains("plex") ||
                               p.ProcessName.Contains("rm") ||
                               p.ProcessName.Contains("docker"))
                    .Take(3)
                    .Select(p => p.ProcessName)
                    .ToList();

                if (processes.Any())
                {
                    return $"{currentUser} - Processus: {string.Join(", ", processes)}";
                }

                return $"{currentUser} (processus inconnus)";
            }
            catch
            {
                return Environment.UserName ?? "Utilisateur inconnu";
            }
        }

        private static async Task SendEmailAlert(string deletedPath, string timestamp, string fileType, string whoDeleted, string? extension)
        {
            try
            {
                using (var client = new SmtpClient(config!.SmtpServer, config.SmtpPort))
                {
                    client.EnableSsl = config.EnableSsl;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(config.EmailFrom, config.EmailPassword);

                    var mail = new MailMessage();
                    mail.From = new MailAddress(config.EmailFrom, "WhoReapedWhat");
                    mail.To.Add(config.EmailTo);
                    mail.Subject = $"[ALERT] {fileType} supprimé - {Path.GetFileName(deletedPath)}";

                    var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
                                  RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : "macOS";

                    var extensionInfo = !string.IsNullOrEmpty(extension) ? $"<p><strong>Type:</strong> .{extension}</p>" : "";

                    mail.Body = $@"
<html><body>
<h3>🗑️ {fileType} supprimé sur le serveur</h3>
<p><strong>Horodatage:</strong> {timestamp}</p>
<p><strong>Nom:</strong> {Path.GetFileName(deletedPath)}</p>
{extensionInfo}
<p><strong>Chemin:</strong> {deletedPath}</p>
<p><strong>Dossier parent:</strong> {Path.GetDirectoryName(deletedPath)}</p>
<p><strong>Supprimé par:</strong> {whoDeleted}</p>
<p><strong>Plateforme:</strong> {platform}</p>
<hr>
<p><em>Message automatique du service WhoReapedWhat</em></p>
</body></html>";

                    mail.IsBodyHtml = true;

                    await client.SendMailAsync(mail);
                    Console.WriteLine($"[EMAIL] Email envoyé pour: {Path.GetFileName(deletedPath)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Erreur envoi email: {ex.Message}");
            }
        }

        private static async Task LogDeletion(string deletedPath, string timestamp, string fileType, string whoDeleted, string? extension)
        {
            try
            {
                var extensionInfo = !string.IsNullOrEmpty(extension) ? $" (.{extension})" : "";
                var logEntry = $"[{timestamp}] {fileType} SUPPRIMÉ{extensionInfo}: {deletedPath} - PAR: {whoDeleted}\n";
                await File.AppendAllTextAsync("suppressions.log", logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Erreur log: {ex.Message}");
            }
        }

        private static void OnError(object sender, ErrorEventArgs e)
        {
            Console.WriteLine($"[ERROR] Erreur FileSystemWatcher: {e.GetException().Message}");
        }
    }
}