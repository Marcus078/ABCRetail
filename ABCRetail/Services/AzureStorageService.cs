using ABCRetail.Models;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using System.Text;

namespace ABCRetail.Services
{
    public class AzureStorageService
    {
        private readonly string _connectionString;

        public AzureStorageService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("AzureStorage")
                ?? throw new InvalidOperationException("Azure Storage connection string missing.");
        }

        // =========================================================
        // 1. AZURE TABLE STORAGE - CUSTOMERS
        // =========================================================

        public async Task AddCustomerAsync(CustomerProfile customer)
        {
            var tableClient = new TableClient(
                _connectionString,
                "Customers");

            await tableClient.CreateIfNotExistsAsync();

            // Make sure every customer gets a unique RowKey
            if (string.IsNullOrWhiteSpace(customer.RowKey))
            {
                customer.RowKey = Guid.NewGuid().ToString();
            }

            customer.PartitionKey = "Customer";

            await tableClient.AddEntityAsync(customer);
        }

        public async Task<List<CustomerProfile>> GetCustomersAsync()
        {
            var tableClient = new TableClient(
                _connectionString,
                "Customers");

            await tableClient.CreateIfNotExistsAsync();

            var customers = new List<CustomerProfile>();

            await foreach (var customer in tableClient.QueryAsync<CustomerProfile>())
            {
                customers.Add(customer);
            }

            return customers;
        }


        // =========================================================
        // 2. AZURE TABLE STORAGE - PRODUCTS
        // =========================================================

        public async Task AddProductAsync(Product product)
        {
            var tableClient = new TableClient(
                _connectionString,
                "Products");

            await tableClient.CreateIfNotExistsAsync();

            if (string.IsNullOrWhiteSpace(product.RowKey))
            {
                product.RowKey = Guid.NewGuid().ToString();
            }

            product.PartitionKey = "Product";

            await tableClient.AddEntityAsync(product);
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            var tableClient = new TableClient(
                _connectionString,
                "Products");

            await tableClient.CreateIfNotExistsAsync();

            var products = new List<Product>();

            await foreach (var product in tableClient.QueryAsync<Product>())
            {
                products.Add(product);
            }

            return products;
        }


        // =========================================================
        // 3. AZURE BLOB STORAGE
        // =========================================================

        public async Task<string> UploadBlobAsync(IFormFile file)
        {
            var container = new BlobContainerClient(
                _connectionString,
                "product-images");

            // DO NOT request public access
            await container.CreateIfNotExistsAsync();

            string uniqueFileName =
                $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";

            var blob = container.GetBlobClient(uniqueFileName);

            using var stream = file.OpenReadStream();

            await blob.UploadAsync(
                stream,
                overwrite: true);

            return blob.Uri.ToString();
        }


        // =========================================================
        // 4. AZURE QUEUE STORAGE
        // =========================================================

        public async Task SendOrderMessageAsync(string message)
        {
            var queue = new QueueClient(
                _connectionString,
                "orders");

            await queue.CreateIfNotExistsAsync();

            await queue.SendMessageAsync(message);
        }

        public async Task<List<string>> PeekOrderMessagesAsync(int maxMessages = 32)
        {
            var queue = new QueueClient(_connectionString, "orders");
            await queue.CreateIfNotExistsAsync();

            // Ensure maxMessages stays within Azure's valid range (1 to 32)
            int safeMax = Math.Clamp(maxMessages, 1, 32);

            var result = await queue.PeekMessagesAsync(safeMax);

            var messages = new List<string>();
            foreach (var message in result.Value)
            {
                messages.Add(message.MessageText);
            }

            return messages;
        }


        // =========================================================
        // 5. AZURE FILE STORAGE
        // =========================================================

        public async Task WriteLogFileAsync(
            string fileName,
            string logMessage)
        {
            var share = new ShareClient(
                _connectionString,
                "logs");

            await share.CreateIfNotExistsAsync();

            var directory = share.GetRootDirectoryClient();

            var file = directory.GetFileClient(fileName);

            byte[] data = Encoding.UTF8.GetBytes(
                $"{DateTime.UtcNow:O} - {logMessage}");

            using var stream = new MemoryStream(data);

            // Create the Azure File
            await file.CreateAsync(data.Length);

            // Upload its contents
            stream.Position = 0;

            await file.UploadRangeAsync(
                new HttpRange(0, data.Length),
                stream);
        }


