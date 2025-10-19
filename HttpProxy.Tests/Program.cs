using System.Net;
using System.Text;

namespace HttpProxy.Tests
{
    internal class Program
    {
        private const string ProxyAddress = "http://localhost:8888";
        
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== HTTP Proxy Test Client ===");
            Console.WriteLine($"Testing proxy at: {ProxyAddress}");
            Console.WriteLine("Make sure the proxy server is running on port 8888!\n");
            
            await Task.Delay(1000); // Give user time to read

            // Run all tests
            await TestHttpGetRequest();
            await TestHttpsGetRequest();
            await TestHttpPostRequest();
            await TestMultipleRequests();
            await TestErrorHandling();

            Console.WriteLine("\n=== All Tests Completed ===");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task TestHttpGetRequest()
        {
            Console.WriteLine("\n--- Test 1: HTTP GET Request ---");
            try
            {
                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy(ProxyAddress),
                    UseProxy = true
                };

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetAsync("http://httpbin.org/get");
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Content Length: {content.Length} characters");
                Console.WriteLine($"Success: {response.IsSuccessStatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("? HTTP GET test PASSED");
                }
                else
                {
                    Console.WriteLine("? HTTP GET test FAILED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? HTTP GET test FAILED: {ex.Message}");
            }
        }

        static async Task TestHttpsGetRequest()
        {
            Console.WriteLine("\n--- Test 2: HTTPS GET Request ---");
            try
            {
                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy(ProxyAddress),
                    UseProxy = true,
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetAsync("https://httpbin.org/get");
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Content Length: {content.Length} characters");
                Console.WriteLine($"Success: {response.IsSuccessStatusCode}");
                
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("? HTTPS GET test PASSED");
                }
                else
                {
                    Console.WriteLine("? HTTPS GET test FAILED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? HTTPS GET test FAILED: {ex.Message}");
            }
        }

        static async Task TestHttpPostRequest()
        {
            Console.WriteLine("\n--- Test 3: HTTP POST Request ---");
            try
            {
                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy(ProxyAddress),
                    UseProxy = true
                };

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(10);

                var postData = new Dictionary<string, string>
                {
                    { "name", "Test User" },
                    { "message", "Testing HTTP Proxy" },
                    { "timestamp", DateTime.UtcNow.ToString("o") }
                };

                var content = new FormUrlEncodedContent(postData);
                var response = await client.PostAsync("http://httpbin.org/post", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine($"Content Length: {responseContent.Length} characters");
                Console.WriteLine($"Success: {response.IsSuccessStatusCode}");
                
                if (response.IsSuccessStatusCode && responseContent.Contains("Test User"))
                {
                    Console.WriteLine("? HTTP POST test PASSED");
                }
                else
                {
                    Console.WriteLine("? HTTP POST test FAILED");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? HTTP POST test FAILED: {ex.Message}");
            }
        }

        static async Task TestMultipleRequests()
        {
            Console.WriteLine("\n--- Test 4: Multiple Concurrent Requests ---");
            try
            {
                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy(ProxyAddress),
                    UseProxy = true
                };

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(15);

                var tasks = new List<Task<HttpResponseMessage>>();
                var urls = new[]
                {
                    "http://httpbin.org/delay/1",
                    "http://httpbin.org/uuid",
                    "http://httpbin.org/user-agent",
                    "http://httpbin.org/headers",
                    "http://httpbin.org/ip"
                };

                Console.WriteLine($"Sending {urls.Length} concurrent requests...");
                
                foreach (var url in urls)
                {
                    tasks.Add(client.GetAsync(url));
                }

                var responses = await Task.WhenAll(tasks);
                var successCount = responses.Count(r => r.IsSuccessStatusCode);

                Console.WriteLine($"Completed: {responses.Length}/{urls.Length}");
                Console.WriteLine($"Successful: {successCount}/{urls.Length}");
                
                if (successCount == urls.Length)
                {
                    Console.WriteLine("? Multiple concurrent requests test PASSED");
                }
                else
                {
                    Console.WriteLine($"? Multiple concurrent requests test FAILED ({successCount}/{urls.Length} succeeded)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Multiple concurrent requests test FAILED: {ex.Message}");
            }
        }

        static async Task TestErrorHandling()
        {
            Console.WriteLine("\n--- Test 5: Error Handling (Invalid Host) ---");
            try
            {
                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy(ProxyAddress),
                    UseProxy = true
                };

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(5);

                try
                {
                    var response = await client.GetAsync("http://this-domain-does-not-exist-12345.com");
                    Console.WriteLine($"Status: {response.StatusCode}");
                    
                    if (response.StatusCode == HttpStatusCode.BadGateway || !response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("? Error handling test PASSED (received error response as expected)");
                    }
                    else
                    {
                        Console.WriteLine("? Error handling test FAILED (expected error response)");
                    }
                }
                catch (HttpRequestException)
                {
                    Console.WriteLine("? Error handling test PASSED (connection failed as expected)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error handling test FAILED: {ex.Message}");
            }
        }
    }
}
