using System;
using System.Net;
using System.Net.Mail;
using Application.Shared.Models;
using Microsoft.Extensions.Options;


namespace Application.Helpers;
public class EmailHelper
{
    private readonly EmailSettings _emailSettings;

    public EmailHelper(IOptions<EmailSettings> emailSettings)
    {
        _emailSettings = emailSettings.Value;
    }

    public void SendEmail(string recipientEmail, string subject, string body)
    {
        try
        {
            MailMessage mail = new MailMessage
            {
                From = new MailAddress(_emailSettings.SenderEmail),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mail.To.Add(recipientEmail);

            SmtpClient smtp = new SmtpClient
            {
                Host = _emailSettings.SmtpHost,
                Port = _emailSettings.SmtpPort,
                EnableSsl = true,
                Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            smtp.Send(mail);
            Console.WriteLine("Email sent successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while sending the email: {ex.Message}");
        }
    }
}
