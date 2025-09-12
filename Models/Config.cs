

namespace WhoReapedWhat.Models
{
    public class Config
    {
        // Chemins multiples à surveiller
        public List<string> WatchPaths { get; set; } = new List<string> { @"D:\Media" };

        // Types de fichiers à surveiller (extensions sans le point)
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

        // Si true, surveille TOUS les fichiers (ignore FileTypesToWatch)
        public bool WatchAllFileTypes { get; set; } = false;

        // Configuration email
        public string SmtpServer { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string EmailFrom { get; set; } = "votre-email@gmail.com";
        public string EmailPassword { get; set; } = "votre-mot-de-passe-app";
        public string EmailTo { get; set; } = "admin@votre-domaine.com";
        public bool EnableSsl { get; set; } = true;
    }
}