        // =========================================================
        // 6. TEST DATA FOR PROJECT 1
        // =========================================================

        public async Task CreateProjectTestDataAsync()
        {
            // -----------------------------------------------------
            // CUSTOMERS - 5 TABLE RECORDS
            // -----------------------------------------------------

            var existingCustomers = await GetCustomersAsync();

            var customers = new[]
            {
                new CustomerProfile
                {
                    FirstName = "John",
                    LastName = "Smith",
                    Email = "john.smith@example.com",
                    PhoneNumber = "0711111111"
                },

                new CustomerProfile
                {
                    FirstName = "Sarah",
                    LastName = "Mokoena",
                    Email = "sarah.mokoena@example.com",
                    PhoneNumber = "0722222222"
                },

                new CustomerProfile
                {
                    FirstName = "David",
                    LastName = "Nkosi",
                    Email = "david.nkosi@example.com",
                    PhoneNumber = "0733333333"
                },

                new CustomerProfile
                {
                    FirstName = "Thabo",
                    LastName = "Maseko",
                    Email = "thabo.maseko@example.com",
                    PhoneNumber = "0744444444"
                },

                new CustomerProfile
                {
                    FirstName = "Lerato",
                    LastName = "Molefe",
                    Email = "lerato.molefe@example.com",
                    PhoneNumber = "0755555555"
                }
            };

            foreach (var customer in customers)
            {
                if (!existingCustomers.Any(
                    x => x.Email.Equals(
                        customer.Email,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    await AddCustomerAsync(customer);
                }
            }


            // -----------------------------------------------------
            // PRODUCTS - 5 TABLE RECORDS
            // -----------------------------------------------------

            var existingProducts = await GetProductsAsync();

            var products = new[]
            {
                new Product
                {
                    ProductName = "Laptop",
                    Price = 12999,
                    StockQuantity = 20
                },

                new Product
                {
                    ProductName = "Wireless Mouse",
                    Price = 499,
                    StockQuantity = 50
                },

                new Product
                {
                    ProductName = "Mechanical Keyboard",
                    Price = 899,
                    StockQuantity = 30
                },

                new Product
                {
                    ProductName = "24 Inch Monitor",
                    Price = 2499,
                    StockQuantity = 15
                },

                new Product
                {
                    ProductName = "Wireless Headphones",
                    Price = 1299,
                    StockQuantity = 25
                }
            };

            foreach (var product in products)
            {
                if (!existingProducts.Any(
                    x => x.ProductName.Equals(
                        product.ProductName,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    await AddProductAsync(product);

                    // Queue should contain inventory-related
                    // messages.
                    await SendOrderMessageAsync(
                        $"Processing inventory management: Added product '{product.ProductName}' with initial stock of {product.StockQuantity}");

                    await WriteLogFileAsync(
                        $"log_inventory_{Guid.NewGuid():N}.txt",
                        $"Inventory record created for product '{product.ProductName}' with stock quantity {product.StockQuantity}");
                }
            }


            // -----------------------------------------------------
            // 5+ FILES
            // -----------------------------------------------------

            for (int i = 1; i <= 5; i++)
            {
                await WriteLogFileAsync(
                    $"project1_test_log_{i}_{Guid.NewGuid():N}.txt",
                    $"ABC Retail Project 1 test log #{i}");
            }


            // -----------------------------------------------------
            // 5+ QUEUE RECORDS
            // -----------------------------------------------------

            await SendOrderMessageAsync(
                "Processing order ORD-TEST01 for john.smith@example.com - Product: Laptop x1 (Total: R12999.00)");

            await SendOrderMessageAsync(
                "Processing order ORD-TEST02 for sarah.mokoena@example.com - Product: Wireless Mouse x2 (Total: R998.00)");

            await SendOrderMessageAsync(
                "Processing order ORD-TEST03 for david.nkosi@example.com - Product: Mechanical Keyboard x1 (Total: R899.00)");

            await SendOrderMessageAsync(
                "Processing order ORD-TEST04 for thabo.maseko@example.com - Product: 24 Inch Monitor x1 (Total: R2499.00)");

            await SendOrderMessageAsync(
                "Processing order ORD-TEST05 for lerato.molefe@example.com - Product: Wireless Headphones x2 (Total: R2598.00)");
        }
    }
}