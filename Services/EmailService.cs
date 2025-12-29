using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using WhoReapedWhat.Models;
namespace WhoReapedWhat.Services;

public class EmailService
{
    private readonly Config _config;
    private readonly ILogger<EmailService> _logger;
    private readonly List<DeletionEvent> _dailyDeletions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Timer? _dailyTimer;

    public EmailService(Config config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
        ScheduleDailyReport();
    }

    private void ScheduleDailyReport()
    {
        var now = DateTime.Now;
        var scheduledTime = new DateTime(now.Year, now.Month, now.Day, _config.DailyReportHour, 0, 0);

        // Si l'heure est déjà passée aujourd'hui, planifier pour demain
        if (now > scheduledTime)
        {
            scheduledTime = scheduledTime.AddDays(1);
        }

        var timeUntilReport = scheduledTime - now;

        _logger.LogInformation("Prochain rapport quotidien prévu à {time}", scheduledTime);

        _dailyTimer = new Timer(async _ => await SendDailyReport(), null, timeUntilReport, TimeSpan.FromDays(1));
    }

    public async Task AddDeletion(DeletionEvent deletion)
    {
        await _lock.WaitAsync();
        try
        {
            _dailyDeletions.Add(deletion);
            _logger.LogInformation("Suppression enregistrée : {file} à {time}", deletion.FileName, deletion.DeletedAt.ToString("HH:mm:ss"));
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task SendDailyReport()
    {
        await _lock.WaitAsync();
        List<DeletionEvent> deletionsToSend;

        try
        {
            if (_dailyDeletions.Count == 0)
            {
                _logger.LogInformation("Aucune suppression aujourd'hui, pas d'email envoyé");
                return;
            }

            deletionsToSend = new List<DeletionEvent>(_dailyDeletions);
            _dailyDeletions.Clear();
        }
        finally
        {
            _lock.Release();
        }

        try
        {
            using var client = new SmtpClient(_config.SmtpServer, _config.SmtpPort)
            {
                Credentials = new NetworkCredential(_config.EmailFrom, _config.EmailPassword),
                EnableSsl = _config.EnableSsl
            };

            var date = DateTime.Now.ToString("dd/MM/yyyy");
            var subject = $"Rapport quotidien - {deletionsToSend.Count} suppression(s) détectée(s) le {date}";

            // Grouper par utilisateur
            var groupedByUser = deletionsToSend.GroupBy(d => d.User).OrderBy(g => g.Key);

            var tableRows = "";
            foreach (var userGroup in groupedByUser)
            {
                var deletions = userGroup.OrderBy(d => d.DeletedAt).ToList();
                foreach (var deletion in deletions)
                {
                    var rowColor = deletions.IndexOf(deletion) % 2 == 0 ? "#f9f9f9" : "#ffffff";
                    tableRows += $@"
                <tr style='background:{rowColor};'>
                    <td style='padding:10px;border-bottom:1px solid #e0e0e0;'>{WebUtility.HtmlEncode(deletion.FileName)}</td>
                    <td style='padding:10px;border-bottom:1px solid #e0e0e0;font-size:12px;color:#666;'>{WebUtility.HtmlEncode(deletion.FilePath)}</td>
                    <td style='padding:10px;border-bottom:1px solid #e0e0e0;text-align:center;'>{WebUtility.HtmlEncode(deletion.User)}</td>
                    <td style='padding:10px;border-bottom:1px solid #e0e0e0;text-align:center;font-weight:bold;'>{deletion.DeletedAt:HH:mm:ss}</td>
                </tr>";
                }
            }

            var body = $@"
<html>
<body style='font-family:Segoe UI,Arial,sans-serif;background:#f7f7f7;padding:24px;margin:0;'>
    <div style='max-width:900px;margin:auto;background:#fff;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);overflow:hidden;'>
        <div style='background:linear-gradient(135deg, #d32f2f 0%, #c62828 100%);padding:24px;color:#fff;'>
            <h2 style='margin:0;font-size:24px;'>🗑️ Rapport quotidien - Suppressions de fichiers</h2>
            <p style='margin:8px 0 0 0;opacity:0.9;'>Date : {date}</p>
        </div>
        
        <div style='padding:24px;'>
            <div style='background:#fff3cd;border-left:4px solid #ffc107;padding:16px;margin-bottom:24px;border-radius:4px;'>
                <strong style='color:#856404;'>📊 Résumé :</strong>
                <span style='color:#856404;'> {deletionsToSend.Count} fichier(s) supprimé(s) aujourd'hui</span>
            </div>

            <table style='width:100%;border-collapse:collapse;'>
                <thead>
                    <tr style='background:#f5f5f5;'>
                        <th style='padding:12px;text-align:left;border-bottom:2px solid #d32f2f;font-weight:600;'>Nom du fichier</th>
                        <th style='padding:12px;text-align:left;border-bottom:2px solid #d32f2f;font-weight:600;'>Chemin complet</th>
                        <th style='padding:12px;text-align:center;border-bottom:2px solid #d32f2f;font-weight:600;'>Utilisateur</th>
                        <th style='padding:12px;text-align:center;border-bottom:2px solid #d32f2f;font-weight:600;'>Heure</th>
                    </tr>
                </thead>
                <tbody>
                    {tableRows}
                </tbody>
            </table>
        </div>

        <div style='background:#f7f7f7;padding:16px 24px;border-top:1px solid #e0e0e0;'>
            <p style='color:#888;font-size:13px;margin:0;'>
                Ce message a été généré automatiquement par <strong>WhoReapedWhat</strong> à {DateTime.Now:HH:mm:ss}.
            </p>
        </div>
    </div>
</body>
</html>";

            var mail = new MailMessage
            {
                From = new MailAddress(_config.EmailFrom, "WhoReapedWhat"),
                Subject = subject,
                IsBodyHtml = true,
                Body = body
            };
            mail.To.Add(new MailAddress(_config.EmailTo));

            await client.SendMailAsync(mail);

            _logger.LogInformation("Rapport quotidien envoyé : {count} suppression(s)", deletionsToSend.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'envoi du rapport quotidien");
        }
    }

    public void Dispose()
    {
        _dailyTimer?.Dispose();
    }
}