using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DA_Assets.Shared.Extensions;
using UnityEngine;

#if JSONNET_PLASTIC_EXISTS
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;
#elif JSONNET_EXISTS
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#endif

namespace DA_Assets.Shared.MCP
{
    public class McpServer : IDisposable
    {
        private readonly List<IMcpTool> _tools = new();
        private HttpListener _listener;
        private CancellationTokenSource _cts;

        public bool IsRunning => _listener?.IsListening ?? false;
        public string Endpoint => $"http://{_config.Host}:{_config.Port}/";

        private McpServerConfig _config;

        public McpServer(McpServerConfig config)
        {
            _config = config;
        }

        public void RegisterTool(IMcpTool tool)
        {
            if (tool != null && !_tools.Any(t => t.Name == tool.Name))
                _tools.Add(tool);
        }

        public void Start()
        {
            if (IsRunning) return;

            _listener = new HttpListener();
            _listener.Prefixes.Add(Endpoint);
            _listener.Start();

            _cts = new CancellationTokenSource();
            _ = ListenLoopAsync(_cts.Token);

            Debug.Log(SharedLocKey.log_mcp_started.Localize("MCP", _config.ServerName, Endpoint));
        }

        public void Stop()
        {
            if (!IsRunning) return;

            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                _listener = null;
                _cts?.Dispose();
                _cts = null;
            }

            Debug.Log(SharedLocKey.log_mcp_stopped.Localize("MCP", _config.ServerName));
        }

        private async Task ListenLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener?.IsListening == true)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = HandleRequestAsync(context, token);
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken token)
        {
            string responseJson = "";

            try
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var rawJson = await reader.ReadToEndAsync();

                Debug.Log(SharedLocKey.log_mcp_request.Localize("MCP", rawJson));
                responseJson = await RouteAsync(rawJson);
                Debug.Log(SharedLocKey.log_mcp_response.Localize("MCP", responseJson));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                responseJson = BuildError(null, -32603, ex.Message);
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    await WriteResponseAsync(context, responseJson);
            }
        }

        private async Task<string> RouteAsync(string rawJson)
        {
            JsonRpcRequest request = default;

            try
            {
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
                request = JsonConvert.DeserializeObject<JsonRpcRequest>(rawJson);
#endif
            }
            catch (Exception ex)
            {
                return BuildError(null, -32700, $"Parse error: {ex.Message}");
            }

            if (string.IsNullOrEmpty(request.Method))
                return BuildError(request.Id, -32600, "Invalid request");

            return request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "notifications/initialized" => "",
                "tools/list" => HandleListTools(request),
                "tools/call" => await HandleToolCallAsync(request),
                _ => BuildError(request.Id, -32601, "Method not found")
            };
        }

        private string HandleInitialize(JsonRpcRequest request)
        {
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
            return JsonConvert.SerializeObject(new JsonRpcResponse<InitializeResult>
            {
                JsonRpc = _config.RpcVersion,
                Id = request.Id,
                Result = new InitializeResult
                {
                    ProtocolVersion = _config.ProtocolVersion,
                    Capabilities = new ServerCapabilities { Tools = new { } },
                    ServerInfo = new ServerInfo { Name = _config.ServerName, Version = _config.ServerVersion }
                }
            });
#else
            return "";
#endif
        }

        private string HandleListTools(JsonRpcRequest request)
        {
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
            return JsonConvert.SerializeObject(new JsonRpcResponse<ListToolsResult>
            {
                JsonRpc = _config.RpcVersion,
                Id = request.Id,
                Result = new ListToolsResult
                {
                    Tools = _tools.Select(t => new ToolDescription
                    {
                        Name = t.Name,
                        Description = t.Description,
                        InputSchema = t.InputSchema
                    }).ToList()
                }
            });
#else
            return "";
#endif
        }

        private async Task<string> HandleToolCallAsync(JsonRpcRequest request)
        {
            try
            {
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
                var callParams = request.Params is JObject jo
                    ? jo.ToObject<CallToolParams>()
                    : new CallToolParams();

                if (string.IsNullOrEmpty(callParams.Name))
                    return BuildError(request.Id, -32602, "Tool name required");

                var tool = _tools.FirstOrDefault(t => t.Name.Equals(callParams.Name, StringComparison.OrdinalIgnoreCase));
                if (tool == null)
                    return BuildError(request.Id, -32601, $"Tool '{callParams.Name}' not found");

                var content = await tool.ExecuteAsync(callParams.Arguments ?? new Dictionary<string, object>());

                return JsonConvert.SerializeObject(new JsonRpcResponse<CallToolResult>
                {
                    JsonRpc = _config.RpcVersion,
                    Id = request.Id,
                    Result = new CallToolResult { Content = content?.ToList() ?? new List<ContentItem>() }
                });
#else
                return "";
#endif
            }
            catch (Exception ex)
            {
                return BuildError(request.Id, -32001, ex.Message);
            }
        }

        private string BuildError(object id, int code, string message)
        {
#if JSONNET_EXISTS || JSONNET_PLASTIC_EXISTS
            return JsonConvert.SerializeObject(new JsonRpcErrorResponse
            {
                JsonRpc = _config.RpcVersion,
                Id = id,
                Error = new RpcError { Code = code, Message = message }
            });
#else
            return "";
#endif

        }

        private static async Task WriteResponseAsync(HttpListenerContext context, string json)
        {
            try
            {
                var buffer = Encoding.UTF8.GetBytes(json ?? "");
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        public void Dispose() => Stop();
    }
}