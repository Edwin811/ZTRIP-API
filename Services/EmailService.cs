using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Z_TRIP.Services
{
    public class EmailService
    {
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _senderEmail;
        private readonly string _senderName;
        private readonly bool _enableSsl;
        private readonly bool _useDefaultCredentials;

        public EmailService(IConfiguration config)
        {
            _smtpServer = config["Email:SmtpServer"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(config["Email:SmtpPort"] ?? "587");
            _smtpUsername = config["Email:Username"] ?? throw new InvalidOperationException("Email username not configured");
            _smtpPassword = config["Email:Password"] ?? throw new InvalidOperationException("Email password not configured");
            _senderEmail = config["Email:SenderEmail"] ?? _smtpUsername;
            _senderName = config["Email:SenderName"] ?? "Z-TRIP System";

            // Pastikan nilai default adalah true untuk Gmail
            _enableSsl = bool.Parse(config["Email:EnableSsl"] ?? "true");
            _useDefaultCredentials = bool.Parse(config["Email:UseDefaultCredentials"] ?? "false");
        }

        public async Task SendOTP(string toEmail, string otp)
        {
            try
            {
                var client = new SmtpClient(_smtpServer, _smtpPort)
                {
                    EnableSsl = _enableSsl,  // Menggunakan nilai dari konfigurasi
                    UseDefaultCredentials = _useDefaultCredentials,
                    Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_senderEmail, _senderName),
                    Subject = "Z-TRIP - Kode Reset Password",
                    IsBodyHtml = true
                };

                // HTML body dengan styling untuk tampilan yang lebih baik
                mailMessage.Body = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px; }}
                        .header {{ background-color: #4A90E2; padding: 10px; color: white; text-align: center; border-radius: 5px 5px 0 0; }}
                        .content {{ padding: 20px; }}
                        .otp-code {{ font-size: 32px; font-weight: bold; text-align: center; letter-spacing: 5px; margin: 20px 0; color: #4A90E2; }}
                        .footer {{ font-size: 12px; text-align: center; margin-top: 30px; color: #777; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>Reset Password Z-TRIP</h2>
                        </div>
                        <div class='content'>
                            <p>Halo,</p>
                            <p>Kami menerima permintaan untuk mengatur ulang kata sandi akun Z-TRIP Anda. Berikut adalah kode verifikasi untuk mengatur ulang kata sandi:</p>
                            
                            <div class='otp-code'>{otp}</div>
                            
                            <p>Kode ini berlaku selama 30 menit. Jika Anda tidak meminta pengaturan ulang kata sandi, silakan abaikan email ini.</p>
                            
                            <p>Terima kasih,<br>Tim Z-TRIP</p>
                        </div>
                        <div class='footer'>
                            <p>Email ini dikirim secara otomatis, mohon tidak membalas email ini.</p>
                        </div>
                    </div>
                </body>
                </html>";

                mailMessage.To.Add(toEmail);

                try
                {
                    await client.SendMailAsync(mailMessage);
                }
                catch (Exception ex)
                {
                    // Log error
                    Console.WriteLine($"Error sending email: {ex.Message}");
                    throw new Exception("Gagal mengirim email", ex);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending email: {ex.Message}");
                throw;
            }
        }
    }
}