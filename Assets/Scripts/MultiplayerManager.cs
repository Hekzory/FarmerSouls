using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TMPro;
using System;
using System.Collections;

public class MultiplayerManager : MonoBehaviour
{
    [Header("Network Settings")]
    public int port = 7778;
    public string hostIP = "localhost";
    public float autoDiscoveryTimeout = 2.0f; // Seconds to wait for server response
    public float connectionRetryDelay = 1.0f; // Seconds between auto-connection attempts
    public bool autoConnectOnStart = true;    // Whether to auto-connect on startup

    [Header("User Settings")]
    public string userName = "";             // Custom username (can be blank for auto-generated)
    public bool randomizeUserName = true;    // Whether to generate a random name

    [Header("UI Elements")]
    public GameObject chatPanel;
    public TMP_InputField messageInput;
    public Button sendButton;
    public TextMeshProUGUI chatHistoryText;
    public Button toggleChatButton;

    // UDP Components
    private UdpClient udpClient;         // For sending messages
    private UdpClient udpListener;       // For receiving messages (server mode)
    private UdpClient udpClientListener; // For receiving messages (client mode)
    private IPEndPoint serverEndPoint;   // Server endpoint for client

    private Thread receiveThread;        // Server receive thread
    private Thread clientReceiveThread;  // Client receive thread

    private bool isRunning = false;
    private bool isServer = false;
    private bool isConnecting = false;

    private int maxMessageCount = 50;
    private List<string> messageHistory = new List<string>();
    private bool isChatVisible = false;
    private string clientId;                 // Unique identifier for this client

    // Add dictionary to track connected clients
    private Dictionary<string, IPEndPoint> connectedClients = new Dictionary<string, IPEndPoint>();
    private bool isLocalClientConnected = false;

    // For thread-safe UI updates
    private Queue<Action> mainThreadActions = new Queue<Action>();

    // Singleton pattern
    public static MultiplayerManager Instance { get; private set; }

    void Awake()
    {
        Debug.Log("[MultiplayerManager] Initializing MultiplayerManager");

        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MultiplayerManager] Multiple instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Generate a unique client ID
        GenerateUniqueId();

