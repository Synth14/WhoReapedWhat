using System.Text.Json;

namespace WhoReapedWhat.Models
{
    public class Config
    {
        // Chemins multiples à surveiller
        public List<string> WatchPaths { get; set; } = new List<string> { @"D:\Media" };

        // Types de fichiers à surveiller 
        public List<string> FileTypesToWatch { get; set; } = new List<string>
        {
            // Vidéos
            "mp4", "avi", "mkv", "mov", "wmv", "flv", "webm", "m4v",
            
            // Audio
            "mp3", "flac", "wav", "aac", "ogg", "wma", "m4a",
            
            // Images
            "jpg", "jpeg", "png", "gif", "bmp", "tiff", "webp", "svg",
            
            // Documents
            "pdf", "docx", "doc", "xlsx", "xls", "pptx", "ppt", "txt", "rtf",
            
            // Archives
            "zip", "rar", "7z", "tar", "gz",
            
            // Code/Config
            "cs", "js", "html", "css", "json", "xml", "yml", "yaml",
            
            // Autres
            "iso", "exe", "msi", "dmg"
        };
        public bool WatchAllFileTypes { get; set; } = false;

        public string SmtpServer { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string EmailFrom { get; set; } = "votre-email@gmail.com";
        public string EmailPassword { get; set; } = "votre-mot-de-passe-app";
        public string EmailTo { get; set; } = "admin@votre-domaine.com";
        public bool EnableSsl { get; set; } = true;
        public bool IncludeSubdirectories { get; set; } = true;

        public int DailyReportHour { get; set; } = 18; 

        public static void CreateDefault(string filePath)
        {
            var config = new Config();
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(filePath, json);
        }

        public static Config Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                CreateDefault(filePath);
                throw new FileNotFoundException($"Fichier de config introuvable : {filePath}. Un fichier par défaut a été créé, veuillez le remplir puis relancer l'application.");
            }

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Config>(json)
                   ?? throw new InvalidOperationException("Impossible de parser config.json");
        }
    }
}