using ABCRetail.Models;
using ABCRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Controllers
{
    public class ProductController : Controller
    {
        private readonly AzureStorageService _storageService;

        public ProductController(AzureStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _storageService.GetProductsAsync();
            return View(products);
        }

        [HttpPost]
        public async Task<IActionResult> AddProduct(
    Product product,
    IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                TempData["Error"] = "Please select a valid image file.";
                return RedirectToAction("Index");
            }

            try
            {
                // 1. BLOB STORAGE
                string imageUrl =
                    await _storageService.UploadBlobAsync(imageFile);

                product.ImageUrl = imageUrl;

                // 2. TABLE STORAGE
                await _storageService.AddProductAsync(product);

                // 3. QUEUE - INVENTORY ONLY
                await _storageService.SendOrderMessageAsync(
                    $"Processing inventory management: Added product '{product.ProductName}' with initial stock of {product.StockQuantity}");

                // 4. AZURE FILES
                string logFileName =
                    $"log_product_{Guid.NewGuid():N}.txt";

                await _storageService.WriteLogFileAsync(
                    logFileName,
                    $"Product added: {product.ProductName}. Image: {imageFile.FileName}");

                TempData["Success"] =
                    "Product saved to Table Storage, image uploaded to Blob Storage, inventory queued and log created.";
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    $"Error processing product: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}