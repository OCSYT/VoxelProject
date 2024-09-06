using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Netcode;

public class ShareFile : NetworkBehaviour
{
    private const int ChunkSize = 2048; // Adjust the chunk size as needed
    private Dictionary<ulong, List<byte[]>> receivedChunks = new Dictionary<ulong, List<byte[]>>();

    [ClientRpc]
    private void ReceiveChunkClientRPC(FileChunkMessage message, ClientRpcParams clientRpcParams)
    {
        // Client receives chunk from server
        if (!receivedChunks.ContainsKey(message.SenderClientId))
        {
            receivedChunks[message.SenderClientId] = new List<byte[]>();
        }

        receivedChunks[message.SenderClientId].Add(message.ChunkData);

        // Check if all chunks have been received
        if (receivedChunks[message.SenderClientId].Count == message.TotalChunks)
        {
            SaveFile(receivedChunks[message.SenderClientId], message.DestinationFolder, message.Filename);
            receivedChunks.Remove(message.SenderClientId);
        }
    }

    public void SendFile(string path, string destinationFolder, ulong clientID)
    {
        if (IsServer)
        {
            StartCoroutine(SendFileCoroutine(path, destinationFolder, clientID));
        }
    }

    private IEnumerator SendFileCoroutine(string path, string destinationFolder, ulong clientID)
    {
        byte[] fileData = File.ReadAllBytes(path);
        string filename = Path.GetFileName(path);
        int totalChunks = Mathf.CeilToInt(fileData.Length / (float)ChunkSize);

        for (int i = 0; i < totalChunks; i++)
        {
            int offset = i * ChunkSize;
            int size = Mathf.Min(ChunkSize, fileData.Length - offset);
            byte[] chunkData = new byte[size];
            System.Buffer.BlockCopy(fileData, offset, chunkData, 0, size);

            // Server sends chunk to client
            SendChunkToClientServerRPC(chunkData, i, totalChunks, destinationFolder, filename, clientID);

            yield return new WaitForFixedUpdate(); // Optional: throttle sending to avoid network congestion
        }
    }

    [ServerRpc]
    private void SendChunkToClientServerRPC(byte[] chunkData, int chunkIndex, int totalChunks, string destinationFolder, string filename, ulong clientID)
    {
        var message = new FileChunkMessage
        {
            ChunkData = chunkData,
            ChunkIndex = chunkIndex,
            TotalChunks = totalChunks,
            DestinationFolder = destinationFolder,
            Filename = filename,
            SenderClientId = NetworkObjectId
        };

        // Client receives chunk from server
        ClientRpcSendParams sendParams = new ClientRpcSendParams();
        sendParams = default;
        sendParams.TargetClientIds = new[] { clientID };
        ClientRpcParams  rpcParams = default;
        rpcParams.Send = sendParams;
        ReceiveChunkClientRPC(message, rpcParams);
    }

    private void SaveFile(List<byte[]> chunks, string destinationFolder, string filename)
    {
        // Ensure the destination directory exists
        destinationFolder = Application.dataPath + destinationFolder;

        if (!Directory.Exists(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
        }

        // Combine the destination folder with the filename to get the full path
        string destinationPath = Path.GetFullPath(Path.Combine(destinationFolder, filename));

        // Write all chunks to the file at the destination path
        using (var fileStream = new FileStream(destinationPath, FileMode.Create))
        {
            foreach (var chunk in chunks)
            {
                fileStream.Write(chunk, 0, chunk.Length);
            }
        }
        Debug.Log(destinationPath);
    }
}

public struct FileChunkMessage : INetworkSerializable
{
    public byte[] ChunkData;
    public int ChunkIndex;
    public int TotalChunks;
    public string DestinationFolder;
    public string Filename;
    public ulong SenderClientId; // Added to identify the sender client

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ChunkData);
        serializer.SerializeValue(ref ChunkIndex);
        serializer.SerializeValue(ref TotalChunks);
        serializer.SerializeValue(ref DestinationFolder);
        serializer.SerializeValue(ref Filename);
        serializer.SerializeValue(ref SenderClientId);
    }
}
