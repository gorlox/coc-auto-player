using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;

namespace Clash_Runner
{
    public class Mailer
    {
        public void SendMail(string subject, string message)
        {
            var fromAddress = new MailAddress("some@email.com", "Clash Robot");
            var toAddress = new MailAddress("some@email.com", "Clash Robot");
            const string fromPassword = "asdfasdfasdf";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };
            using (var msg = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = message
            })
            {
                smtp.Send(msg);
            }
        }
    }
}
