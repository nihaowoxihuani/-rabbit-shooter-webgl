using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// ============================================
// MCP Unity Bridge - Unity 客户端
// ============================================
// 功能：
// 1. 与 MCP 服务器建立 WebSocket 连接
// 2. 接收 Claude 发送的命令并执行
// 3. 向 Claude 发送 Unity 状态和事件
// 4. 支持双向通信
// ============================================

namespace MCPUnityBridge
{
    /// <summary>
    /// 消息类型定义
    /// </summary>
    public enum MessageType
    {
        Connected,
        Register,
        Request,
        Response,
        Event
    }

    /// <summary>
    /// 基础消息
    /// </summary>
    [Serializable]
    public class BaseMessage
    {
        public string id;
        public string type;
        public string requestId;
        public object data;
    }

    /// <summary>
    /// 连接消息
    /// </summary>
    [Serializable]
    public class ConnectedMessage : BaseMessage
    {
        public string clientId;
        public string serverTime;
    }

    /// <summary>
    /// 注册消息
    /// </summary>
    [Serializable]
    public class RegisterMessage : BaseMessage
    {
        public ClientMetadata metadata;
    }

    [Serializable]
    public class ClientMetadata
    {
        public string clientType = "unity";
        public string unityVersion;
        public string projectName;
        public string sceneName;
    }

    /// <summary>
    /// 请求消息
    /// </summary>
    [Serializable]
    public class RequestMessage : BaseMessage
    {
        public string requestType;
        public object requestData;
    }

    /// <summary>
    /// 响应消息
    /// </summary>
    [Serializable]
    public class ResponseMessage : BaseMessage
    {
        public string error;
    }

    /// <summary>
    /// 事件消息
    /// </summary>
    [Serializable]
    public class EventMessage : BaseMessage
    {
        public string eventName;
        public object eventData;
    }

    /// <summary>
    /// MCP Bridge 主类
    /// </summary>
    public class MCPBridge : MonoBehaviour
    {
        [Header("连接配置")]
        [SerializeField] private string serverUrl = "ws://localhost:8080/mcp";
        [SerializeField] private float reconnectInterval = 5f;
        [SerializeField] private int maxReconnectAttempts = 10;

        [Header("Unity 元数据")]
        [SerializeField] private string projectName = "MyProject";

        [Header("调试")]
        [SerializeField] private bool showDebugLogs = true;
        [SerializeField] private bool autoConnect = true;

        // 私有变量
        private ClientWebSocket webSocket;
        private CancellationTokenSource cancellationTokenSource;
        private string clientId;
        private int reconnectAttempts = 0;
        private bool isConnected = false;
        private Dictionary<string, Action<Dictionary<string, object>>> requestHandlers = new Dictionary<string, Action<Dictionary<string, object>>>();
        private Queue<string> messageQueue = new Queue<string>();
        private object queueLock = new object();

        // 日志系统
        private List<LogEntry> logEntries = new List<LogEntry>();
        private const int MAX_LOG_ENTRIES = 1000;

        [Serializable]
        public class LogEntry
        {
            public string timestamp;
            public string level;
            public string message;
            public string stackTrace;
        }

        // 事件定义
        public event Action<string, object> OnEventReceived;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnError;

        #region Unity Lifecycle

        private void Awake()
        {
            // 初始化请求处理器
            RegisterRequestHandlers();
        }

        private void Start()
        {
            // 替换日志处理器
            Application.logMessageReceived += HandleLog;

            if (autoConnect)
            {
                Connect();
            }
        }

        private void Update()
        {
            // 处理主线程消息队列
            lock (queueLock)
            {
                while (messageQueue.Count > 0)
                {
                    string message = messageQueue.Dequeue();
                    ProcessMessage(message);
                }
            }
        }

        private void OnDestroy()
        {
            Disconnect();
            Application.logMessageReceived -= HandleLog;
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        #endregion

        #region Connection Management

        public async void Connect()
        {
            if (isConnected || webSocket != null && webSocket.State == WebSocketState.Open)
            {
                LogInfo("已经连接到 MCP 服务器");
                return;
            }

            LogInfo($"正在连接到 MCP 服务器: {serverUrl}");

            try
            {
                webSocket = new ClientWebSocket();
                cancellationTokenSource = new CancellationTokenSource();

                await webSocket.ConnectAsync(new Uri(serverUrl), cancellationTokenSource.Token);

                isConnected = true;
                reconnectAttempts = 0;

                LogInfo("✓ WebSocket 连接成功");

                // 开始接收消息
                _ = ReceiveMessages();

                // 发送注册消息
                await SendRegisterMessage();

                OnConnected?.Invoke();
            }
            catch (Exception e)
            {
                LogError($"连接失败: {e.Message}");
                OnError?.Invoke(e.Message);

                // 尝试重连
                if (reconnectAttempts < maxReconnectAttempts)
                {
                    reconnectAttempts++;
                    LogInfo($"将在 {reconnectInterval} 秒后重连 ({reconnectAttempts}/{maxReconnectAttempts})...");
                    await Task.Delay((int)(reconnectInterval * 1000));
                    Connect();
                }
                else
                {
                    LogError("达到最大重连次数，停止重连");
                }
            }
        }

        public void Disconnect()
        {
            if (!isConnected) return;

            LogInfo("断开 MCP 连接...");

            isConnected = false;
            cancellationTokenSource?.Cancel();

            try
            {
                webSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                webSocket?.Dispose();
            }
            catch (Exception e)
            {
                LogError($"断开连接时出错: {e.Message}");
            }

            webSocket = null;
            cancellationTokenSource = null;

            OnDisconnected?.Invoke();
        }

        public bool IsConnected()
        {
            return isConnected && webSocket != null && webSocket.State == WebSocketState.Open;
        }

        #endregion

        #region Message Handling

        private async Task ReceiveMessages()
        {
            var buffer = new byte[4096];

            while (isConnected && webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        LogInfo("服务器关闭了连接");
                        Disconnect();
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        EnqueueMessage(message);
                    }
                }
                catch (Exception e)
                {
                    if (isConnected)
                    {
                        LogError($"接收消息时出错: {e.Message}");
                    }
                    break;
                }
            }
        }

