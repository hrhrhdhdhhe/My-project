// World Crisis | Слой: Core | Ответственный: OpenCode
// Тонкая сетевая прослойка без сторонних пакетов: локальный транспорт для стенда
// и TCP-транспорт для мультиплеера по локальной сети. Формат сообщений — JSON + '\n'.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace WorldCrisis
{
    public interface INetworkTransport : IDisposable
    {
        NetRole Role { get; }

        bool IsListening { get; }

        bool IsConnected { get; }

        string StatusText { get; }

        /// <summary>Прокачка приёма/отправки. Вызывается каждый тик из MatchManager.</summary>
        void Poll();

        void SendToServer(string payload);

        void SendToPeer(string peerId, string payload);

        void Broadcast(string payload);

        bool TryReceive(out string peerId, out string payload);
    }

    /// <summary>
    /// Локальный транспорт: отправленные команды сразу попадают во входящий ящик.
    /// Нужен для стенда без сокетов и для мгновенного прогона 6 раундов.
    /// </summary>
    public sealed class LocalLoopbackTransport : INetworkTransport
    {
        private readonly Queue<string[]> _inbox = new Queue<string[]>();
        private readonly List<string> _sent = new List<string>();

        public NetRole Role
        {
            get { return NetRole.Solo; }
        }

        public bool IsListening
        {
            get { return true; }
        }

        public bool IsConnected
        {
            get { return true; }
        }

        public string StatusText
        {
            get { return "локальный стенд (" + _sent.Count + " команд)"; }
        }

        public int SentCount
        {
            get { return _sent.Count; }
        }

        public void Poll()
        {
        }

        public void SendToServer(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return;
            }

            _sent.Add(payload);
            _inbox.Enqueue(new[] { "server", payload });
        }

        public void SendToPeer(string peerId, string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return;
            }

            _sent.Add(payload);
            _inbox.Enqueue(new[] { peerId, payload });
        }

        public void Broadcast(string payload)
        {
            SendToPeer("client", payload);
        }

        public bool TryReceive(out string peerId, out string payload)
        {
            if (_inbox.Count == 0)
            {
                peerId = string.Empty;
                payload = string.Empty;
                return false;
            }

            string[] item = _inbox.Dequeue();
            peerId = item[0];
            payload = item[1];
            return true;
        }

        public void Dispose()
        {
            _inbox.Clear();
            _sent.Clear();
        }
    }

    /// <summary>
    /// TCP-транспорт. Хост принимает подключения и шлёт всем снимки матча,
    /// клиент отправляет команды и рисует присланное состояние.
    /// Никаких потоков: всё крутится из Poll() в главном потоке Unity.
    /// </summary>
    public sealed class TcpNetworkTransport : INetworkTransport
    {
        private sealed class Peer
        {
            public string Id;
            public TcpClient Client;
            public readonly StringBuilder Buffer = new StringBuilder();
            public bool Disconnected;
        }

        private sealed class Outgoing
        {
            public string PeerId;
            public string Payload;
            public bool BroadcastAll;
        }

        private const int ReceiveChunk = 4096;

        private readonly NetRole _role;
        private readonly ConcurrentQueue<string[]> _inbox = new ConcurrentQueue<string[]>();
        private readonly ConcurrentQueue<Outgoing> _outbox = new ConcurrentQueue<Outgoing>();
        private readonly List<Peer> _peers = new List<Peer>();
        private readonly byte[] _chunk = new byte[ReceiveChunk];

        private TcpListener _listener;
        private TcpClient _serverConnection;
        private readonly StringBuilder _serverBuffer = new StringBuilder();
        private int _peerCounter;
        private string _status = "не запущен";

        public TcpNetworkTransport(NetRole role)
        {
            _role = role;
        }

        public NetRole Role
        {
            get { return _role; }
        }

        public bool IsListening
        {
            get { return _listener != null; }
        }

        public bool IsConnected
        {
            get { return _role == NetRole.Host ? _listener != null : _serverConnection != null; }
        }

        public int PeerCount
        {
            get { return _peers.Count; }
        }

        public string StatusText
        {
            get
            {
                if (_role == NetRole.Host)
                {
                    return IsListening
                        ? "хост " + ((IPEndPoint)_listener.LocalEndpoint).Port + ", клиентов: " + _peers.Count
                        : "хост не запущен";
                }

                return IsConnected ? "подключено к " + _status : "нет соединения";
            }
        }

        /// <summary>Запуск сервера на указанном порту. Бросает исключение наружу — вызывающий код ловит.</summary>
        public void StartHosting(int port)
        {
            if (_listener != null)
            {
                return;
            }

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _status = "порт " + port;
            Debug.Log("[WorldCrisis] TCP-хост запущен на порту " + port);
        }

        /// <summary>Подключение клиента к серверу. Таймаут фиксированный, чтобы UI не висел.</summary>
        public void Connect(string address, int port, int timeoutMs)
        {
            if (_serverConnection != null)
            {
                return;
            }

            TcpClient client = new TcpClient();
            client.NoDelay = true;

            IAsyncResult result = client.BeginConnect(address, port, null, null);
            try
            {
                if (!result.AsyncWaitHandle.WaitOne(timeoutMs))
                {
                    throw new TimeoutException("Таймаут подключения к " + address + ":" + port);
                }

                client.EndConnect(result);
            }
            finally
            {
                result.AsyncWaitHandle.Close();
            }

            if (!client.Connected)
            {
                client.Close();
                throw new InvalidOperationException("Соединение не установлено.");
            }

            _serverConnection = client;
            _status = address + ":" + port;
            Debug.Log("[WorldCrisis] TCP-клиент подключён к " + _status);
        }

        public void Poll()
        {
            if (_role == NetRole.Host)
            {
                AcceptPending();
                ReadPeers();
            }
            else if (_serverConnection != null)
            {
                ReadServer();
            }

            FlushOutbox();
        }

        public void SendToServer(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return;
            }

            Outgoing item = new Outgoing();
            item.Payload = payload;
            _outbox.Enqueue(item);
        }

        public void SendToPeer(string peerId, string payload)
        {
            if (string.IsNullOrEmpty(payload) || string.IsNullOrEmpty(peerId))
            {
                return;
            }

            Outgoing item = new Outgoing();
            item.PeerId = peerId;
            item.Payload = payload;
            _outbox.Enqueue(item);
        }

        public void Broadcast(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return;
            }

            Outgoing item = new Outgoing();
            item.Payload = payload;
            item.BroadcastAll = true;
            _outbox.Enqueue(item);
        }

        public bool TryReceive(out string peerId, out string payload)
        {
            if (_inbox.TryDequeue(out string[] item))
            {
                peerId = item[0];
                payload = item[1];
                return true;
            }

            peerId = string.Empty;
            payload = string.Empty;
            return false;
        }

        private void AcceptPending()
        {
            if (_listener == null)
            {
                return;
            }

            try
            {
                while (_listener.Pending())
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    client.NoDelay = true;
                    Peer peer = new Peer();
                    peer.Client = client;
                    peer.Id = "peer" + (++_peerCounter);
                    _peers.Add(peer);
                    Debug.Log("[WorldCrisis] Подключился " + peer.Id);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WorldCrisis] Ошибка приёма подключения: " + exception.Message);
            }
        }

        private void ReadPeers()
        {
            for (int i = _peers.Count - 1; i >= 0; i--)
            {
                Peer peer = _peers[i];
                if (!ReadConnection(peer.Client, peer.Buffer, peer.Id))
                {
                    peer.Disconnected = true;
                }

                if (peer.Disconnected)
                {
                    Debug.Log("[WorldCrisis] Отключился " + peer.Id);
                    CloseClient(peer.Client);
                    _peers.RemoveAt(i);
                }
            }
        }

        private void ReadServer()
        {
            if (!ReadConnection(_serverConnection, _serverBuffer, "server"))
            {
                Debug.LogWarning("[WorldCrisis] Соединение с хостом потеряно.");
                CloseClient(_serverConnection);
                _serverConnection = null;
            }
        }

        /// <summary>Читает всё доступное из сокета и вырезает сообщения по символу перевода строки.</summary>
        private bool ReadConnection(TcpClient client, StringBuilder buffer, string peerId)
        {
            if (client == null)
            {
                return false;
            }

            try
            {
                NetworkStream stream = client.GetStream();
                while (stream.DataAvailable)
                {
                    int read = stream.Read(_chunk, 0, _chunk.Length);
                    if (read <= 0)
                    {
                        return false;
                    }

                    buffer.Append(Encoding.UTF8.GetString(_chunk, 0, read));
                    if (buffer.Length > 1 << 20)
                    {
                        Debug.LogWarning("[WorldCrisis] Сообщение от " + peerId + " слишком велико, сброшено.");
                        buffer.Clear();
                    }
                }

                int newlineIndex;
                while ((newlineIndex = buffer.ToString().IndexOf('\n')) >= 0)
                {
                    string message = buffer.ToString(0, newlineIndex);
                    buffer.Remove(0, newlineIndex + 1);
                    if (message.Length > 0)
                    {
                        _inbox.Enqueue(new[] { peerId, message });
                    }
                }

                // Проверка закрытия сокета: читаемый, но пустой сокет означает разрыв.
                if (client.Client.Poll(500, SelectMode.SelectRead) && client.Available == 0)
                {
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WorldCrisis] Ошибка чтения от " + peerId + ": " + exception.Message);
                return false;
            }
        }

        private void FlushOutbox()
        {
            while (_outbox.TryDequeue(out Outgoing item))
            {
                if (item.BroadcastAll)
                {
                    for (int i = 0; i < _peers.Count; i++)
                    {
                        WriteTo(_peers[i].Client, item.Payload);
                    }

                    continue;
                }

                Peer target = FindPeer(item.PeerId);
                if (target != null)
                {
                    WriteTo(target.Client, item.Payload);
                }
                else if (_serverConnection != null && item.PeerId == "server")
                {
                    WriteTo(_serverConnection, item.Payload);
                }
            }
        }

        private Peer FindPeer(string peerId)
        {
            for (int i = 0; i < _peers.Count; i++)
            {
                if (string.Equals(_peers[i].Id, peerId, StringComparison.Ordinal))
                {
                    return _peers[i];
                }
            }

            return null;
        }

        private void WriteTo(TcpClient client, string payload)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(payload + "\n");
                NetworkStream stream = client.GetStream();
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WorldCrisis] Ошибка отправки: " + exception.Message);
            }
        }

        private static void CloseClient(TcpClient client)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                client.Close();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[WorldCrisis] Ошибка закрытия сокета: " + exception.Message);
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _peers.Count; i++)
            {
                CloseClient(_peers[i].Client);
            }

            _peers.Clear();
            CloseClient(_serverConnection);
            _serverConnection = null;

            if (_listener != null)
            {
                try
                {
                    _listener.Stop();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[WorldCrisis] Ошибка остановки слушателя: " + exception.Message);
                }

                _listener = null;
            }
        }
    }
}