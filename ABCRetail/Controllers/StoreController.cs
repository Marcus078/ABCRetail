using ABCRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Controllers
{
    public class StoreController : Controller
    {
        private readonly AzureStorageService _storageService;

        public StoreController(AzureStorageService storageService)
        {
            _storageService = storageService;
        }

        // GET: /Store
        public async Task<IActionResult> Index()
        {
            // Load customers from Azure Table Storage
            ViewBag.Customers = await _storageService.GetCustomersAsync();

            // Load products from Azure Table Storage
            var products = await _storageService.GetProductsAsync();

            return View(products);
        }

        // GET: /Store/OrderHistory
        public async Task<IActionResult> OrderHistory()
        {
            // Pass 32 (Azure Queue Storage API limit) or call without arguments
            var queueMessages = await _storageService.PeekOrderMessagesAsync(32);

            // Only display actual order-processing messages
            var orderMessages = queueMessages
                .Where(msg => msg.Contains(
                    "Processing order",
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            return View("~/Views/Order/Index.cshtml", orderMessages);
        }

        // POST: /Store/PlaceOrder
        [HttpPost]
        public async Task<IActionResult> PlaceOrder(
            string customerEmail,
            string productName,
            double price,
            int quantity)
        {
            // -----------------------------------------
            // 1. VALIDATE CUSTOMER
            // -----------------------------------------
            if (string.IsNullOrWhiteSpace(customerEmail))
            {
                TempData["Error"] =
                    "Please select a customer before submitting an order!";

                return RedirectToAction("Index");
            }

            // -----------------------------------------
            // 2. VALIDATE QUANTITY
            // -----------------------------------------
            if (quantity <= 0)
            {
                TempData["Error"] =
                    "Quantity must be at least 1!";

                return RedirectToAction("Index");
            }

            // -----------------------------------------
            // 3. FIND PRODUCT IN AZURE TABLE STORAGE
            // -----------------------------------------
            var products =
                await _storageService.GetProductsAsync();

            var selectedProduct = products.FirstOrDefault(
                p => p.ProductName.Equals(
                    productName,
                    StringComparison.OrdinalIgnoreCase));

            if (selectedProduct == null)
            {
                TempData["Error"] =
                    "Selected product was not found in the catalog!";

                return RedirectToAction("Index");
            }

            // -----------------------------------------
            // 4. CHECK STOCK
            // -----------------------------------------
            if (selectedProduct.StockQuantity < quantity)
            {
                TempData["Error"] =
                    $"Insufficient stock! Only {selectedProduct.StockQuantity} unit(s) left for '{productName}'.";

                return RedirectToAction("Index");
            }

            // -----------------------------------------
            // 5. CALCULATE ORDER
            // -----------------------------------------
            double totalPrice = price * quantity;

            string orderId =
                $"ORD-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";

            // -----------------------------------------
            // 6. AZURE QUEUE STORAGE
            // -----------------------------------------
            string orderQueueMessage =
                $"Processing order {orderId} for customer '{customerEmail}' - " +
                $"Product: '{productName}' x{quantity} " +
                $"(Total: R{totalPrice:F2})";

            await _storageService.SendOrderMessageAsync(
                orderQueueMessage);

            // -----------------------------------------
            // 7. AZURE FILES
            // -----------------------------------------
            string logFileName =
                $"log_order_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.txt";

            string logText =
                $"Order processed: {orderId} | " +
                $"Customer: {customerEmail} | " +
                $"Product: {productName} | " +
                $"Quantity: {quantity} | " +
                $"Total: R{totalPrice:F2}";

            await _storageService.WriteLogFileAsync(
                logFileName,
                logText);

            // -----------------------------------------
            // 8. SUCCESS MESSAGE
            // -----------------------------------------
            TempData["Success"] =
                $"Order {orderId} placed successfully and added to Azure Queue!";

            // -----------------------------------------
            // 9. SHOW ORDER HISTORY
            // -----------------------------------------
            return RedirectToAction("OrderHistory");
        }
    }
}