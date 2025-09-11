

namespace WhoReapedWhat.Models
{
    public class Config
    {
        public string WatchPath { get; set; } = @"D:\Media";
        public string SmtpServer { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string EmailFrom { get; set; } = "votre-email@gmail.com";
        public string EmailPassword { get; set; } = "votre-mot-de-passe-app";
        public string EmailTo { get; set; } = "admin@votre-domaine.com";
        public bool EnableSsl { get; set; } = true;
    }
}
