using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebApplication1.Services
{
    public class ConnectionManager
    {
        private Dictionary<string, WebSocket> Clients = new();

        public void AddClient(string userId, WebSocket connection)
        {
            Clients[userId] = connection;
        }

        public void RemoveClient(string userId)
        {
            Clients.Remove(userId);
        }

        public async void SendMessageToClient(string userId, string message)
        {
            if (Clients.TryGetValue(userId, out var socket) && socket.State == WebSocketState.Open)
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        public async void BroadcastMessage(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            foreach (var socket in Clients.Values.Where(s => s.State == WebSocketState.Open))
            {
                await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
    }
} 