        private void EnqueueMessage(string message)
        {
            lock (queueLock)
            {
                messageQueue.Enqueue(message);
            }
        }

        private void ProcessMessage(string messageJson)
        {
            try
            {
                var message = JsonConvert.DeserializeObject<BaseMessage>(messageJson);

                switch (message.type)
                {
                    case "connected":
                        HandleConnectedMessage(JsonConvert.DeserializeObject<ConnectedMessage>(messageJson));
                        break;

                    case "ready":
                        clientId = message.id ?? clientId;
                        LogInfo($"✓ MCP 服务器已就绪，客户端 ID: {clientId}");
                        break;

                    case "execute_command":
                    case "query_scene":
                    case "send_event":
                    case "get_logs":
                    case "execute_code":
                        var reqMsg = JsonConvert.DeserializeObject<RequestMessage>(messageJson);
                        var dataDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(reqMsg.requestData?.ToString() ?? "{}")
                                       ?? new Dictionary<string, object>();
                        dataDict["requestId"] = reqMsg.requestId;
                        HandleRequestMessage(reqMsg.type, dataDict);
                        break;

                    case "ping":
                        HandlePingMessage();
                        break;

                    default:
                        LogWarning($"未知消息类型: {message.type}");
                        break;
                }
            }
            catch (Exception e)
            {
                LogError($"处理消息时出错: {e.Message}\n{e.StackTrace}");
            }
        }

        private void HandleConnectedMessage(ConnectedMessage message)
        {
            clientId = message.clientId;
            LogInfo($"✓ 已注册到 MCP 服务器，客户端 ID: {clientId}");
        }

        private void HandleRequestMessage(string type, Dictionary<string, object> data)
        {
            data.TryGetValue("requestId", out var requestIdObj);
            var requestId = requestIdObj?.ToString();

            if (requestHandlers.TryGetValue(type, out var handler))
            {
                try
                {
                    handler(data);
                }
                catch (Exception e)
                {
                    _ = SendErrorResponse(requestId, e.Message);
                }
            }
            else
            {
                _ = SendErrorResponse(requestId, $"未处理的请求类型: {type}");
            }
        }

        private void HandlePingMessage()
        {
            _ = SendPongMessage();
        }

        private async Task SendPongMessage()
        {
            var pong = new BaseMessage
            {
                type = "pong",
                requestId = null,
                data = null
            };
            await SendMessage(pong);
        }

        private async Task SendMessage(BaseMessage message)
        {
            if (!IsConnected())
            {
                LogWarning("未连接到服务器，无法发送消息");
                return;
            }

            try
            {
                var json = JsonConvert.SerializeObject(message);
                var buffer = Encoding.UTF8.GetBytes(json);
                await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                LogError($"发送消息失败: {e.Message}");
            }
        }

        private async Task SendResponse(string requestId, object data)
        {
            var response = new ResponseMessage
            {
                id = requestId,
                type = "response",
                requestId = requestId,
                data = data,
                error = null
            };

            await SendMessage(response);
        }

        private async Task SendErrorResponse(string requestId, string error)
        {
            var response = new ResponseMessage
            {
                type = "response",
                requestId = requestId,
                data = null,
                error = error
            };

            await SendMessage(response);
        }

        private async Task SendRegisterMessage()
        {
            var registerMsg = new RegisterMessage
            {
                type = "register",
                metadata = new ClientMetadata
                {
                    clientType = "unity",
                    unityVersion = Application.unityVersion,
                    projectName = projectName,
                    sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                }
            };

            await SendMessage(registerMsg);
        }

        #endregion

        #region Request Handlers

        private void RegisterRequestHandlers()
        {
            // 执行命令
            requestHandlers["execute_command"] = (data) =>
            {
                var request = JsonConvert.DeserializeObject<ExecuteCommandRequest>(JsonConvert.SerializeObject(data));
                HandleExecuteCommand(request);
            };

            // 查询场景
            requestHandlers["query_scene"] = (data) =>
            {
                var request = JsonConvert.DeserializeObject<QuerySceneRequest>(JsonConvert.SerializeObject(data));
                HandleQueryScene(request);
            };

            // 发送事件
            requestHandlers["send_event"] = (data) =>
            {
                var request = JsonConvert.DeserializeObject<SendEventRequest>(JsonConvert.SerializeObject(data));
                HandleSendEvent(request);
            };

            // 获取日志
            requestHandlers["get_logs"] = (data) =>
            {
                var request = JsonConvert.DeserializeObject<GetLogsRequest>(JsonConvert.SerializeObject(data));
                HandleGetLogs(request);
            };

            // 执行代码
            requestHandlers["execute_code"] = (data) =>
            {
                var request = JsonConvert.DeserializeObject<ExecuteCodeRequest>(JsonConvert.SerializeObject(data));
                HandleExecuteCode(request);
            };
        }

        #endregion

        #region Command Implementations

