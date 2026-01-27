using NPoco;
using Stripe;
using System.Globalization;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common;
using Umbraco.Cms.Web.Common.PublishedModels;
using website.Helpers;
using website.Models;
using website.Models.Database;
using Event = Umbraco.Cms.Web.Common.PublishedModels.Event;

namespace website.Services
{
    public interface IOrderProcessingService
    {
        Task<OrderCompletionResult> FinalizeOrderAsync(string paymentIntentId);
        Task<OrderCompletionResult> FinalizeFreeOrderAsync(int orderId, string pin);
    }

    public class OrderProcessingService : IOrderProcessingService
    {
        private readonly IUmbracoDatabaseFactory _databaseFactory;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly ILogger<OrderProcessingService> _logger;
        private readonly UmbracoHelper _umbracoHelper;

        public OrderProcessingService(
            IUmbracoDatabaseFactory databaseFactory,
            IConfiguration configuration,
            IEmailService emailService,
            ILogger<OrderProcessingService> logger,
            UmbracoHelper umbracoHelper)
        {
            _databaseFactory = databaseFactory;
            _configuration = configuration;
            _emailService = emailService;
            _logger = logger;
            _umbracoHelper = umbracoHelper;
        }

        public async Task<OrderCompletionResult> FinalizeOrderAsync(string paymentIntentId)
        {
            using var db = _databaseFactory.CreateDatabase();
            var orderQuery = db.SqlContext.Sql("WHERE StripeSessionId = @0", paymentIntentId);
            var order = await db.SingleOrDefaultAsync<OrderModel>(orderQuery);

            if (order == null)
            {
                _logger.LogWarning("FinalizeOrderAsync called with invalid payment_intent_id: {PaymentIntentId}", paymentIntentId);
                return new OrderCompletionResult { Success = false };
            }

            var ticketsQuery = db.SqlContext.Sql("WHERE OrderId = @0", order.Id);
            var tickets = await db.FetchAsync<TicketModel>(ticketsQuery);

            if (order.Status == "Completed")
            {
                return new OrderCompletionResult { Success = true, WasAlreadyCompleted = true, Order = order, Tickets = tickets };
            }

            if (!paymentIntentId.StartsWith("pi_"))
            {
                return await FulfillOrderAsync(db, order, tickets);
            }

            string stripeSecretKey = null;
            var firstTicket = tickets.FirstOrDefault();
            if (firstTicket != null)
            {
                var eventPage = _umbracoHelper.Content(firstTicket.EventNodeId) as Event;
                if (eventPage?.Organizer != null && !string.IsNullOrWhiteSpace(eventPage.Organizer.Value<string>("stripeSecretKey")))
                {
                    stripeSecretKey = eventPage.Organizer.Value<string>("stripeSecretKey");
                }
            }

            StripeConfiguration.ApiKey = stripeSecretKey;
            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(paymentIntentId);

            if (paymentIntent.Status == "succeeded")
            {
                order.CustomerName = paymentIntent.Shipping?.Name ?? order.CustomerName;
                order.CustomerEmail = paymentIntent.ReceiptEmail ?? order.CustomerEmail;

                return await FulfillOrderAsync(db, order, tickets);
            }

            return new OrderCompletionResult { Success = false, Order = order, Tickets = tickets };
        }

        public async Task<OrderCompletionResult> FinalizeFreeOrderAsync(int orderId, string pin)
        {
            using var db = _databaseFactory.CreateDatabase();
            var query = db.SqlContext.Sql("WHERE Id = @0", orderId);
            var order = await db.SingleOrDefaultAsync<OrderModel>(query);

            if (order == null) return new OrderCompletionResult { Success = false };

            var ticketsQuery = db.SqlContext.Sql("WHERE OrderId = @0", order.Id);
            var tickets = await db.FetchAsync<TicketModel>(ticketsQuery);

            if (order.Status == "Completed")
            {
                return new OrderCompletionResult { Success = true, WasAlreadyCompleted = true, Order = order, Tickets = tickets };
            }

            if (order.StripeSessionId?.Trim() == pin?.Trim())
            {
                return await FulfillOrderAsync(db, order, tickets);
            }

            return new OrderCompletionResult { Success = false, Order = order, Tickets = tickets };
        }

        private async Task<OrderCompletionResult> FulfillOrderAsync(IUmbracoDatabase db, OrderModel order, List<TicketModel> tickets)
        {
            order.Status = "Completed";
            await db.UpdateAsync(order);

            var allTicketsForEmail = new List<string>();
            foreach (var ticket in tickets)
            {
                if (string.IsNullOrEmpty(ticket.TicketCodes))
                {
                    var codes = new List<string>();
                    for (int i = 0; i < ticket.Quantity; i++)
                    {
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

            string recipient = !string.IsNullOrEmpty(order.CustomerEmail) ? order.CustomerEmail : "";

            if (!string.IsNullOrEmpty(recipient))
            {
                await _emailService.SendEmailAsync(recipient, subject, body, new[] { toAddress });
            }

            return new OrderCompletionResult
            {
                Success = true,
                WasAlreadyCompleted = false,
                Order = order,
                Tickets = tickets
            };
        }
    }
}