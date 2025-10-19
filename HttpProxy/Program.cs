using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HttpProxy
{
    internal class Program
    {
        private static readonly HttpClient httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None
        });

        static async Task Main(string[] args)
        {
            int port = args.Length > 0 && int.TryParse(args[0], out int p) ? p : 8888;
            
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            
            Console.WriteLine($"HTTP/HTTPS Proxy Server started on port {port}");
            Console.WriteLine("Press Ctrl+C to stop the server...");
            
            while (true)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    var buffer = new byte[8192];
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0) return;

                    var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var requestLines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    
                    if (requestLines.Length == 0) return;

                    var requestLine = requestLines[0].Split(' ');
                    if (requestLine.Length < 3) return;

                    var method = requestLine[0];
                    var url = requestLine[1];

                    Console.WriteLine($"{DateTime.Now:HH:mm:ss} - {method} {url}");

                    if (method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleHttpsConnectAsync(stream, url);
                    }
                    else
                    {
                        await HandleHttpRequestAsync(stream, method, url, request, buffer, bytesRead);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
        }

        private static async Task HandleHttpsConnectAsync(NetworkStream clientStream, string destination)
        {
            try
            {
                var parts = destination.Split(':');
                var host = parts[0];
                var port = parts.Length > 1 ? int.Parse(parts[1]) : 443;

                using var targetClient = new TcpClient();
                await targetClient.ConnectAsync(host, port);
                
                // Send 200 Connection Established response
                var response = "HTTP/1.1 200 Connection Established\r\n\r\n";
                var responseBytes = Encoding.UTF8.GetBytes(response);
                await clientStream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await clientStream.FlushAsync();

                var targetStream = targetClient.GetStream();

                // Tunnel data bidirectionally
                var clientToTarget = CopyStreamAsync(clientStream, targetStream, "Client->Target");
                var targetToClient = CopyStreamAsync(targetStream, clientStream, "Target->Client");

                await Task.WhenAny(clientToTarget, targetToClient);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HTTPS tunnel error: {ex.Message}");
                try
                {
                    var errorResponse = "HTTP/1.1 502 Bad Gateway\r\n\r\n";
                    var errorBytes = Encoding.UTF8.GetBytes(errorResponse);
                    await clientStream.WriteAsync(errorBytes, 0, errorBytes.Length);
                }
                catch { }
            }
        }

        private static async Task CopyStreamAsync(NetworkStream source, NetworkStream destination, string direction)
        {
            try
            {
                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await destination.WriteAsync(buffer, 0, bytesRead);
                    await destination.FlushAsync();
                }
            }
            catch (Exception ex)
            {
                // Connection closed or error - this is normal when either side disconnects
                Console.WriteLine($"{direction} stream ended: {ex.Message}");
            }
        }

        private static async Task HandleHttpRequestAsync(NetworkStream clientStream, string method, string url, string request, byte[] initialBuffer, int initialBytesRead)
        {
            try
            {
                // Parse the request
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    // Handle relative URLs by extracting host from headers
                    var hostHeader = ExtractHeader(request, "Host");
                    if (!string.IsNullOrEmpty(hostHeader))
                    {
                        url = $"http://{hostHeader}{url}";
                        Uri.TryCreate(url, UriKind.Absolute, out uri);
                    }
                }

                if (uri == null)
                {
                    await SendErrorResponse(clientStream, "400 Bad Request");
                    return;
                }

                var requestMessage = new HttpRequestMessage(new HttpMethod(method), uri);

                // Copy headers from original request
                var headers = ParseHeaders(request);
                foreach (var header in headers)
                {
                    if (!IsHopByHopHeader(header.Key))
                    {
                        try
                        {
                            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
                                header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                            {
                                continue; // Will be set with content
                            }
                            requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                        catch { }
                    }
                }

                // Handle request body for POST, PUT, etc.
                if (method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
                    method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                    method.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
                {
                    var contentLength = ExtractContentLength(request);
                    if (contentLength > 0)
                    {
                        var headerEndIndex = request.IndexOf("\r\n\r\n");
                        if (headerEndIndex >= 0)
                        {
                            var headerLength = Encoding.UTF8.GetBytes(request.Substring(0, headerEndIndex + 4)).Length;
                            var bodyBytesReceived = initialBytesRead - headerLength;
                            
                            var bodyBuffer = new byte[contentLength];
                            Array.Copy(initialBuffer, headerLength, bodyBuffer, 0, bodyBytesReceived);

                            // Read remaining body if needed
                            while (bodyBytesReceived < contentLength)
                            {
                                var bytesRead = await clientStream.ReadAsync(bodyBuffer, bodyBytesReceived, contentLength - bodyBytesReceived);
                                if (bytesRead == 0) break;
                                bodyBytesReceived += bytesRead;
                            }

                            requestMessage.Content = new ByteArrayContent(bodyBuffer);
                            
                            var contentType = ExtractHeader(request, "Content-Type");
                            if (!string.IsNullOrEmpty(contentType))
                            {
                                requestMessage.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
                            }
                        }
                    }
                }

                // Send request to target server
                var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

                // Send response back to client
                var responseBuilder = new StringBuilder();
                responseBuilder.Append($"HTTP/1.1 {(int)response.StatusCode} {response.ReasonPhrase}\r\n");

                foreach (var header in response.Headers)
                {
                    if (!IsHopByHopHeader(header.Key))
                    {
                        responseBuilder.Append($"{header.Key}: {string.Join(", ", header.Value)}\r\n");
                    }
                }

                if (response.Content != null)
                {
                    foreach (var header in response.Content.Headers)
                    {
                        if (!IsHopByHopHeader(header.Key))
                        {
                            responseBuilder.Append($"{header.Key}: {string.Join(", ", header.Value)}\r\n");
                        }
                    }
                }

                responseBuilder.Append("\r\n");

                var responseHeaderBytes = Encoding.UTF8.GetBytes(responseBuilder.ToString());
                await clientStream.WriteAsync(responseHeaderBytes, 0, responseHeaderBytes.Length);

                // Send response body
                if (response.Content != null)
                {
                    var responseBody = await response.Content.ReadAsByteArrayAsync();
                    await clientStream.WriteAsync(responseBody, 0, responseBody.Length);
                }

                await clientStream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HTTP request error: {ex.Message}");
                await SendErrorResponse(clientStream, "502 Bad Gateway");
            }
        }

        private static Dictionary<string, string> ParseHeaders(string request)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = request.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 1; i < lines.Length; i++)
            {
                var colonIndex = lines[i].IndexOf(':');
                if (colonIndex > 0)
                {
                    var key = lines[i].Substring(0, colonIndex).Trim();
                    var value = lines[i].Substring(colonIndex + 1).Trim();
                    headers[key] = value;
                }
            }
            
            return headers;
        }

        private static string ExtractHeader(string request, string headerName)
        {
            var headers = ParseHeaders(request);
            return headers.TryGetValue(headerName, out var value) ? value : string.Empty;
        }

        private static int ExtractContentLength(string request)
        {
            var contentLength = ExtractHeader(request, "Content-Length");
            return int.TryParse(contentLength, out int length) ? length : 0;
        }

        private static bool IsHopByHopHeader(string headerName)
        {
            var hopByHopHeaders = new[] { "Connection", "Keep-Alive", "Proxy-Authenticate", 
                                         "Proxy-Authorization", "TE", "Trailers", 
                                         "Transfer-Encoding", "Upgrade" };
            return hopByHopHeaders.Contains(headerName, StringComparer.OrdinalIgnoreCase);
        }

        private static async Task SendErrorResponse(NetworkStream stream, string status)
        {
            try
            {
                var response = $"HTTP/1.1 {status}\r\nContent-Length: 0\r\n\r\n";
                var bytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(bytes, 0, bytes.Length);
                await stream.FlushAsync();
            }
            catch { }
        }
    }
}