        private void HandleExecuteCommand(ExecuteCommandRequest request)
        {
            try
            {
                object result = null;

                switch (request.command)
                {
                    case "create_object":
                        result = ExecuteCreateObject(request.params_obj);
                        break;

                    case "destroy_object":
                        result = ExecuteDestroyObject(request.params_obj);
                        break;

                    case "set_position":
                        result = ExecuteSetPosition(request.params_obj);
                        break;

                    case "set_rotation":
                        result = ExecuteSetRotation(request.params_obj);
                        break;

                    case "set_scale":
                        result = ExecuteSetScale(request.params_obj);
                        break;

                    case "get_transform":
                        result = ExecuteGetTransform(request.params_obj);
                        break;

                    case "set_property":
                        result = ExecuteSetProperty(request.params_obj);
                        break;

                    case "get_property":
                        result = ExecuteGetProperty(request.params_obj);
                        break;

                    case "play_animation":
                        result = ExecutePlayAnimation(request.params_obj);
                        break;

                    case "stop_animation":
                        result = ExecuteStopAnimation(request.params_obj);
                        break;

                    case "load_scene":
                        result = ExecuteLoadScene(request.params_obj, request.requestId);
                        break;

                    case "screenshot":
                        result = ExecuteScreenshot(request.params_obj, request.requestId);
                        break;

                    default:
                        throw new Exception($"未知命令: {request.command}");
                }

                _ = SendResponse(request.requestId, new CommandResult
                {
                    success = true,
                    command = request.command,
                    result = result
                });
            }
            catch (Exception e)
            {
                _ = SendErrorResponse(request.requestId, e.Message);
            }
        }

        private object ExecuteCreateObject(Dictionary<string, object> parameters)
        {
            string type = parameters.GetValueOrDefault("type", "Cube").ToString();
            string name = parameters.GetValueOrDefault("name", $"Generated_{type}").ToString();

            GameObject prefab = null;
            switch (type.ToLower())
            {
                case "cube":
                    prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    break;
                case "sphere":
                    prefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    break;
                case "cylinder":
                    prefab = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    break;
                case "plane":
                    prefab = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    break;
                case "capsule":
                    prefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    break;
                default:
                    prefab = new GameObject(type);
                    break;
            }

            prefab.name = name;

            if (parameters.TryGetValue("position", out var pos) && pos is JArray jPos)
            {
                prefab.transform.position = new Vector3(jPos[0].ToObject<float>(), jPos[1].ToObject<float>(), jPos[2].ToObject<float>());
            }

            if (parameters.TryGetValue("rotation", out var rot) && rot is JArray jRot)
            {
                prefab.transform.rotation = Quaternion.Euler(jRot[0].ToObject<float>(), jRot[1].ToObject<float>(), jRot[2].ToObject<float>());
            }

            if (parameters.TryGetValue("scale", out var scale) && scale is JArray jScale)
            {
                prefab.transform.localScale = new Vector3(jScale[0].ToObject<float>(), jScale[1].ToObject<float>(), jScale[2].ToObject<float>());
            }

            LogInfo($"创建物体: {name} (类型: {type})");

            return new
            {
                name = prefab.name,
                instanceId = prefab.GetInstanceID(),
                transform = new
                {
                    position = new[] { prefab.transform.position.x, prefab.transform.position.y, prefab.transform.position.z },
                    rotation = new[] { prefab.transform.rotation.eulerAngles.x, prefab.transform.rotation.eulerAngles.y, prefab.transform.rotation.eulerAngles.z },
                    scale = new[] { prefab.transform.localScale.x, prefab.transform.localScale.y, prefab.transform.localScale.z }
                }
            };
        }

        private object ExecuteDestroyObject(Dictionary<string, object> parameters)
        {
            string objectName = parameters.GetValueOrDefault("objectName").ToString();
            var obj = GameObject.Find(objectName);

            if (obj == null)
            {
                throw new Exception($"找不到物体: {objectName}");
            }

            Destroy(obj);
            LogInfo($"销毁物体: {objectName}");

            return new { success = true, message = $"已销毁 {objectName}" };
        }

        private object ExecuteSetPosition(Dictionary<string, object> parameters)
        {
            string objectName = parameters.GetValueOrDefault("objectName").ToString();
            var obj = GameObject.Find(objectName);

            if (obj == null)
            {
                throw new Exception($"找不到物体: {objectName}");
            }

            if (parameters.TryGetValue("position", out var pos) && pos is JArray jPos)
            {
                obj.transform.position = new Vector3(jPos[0].ToObject<float>(), jPos[1].ToObject<float>(), jPos[2].ToObject<float>());
            }

            return new { name = objectName, position = new[] { obj.transform.position.x, obj.transform.position.y, obj.transform.position.z } };
        }

        private object ExecuteSetRotation(Dictionary<string, object> parameters)
        {
            string objectName = parameters.GetValueOrDefault("objectName").ToString();
            var obj = GameObject.Find(objectName);

            if (obj == null)
            {
                throw new Exception($"找不到物体: {objectName}");
            }

            if (parameters.TryGetValue("rotation", out var rot) && rot is JArray jRot)
            {
                obj.transform.rotation = Quaternion.Euler(jRot[0].ToObject<float>(), jRot[1].ToObject<float>(), jRot[2].ToObject<float>());
            }

            return new { name = objectName, rotation = new[] { obj.transform.rotation.eulerAngles.x, obj.transform.rotation.eulerAngles.y, obj.transform.rotation.eulerAngles.z } };
        }

        private object ExecuteSetScale(Dictionary<string, object> parameters)
        {
            string objectName = parameters.GetValueOrDefault("objectName").ToString();
            var obj = GameObject.Find(objectName);

            if (obj == null)
            {
                throw new Exception($"找不到物体: {objectName}");
            }

            if (parameters.TryGetValue("scale", out var scale) && scale is JArray jScale)
            {
                obj.transform.localScale = new Vector3(jScale[0].ToObject<float>(), jScale[1].ToObject<float>(), jScale[2].ToObject<float>());
            }

            return new { name = objectName, scale = new[] { obj.transform.localScale.x, obj.transform.localScale.y, obj.transform.localScale.z } };
        }

        private object ExecuteGetTransform(Dictionary<string, object> parameters)
        {
            string objectName = parameters.GetValueOrDefault("objectName").ToString();
            var obj = GameObject.Find(objectName);

            if (obj == null)
            {
                throw new Exception($"找不到物体: {objectName}");
            }

            return new
            {
                name = objectName,
                position = new[] { obj.transform.position.x, obj.transform.position.y, obj.transform.position.z },
                rotation = new[] { obj.transform.rotation.eulerAngles.x, obj.transform.rotation.eulerAngles.y, obj.transform.rotation.eulerAngles.z },
                scale = new[] { obj.transform.localScale.x, obj.transform.localScale.y, obj.transform.localScale.z }
            };
        }

