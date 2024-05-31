using Steamworks;
using UnityEngine;
using Unity.Netcode;
using System.IO;
using System;

[RequireComponent(typeof(AudioSource))]
public class VoiceChat : NetworkBehaviour
{
    public bool MuteLocalClient = true;
    public bool Muted = false;
    private MemoryStream output;
    private MemoryStream stream;
    private MemoryStream input;
    private AudioSource audioSource;
    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = false;
        audioSource.clip = AudioClip.Create("VoiceData", (int)256, 1, (int)SteamUser.OptimalSampleRate, true, OnAudioRead, null);
        audioSource.loop = true;
        audioSource.Play();

        stream = new MemoryStream();
        output = new MemoryStream();
        input = new MemoryStream();



        clipBufferSize = (int)SteamUser.OptimalSampleRate * 5;
        clipBuffer = new float[clipBufferSize];
    }

    void Update()
    {
        if(IsOwner && MuteLocalClient)
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }
        else
        {
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }

        if (IsOwner)
        {
            SteamUser.VoiceRecord = !Muted;
        }

        // Check if this client is the owner and recording voice
        if (IsOwner && SteamUser.HasVoiceData)
        {
            // Create a MemoryStream to write the voice data into
            using (MemoryStream memoryStream = new MemoryStream())
            {
                // Read voice data from Steam and write it into the MemoryStream
                int compressedWritten = SteamUser.ReadVoiceData(memoryStream);

                // Get the byte array from the MemoryStream
                byte[] voiceData = memoryStream.GetBuffer();

                // Send voice data to the server
                if (voiceData.Length > 0)
                {
                    SendAudioServerRPC(voiceData, compressedWritten);
                }
            }
        }
    }

    [ServerRpc]
    void SendAudioServerRPC(byte[] data, int bytesWritten)
    {
        TargetReceiveAudioClientRPC(data, bytesWritten);
    }

    [ClientRpc]
    void TargetReceiveAudioClientRPC(byte[] data, int bytesWritten)
    {
        Debug.Log("Received voice data from server");



        input.Write(data, 0, bytesWritten);
        input.Position = 0;

        int uncompressedWritten = SteamUser.DecompressVoice(input, bytesWritten, output);
        input.Position = 0;

        byte[] outputBuffer = output.GetBuffer();
        WriteToClip(outputBuffer, uncompressedWritten);
        output.Position = 0;

    }

    private int clipBufferSize;
    private float[] clipBuffer;
    private int playbackBuffer;
    private int dataPosition;
    private int dataReceived;
    private void OnAudioRead(float[] data)
    {
        for (int i = 0; i < data.Length; ++i)
        {
            // start with silence
            data[i] = 0;

            // do I  have anything to play?
            if (playbackBuffer > 0)
            {
                // current data position playing
                dataPosition = (dataPosition + 1) % clipBufferSize;

                data[i] = clipBuffer[dataPosition];

                playbackBuffer--;
            }
        }

    }
    private void WriteToClip(byte[] uncompressed, int iSize)
    {
        for (int i = 0; i < iSize; i += 2)
        {
            // insert converted float to buffer
            float converted = (short)(uncompressed[i] | uncompressed[i + 1] << 8) / 32767.0f;
            clipBuffer[dataReceived] = converted;

            // buffer loop
            dataReceived = (dataReceived + 1) % clipBufferSize;

            playbackBuffer++;
        }
    }


}