        Debug.Log("[MultiplayerManager] Singleton instance set up successfully with ID: " + clientId);
    }

    private void GenerateUniqueId()
    {
        // Get a random name if requested or username is empty
        if (randomizeUserName || string.IsNullOrEmpty(userName))
        {
            // Generate a random name with prefix "Player" and random number
            int randomNumber = UnityEngine.Random.Range(1000, 9999);
            userName = "Player" + randomNumber;
        }

        // Use the username as the client ID but ensure uniqueness with a GUID suffix
        clientId = userName + "-" + System.Guid.NewGuid().ToString().Substring(0, 8);

        Debug.Log("[MultiplayerManager] Generated client ID: " + clientId + " with username: " + userName);
    }

    void Start()
    {
        Debug.Log("[MultiplayerManager] Starting MultiplayerManager");

        // Add listeners to UI elements
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(SendChatMessage);
            Debug.Log("[MultiplayerManager] Added send button listener");
        }
        else
            Debug.LogWarning("[MultiplayerManager] Send button reference is missing");

        if (toggleChatButton != null)
        {
            toggleChatButton.onClick.AddListener(ToggleChat);
            Debug.Log("[MultiplayerManager] Added toggle chat button listener");
        }
        else
            Debug.LogWarning("[MultiplayerManager] Toggle chat button reference is missing");

        if (messageInput != null)
        {
            messageInput.onSubmit.AddListener(delegate { SendChatMessage(); });
            Debug.Log("[MultiplayerManager] Added message input submit listener");
        }
        else
            Debug.LogWarning("[MultiplayerManager] Message input reference is missing");

        // Initialize chat UI state
        SetChatVisibility(false);
        Debug.Log("[MultiplayerManager] Chat UI initialized");

        // Auto-connect if enabled
        if (autoConnectOnStart)
        {
            Invoke("AutoConnectOrHost", 0.5f); // Slight delay to let everything initialize
        }
    }

    void Update()
    {
        // Execute any queued actions from the networking thread
        while (mainThreadActions.Count > 0)
        {
            Action action = null;
            lock (mainThreadActions)
            {
                if (mainThreadActions.Count > 0)
                    action = mainThreadActions.Dequeue();
            }
            action?.Invoke();
        }

        // Toggle chat with Enter key
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Debug.Log("[MultiplayerManager] Enter key pressed, toggling chat");
            ToggleChat();

            if (isChatVisible && messageInput != null)
            {
                messageInput.Select();
                messageInput.ActivateInputField();
                Debug.Log("[MultiplayerManager] Chat is now visible, focusing input field");
            }
        }
    }

    void OnApplicationQuit()
    {
        Debug.Log("[MultiplayerManager] Application quitting, stopping network");
        // Clean up network resources
        StopNetworking();
    }

    void OnDestroy()
    {
        Debug.Log("[MultiplayerManager] MultiplayerManager being destroyed, stopping network");
        // Clean up network resources
        StopNetworking();
    }

    private void StopNetworking()
    {
        Debug.Log("[MultiplayerManager] Stopping networking");
        isRunning = false;

        // Stop threads
        if (receiveThread != null && receiveThread.IsAlive)
        {
            Debug.Log("[MultiplayerManager] Aborting server receive thread");
            receiveThread.Abort();
            receiveThread = null;
        }

        if (clientReceiveThread != null && clientReceiveThread.IsAlive)
        {
            Debug.Log("[MultiplayerManager] Aborting client receive thread");
            clientReceiveThread.Abort();
            clientReceiveThread = null;
        }

        // Close UDP clients
        try {
            if (udpClient != null)
            {
                Debug.Log("[MultiplayerManager] Closing UDP client");
                udpClient.Close();
                udpClient = null;
            }
        } catch (Exception e) {
            Debug.LogError("[MultiplayerManager] Error closing UDP client: " + e);
        }

        try {
            if (udpListener != null)
            {
                Debug.Log("[MultiplayerManager] Closing UDP listener");
                udpListener.Close();
                udpListener = null;
            }
        } catch (Exception e) {
            Debug.LogError("[MultiplayerManager] Error closing UDP listener: " + e);
        }

        try {
            if (udpClientListener != null)
            {
                Debug.Log("[MultiplayerManager] Closing UDP client listener");
                udpClientListener.Close();
                udpClientListener = null;
            }
        } catch (Exception e) {
            Debug.LogError("[MultiplayerManager] Error closing UDP client listener: " + e);
        }

        // Reset state
        connectedClients.Clear();
        isLocalClientConnected = false;

        Debug.Log("[MultiplayerManager] Network resources cleaned up");
    }

    /// <summary>
    /// Automatically detects if a server exists, then either connects to them or hosts a new one
    /// </summary>
    public void AutoConnectOrHost()
    {
        if (isConnecting)
        {
            Debug.Log("[MultiplayerManager] Already attempting to connect, ignoring request");
            return;
        }

        Debug.Log("[MultiplayerManager] Starting auto-discovery on port: " + port);
        StopNetworking();
        isConnecting = true;

        // Show message
        AddSystemMessage("Looking for existing servers on port " + port + "...");

        // Start coroutine for server detection
        StartCoroutine(DetectServerAndConnect());
    }

    private System.Collections.IEnumerator DetectServerAndConnect()
    {
        bool serverDetected = false;

        Debug.Log("[MultiplayerManager] Beginning server detection");
        AddSystemMessage("Scanning network for servers...");

        // Create a temporary client to send discovery packets
        UdpClient discoveryClient = null;

        try
        {
            // Create discovery client with broadcast enabled
            discoveryClient = new UdpClient();
            discoveryClient.EnableBroadcast = true;
            discoveryClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // Also try to bind to any address to receive responses
            discoveryClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            var localEndPoint = (IPEndPoint)discoveryClient.Client.LocalEndPoint;
            Debug.Log("[MultiplayerManager] Discovery client bound to port: " + localEndPoint.Port);

            // Set a reasonable timeout for response
            discoveryClient.Client.ReceiveTimeout = (int)(autoDiscoveryTimeout * 1000);

            // Create endpoint for broadcast
            IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, port);

            // Create message with our endpoint info so server can respond directly
            string discoveryMessage = "DISCOVER:" + localEndPoint.Port;
            byte[] discoveryData = Encoding.UTF8.GetBytes(discoveryMessage);

            // Send discovery packet
            Debug.Log("[MultiplayerManager] Sending discovery packet to broadcast: " + broadcastEndPoint);
            discoveryClient.Send(discoveryData, discoveryData.Length, broadcastEndPoint);
            Debug.Log("[MultiplayerManager] Discovery packet sent. Waiting for responses...");

            // Also try localhost directly since broadcast sometimes doesn't work on localhost
            IPEndPoint localhostEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);
            discoveryClient.Send(discoveryData, discoveryData.Length, localhostEndPoint);
            Debug.Log("[MultiplayerManager] Also sent discovery to localhost: " + localhostEndPoint);

            // Wait for response - try multiple times in case of packet loss
            IPEndPoint senderEP = new IPEndPoint(IPAddress.Any, 0);
            int attemptCount = 0;

            // Try a few times to receive a response
            while (!serverDetected && attemptCount < 3)
            {
                attemptCount++;
                Debug.Log("[MultiplayerManager] Listening for server response (attempt " + attemptCount + "/3)");
                try
                {
                    // Wait for reply
                    byte[] responseData = discoveryClient.Receive(ref senderEP);
                    string response = Encoding.UTF8.GetString(responseData);

                    Debug.Log("[MultiplayerManager] Received discovery response: " + response + " from " + senderEP);

                    if (response.StartsWith("SERVER_AVAILABLE"))
                    {
                        Debug.Log("[MultiplayerManager] Server detected at: " + senderEP.Address);
                        AddSystemMessage("Found server at " + senderEP.Address);
                        serverDetected = true;

                        // Store the server IP for connecting after cleanup
                        hostIP = senderEP.Address.ToString();
                        break;
                    }
                }
                catch (SocketException se)
                {
                    Debug.Log("[MultiplayerManager] No server response on attempt " + attemptCount + ": " + se.Message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[MultiplayerManager] Error during auto-discovery: " + e.Message + "\n" + e.StackTrace);
            AddSystemMessage("Error during server discovery: " + e.Message);
        }
        finally
        {
            // Clean up discovery resources
            if (discoveryClient != null)
            {
                try {
                    discoveryClient.Close();
                } catch (Exception e) {
                    Debug.LogError("[MultiplayerManager] Error closing discovery client: " + e);
                }
                discoveryClient = null;
            }
        }

        // Brief delay - now outside of try/catch
        yield return new WaitForSeconds(0.2f);

        // Connect to server or become host - outside of try/catch
        if (serverDetected)
        {
            ConnectToServer(hostIP);
        }
        else
        {
            Debug.Log("[MultiplayerManager] No server found, becoming host");
            AddSystemMessage("No server found. Starting as host...");
            yield return new WaitForSeconds(0.2f); // Brief delay
            StartHost();
        }

        isConnecting = false;
    }

    // Server discovery response handler
    private void HandleDiscoveryRequest(IPEndPoint clientEP, string message, UdpClient server)
    {
        if (!isServer) return;

        try
        {
            Debug.Log("[MultiplayerManager] Processing discovery request from: " + clientEP);

            // Check if the message contains a port for direct reply
            int replyPort = clientEP.Port; // Default to the sender port

            if (message.StartsWith("DISCOVER:"))
            {
                string[] parts = message.Split(':');
                if (parts.Length > 1 && int.TryParse(parts[1], out int port))
                {
                    replyPort = port;
                    Debug.Log("[MultiplayerManager] Client specified reply port: " + replyPort);
                }
            }

            // Create reply endpoint with the correct port
            IPEndPoint replyEP = new IPEndPoint(clientEP.Address, replyPort);

            // Send response directly to the client's specified endpoint
            Debug.Log("[MultiplayerManager] Sending server availability response to: " + replyEP);
            byte[] responseData = Encoding.UTF8.GetBytes("SERVER_AVAILABLE:" + "PLAYER-NAME");
            server.Send(responseData, responseData.Length, replyEP);

            // Also try to send to the original endpoint in case the specified port isn't working
            if (replyPort != clientEP.Port)
            {
                Debug.Log("[MultiplayerManager] Also sending response to original endpoint: " + clientEP);
                server.Send(responseData, responseData.Length, clientEP);
            }

            Debug.Log("[MultiplayerManager] Server discovery response sent successfully");
        }
        catch (Exception e)
        {
            Debug.LogError("[MultiplayerManager] Error sending discovery response: " + e.Message + "\n" + e.StackTrace);
        }
    }

    public void StartHost()
    {
        Debug.Log("[MultiplayerManager] Starting host on port: " + port);
        StopNetworking();

        isServer = true;
        isRunning = true;

        try
        {
            // Create server listener on specified port
            udpListener = new UdpClient(port);
            udpListener.EnableBroadcast = true;
            udpListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            Debug.Log("[MultiplayerManager] Created UDP listener on port: " + port);

            // Start server listening thread
            receiveThread = new Thread(new ThreadStart(ServerReceiveLoop));
            receiveThread.IsBackground = true;
            receiveThread.Start();
            Debug.Log("[MultiplayerManager] Started server receive thread");

            // Also connect as a client to broadcast to others
            ConnectToServer("127.0.0.1");

            AddSystemMessage("Started server on port " + port);
        }
        catch (Exception e)
        {
            Debug.LogError("[MultiplayerManager] Error starting server: " + e.Message);
            AddSystemMessage("Error starting server: " + e.Message);
        }
    }

    public void ConnectToServer(string ip)
    {
        Debug.Log("[MultiplayerManager] Connecting to server at: " + ip + ":" + port);

        try
        {
            // Resolve the server IP
            IPAddress serverIP = IPAddress.Parse(ip);

            // Set up client UDP for sending
            udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            serverEndPoint = new IPEndPoint(serverIP, port);
            Debug.Log("[MultiplayerManager] Created UDP client targeting: " + ip + ":" + port);

            // Set up listener for receiving messages on a random port
            IPEndPoint localEP = new IPEndPoint(IPAddress.Any, 0);
            udpClientListener = new UdpClient(localEP);
            udpClientListener.EnableBroadcast = true;
            int clientPort = ((IPEndPoint)udpClientListener.Client.LocalEndPoint).Port;
            Debug.Log("[MultiplayerManager] Created UDP client listener on port: " + clientPort);

            isRunning = true;

            // Start client receive thread
            clientReceiveThread = new Thread(new ThreadStart(ClientReceiveLoop));
            clientReceiveThread.IsBackground = true;
            clientReceiveThread.Start();
            Debug.Log("[MultiplayerManager] Started client receive thread");

            // Send a join message to the server
            string joinMessage = "JOIN:" + clientId + ":" + userName + ":" + clientPort;
            SendRawMessage(joinMessage);
            Debug.Log("[MultiplayerManager] Sent join message: " + joinMessage);

            AddSystemMessage("Connecting to " + ip + ":" + port + "...");
        }
        catch (Exception e)
        {
            Debug.LogError("[MultiplayerManager] Error connecting to server: " + e.Message + "\n" + e.StackTrace);
            AddSystemMessage("Error connecting to server: " + e.Message);
        }
    }

    private void ServerReceiveLoop()
    {
        Debug.Log("[MultiplayerManager] Server receive loop started");
        IPEndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] data = udpListener.Receive(ref clientEP);
                string message = Encoding.UTF8.GetString(data);
                Debug.Log("[MultiplayerManager] Server received: " + message + " from " + clientEP.ToString());

                // Process message
                if (message.StartsWith("DISCOVER"))
                {
                    Debug.Log("[MultiplayerManager] Received discovery request from: " + clientEP.ToString());
                    HandleDiscoveryRequest(clientEP, message, udpListener);
                }
                else if (message.StartsWith("JOIN:"))
                {
                    // Extract client ID, username and reply port
                    string[] parts = message.Split(':');
                    if (parts.Length >= 3)
                    {
                        string clientId = parts[1];
                        string playerName = parts[2];
                        int clientPort = (parts.Length >= 4) ? int.Parse(parts[3]) : clientEP.Port;

                        // Store client endpoint for broadcasting
                        IPEndPoint replyEP = new IPEndPoint(clientEP.Address, clientPort);
                        connectedClients[clientId] = replyEP;
                        Debug.Log("[MultiplayerManager] Added client to connected list: " + clientId + " at " + replyEP);

                        Debug.Log("[MultiplayerManager] Player joining: " + playerName + " (ID: " + clientId + ") from " + clientEP.ToString());
                        BroadcastSystemMessage("Player " + playerName + " connected");

                        // Send welcome message directly to the client
                        SendDirectMessage(replyEP, "MSG:<color=#FFDD00>System: Welcome to the chat, " + playerName + "!</color>");

                        // Check if this is a local client joining
                        if (clientId == this.clientId)
                        {
                            isLocalClientConnected = true;
                            Debug.Log("[MultiplayerManager] Local client joined as both server and client");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[MultiplayerManager] Received malformed JOIN message: " + message);
                    }
                }
                else if (message.StartsWith("RENAME:"))
                {
                    // Extract client ID and new username
                    string[] parts = message.Split(':');
                    if (parts.Length >= 3)
                    {
                        string clientId = parts[1];
                        string newName = parts[2];
                        Debug.Log("[MultiplayerManager] Player renamed: " + newName + " (ID: " + clientId + ")");
                        BroadcastSystemMessage("Player changed name to " + newName);
                    }
                    else
                    {
                        Debug.LogWarning("[MultiplayerManager] Received malformed RENAME message: " + message);
                    }
                }
                else if (message.StartsWith("MSG:"))
                {
                    Debug.Log("[MultiplayerManager] Chat message received, broadcasting to all clients");
                    // Broadcast the message to all clients
                    BroadcastRawMessage(message);
                }
            }
            catch (SocketException e)
            {
                // Normal socket close
                if (e.ErrorCode != 10004) // Not a normal close
                {
                    Debug.LogError("[MultiplayerManager] Server receive error: " + e.Message);
                }
                Debug.Log("[MultiplayerManager] Server socket closed");
                break;
            }
            catch (ThreadAbortException)
            {
                // Normal thread abort
                Debug.Log("[MultiplayerManager] Server thread aborted");
                break;
            }
            catch (Exception e)
            {
                Debug.LogError("[MultiplayerManager] Server receive error: " + e.Message);
            }
        }
        Debug.Log("[MultiplayerManager] Server receive loop ended");
    }

    private void ClientReceiveLoop()
    {
        Debug.Log("[MultiplayerManager] Client receive loop started");
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                // Use the client-specific listener
                byte[] data = udpClientListener.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data);
                Debug.Log("[MultiplayerManager] Client received: " + message + " from " + remoteEP.ToString());

                // Process message
                if (message.StartsWith("MSG:"))
                {
                    string chatMessage = message.Substring(4);
                    Debug.Log("[MultiplayerManager] Received chat message: " + chatMessage);
                    QueueOnMainThread(() => AddChatMessage(chatMessage));
                }
            }
            catch (SocketException e)
            {
                // Normal socket close
                if (e.ErrorCode != 10004) // Not a normal close
                {
                    Debug.LogError("[MultiplayerManager] Client receive error: " + e.Message);
                }
                Debug.Log("[MultiplayerManager] Client socket closed");
                break;
            }
            catch (ThreadAbortException)
            {
                // Normal thread abort
                Debug.Log("[MultiplayerManager] Client thread aborted");
                break;
            }
            catch (Exception e)
            {
                Debug.LogError("[MultiplayerManager] Client receive error: " + e.Message + "\n" + e.StackTrace);
            }
        }
        Debug.Log("[MultiplayerManager] Client receive loop ended");
    }

    private void QueueOnMainThread(Action action)
    {
        lock (mainThreadActions)
        {
            mainThreadActions.Enqueue(action);
        }
    }

    private void SendRawMessage(string message)
    {
        if (udpClient == null)
        {
            Debug.LogWarning("[MultiplayerManager] Cannot send message - UDP client is null");
            return;
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            udpClient.Send(data, data.Length, serverEndPoint);
            Debug.Log("[MultiplayerManager] Sent message: " + message + " to " + serverEndPoint.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError("[MultiplayerManager] Error sending message: " + e.Message + "\n" + e.StackTrace);
        }
    }

    private void SendDirectMessage(IPEndPoint endpoint, string message)
    {
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            udpListener.Send(data, data.Length, endpoint);
            Debug.Log("[MultiplayerManager] Sent direct message: " + message + " to " + endpoint.ToString());
        }
        catch (Exception e)
        {
            Debug.LogError("[MultiplayerManager] Error sending direct message: " + e.Message);
        }
    }

    private void BroadcastRawMessage(string message)
    {
        if (!isServer)
        {
            Debug.LogWarning("[MultiplayerManager] Only server can broadcast messages");
            return;
        }

        // Add to our own chat (if it's a chat message)
        if (message.StartsWith("MSG:"))
        {
            string chatMsg = message.Substring(4);
            Debug.Log("[MultiplayerManager] Broadcasting chat message: " + chatMsg);

            // Only add to our own chat if we're not also connected as a client
            // (to avoid duplicate messages)
            if (!isLocalClientConnected)
            {
                QueueOnMainThread(() => AddChatMessage(chatMsg));
            }
        }

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);

            // Broadcast to all connected clients
            Debug.Log("[MultiplayerManager] Broadcasting to " + connectedClients.Count + " clients");

            // If we have clients connected, broadcast to them
            foreach (var client in connectedClients)
            {
                try
                {
                    // Send to this client's endpoint
                    Debug.Log("[MultiplayerManager] Attempting to broadcast to client " + client.Key + " at " + client.Value);
                    udpListener.Send(data, data.Length, client.Value);
                    Debug.Log("[MultiplayerManager] Broadcasted message to client " + client.Key + ": " + message);
                }
                catch (Exception e)
                {
                    Debug.LogError("[MultiplayerManager] Error broadcasting to client " + client.Key + ": " + e.Message);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[MultiplayerManager] Error broadcasting message: " + e.Message);
        }
    }

    // UI Methods
    public void ToggleChat()
    {
        Debug.Log("[MultiplayerManager] Toggling chat visibility");
        SetChatVisibility(!isChatVisible);
    }

    public void SetChatVisibility(bool isVisible)
    {
        isChatVisible = isVisible;
        Debug.Log("[MultiplayerManager] Setting chat visibility to: " + isVisible);

        if (chatPanel != null)
            chatPanel.SetActive(isVisible);
        else
            Debug.LogWarning("[MultiplayerManager] Cannot set visibility - chat panel is null");

        // When chat is visible, focus the input field
        if (isVisible && messageInput != null)
        {
            messageInput.Select();
            messageInput.ActivateInputField();
            Debug.Log("[MultiplayerManager] Focused chat input field");
        }
    }

    public void SendChatMessage()
    {
        if (messageInput == null)
        {
            Debug.LogWarning("[MultiplayerManager] Cannot send message - message input is null");
            return;
        }

        if (string.IsNullOrEmpty(messageInput.text))
        {
            Debug.Log("[MultiplayerManager] Not sending empty message");
            return;
        }

        // Get the message from the input field
        string messageText = messageInput.text;
        Debug.Log("[MultiplayerManager] Sending chat message: " + messageText);

        // Format and send the message
        string formattedMessage = userName + ": " + messageText;
        SendRawMessage("MSG:" + formattedMessage);

        // Clear the input field
        messageInput.text = "";
        messageInput.ActivateInputField();
        Debug.Log("[MultiplayerManager] Cleared input field");

        // Show message locally if we're not the server (server adds it during broadcast)
        if (!isServer)
        {
            Debug.Log("[MultiplayerManager] Adding message to local chat (client mode)");
            AddChatMessage(formattedMessage);
        }
    }

    private void AddChatMessage(string message)
    {
        Debug.Log("[MultiplayerManager] Adding chat message: " + message);
        messageHistory.Add(message);

        // Trim history if it exceeds max count
        while (messageHistory.Count > maxMessageCount)
        {
            messageHistory.RemoveAt(0);
            Debug.Log("[MultiplayerManager] Trimmed chat history (exceeded max count)");
        }

        // Update chat history UI
        UpdateChatHistory();
    }

    private void AddSystemMessage(string message)
    {
        Debug.Log("[MultiplayerManager] Adding system message: " + message);
        AddChatMessage("<color=#FFDD00>System: " + message + "</color>");
    }

    private void BroadcastSystemMessage(string message)
    {
        Debug.Log("[MultiplayerManager] Broadcasting system message: " + message);
        BroadcastRawMessage("MSG:<color=#FFDD00>System: " + message + "</color>");
    }

    private void UpdateChatHistory()
    {
        if (chatHistoryText == null)
        {
            Debug.LogWarning("[MultiplayerManager] Cannot update chat history - chat history text is null");
            return;
        }

        string fullText = string.Join("\n", messageHistory);
        chatHistoryText.text = fullText;
        Debug.Log("[MultiplayerManager] Updated chat history UI");
    }
}