        private object ExecuteSetProperty(Dictionary<string, object> parameters)
        {
            string objectName = parameters.GetValueOrDefault("objectName").ToString();
            string property = parameters.GetValueOrDefault("property").ToString();
            var value = parameters.GetValueOrDefault("value");

            var obj = GameObject.Find(objectName);
            if (obj == null)
            {
                throw new Exception($"找不到物体: {objectName}");
            }

            // 简单实现：只支持一些基本属性
            switch (property)
            {
                case "isActive":
                    obj.SetActive(Convert.ToBoolean(value));
                    break;
                case "tag":
                    obj.tag = value.ToString();
                    break;
                case "layer":
                    obj.layer = Convert.ToInt32(value);
                    break;
                default:
                    throw new Exception($"不支持的属性: {property}");
            }

            return new { name = objectName, property, value };
        }

        private object ExecuteGetProperty(Dictionary<string, object> parameters)
        {
            string objectName = parameters.GetValueOrDefault("objectName").ToString();
            string property = parameters.GetValueOrDefault("property").ToString();

            var obj = GameObject.Find(objectName);
            if (obj == null)
            {
                throw new Exception($"找不到物体: {objectName}");
            }

            object value = null;
            switch (property)
            {
                case "isActive":
                    value = obj.activeSelf;
                    break;
                case "tag":
                    value = obj.tag;
                    break;
                case "layer":
                    value = obj.layer;
                    break;
                case "name":
                    value = obj.name;
                    break;
                default:
                    throw new Exception($"不支持的属性: {property}");
            }

            return new { name = objectName, property, value };
        }

        private object ExecutePlayAnimation(Dictionary<string, object> parameters)
        {
            string objectName = parameters.GetValueOrDefault("objectName").ToString();
            string animationName = parameters.GetValueOrDefault("animationName").ToString();

            var obj = GameObject.Find(objectName);
            if (obj == null)
            {
                throw new Exception($"找不到物体: {objectName}");
            }

            var animator = obj.GetComponent<Animator>();
            if (animator == null)
            {
                throw new Exception($"物体 {objectName} 没有 Animator 组件");
            }

            animator.Play(animationName);
            LogInfo($"播放动画: {animationName} on {objectName}");

            return new { name = objectName, animation = animationName };
        }

        private object ExecuteStopAnimation(Dictionary<string, object> parameters)
        {
            string objectName = parameters.GetValueOrDefault("objectName").ToString();

            var obj = GameObject.Find(objectName);
            if (obj == null)
            {
                throw new Exception($"找不到物体: {objectName}");
            }

            var animator = obj.GetComponent<Animator>();
            if (animator == null)
            {
                throw new Exception($"物体 {objectName} 没有 Animator 组件");
            }

            // 获取当前状态名用于日志
            var currentState = animator.GetCurrentAnimatorStateInfo(0);
            var stateNameHash = currentState.fullPathHash;

            // 重置 Animator 到默认状态（彻底停止并清除参数）
            animator.Rebind();

            LogInfo($"停止动画: {objectName} (状态 hash: {stateNameHash})");

            return new { name = objectName, message = "动画已停止并重置" };
        }

        private object ExecuteLoadScene(Dictionary<string, object> parameters, string requestId)
        {
            string sceneName = parameters.GetValueOrDefault("sceneName").ToString();
            bool additive = Convert.ToBoolean(parameters.GetValueOrDefault("additive", false));

            var mode = additive ? UnityEngine.SceneManagement.LoadSceneMode.Additive : UnityEngine.SceneManagement.LoadSceneMode.Single;

            StartCoroutine(LoadSceneAsync(sceneName, mode, requestId));

            return new { sceneName, mode, message = "场景加载中..." };
        }

