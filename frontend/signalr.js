let signalRConnection = null;
let isSignalRConnected = false;

// SignalR bağlantısını başlat
async function initializeSignalR() {
  const token = localStorage.getItem("token");
  if (!token) {
    console.log("No token, skipping SignalR connection");
    return;
  }

  try {
    signalRConnection = new signalR.HubConnectionBuilder()
      .withUrl("https://localhost:7030/chatHub", {
        accessTokenFactory: () => token,
        transport: signalR.HttpTransportType.WebSockets,
        skipNegotiation: true
      })
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build();

    setupSignalREventListeners();
    await signalRConnection.start();
    isSignalRConnected = true;
    console.log("SignalR Connected successfully!");
  } catch (error) {
    console.error("SignalR Connection Error:", error);
    isSignalRConnected = false;
  }
}

function setupSignalREventListeners() {
  signalRConnection.on("ReceiveMessage", function (user, message, timestamp) {
    // Burada chat.html veya diğer sayfalarda mesajı ekrana yazacak fonksiyon çağrılabilir
    // Örneğin: addRealTimeMessage(user, message, timestamp);
    // Veya global bir event tetiklenebilir
    if (typeof window.onSignalRMessage === 'function') {
      window.onSignalRMessage(user, message, timestamp);
    }
  });
  // Diğer eventler de eklenebilir
}

function disconnectSignalR() {
  if (signalRConnection && isSignalRConnected) {
    signalRConnection.stop();
    isSignalRConnected = false;
  }
} 