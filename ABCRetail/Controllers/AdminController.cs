using ABCRetail.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.Controllers
{
    public class AdminController : Controller
    {
        private readonly AzureStorageService _storageService;

        public AdminController(AzureStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task<IActionResult> Index()
        {
            // Peek at active messages in Queue Storage
            ViewBag.QueueMessages = await _storageService.PeekOrderMessagesAsync(15);
            return View();
        }

        
    }
}