        private IEnumerator LoadSceneAsync(string sceneName, UnityEngine.SceneManagement.LoadSceneMode mode, string requestId)
        {
            var asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, mode);

            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            _ = SendResponse(requestId, new { sceneName, loaded = true });
        }

        private object ExecuteScreenshot(Dictionary<string, object> parameters, string requestId)
        {
            int width = Convert.ToInt32(parameters.GetValueOrDefault("width", 1920));
            int height = Convert.ToInt32(parameters.GetValueOrDefault("height", 1080));
            string path = parameters.GetValueOrDefault("path", "").ToString();

            if (string.IsNullOrEmpty(path))
            {
                path = System.IO.Path.Combine(Application.persistentDataPath, $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            }

            StartCoroutine(CaptureScreenshot(width, height, path, requestId));

            return new { path, width, height, message = "截图处理中..." };
        }

        private IEnumerator CaptureScreenshot(int width, int height, string path, string requestId)
        {
            yield return new WaitForEndOfFrame();

            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();

            var bytes = texture.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);

            Destroy(texture);

            _ = SendResponse(requestId, new { path, saved = true });
        }

        #endregion

        #region Query Implementations

        private void HandleQueryScene(QuerySceneRequest request)
        {
            try
            {
                object result = null;

                switch (request.query)
                {
                    case "hierarchy":
                        result = QueryHierarchy(request.filter);
                        break;

                    case "objects":
                        result = QueryObjects(request.filter);
                        break;

                    case "components":
                        result = QueryComponents(request.filter);
                        break;

                    case "camera_info":
                        result = QueryCameraInfo(request.filter);
                        break;

                    case "lighting_info":
                        result = QueryLightingInfo(request.filter);
                        break;

                    default:
                        throw new Exception($"未知查询类型: {request.query}");
                }

                _ = SendResponse(request.requestId, new QueryResult
                {
                    success = true,
                    query = request.query,
                    result = result
                });
            }
            catch (Exception e)
            {
                _ = SendErrorResponse(request.requestId, e.Message);
            }
        }

        private object QueryHierarchy(Dictionary<string, object> filter)
        {
            var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var hierarchy = new List<object>();

            foreach (var root in rootObjects)
            {
                hierarchy.Add(BuildHierarchyNode(root));
            }

            return hierarchy;
        }

        private object BuildHierarchyNode(GameObject obj)
        {
            var children = new List<object>();
            foreach (Transform child in obj.transform)
            {
                children.Add(BuildHierarchyNode(child.gameObject));
            }

            return new
            {
                name = obj.name,
                instanceId = obj.GetInstanceID(),
                active = obj.activeSelf,
                tag = obj.tag,
                layer = obj.layer,
                transform = new
                {
                    position = new[] { obj.transform.position.x, obj.transform.position.y, obj.transform.position.z },
                    rotation = new[] { obj.transform.rotation.eulerAngles.x, obj.transform.rotation.eulerAngles.y, obj.transform.rotation.eulerAngles.z },
                    scale = new[] { obj.transform.localScale.x, obj.transform.localScale.y, obj.transform.localScale.z }
                },
                children = children
            };
        }

        private object QueryObjects(Dictionary<string, object> filter)
        {
            var objects = GameObject.FindObjectsOfType<GameObject>(true);
            var result = new List<object>();

            foreach (var obj in objects)
            {
                if (MatchesFilter(obj, filter))
                {
                    result.Add(new
                    {
                        name = obj.name,
                        instanceId = obj.GetInstanceID(),
                        active = obj.activeSelf,
                        tag = obj.tag,
                        layer = obj.layer,
                        scene = obj.scene.name
                    });
                }
            }

            return result;
        }

        private object QueryComponents(Dictionary<string, object> filter)
        {
            string objectName = filter?.GetValueOrDefault("objectName")?.ToString();

            if (string.IsNullOrEmpty(objectName))
            {
                throw new Exception("需要指定 objectName");
            }

            var obj = GameObject.Find(objectName);
            if (obj == null)
            {
                throw new Exception($"找不到物体: {objectName}");
            }

            var components = obj.GetComponents<Component>();
            var result = new List<object>();

            foreach (var component in components)
            {
                if (component == null) continue;
                result.Add(new
                {
                    type = component.GetType().Name,
                    enabled = component is Behaviour b ? b.enabled : true
                });
            }

            return new
            {
                objectName,
                components = result
            };
        }

        private object QueryCameraInfo(Dictionary<string, object> filter)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                throw new Exception("找不到主摄像机");
            }

            return new
            {
                name = camera.name,
                enabled = camera.enabled,
                fieldOfView = camera.fieldOfView,
                nearClipPlane = camera.nearClipPlane,
                farClipPlane = camera.farClipPlane,
                position = new[] { camera.transform.position.x, camera.transform.position.y, camera.transform.position.z },
                rotation = new[] { camera.transform.rotation.eulerAngles.x, camera.transform.rotation.eulerAngles.y, camera.transform.rotation.eulerAngles.z }
            };
        }

        private object QueryLightingInfo(Dictionary<string, object> filter)
        {
            var lights = GameObject.FindObjectsOfType<Light>(true);
            var result = new List<object>();

            foreach (var light in lights)
            {
                result.Add(new
                {
                    name = light.name,
                    type = light.type.ToString(),
                    intensity = light.intensity,
                    color = new[] { light.color.r, light.color.g, light.color.b, light.color.a },
                    range = light.range,
                    spotAngle = light.spotAngle,
                    shadows = light.shadows.ToString()
                });
            }

            return new
            {
                lightCount = lights.Length,
                lights = result
            };
        }

        private bool MatchesFilter(GameObject obj, Dictionary<string, object> filter)
        {
            if (filter == null || filter.Count == 0) return true;

            foreach (var kvp in filter)
            {
                switch (kvp.Key)
                {
                    case "name":
                        if (!obj.name.Contains(kvp.Value.ToString())) return false;
                        break;
                    case "tag":
                        if (obj.tag != kvp.Value.ToString()) return false;
                        break;
                    case "layer":
                        if (obj.layer != Convert.ToInt32(kvp.Value)) return false;
                        break;
                    case "active":
                        if (obj.activeSelf != Convert.ToBoolean(kvp.Value)) return false;
                        break;
                }
            }

            return true;
        }

        #endregion

        #region Event Implementations

        private void HandleSendEvent(SendEventRequest request)
        {
            try
            {
                OnEventReceived?.Invoke(request.eventName, request.eventData);

                LogInfo($"收到事件: {request.eventName}");

                _ = SendResponse(request.requestId, new
                {
                    success = true,
                    eventName = request.eventName,
                    processed = true
                });
            }
            catch (Exception e)
            {
                _ = SendErrorResponse(request.requestId, e.Message);
            }
        }

        public async Task SendEvent(string eventName, object eventData)
        {
            var eventMsg = new EventMessage
            {
                type = "event",
                eventName = eventName,
                eventData = eventData
            };

            await SendMessage(eventMsg);
        }

        #endregion

        #region Log Implementations

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            var level = type.ToString().ToLower();
            if (level == "log") level = "info";

            var entry = new LogEntry
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                level = level,
                message = logString,
                stackTrace = stackTrace
            };

            logEntries.Add(entry);

            // 限制日志数量
            while (logEntries.Count > MAX_LOG_ENTRIES)
            {
                logEntries.RemoveAt(0);
            }

            // 发送日志事件到服务器
            if (isConnected)
            {
                _ = SendEvent("log", entry);
            }
        }

        private void HandleGetLogs(GetLogsRequest request)
        {
            try
            {
                string level = request.level ?? "all";
                int limit = request.limit > 0 ? request.limit : 100;

                var filteredLogs = logEntries;

                if (level != "all")
                {
                    filteredLogs = logEntries.FindAll(l => l.level == level);
                }

                var result = filteredLogs.Take(limit).ToList();

                _ = SendResponse(request.requestId, new
                {
                    count = result.Count,
                    logs = result
                });
            }
            catch (Exception e)
            {
                _ = SendErrorResponse(request.requestId, e.Message);
            }
        }

        #endregion

        #region Code Execution

        private void HandleExecuteCode(ExecuteCodeRequest request)
        {
            try
            {
                var result = SafeEvaluateCode(request.code);
                _ = SendResponse(request.requestId, new { success = true, result });
            }
            catch (Exception e)
            {
                _ = SendErrorResponse(request.requestId, e.Message);
            }
        }

        private object SafeEvaluateCode(string code)
        {
            code = code.Trim();
            if (string.IsNullOrEmpty(code))
                throw new Exception("代码为空");

            // 1. Debug.Log("...")
            var logMatch = System.Text.RegularExpressions.Regex.Match(code, @"Debug\.Log\(""([^""]*)""\)");
            if (logMatch.Success)
            {
                string msg = logMatch.Groups[1].Value;
                Debug.Log($"[ExecuteCode] {msg}");
                return new { message = msg };
            }

            // 2. Destroy(GameObject.Find("..."))
            var destroyMatch = System.Text.RegularExpressions.Regex.Match(code, @"Destroy\s*\(\s*GameObject\.Find\s*\(\s*""([^""]+)""\s*\)\s*\)");
            if (destroyMatch.Success)
            {
                string name = destroyMatch.Groups[1].Value;
                var obj = GameObject.Find(name);
                if (obj == null) throw new Exception($"找不到物体: {name}");
                Destroy(obj);
                return new { destroyed = name };
            }

            // 3. new GameObject("...")
            var newGoMatch = System.Text.RegularExpressions.Regex.Match(code, @"new\s+GameObject\s*\(\s*""([^""]*)""\s*\)");
            if (newGoMatch.Success)
            {
                string name = newGoMatch.Groups[1].Value;
                var obj = new GameObject(name);
                return new { created = name, instanceId = obj.GetInstanceID() };
            }

            // 4. GameObject.CreatePrimitive(PrimitiveType.XXX)
            var primitiveMatch = System.Text.RegularExpressions.Regex.Match(code,
                @"GameObject\.CreatePrimitive\s*\(\s*PrimitiveType\.(\w+)\s*\)");
            if (primitiveMatch.Success)
            {
                string typeName = primitiveMatch.Groups[1].Value;
                if (System.Enum.TryParse<PrimitiveType>(typeName, out var pType))
                {
                    var obj = GameObject.CreatePrimitive(pType);
                    return new { primitive = typeName, name = obj.name, instanceId = obj.GetInstanceID() };
                }
                throw new Exception($"未知的基本体类型: {typeName}");
            }

            // 5. GameObject.Find("...").transform.position = new Vector3(x,y,z)
            var setTransformMatch = System.Text.RegularExpressions.Regex.Match(code,
                @"GameObject\.Find\s*\(\s*""([^""]+)""\s*\)(?:\.transform)?\.(position|localPosition|rotation|localRotation|eulerAngles|localEulerAngles|scale|localScale)\s*=\s*new\s+Vector3\s*\(\s*([^,]+)\s*,\s*([^,]+)\s*,\s*([^)]+)\s*\)");
            if (setTransformMatch.Success)
            {
                string objName = setTransformMatch.Groups[1].Value;
                string prop = setTransformMatch.Groups[2].Value;
                float x = ParseFloat(setTransformMatch.Groups[3].Value);
                float y = ParseFloat(setTransformMatch.Groups[4].Value);
                float z = ParseFloat(setTransformMatch.Groups[5].Value);
                Vector3 v = new Vector3(x, y, z);

                var obj = GameObject.Find(objName);
                if (obj == null) throw new Exception($"找不到物体: {objName}");

                switch (prop)
                {
                    case "position": obj.transform.position = v; break;
                    case "localPosition": obj.transform.localPosition = v; break;
                    case "rotation": obj.transform.rotation = Quaternion.Euler(v); break;
                    case "localRotation": obj.transform.localRotation = Quaternion.Euler(v); break;
                    case "eulerAngles": obj.transform.eulerAngles = v; break;
                    case "localEulerAngles": obj.transform.localEulerAngles = v; break;
                    case "scale":
                    case "localScale": obj.transform.localScale = v; break;
                }
                return new { objectName = objName, property = prop, value = new[] { x, y, z } };
            }

            // 6. GameObject.Find("...").SetActive(true/false)
            var setActiveMatch = System.Text.RegularExpressions.Regex.Match(code,
                @"GameObject\.Find\s*\(\s*""([^""]+)""\s*\)\.SetActive\s*\(\s*(true|false)\s*\)");
            if (setActiveMatch.Success)
            {
                string objName = setActiveMatch.Groups[1].Value;
                bool active = bool.Parse(setActiveMatch.Groups[2].Value);
                var obj = GameObject.Find(objName);
                if (obj == null) throw new Exception($"找不到物体: {objName}");
                obj.SetActive(active);
                return new { objectName = objName, active };
            }

            // 7. GameObject.Find("...").transform.Translate(x,y,z)
            var translateMatch = System.Text.RegularExpressions.Regex.Match(code,
                @"GameObject\.Find\s*\(\s*""([^""]+)""\s*\)\.transform\.Translate\s*\(\s*([^,]+)\s*,\s*([^,]+)\s*,\s*([^)]+)\s*\)");
            if (translateMatch.Success)
            {
                string objName = translateMatch.Groups[1].Value;
                float x = ParseFloat(translateMatch.Groups[2].Value);
                float y = ParseFloat(translateMatch.Groups[3].Value);
                float z = ParseFloat(translateMatch.Groups[4].Value);
                var obj = GameObject.Find(objName);
                if (obj == null) throw new Exception($"找不到物体: {objName}");
                obj.transform.Translate(x, y, z);
                return new { objectName = objName, action = "Translate", value = new[] { x, y, z }, newPosition = new[] { obj.transform.position.x, obj.transform.position.y, obj.transform.position.z } };
            }

            // 8. GameObject.Find("...").transform.Rotate(x,y,z)
            var rotateMatch = System.Text.RegularExpressions.Regex.Match(code,
                @"GameObject\.Find\s*\(\s*""([^""]+)""\s*\)\.transform\.Rotate\s*\(\s*([^,]+)\s*,\s*([^,]+)\s*,\s*([^)]+)\s*\)");
            if (rotateMatch.Success)
            {
                string objName = rotateMatch.Groups[1].Value;
                float x = ParseFloat(rotateMatch.Groups[2].Value);
                float y = ParseFloat(rotateMatch.Groups[3].Value);
                float z = ParseFloat(rotateMatch.Groups[4].Value);
                var obj = GameObject.Find(objName);
                if (obj == null) throw new Exception($"找不到物体: {objName}");
                obj.transform.Rotate(x, y, z);
                return new { objectName = objName, action = "Rotate", value = new[] { x, y, z }, newRotation = new[] { obj.transform.eulerAngles.x, obj.transform.eulerAngles.y, obj.transform.eulerAngles.z } };
            }

            // 9. GameObject.Find("...").GetComponent("Type").property = value
            var setCompMatch = System.Text.RegularExpressions.Regex.Match(code,
                @"GameObject\.Find\s*\(\s*""([^""]+)""\s*\)\.GetComponent\s*\(\s*""([^""]+)""\s*\)\.(\w+)\s*=\s*(.+)$");
            if (setCompMatch.Success)
            {
                string objName = setCompMatch.Groups[1].Value;
                string compType = setCompMatch.Groups[2].Value;
                string propName = setCompMatch.Groups[3].Value;
                string valStr = setCompMatch.Groups[4].Value.Trim();

                var obj = GameObject.Find(objName);
                if (obj == null) throw new Exception($"找不到物体: {objName}");

                var type = FindTypeInUnity(compType);
                var comp = obj.GetComponent(type);
                if (comp == null) throw new Exception($"物体 {objName} 没有组件 {compType}");

                SetMemberValue(comp, propName, valStr);
                return new { objectName = objName, component = compType, property = propName, value = valStr };
            }

            // 10. GameObject.Find("...").SendMessage("MethodName")
            var sendMsgMatch = System.Text.RegularExpressions.Regex.Match(code,
                @"GameObject\.Find\s*\(\s*""([^""]+)""\s*\)\.SendMessage\s*\(\s*""([^""]+)""\s*\)");
            if (sendMsgMatch.Success)
            {
                string objName = sendMsgMatch.Groups[1].Value;
                string method = sendMsgMatch.Groups[2].Value;
                var obj = GameObject.Find(objName);
                if (obj == null) throw new Exception($"找不到物体: {objName}");
                obj.SendMessage(method, SendMessageOptions.DontRequireReceiver);
                return new { objectName = objName, method };
            }

            // 11. GameObject.Find("...")  (仅查询)
            var findMatch = System.Text.RegularExpressions.Regex.Match(code,
                @"GameObject\.Find\s*\(\s*""([^""]+)""\s*\)$");
            if (findMatch.Success)
            {
                string objName = findMatch.Groups[1].Value;
                var obj = GameObject.Find(objName);
                if (obj == null) throw new Exception($"找不到物体: {objName}");
                return new
                {
                    name = obj.name,
                    active = obj.activeSelf,
                    position = new[] { obj.transform.position.x, obj.transform.position.y, obj.transform.position.z },
                    rotation = new[] { obj.transform.eulerAngles.x, obj.transform.eulerAngles.y, obj.transform.eulerAngles.z },
                    scale = new[] { obj.transform.localScale.x, obj.transform.localScale.y, obj.transform.localScale.z }
                };
            }

            // 12. GameObject.Find("...").transform.position (读取属性)
            var getTransformMatch = System.Text.RegularExpressions.Regex.Match(code,
                @"GameObject\.Find\s*\(\s*""([^""]+)""\s*\)(?:\.transform)?\.(position|localPosition|eulerAngles|localEulerAngles|scale|localScale)$");
            if (getTransformMatch.Success)
            {
                string objName = getTransformMatch.Groups[1].Value;
                string prop = getTransformMatch.Groups[2].Value;
                var obj = GameObject.Find(objName);
                if (obj == null) throw new Exception($"找不到物体: {objName}");
                Vector3 v = Vector3.zero;
                switch (prop)
                {
                    case "position": v = obj.transform.position; break;
                    case "localPosition": v = obj.transform.localPosition; break;
                    case "eulerAngles": v = obj.transform.eulerAngles; break;
                    case "localEulerAngles": v = obj.transform.localEulerAngles; break;
                    case "scale":
                    case "localScale": v = obj.transform.localScale; break;
                }
                return new { objectName = objName, property = prop, value = new[] { v.x, v.y, v.z } };
            }

            throw new Exception($"不支持的代码格式: {code}\n支持的模式: Debug.Log, Destroy, new GameObject, CreatePrimitive, transform.position=..., SetActive, Translate, Rotate, GetComponent...=..., SendMessage, GameObject.Find");
        }

        private float ParseFloat(string s)
        {
            s = s.Trim().ToLower();
            if (s == "mathf.pi" || s == "pi") return Mathf.PI;
            if (s == "mathf.deg2rad" || s == "deg2rad") return Mathf.Deg2Rad;
            if (s == "mathf.rad2deg" || s == "rad2deg") return Mathf.Rad2Deg;
            if (s.EndsWith("f")) s = s.TrimEnd('f');
            return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        }

        private System.Type FindTypeInUnity(string typeName)
        {
            // 常见类型快捷映射
            var wellKnown = new System.Collections.Generic.Dictionary<string, System.Type>(StringComparer.OrdinalIgnoreCase)
            {
                ["Rigidbody"] = typeof(Rigidbody),
                ["Rigidbody2D"] = typeof(Rigidbody2D),
                ["Collider"] = typeof(Collider),
                ["Collider2D"] = typeof(Collider2D),
                ["BoxCollider"] = typeof(BoxCollider),
                ["BoxCollider2D"] = typeof(BoxCollider2D),
                ["SphereCollider"] = typeof(SphereCollider),
                ["CapsuleCollider"] = typeof(CapsuleCollider),
                ["MeshRenderer"] = typeof(MeshRenderer),
                ["SkinnedMeshRenderer"] = typeof(SkinnedMeshRenderer),
                ["SpriteRenderer"] = typeof(SpriteRenderer),
                ["Camera"] = typeof(Camera),
                ["Light"] = typeof(Light),
                ["AudioSource"] = typeof(AudioSource),
                ["Animator"] = typeof(Animator),
                ["Animation"] = typeof(Animation),
                ["Transform"] = typeof(Transform),
                ["ParticleSystem"] = typeof(ParticleSystem),
                ["Canvas"] = typeof(Canvas),
                ["Image"] = typeof(UnityEngine.UI.Image),
                ["Text"] = typeof(UnityEngine.UI.Text),
                ["Button"] = typeof(UnityEngine.UI.Button),
            };

            if (wellKnown.TryGetValue(typeName, out var t)) return t;

            // 全局搜索类型
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = asm.GetType(typeName);
                if (type != null) return type;
                // 尝试加上 UnityEngine 命名空间
                type = asm.GetType($"UnityEngine.{typeName}");
                if (type != null) return type;
            }

            throw new Exception($"找不到类型: {typeName}");
        }

        private void SetMemberValue(object target, string memberName, string valueStr)
        {
            var type = target.GetType();
            var prop = type.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                object value = ConvertStringToType(valueStr, prop.PropertyType);
                prop.SetValue(target, value);
                return;
            }

            var field = type.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                object value = ConvertStringToType(valueStr, field.FieldType);
                field.SetValue(target, value);
                return;
            }

            throw new Exception($"类型 {type.Name} 没有可写的属性或字段: {memberName}");
        }

        private object ConvertStringToType(string valueStr, System.Type targetType)
        {
            valueStr = valueStr.Trim();

            if (targetType == typeof(string))
            {
                if (valueStr.StartsWith("\"") && valueStr.EndsWith("\""))
                    return valueStr.Substring(1, valueStr.Length - 2);
                return valueStr;
            }
            if (targetType == typeof(int) || targetType == typeof(long) || targetType == typeof(short))
                return System.Convert.ChangeType(int.Parse(valueStr), targetType);
            if (targetType == typeof(float) || targetType == typeof(double))
                return System.Convert.ChangeType(ParseFloat(valueStr), targetType);
            if (targetType == typeof(bool))
                return bool.Parse(valueStr);
            if (targetType == typeof(Vector3))
            {
                var m = System.Text.RegularExpressions.Regex.Match(valueStr, @"new\s+Vector3\s*\(\s*([^,]+)\s*,\s*([^,]+)\s*,\s*([^)]+)\s*\)");
                if (m.Success)
                    return new Vector3(ParseFloat(m.Groups[1].Value), ParseFloat(m.Groups[2].Value), ParseFloat(m.Groups[3].Value));
                throw new Exception($"无法解析 Vector3: {valueStr}");
            }
            if (targetType == typeof(Vector2))
            {
                var m = System.Text.RegularExpressions.Regex.Match(valueStr, @"new\s+Vector2\s*\(\s*([^,]+)\s*,\s*([^)]+)\s*\)");
                if (m.Success)
                    return new Vector2(ParseFloat(m.Groups[1].Value), ParseFloat(m.Groups[2].Value));
                throw new Exception($"无法解析 Vector2: {valueStr}");
            }
            if (targetType == typeof(Color))
            {
                var m = System.Text.RegularExpressions.Regex.Match(valueStr, @"new\s+Color\s*\(\s*([^,]+)\s*,\s*([^,]+)\s*,\s*([^,]+)(?:\s*,\s*([^)]+))?\s*\)");
                if (m.Success)
                    return new Color(ParseFloat(m.Groups[1].Value), ParseFloat(m.Groups[2].Value), ParseFloat(m.Groups[3].Value),
                        m.Groups[4].Success ? ParseFloat(m.Groups[4].Value) : 1f);
                // 尝试命名颜色
                var colorProp = typeof(Color).GetProperty(valueStr, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (colorProp != null) return colorProp.GetValue(null);
                throw new Exception($"无法解析 Color: {valueStr}");
            }
            if (targetType.IsEnum)
            {
                return System.Enum.Parse(targetType, valueStr, true);
            }

            return System.Convert.ChangeType(valueStr, targetType);
        }

        #endregion

        #region Logging Helpers

        private void LogInfo(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"[MCP Bridge] {message}");
            }
        }

        private void LogWarning(string message)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning($"[MCP Bridge] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[MCP Bridge] {message}");
        }

        #endregion
    }

    [Serializable]
    public class ExecuteCommandRequest
    {
        public string command;
        public Dictionary<string, object> params_obj;
        public string requestId;
    }

    [Serializable]
    public class QuerySceneRequest
    {
        public string query;
        public Dictionary<string, object> filter;
        public string requestId;
    }

    [Serializable]
    public class SendEventRequest
    {
        public string eventName;
        public object eventData;
        public string requestId;
    }

    [Serializable]
    public class GetLogsRequest
    {
        public string level;
        public int limit;
        public string requestId;
    }

    [Serializable]
    public class ExecuteCodeRequest
    {
        public string code;
        public string requestId;
    }

    [Serializable]
    public class CommandResult
    {
        public bool success;
        public string command;
        public object result;
    }

    [Serializable]
    public class QueryResult
    {
        public bool success;
        public string query;
        public object result;
    }

    public static class DictionaryExtensions
    {
        public static object GetValueOrDefault(this Dictionary<string, object> dict, string key, object defaultValue = null)
        {
            return dict.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }
}
