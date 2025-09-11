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

            // MODE SERVICE - Tourne indéfiniment
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

            Console.WriteLine($"🗑️  [{timestamp}] {fileType} supprimé: {fileName}");

            _ = Task.Run(() => SendEmailAlert(e.FullPath, timestamp, fileType));
            await LogDeletion(e.FullPath, timestamp, fileType);
        }

        private static async Task SendEmailAlert(string deletedPath, string timestamp, string fileType)
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

                    mail.Body = $@"
<html><body>
<h3>🗑️ {fileType} supprimé sur le serveur</h3>
<p><strong>Horodatage:</strong> {timestamp}</p>
<p><strong>Nom:</strong> {Path.GetFileName(deletedPath)}</p>
<p><strong>Chemin:</strong> {deletedPath}</p>
<p><strong>Dossier parent:</strong> {Path.GetDirectoryName(deletedPath)}</p>
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

        private static async Task LogDeletion(string deletedPath, string timestamp, string fileType)
        {
            try
            {
                var logEntry = $"[{timestamp}] {fileType} SUPPRIMÉ: {deletedPath}\n";
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