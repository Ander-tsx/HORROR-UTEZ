using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace HorrorUtez.Remake
{
    [Serializable] public sealed class StudentState
    {
        public int id, held = -1, health = 100, strength, credits;
        public string name;
        public Vector3 position, aim = Vector3.forward;
        public float yaw, pitch, reach = 1.2f, eye = 1.55f;
        public bool alive = true, torch = true, escaped, tumble;
        // Grab point in the held object's local space and its orientation relative to the student's camera.
        public Vector3 grabLocal;
        public Quaternion hold = Quaternion.identity;
    }
    [Serializable] public sealed class LootState
    {
        public int id, value;
        public Vector3 position;
        public Quaternion rotation;
    }
    [Serializable] public sealed class WireMessage
    {
        public string type, name, text, audio;
        public int id, item = -1, day, quota, cargo, phase, seconds, credits;
        public Vector3 position, aim;
        public Quaternion rotation;
        public float yaw, pitch, reach, eye;
        public bool flag, tumble;
        public StudentState[] students;
        public LootState[] loot;
        public Vector3[] enemies;
        public bool[] doors;
    }

    // Length framed TCP, bounded queues and one writer per socket. All Unity work stays
    // on the main thread. Five-player experimental sessions need no third party service.
    public sealed class RemakeWire : IDisposable
    {
        public sealed class Incoming { public int peer; public string json; public bool connected, disconnected; }
        private sealed class Peer : IDisposable
        {
            public readonly TcpClient client;
            public readonly BlockingCollection<string> outgoing = new BlockingCollection<string>(48);
            public Peer(TcpClient client) { this.client = client; client.NoDelay = true; }
            public void Dispose() { client.Close(); outgoing.CompleteAdding(); }
        }
        public const int Port = 27777;
        private readonly ConcurrentDictionary<int, Peer> peers = new ConcurrentDictionary<int, Peer>();
        private readonly ConcurrentQueue<Incoming> inbox = new ConcurrentQueue<Incoming>();
        private TcpListener listener;
        private volatile bool stopped;
        private int nextId;
        public bool Hosting { get; private set; }
        public string Error { get; private set; }
        public int PeerCount => peers.Count;

        public void Host()
        {
            listener = new TcpListener(IPAddress.Any, Port);
            listener.Start(4);
            Hosting = true;
            _ = AcceptLoop();
        }
        public async Task Join(string address)
        {
            var client = new TcpClient();
            try
            {
                Task connect = client.ConnectAsync(address.Trim(), Port);
                if (await Task.WhenAny(connect, Task.Delay(8000)) != connect)
                    throw new IOException("El anfitrión no respondió en 8 segundos.");
                await connect;
                if (stopped) { client.Close(); return; }
                AddPeer(0, client);
            }
            catch (Exception e) { client.Close(); Error = e.Message; }
        }
        private async Task AcceptLoop()
        {
            try
            {
                while (!stopped)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    if (peers.Count >= 4) { client.Close(); continue; }
                    AddPeer(Interlocked.Increment(ref nextId), client);
                }
            }
            catch (Exception e) { if (!stopped) Error = e.Message; }
        }
        private void AddPeer(int id, TcpClient client)
        {
            var peer = new Peer(client);
            if (!peers.TryAdd(id, peer)) { client.Close(); return; }
            inbox.Enqueue(new Incoming { peer = id, connected = true });
            _ = Task.Run(() => ReadLoop(id, peer));
            _ = Task.Run(() => WriteLoop(id, peer));
        }
        private void ReadLoop(int id, Peer peer)
        {
            try
            {
                var stream = peer.client.GetStream();
                byte[] prefix = new byte[4];
                while (!stopped)
                {
                    ReadExactly(stream, prefix);
                    int size = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(prefix, 0));
                    if (size <= 0 || size > 131072 || inbox.Count > 256) throw new IOException("Paquete inválido.");
                    byte[] bytes = new byte[size];
                    ReadExactly(stream, bytes);
                    inbox.Enqueue(new Incoming { peer = id, json = Encoding.UTF8.GetString(bytes) });
                }
            }
            catch (Exception) { Disconnect(id); }
        }
        private static void ReadExactly(Stream stream, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int count = stream.Read(buffer, offset, buffer.Length - offset);
                if (count == 0) throw new EndOfStreamException();
                offset += count;
            }
        }
        private void WriteLoop(int id, Peer peer)
        {
            try
            {
                var stream = peer.client.GetStream();
                foreach (string json in peer.outgoing.GetConsumingEnumerable())
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    byte[] prefix = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytes.Length));
                    stream.Write(prefix, 0, 4);
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
            catch (Exception) { Disconnect(id); }
        }
        public bool Poll(out Incoming message) => inbox.TryDequeue(out message);
        public void Send(int id, string json)
        {
            if (!peers.TryGetValue(id, out Peer peer)) return;
            try { if (!peer.outgoing.TryAdd(json)) Disconnect(id); }
            catch (InvalidOperationException) { }
        }
        public void Broadcast(string json, int except = -1)
        {
            foreach (int id in peers.Keys) if (id != except) Send(id, json);
        }
        public void Disconnect(int id)
        {
            if (!peers.TryRemove(id, out Peer peer)) return;
            peer.Dispose();
            if (!stopped) inbox.Enqueue(new Incoming { peer = id, disconnected = true });
        }
        public void Dispose()
        {
            stopped = true;
            listener?.Stop();
            foreach (int id in peers.Keys) Disconnect(id);
        }
        public static string LocalAddresses()
        {
            try
            {
                var addresses = new List<string>();
                foreach (IPAddress address in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                        addresses.Add(address.ToString());
                return addresses.Count > 0 ? string.Join(" / ", addresses) : "127.0.0.1";
            }
            catch { return "127.0.0.1"; }
        }
    }
}
