#if WINDOWS
using System.Diagnostics.Eventing.Reader;
#endif
namespace WhoReapedWhat
{
    class Program
    {
        private static Config? config;
        private static FileSystemWatcher? watcher;
        private static readonly string CONFIG_FILE = "config.json";

        static async Task Main(string[] args)
        {
            Console.WriteLine("📁 WhoReapedWhat - Surveillance des suppressions");
            Console.WriteLine("===============================================\n");

            if (!LoadConfig())
            {
                Console.WriteLine("❌ Impossible de charger la configuration. Arrêt du programme.");
                Environment.Exit(1);
            }

            Console.WriteLine($"📂 Surveillance du dossier: {config!.WatchPath}");
            Console.WriteLine($"📧 Notifications envoyées à: {config.EmailTo}");
            Console.WriteLine("🚀 Service démarré.\n");

            if (!StartWatching())
            {
                Console.WriteLine("❌ Impossible de démarrer la surveillance.");
                Environment.Exit(1);
            }

            Console.WriteLine("💤 Service actif - Surveillance en cours...");

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

                if (string.IsNullOrEmpty(config!.WatchPath) || !Directory.Exists(config.WatchPath))
                {
                    Console.WriteLine($"❌ Le dossier à surveiller n'existe pas: {config.WatchPath}");
                    return false;
                }

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
                watcher = new FileSystemWatcher();
                watcher.Path = config!.WatchPath;
                watcher.IncludeSubdirectories = true;
                watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;

                watcher.Deleted += OnDeleted;
                watcher.Error += OnError;

                watcher.EnableRaisingEvents = true;
                Console.WriteLine("✅ Surveillance active !");
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
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var fileName = Path.GetFileName(e.FullPath);
            var fileType = "Fichier";

            var whoDeleted = await GetWhoDeleted(e.FullPath);

            Console.WriteLine($"🗑️  [{timestamp}] {fileType} supprimé: {fileName} par {whoDeleted}");

            _ = Task.Run(() => SendEmailAlert(e.FullPath, timestamp, fileType, whoDeleted));
            await LogDeletion(e.FullPath, timestamp, fileType, whoDeleted);
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
            // Version simplifiée - juste l'utilisateur courant et processus
            try
            {
                var currentUser = Environment.UserName;
                var domain = Environment.UserDomainName;

                // Essayer de détecter quelques processus suspects
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

                // Récupérer l'utilisateur courant
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
                // Fallback minimal
                return Environment.UserName ?? "Utilisateur inconnu";
            }
        }

        private static async Task SendEmailAlert(string deletedPath, string timestamp, string fileType, string whoDeleted)
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
                    mail.Subject = $"🚨 {fileType} supprimé - {Path.GetFileName(deletedPath)}";

                    var platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
                                  RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : "macOS";

                    mail.Body = $@"
<html><body>
<h3>🗑️ {fileType} supprimé sur le serveur</h3>
<p><strong>Horodatage:</strong> {timestamp}</p>
<p><strong>Nom:</strong> {Path.GetFileName(deletedPath)}</p>
<p><strong>Chemin:</strong> {deletedPath}</p>
<p><strong>Dossier parent:</strong> {Path.GetDirectoryName(deletedPath)}</p>
<p><strong>Supprimé par:</strong> {whoDeleted}</p>
<p><strong>Plateforme:</strong> {platform}</p>
<hr>
<p><em>Message automatique du service WhoReapedWhat</em></p>
</body></html>";

                    mail.IsBodyHtml = true;

                    await client.SendMailAsync(mail);
                    Console.WriteLine($"✅ Email envoyé pour: {Path.GetFileName(deletedPath)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur envoi email: {ex.Message}");
            }
        }

        private static async Task LogDeletion(string deletedPath, string timestamp, string fileType, string whoDeleted)
        {
            try
            {
                var logEntry = $"[{timestamp}] {fileType} SUPPRIMÉ: {deletedPath} - PAR: {whoDeleted}\n";
                await File.AppendAllTextAsync("suppressions.log", logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erreur log: {ex.Message}");
            }
        }

        private static void OnError(object sender, ErrorEventArgs e)
        {
            Console.WriteLine($"❌ Erreur FileSystemWatcher: {e.GetException().Message}");
        }
    }
}