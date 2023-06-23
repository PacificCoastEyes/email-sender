using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace email_sender
{
    public class SendEmail
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public SendEmail(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<SendEmail>();
            _configuration = configuration;
        }

        public class Message
        {
            public string firstName { get; set;}
            public string lastName { get; set;}
            public string email { get; set;}
            public string phone { get; set;}
            public string message { get; set;}
        }

        public async Task<bool> DispatchEmail(Message message)
        {
            EmailClient emailClient = new(_configuration["AZURE_EMAIL_SERVICE_CONNECTION_STRING"]);

            string subject = "New message via Contact Form";
            string content = $"First Name: {message.firstName}\nLast Name: { message.lastName}\nEmail: {message.email}\nPhone: {message.phone}\nMessage: {message.message}";
            string sender = _configuration["SENDER_EMAIL_ADDRESS"];
            string recipient = _configuration["RECIPIENT_EMAIL_ADDRESS"];

            try
            {
                EmailSendOperation emailSendOperation = await emailClient.SendAsync(
                    Azure.WaitUntil.Completed,
                    sender,
                    recipient,
                    subject,
                    null,
                    content
                );
                EmailSendStatus status = emailSendOperation.Value.Status;

                Console.WriteLine($"Email Sent. Status = {status}");
                Console.WriteLine($"Email operation id = {emailSendOperation.Id}");

                if (status == "Succeeded")
                {
                    return true;
                }
                else
                {
                    throw new RequestFailedException("Failed");
                }
            } 
            catch ( RequestFailedException ex )
            {
                Console.WriteLine($"Email send operation failed with error code: {ex.ErrorCode}, message: {ex.Message}");
                return false;
            }
        }

        [Function("SendEmail")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request to send an email.");

            StreamReader reader = new(req.Body);
            string payload = reader.ReadToEnd();
            Message message = JsonSerializer.Deserialize<Message>(payload)!;

            bool emailSentSuccessfully = await DispatchEmail(message);

            HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync($"{{\"ok\": {emailSentSuccessfully.ToString().ToLower()}}}");

            return response;
        }
    }
}
