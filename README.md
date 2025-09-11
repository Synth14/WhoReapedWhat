# WhoReapedWhat

File deletion monitoring service with email alerts. Track what gets deleted from your storage and get notified instantly.

## Features

- **Real-time monitoring** of file deletions
- **Email notifications** with detailed information
- **Cross-platform** support (Windows, Linux, macOS)
- **User detection** - attempts to identify who deleted files
- **Audit integration** on Windows with NTFS audit logs
- **Docker support** with multi-platform builds
- **Configuration via JSON** file

## Quick Start

### Native Installation

1. Download the latest release for your platform
2. Run the executable once, it will create a `config.json` file in its own folder
3. Fill the parameters (SMTP server, port, ssl, email, etc)
4. Re-run the executable.

### Docker

```bash
# Pull and run
docker pull your-registry/whoreaped:latest
docker run -d \
  -v /path/to/watch:/app/watch:ro \
  -v ./config.json:/app/config.json \
  your-registry/whoreaped:latest
```

### Docker Compose

```yaml
version: '3.8'
services:
  whoreaped:
    image: your-registry/whoreaped:latest
    volumes:
      - /path/to/monitor:/app/watch:ro
      - ./config.json:/app/config.json
      - ./logs:/app/logs
    restart: unless-stopped
```

## Configuration

Create a `config.json` file in the same directory as the executable:

```json
{
  "WatchPath": "/path/to/monitor",
  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": 587,
  "EmailFrom": "your-email@gmail.com",
  "EmailPassword": "your-app-password",
  "EmailTo": "admin@your-domain.com",
  "EnableSsl": true
}
```

### Configuration Options

| Option | Description | Example |
|--------|-------------|---------|
| `WatchPath` | Directory to monitor | `"D:\Media"` (Windows) or `"/mnt/storage"` (Linux) |
| `SmtpServer` | SMTP server hostname | `"smtp.gmail.com"` |
| `SmtpPort` | SMTP server port | `587` |
| `EmailFrom` | Sender email address | `"monitoring@company.com"` |
| `EmailPassword` | Email password or app password | `"your-app-password"` |
| `EmailTo` | Recipient email address | `"admin@company.com"` |
| `EnableSsl` | Enable SSL/TLS encryption | `true` |

### Email Provider Setup

#### Gmail
1. Enable 2-factor authentication
2. Generate an App Password in Security settings
3. Use the App Password in `EmailPassword` field

#### Outlook/Hotmail
```json
{
  "SmtpServer": "smtp-mail.outlook.com",
  "SmtpPort": 587,
  "EnableSsl": true
}
```

## User Detection

### Windows
- **With NTFS Audit**: Shows actual user and process that deleted the file
- **Without Audit**: Shows service user and active processes
- **Enable NTFS Audit**: Run as admin: `auditpol /set /subcategory:"File System" /success:enable`

### Linux/macOS
- Shows current user and detected processes (Plex, Docker, etc.)

## Platform Differences

### Windows
- Full filesystem event detection
- NTFS audit log integration
- Service installation support

### Linux/Docker  
- Good filesystem event detection
- Process-based user detection
- May miss rapid file operations through Docker volumes

### macOS
- Full filesystem event detection  
- Process-based user detection

## Installation

### Windows Service

```cmd
# Install as Windows Service (requires admin)
sc create WhoReapedWhat binPath="C:\path\to\WhoReapedWhat.exe"
sc start WhoReapedWhat
```

### Systemd Service (Linux)

Create `/etc/systemd/system/whoreaped.service`:

```ini
[Unit]
Description=WhoReapedWhat File Monitor
After=network.target

[Service]
Type=simple
User=monitoring
WorkingDirectory=/opt/whoreaped
ExecStart=/opt/whoreaped/WhoReapedWhat
Restart=always

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable whoreaped
sudo systemctl start whoreaped
```

## Development

### Building

```bash
# Build for current platform
dotnet build -c Release

# Build for specific platform
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
dotnet publish -c Release -r osx-x64 --self-contained
```

### Docker Build

```bash
# Build multi-platform
docker buildx build --platform linux/amd64,linux/arm64 -t whoreaped:latest .
```

## Troubleshooting

### No Email Notifications
- Check SMTP credentials and server settings
- Verify firewall allows outbound SMTP traffic
- Check email provider's security settings

### No File Detection
- Verify `WatchPath` exists and is accessible
- Check file permissions
- On Docker: ensure volume is properly mounted

### Permission Errors
- Windows: Run as Administrator for NTFS audit access
- Linux: Ensure user has read access to monitored directory

### Docker Limitations
- Filesystem events may not work reliably through Docker volumes
- Consider native installation for production use

## Logs

- **Console output**: Real-time monitoring status
- **suppressions.log**: Local file with all deletion events
- **Email alerts**: Detailed HTML notifications

## Security Considerations

- Store email passwords securely (use app passwords)
- Run with minimal required privileges
- Monitor access to configuration file
- Consider network security for SMTP traffic

## License

MIT License - see LICENSE file for details.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## Support

For issues and questions:
- Check the troubleshooting section
- Review existing GitHub issues
- Create a new issue with detailed information
