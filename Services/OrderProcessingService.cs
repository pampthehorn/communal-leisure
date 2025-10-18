using Stripe;
using System.Globalization;
using Umbraco.Cms.Infrastructure.Persistence;
using website.Helpers;
using website.Models;
using website.Models.Database;

namespace website.Services
{
    public interface IOrderProcessingService
    {
        Task<OrderCompletionResult> FinalizeOrderAsync(string paymentIntentId);
    }

    public class OrderProcessingService : IOrderProcessingService
    {
        private readonly IUmbracoDatabaseFactory _databaseFactory;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ILogger<OrderProcessingService> _logger;

        public OrderProcessingService(
            IUmbracoDatabaseFactory databaseFactory,
            IConfiguration configuration,
            IEmailService emailService,
            ILogger<OrderProcessingService> logger)
        {
            _databaseFactory = databaseFactory;
            _configuration = configuration;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<OrderCompletionResult> FinalizeOrderAsync(string paymentIntentId)
        {
            using var db = _databaseFactory.CreateDatabase();
            var order = await db.SingleOrDefaultAsync<OrderModel>("WHERE StripeSessionId = @0", paymentIntentId);

            if (order == null)
            {
                _logger.LogWarning("FinalizeOrderAsync was called with an invalid payment_intent_id: {PaymentIntentId}", paymentIntentId);
                return new OrderCompletionResult { Success = false };
            }

            var tickets = await db.FetchAsync<TicketModel>("WHERE OrderId = @0", order.Id);

            if (order.Status == "Completed")
            {
                return new OrderCompletionResult
                {
                    Success = true,
                    WasAlreadyCompleted = true,
                    Order = order,
                    Tickets = tickets
                };
            }

            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(paymentIntentId);

            if (paymentIntent.Status == "succeeded")
            {
                order.Status = "Completed";
                order.CustomerName = paymentIntent.Shipping?.Name ?? order.CustomerName;
                order.CustomerEmail = paymentIntent.ReceiptEmail ?? order.CustomerEmail;
                await db.UpdateAsync(order);

                var allTicketsForEmail = new List<string>();
                foreach (var ticket in tickets)
                {
                    if (string.IsNullOrEmpty(ticket.TicketCodes))
                    {
                        var codes = new List<string>();
                        for (int i = 0; i < ticket.Quantity; i++)
                        {
                            // Use the new static helper
                            codes.Add(TicketCodeGenerator.GenerateUniqueTicketCode());
                        }
                        ticket.TicketCodes = string.Join(",", codes);
                        await db.UpdateAsync(ticket);
                    }
                    allTicketsForEmail.Add($"<li><strong>{ticket.Quantity}x {ticket.Type} ({ticket.EventName})</strong><br/>Codes: {ticket.TicketCodes}</li>");
                }

                var description = string.Join(", ", tickets.Select(t => $"{t.Quantity}x {t.Type} ticket for {t.EventName}"));
                string subject = $"Ticket Purchase: {description}";
                string body = $"<p>Hi {order.CustomerName},</p>" +
                              $"<p>Thank you for your order! Here are your ticket codes:</p>" +
                              "<ul style='list-style: none; padding-left: 0;'>" +
                              string.Join("", allTicketsForEmail) +
                              "</ul>" +
                              $"<p><strong>Order ID:</strong> {order.Id}<br/>" +
                              $"<strong>Total:</strong> {((order.TotalAmount / 100.0).ToString("C", new CultureInfo("en-GB")))}</p>" +
                              "<p>Please show these codes at the event entrance.</p>";

                string toAddress = "comlesweb@gmail.com";
                await _emailService.SendEmailAsync(paymentIntent.ReceiptEmail, subject, body, new[] { toAddress });

                return new OrderCompletionResult
                {
                    Success = true,
                    WasAlreadyCompleted = false,
                    Order = order,
                    Tickets = tickets
                };
            }

            // Payment not successful
            return new OrderCompletionResult { Success = false, Order = order, Tickets = tickets };
        }
    }

}
