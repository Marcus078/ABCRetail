using ABCRetail.Models;
using ABCRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Controllers
{
    public class CustomerController : Controller
    {
        private readonly AzureStorageService _storageService;

        public CustomerController(AzureStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task<IActionResult> Index()
        {
            var customers = await _storageService.GetCustomersAsync();
            return View(customers);
        }

        [HttpPost]
       
        public async Task<IActionResult> AddCustomer(CustomerProfile customer)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required customer fields.";
                return RedirectToAction("Index");
            }

            try
            {
                // TABLE STORAGE
                await _storageService.AddCustomerAsync(customer);

                // FILE STORAGE
                string logFileName =
                    $"log_customer_{Guid.NewGuid():N}.txt";

                await _storageService.WriteLogFileAsync(
                    logFileName,
                    $"Customer registered: {customer.FirstName} {customer.LastName} ({customer.Email})");

                TempData["Success"] =
                    "Customer profile saved to Azure Table Storage!";
            }
            catch (Exception ex)
            {
                TempData["Error"] =
                    $"Error saving customer: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}