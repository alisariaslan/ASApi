using MailKit.Security;
using MimeKit.Text;
using MimeKit;
using SarsMinimalApi.Models;
using MailKit.Net.Smtp;

namespace SarsMinimalApi.Helpers
{
	public static class MailHelper
	{
		public static async Task<bool> SendMailAsync(WebApplicationBuilder builder, MailModel mailModel)
		{

			var from = builder.Configuration["MailSettings:Mail"];
			var email = new MimeMessage();
			email.From.Add(MailboxAddress.Parse(from));
			email.To.Add(MailboxAddress.Parse(mailModel.To));
			email.Subject = mailModel.Subject;
			email.Body = new TextPart(TextFormat.Html) { Text = mailModel.Body.ToString() };
			var host = builder.Configuration["MailSettings:Host"];
			var port = builder.Configuration["MailSettings:Port"];
			using var smtp = new SmtpClient();
			smtp.Connect(host, Convert.ToInt32(port), SecureSocketOptions.StartTls);
			var mail = builder.Configuration["MailSettings:Mail"];
			var mailpass = builder.Configuration["MailSettings:Password"];
			smtp.Authenticate(mail, mailpass);
			smtp.Send(email);
			smtp.Disconnect(true);
			return true;
		}
	}
}
