using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using WhoReapedWhat.Models;
namespace WhoReapedWhat.Services;

public class EmailService
{
    private readonly Config _config;
    private readonly ILogger<EmailService> _logger;
    private readonly Channel<(string Subject, string Body)> _queue;

    public EmailService(Config config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
        _queue = Channel.CreateUnbounded<(string, string)>();
        Task.Run(ProcessQueue);
    }

    public async Task QueueMail(string subject, string body)
    {
        await _queue.Writer.WriteAsync((subject, body));
    }

    private async Task ProcessQueue()
    {
        var lastSent = DateTime.MinValue;
        while (await _queue.Reader.WaitToReadAsync())
        {
            var (subject, body) = await _queue.Reader.ReadAsync();

            // cooldown de 5 secondes pour éviter le spam
            if ((DateTime.Now - lastSent).TotalSeconds < 5)
            {
                await Task.Delay(5000);
            }

            try
            {
                using var client = new SmtpClient(_config.SmtpServer, _config.SmtpPort)
                {
                    Credentials = new NetworkCredential(_config.EmailFrom, _config.EmailPassword),
                    EnableSsl = true
                };

                // Extraction des infos depuis le body (format attendu)
                var bodyLines = body.Split('\n');
                string filePath = bodyLines.Length > 0 ? bodyLines[0].Replace("Fichier supprimé : ", "") : "";
                string user = bodyLines.Length > 1 ? bodyLines[1].Replace("Utilisateur suspecté : ", "") : "";
                string date = bodyLines.Length > 2 ? bodyLines[2].Replace("Date : ", "") : "";

                var mail = new MailMessage
                {
                    From = new MailAddress(_config.EmailFrom, "WhoReapedWhat"),
                    Subject = subject,
                    IsBodyHtml = true,
                    Body = $@"
    <html>
    <body style='font-family:Segoe UI,Arial,sans-serif;background:#f7f7f7;padding:24px;'>
        <div style='max-width:520px;margin:auto;background:#fff;border-radius:8px;box-shadow:0 2px 8px #0001;padding:24px;'>
            <h2 style='color:#d32f2f;margin-top:0;'>🗑️ Fichier supprimé détecté</h2>
            <p><strong>Nom du fichier :</strong> {System.Net.WebUtility.HtmlEncode(System.IO.Path.GetFileName(filePath))}</p>
            <p><strong>Chemin :</strong> {System.Net.WebUtility.HtmlEncode(filePath)}</p>
            <p><strong>Utilisateur suspecté :</strong> {System.Net.WebUtility.HtmlEncode(user)}</p>
            <p><strong>Date :</strong> {System.Net.WebUtility.HtmlEncode(date)}</p>
            <hr style='margin:24px 0;'>
            <p style='color:#888;font-size:13px;'>Ce message a été généré automatiquement par <b>WhoReapedWhat</b>.</p>
        </div>
    </body>
    </html>"
                };
                mail.To.Add(new MailAddress(_config.EmailTo));

                await client.SendMailAsync(mail);

                _logger.LogInformation("Email envoyé : {subject}", subject);
                lastSent = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de l’envoi de l’email");
            }
        }
    }
}
