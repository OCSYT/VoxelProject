using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using UnityEngine;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Unity.Netcode;



public class ChunkManager : NetworkBehaviour
{

    public AnimationCurve terrainCurve;


    [Serializable]
    public class SerializableVector3Int
    {
        public int x, y, z;
        public SerializableVector3Int() { }

        public SerializableVector3Int(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public SerializableVector3Int(Vector3Int vector)
        {
            this.x = vector.x;
            this.y = vector.y;
            this.z = vector.z;
        }

        public Vector3Int ToVector3Int()
        {
            return new Vector3Int(x, y, z);
        }
    }



    [Serializable]
    public class SerializableVector3
    {
        public float x, y, z;
        public SerializableVector3() { }

        public SerializableVector3(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public SerializableVector3(Vector3 vector)
        {
            this.x = vector.x;
            this.y = vector.y;
            this.z = vector.z;
        }

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    public class PlayerData
    {
        public string Name;
        public float Y;
        public SerializableVector3 Position;

        public PlayerData() { }

        public PlayerData(string name, Vector3 position, float y)
        {
            Name = name;
            Position = new SerializableVector3(position);
            Y = y;
        }
    }


    [Serializable]
    public class ChunkData
    {
        public SerializableVector3Int Position;
        public List<byte> Data;
        public List<byte> DataDir;

        public ChunkData() { }
        public ChunkData(Vector3Int position, byte[,,] data, byte[,,] dataDir)
        {
            Position = new SerializableVector3Int(position);

            Data = new List<byte>();
            DataDir = new List<byte>();
            int sizeX = data.GetLength(0);
            int sizeY = data.GetLength(1);
            int sizeZ = data.GetLength(2);
            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        Data.Add(data[x, y, z]);
                        DataDir.Add(dataDir[x, y, z]);
                    }
                }
            }
        }
    }

    [Serializable]
    public class SaveData
    {
        public int Seed;
        public bool IsSuperFlat;
        public SerializableVector3Int SpawnPosition;
        public float GameTime;
        public List<ChunkData> Chunks;
        public List<PlayerData> Players;

        public SaveData() { }

        public SaveData(int seed, float time, bool flat, Vector3Int _SpawnPosition, Dictionary<Vector3Int, byte[,,]> chunkCache, Dictionary<Vector3Int, byte[,,]> chunkCacheDir, List<PlayerData> players)
        {
            SpawnPosition = new SerializableVector3Int(_SpawnPosition);
            Seed = seed;
            GameTime = time;
            IsSuperFlat = flat;
            Chunks = chunkCache.Select(kvp => new ChunkData(kvp.Key, kvp.Value, chunkCacheDir[kvp.Key])).ToList();
            Players = players;
        }
    }


    public Player GetHost()
    {
        foreach(Player p in GameObject.FindObjectsOfType<Player>())
        {
            if (p.Hosting.Value)
            {
                return p;
            }
        }
        return null;
    }
    [HideInInspector]
    public bool saving;
    public void SaveGame(string filePath)
    {
        saving = true;
        List<PlayerData> players = new List<PlayerData>();
        Player[] PlayerList = GameObject.FindObjectsOfType<Player>();
        foreach (var player in PlayerList)
        {
            string playerName = player.gameObject.name;
            Vector3 playerPosition = (player.transform.position);
            players.Add(new PlayerData(playerName, playerPosition, player.transform.eulerAngles.y));
        }


        foreach (PlayerData pd in LoadedPlayerData)
        {
            bool exists = false;
            foreach (var player in PlayerList)
            {
                if (pd.Name == player.gameObject.name)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                players.Add(pd);
            }
        }

        Debug.Log(players.Count);
        // Create SaveData object
        SaveData saveData = new SaveData(seed, GameTime, IsSuperFlat, Vector3Int.FloorToInt(SpawnPosition), chunkCache, chunkCacheDirection, players);



        // Serialize the SaveData object to binary
        byte[] saveDataBytes;
        using (MemoryStream memoryStream = new MemoryStream())
        {
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(memoryStream, saveData);
            saveDataBytes = memoryStream.ToArray();
        }

        // Write the byte array to the file
        string directoryPath = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllBytes(filePath, saveDataBytes);

        if (GetHost())
        {
            GetHost().BlockPlace.BufferedBlockEvents.Clear();
        }
        Debug.Log("Game saved successfully!");
        saving = false;
    }


    public void LoadGameFromBytes(byte[] saveDataBytes, bool local)
    {
        if (saveDataBytes != null && saveDataBytes.Length > 0)
        {

            SaveData saveData;
            using (MemoryStream memoryStream = new MemoryStream(saveDataBytes))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                saveData = (SaveData)formatter.Deserialize(memoryStream);
            }

            LoadGameWithSaveData(saveData, false);
        }
        else
        {
            Debug.LogError("Invalid save data byte array!");
        }
    }


    public void LoadGame(string filePath)
    {
        if (File.Exists(filePath))
        {
            BinaryFormatter formatter = new BinaryFormatter();
            FileStream fileStream = File.Open(filePath, FileMode.Open);
            SaveData saveData = (SaveData)formatter.Deserialize(fileStream);

            fileStream.Close();

            LoadGameWithSaveData(saveData, true);
        }
    }

    public void ClearChunks()
    {
        foreach (var chunkPosition in activeChunksObj.Keys.ToList())
        {
            Destroy(activeChunksObj[chunkPosition]);
        }
        activeChunks.Clear();
        activeChunksObj.Clear();
    }

    void LoadGameWithSaveData(SaveData saveData, bool local)
    {
        // Restore game state from SaveData object
        seed = saveData.Seed;
        IsSuperFlat = saveData.IsSuperFlat;

        if (local)
        {
            if (IsHost)
            {
                GetHost().GameTime.Value = saveData.GameTime;
            }
        }
        else
        {
            GameTime = GetHost().GameTime.Value;
        }

        // Clear current chunks
        ClearChunks();

        // Load chunk data
        chunkCache = saveData.Chunks.ToDictionary(
            chunk => chunk.Position.ToVector3Int(),
            chunk => ConvertTo3DArray(chunk.Data, ChunkSize, ChunkSize, ChunkSize)
        );
        chunkCacheDirection = saveData.Chunks.ToDictionary(
            chunk => chunk.Position.ToVector3Int(),
            chunk => ConvertTo3DArray(chunk.DataDir, ChunkSize, ChunkSize, ChunkSize)
        );


        // Restore player positions
        foreach (Player player in GameObject.FindObjectsOfType<Player>())
        {
            bool hasPoint = false;
            foreach (PlayerData playerData in saveData.Players)
            {
                if (player.gameObject.name == playerData.Name)
                {
                    player.Teleport(playerData.Position.ToVector3(), playerData.Y, false);
                    hasPoint = true;
                    break;
                }
            }
            if (!hasPoint)
            {
                player.Teleport(saveData.SpawnPosition.ToVector3Int(), 0, true);
            }
        }
        SpawnPosition = saveData.SpawnPosition.ToVector3Int();

        Debug.Log("Game loaded successfully!");
    }





    [Serializable]
    public class Block
    {
        public string Name = "New Block";
        public byte Value = 0;
        public int FrontFace = 0;
        public int BackFace = 0;
        public int LeftFace = 0;
        public int RightFace = 0;
        public int TopFace = 0;
        public int BottomFace = 0;
        public bool Transparent;
        public bool NoCollision;

        [ColorUsage(true, true)]
        public Color Light = Color.black;
    }
    public List<Block> Blocks = new List<Block>();
    private List<PlayerData> LoadedPlayerData = new List<PlayerData> ();
    private bool IsSuperFlat;
    private int seed;
    public GameObject ChunkBorderPrefab;
    public bool ChunkBorders;
    public int renderDistance = 5;
    public int ChunkSize;
    public float TextureSize = 256;
    public float BlockSize = 16;
    public Material mat;
    public Material transparent;
    [HideInInspector]
    public List<byte> NoCollisonBlocks = new List<byte>();
    [HideInInspector]
    public List<byte> TransparentBlocks = new List<byte>();
    public float Daylength = 1;
    private float GameTime = 0;
    public static ChunkManager Instance { get; private set; }
    public Dictionary<Vector3Int, Chunk> activeChunks = new Dictionary<Vector3Int, Chunk>();
    public Dictionary<Vector3Int, GameObject> activeChunksObj = new Dictionary<Vector3Int, GameObject>();
    private Vector3 previousTargetPosition;
    private float previousTargetRotation;
    private Dictionary<Vector3Int, byte[,,]> chunkCache = new Dictionary<Vector3Int, byte[,,]>();
    private Dictionary<Vector3Int, byte[,,]> chunkCacheDirection = new Dictionary<Vector3Int, byte[,,]>();
    public Dictionary<string, byte> BlockList = new Dictionary<string, byte>();
    public Dictionary<byte, Color> BlockListLight = new Dictionary<byte, Color>();
    public Dictionary<byte, int[]> BlockFaces = new Dictionary<byte, int[]>();
    public Light DirectionalLight;
    public Color AmbientColor;
    public float MinimumAmbient;
    public int IgnoreLayer;
    private ConcurrentQueue<Vector3Int> chunksToCreate = new ConcurrentQueue<Vector3Int>();
    public Vector3 SpawnPosition;
    [HideInInspector]
    public byte[] SaveDataBytes;
    public ShareFile ShareFile;
    private float CPUClock;
    float GetCPUClockSpeed()
    {
        float clockSpeed = 0;
        System.Diagnostics.ProcessStartInfo processStartInfo = new System.Diagnostics.ProcessStartInfo("wmic", "cpu get MaxClockSpeed");
        processStartInfo.RedirectStandardOutput = true;
        processStartInfo.UseShellExecute = false;
        processStartInfo.CreateNoWindow = true;

        using (System.Diagnostics.Process process = new System.Diagnostics.Process())
        {
            process.StartInfo = processStartInfo;
            process.Start();
            process.WaitForExit();

            string output = process.StandardOutput.ReadToEnd();
            clockSpeed = ParseClockSpeed(output);
        }

        return clockSpeed;
    }
    float ParseClockSpeed(string output)
    {
        string[] lines = output.Trim().Split('\n');
        if (lines.Length > 1)
        {
            if (float.TryParse(lines[1].Trim(), out float clockSpeed))
            {
                return clockSpeed;
            }
        }
        return 3200f;
    }


    private void Awake()
    {
        CPUClock = 100000 / GetCPUClockSpeed();
        Debug.Log(CPUClock);
        int index = 0;
        foreach (var block in Blocks)
        {
            block.Value = (byte)index; index++;
            if (block.Transparent)
            {
                TransparentBlocks.Add(block.Value);
            }
            if (block.NoCollision)
            {
                NoCollisonBlocks.Add(block.Value);
            }

            BlockList.Add(block.Name, block.Value);
            BlockListLight.Add(block.Value, block.Light);
            BlockFaces.Add(block.Value, new int[] { block.FrontFace, block.BackFace, block.LeftFace, block.RightFace, block.TopFace, block.BottomFace });
        }
    }

    async public void StartGenerating()
    {

        seed = PlayerPrefs.GetInt("Seed", System.Guid.NewGuid().GetHashCode());
        IsSuperFlat = PlayerPrefs.GetInt("Superflat", 0) == 1;

        previousTargetPosition = Camera.main.transform.position;
        previousTargetRotation = Mathf.Round(Camera.main.transform.rotation.eulerAngles.y);


        if (IsHost)
        {
            if (PlayerPrefs.GetInt("LoadingMode", 0) == 1)
            {
                LoadGame(Application.dataPath + "/../" + "Saves/" + PlayerPrefs.GetString("WorldName") + ".dat");
            }
            else
            {
                Vector3 SpawnPos = await GetFirstLandPosition(IsSuperFlat);
                Player p = GameObject.FindObjectOfType<Player>();
                p.Teleport(SpawnPos, 0, true);
                SpawnPosition = SpawnPos;
                SaveGame(Application.dataPath + "/../" + "Saves/" + PlayerPrefs.GetString("WorldName") + ".dat");
            }
            InvokeRepeating("SaveGameUpdate", 60 * 5, 60 * 5);
        }
        else
        {
            string WorldName = NetworkMenu.instance.currentLobby.Value.GetData("WorldName");
            LoadGame(Application.dataPath + "/../" + "SaveCache/" + WorldName + ".dat");
        }

        InvokeRepeating("GenerateChunkUpdate", 0, .1f);
    }

    void SaveGameUpdate()
    {
        SaveGame(Application.dataPath + "/../" + "Saves/" + PlayerPrefs.GetString("WorldName") + ".dat");
    }

    public void SyncSave(ulong clientID)
    {
        ShareFile.SendFile(Application.dataPath + "/../" + "Saves/" + PlayerPrefs.GetString("WorldName") + ".dat", "/../" + "SaveCache/", clientID);
    }

    private byte[,,] ConvertTo3DArray(List<byte> data, int sizeX, int sizeY, int sizeZ)
    {
        byte[,,] result = new byte[sizeX, sizeY, sizeZ];
        try
        {
            int index = 0;
            for (int x = 0; x < sizeX; x++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        result[x, y, z] = data[index++];
                    }
                }
            }
        }
        catch
        {

        }
        return result;
    }



    bool cancelledGeneration = false;
    async void GenerateChunkUpdate()
    {
        Vector3 targetPosition = Camera.main.transform.position;
        await Task.Run(async () =>
        {

            Vector3 halfChunkSize = new Vector3(ChunkSize / 2f, ChunkSize / 2f, ChunkSize / 2f);
            float distSquared = renderDistance * ChunkSize * renderDistance * ChunkSize;

            if (generatingChunks)
                return;

            generatingChunks = true;


            Vector3Int targetChunkPos = new Vector3Int(
                Mathf.FloorToInt(targetPosition.x / ChunkSize),
                Mathf.FloorToInt(targetPosition.y / ChunkSize),
                Mathf.FloorToInt(targetPosition.z / ChunkSize)
            );

            ConcurrentBag<Vector3Int> chunkPositionsBag = new ConcurrentBag<Vector3Int>();

            Parallel.For(-renderDistance, renderDistance + 1, x =>
            {
                for (int y = -renderDistance; y <= renderDistance; y++)
                {
                    for (int z = -renderDistance; z <= renderDistance; z++)
                    {
                        Vector3Int chunkPosition = new Vector3Int(
                            targetChunkPos.x + x,
                            targetChunkPos.y + y,
                            targetChunkPos.z + z
                        );

                        Vector3 chunkCenter = chunkPosition * ChunkSize + halfChunkSize;
                        float distance = (chunkCenter - targetPosition).sqrMagnitude;

                        if (distance < distSquared && !activeChunks.ContainsKey(chunkPosition))
                        {
                            chunkPositionsBag.Add(chunkPosition);
                        }
                    }
                }
            });

            List<Vector3Int> sortedChunkPositions = chunkPositionsBag.ToList();
            sortedChunkPositions.Sort((a, b) =>
            {
                float distanceA = Vector3.Distance(targetPosition, a * ChunkSize);
                float distanceB = Vector3.Distance(targetPosition, b * ChunkSize);
                return distanceA.CompareTo(distanceB);
            });

            foreach (Vector3Int chunkPosition in sortedChunkPositions)
            {
                await Task.Delay(Mathf.RoundToInt(CPUClock));
                if (cancelledGeneration)
                {
                    cancelledGeneration = false;
                    break;
                }
                chunksToCreate.Enqueue(chunkPosition);
            }

            generatingChunks = false;
            await Task.CompletedTask;


        });


    }


 
    void Update()
    {


        if (Vector3.Distance(Camera.main.transform.position, previousTargetPosition) > ChunkSize || previousTargetRotation != Mathf.Round(Camera.main.transform.rotation.eulerAngles.y))
        {
            previousTargetPosition = Camera.main.transform.position;
            previousTargetRotation = Mathf.Round(Camera.main.transform.rotation.eulerAngles.y);
            cancelledGeneration = true;
        }

       
        if (IsHost)
        {
            GameTime = GetHost().GameTime.Value + (Time.deltaTime * Daylength);
            GetHost().GameTime.Value = GameTime;
        }
        else
        {
            GameTime = GetHost().GameTime.Value;
        }
        float SkyValue = (GameTime) % 360;
        DirectionalLight.transform.rotation = Quaternion.Euler(SkyValue, 45, 0);

        DirectionalLight.intensity = 2 * Vector3.Dot(DirectionalLight.transform.forward, -Vector3.up);
        RenderSettings.ambientLight = AmbientColor * Mathf.Clamp(Vector3.Dot(DirectionalLight.transform.forward, -Vector3.up), MinimumAmbient, 1);


        Instance = this;


        while (chunksToCreate.TryDequeue(out Vector3Int chunkPosition))
        {
            CreateChunk(chunkPosition);
        }

        UpdateChunks();

    }


    private bool generatingChunks = false;



    private void CreateChunk(Vector3Int chunkPosition)
    {
        if (activeChunks.ContainsKey(chunkPosition))
            return;

        GameObject newChunk = new GameObject
        {
            name = $"{chunkPosition.x}_{chunkPosition.y}_{chunkPosition.z}",
            transform = { position = chunkPosition * ChunkSize, parent = transform },
        };


        if (ChunkBorders)
        {
            GameObject border = Instantiate(ChunkBorderPrefab, newChunk.transform);
            border.transform.localScale = Vector3.one * ChunkSize;
        }

        Chunk chunk = newChunk.AddComponent<Chunk>();
        chunk.Init(mat, transparent, TransparentBlocks.ToArray(), NoCollisonBlocks.ToArray(), ChunkSize, TextureSize, BlockSize, chunkPosition);

        activeChunks.Add(chunkPosition, chunk);
        activeChunksObj.Add(chunkPosition, newChunk);

        byte[,,] newData;
        byte[,,] newDataDir;
        if (!chunkCache.ContainsKey(chunkPosition))
        {
            newData = SetTerrain(chunk, IsSuperFlat);
        }
        else
        {
            newData = chunkCache[chunkPosition];
        }
        if (!chunkCacheDirection.ContainsKey(chunkPosition))
        {
            newDataDir = new byte[ChunkSize, ChunkSize, ChunkSize];
        }
        else
        {
            newDataDir = chunkCacheDirection[chunkPosition];
        }

        chunk.SetDirectionData(newDataDir);
        chunk.SetData(newData);

        if (voxelDataForNonExistentChunks.ContainsKey(chunkPosition))
        {
            foreach (var voxelData in voxelDataForNonExistentChunks[chunkPosition])
            {
                chunk.SetVoxel(voxelData.Item1.x, voxelData.Item1.y, voxelData.Item1.z, voxelData.Item3);
                chunk.SetDirection(voxelData.Item1.x, voxelData.Item1.y, voxelData.Item1.z, voxelData.Item2);
            }
            voxelDataForNonExistentChunks.Remove(chunkPosition);
        }

        chunk.GenerateTerrain();
    }





    public float waterLevel = 10;
    public float height = 25;
    public float scale = 0.01f;
    public int worldFloor = -100;
    public int superflatHeight = 3;

    float Errosion(Vector3 position, float scale)
    {
        float initialOffset = seed / 1000f;
        float frequency = scale;

        float sampleX = ((position.x) + initialOffset) * frequency;
        float sampleY = ((position.z) + initialOffset) * frequency;


        float perlinValue = (Mathf.PerlinNoise(sampleX, sampleY) - 0.5f) * 2;

        return perlinValue;
    }

    float Continentalness(Vector3 position, float scale, int octaves, float persistence, float lacunarity)
    {
        float initialOffset = seed / 1000f;

        float amplitude = 1f;
        float frequency = scale;
        float continentalness = 0f;

        for (int octave = 0; octave < octaves; octave++)
        {
            // Convert position to float and adjust based on scale and frequency
            float sampleX = ((position.x) + initialOffset) * frequency;
            float sampleY = ((position.z) + initialOffset) * frequency;

            // Sample the Perlin noise
            float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2f - 1f;
            continentalness += perlinValue * amplitude;

            // Update amplitude and frequency for the next octave
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        // Normalize the result to range between 0 and 1
        continentalness = Mathf.Clamp01((continentalness + 1f) / 2f);
        continentalness = terrainCurve.Evaluate(continentalness);

        return continentalness;
    }


    async Task<Vector3> GetFirstLandPosition(bool superflat)
    {

        if (superflat)
        {
            int flatHeight = worldFloor + superflatHeight;
            return new Vector3(0, flatHeight + 2, 0);
        }

        Vector3 FinalPos = Vector3.zero;

        await Task.Run(async () =>
        {
            for (int x = 0; x < Mathf.Infinity; x++) 
            {
                Vector3 voxelPosition2D = new Vector3(x, 0, 0);
                float _Continentalness = Continentalness(voxelPosition2D, scale, 4, 0.5f, 2);
                float _Errosion = Errosion(voxelPosition2D, scale / 5);

                for (int y = 0; y < ChunkSize; y++)
                {
                    float perlinRaw = (_Continentalness * height) * _Errosion;
                    float perlinValue = perlinRaw;
                    int perlinRounded = (Mathf.RoundToInt(perlinValue));

                    if (y == perlinRounded && y > waterLevel)
                    {
                        FinalPos = new Vector3(x, perlinRounded + 2, 0);
                        return; 
                    }
                }

                await Task.Delay(1); 
            }
        });

        return FinalPos;
    }




    byte[,,] SetTerrain(Chunk chunk, bool superflat)
    {
        System.Random random = new System.Random(seed + (int)chunk.transform.position.x
            + (int)chunk.transform.position.y + (int)chunk.transform.position.z);

        byte[,,] VoxelData = new byte[ChunkSize, ChunkSize, ChunkSize];
        Vector3 chunkCornerWorldPos = chunk.transform.position;

        int flatHeight = worldFloor + superflatHeight;
        if (superflat)
        {
            waterLevel = worldFloor;
        }

        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                if (superflat)
                {

                    for (int y = 0; y < ChunkSize; y++)
                    {
                        byte voxelValue = 0;
                        Vector3 voxelPosition = chunkCornerWorldPos + new Vector3(x, y, z);

                        if (voxelPosition.y == worldFloor)
                        {
                            voxelValue = BlockList["Bedrock"];
                        }
                        else if (voxelPosition.y < flatHeight && voxelPosition.y > worldFloor)
                        {
                            voxelValue = BlockList["Dirt"];
                        }
                        else if (voxelPosition.y == flatHeight)
                        {
                            voxelValue = BlockList["Grass"];
                        }

                        VoxelData[x, y, z] = voxelValue;
                    }
                }
                else
                {
                    Vector3 voxelPosition2D = chunkCornerWorldPos + new Vector3(x, 0, z);
                    float _Continentalness = Continentalness(voxelPosition2D, scale, 4, 0.5f, 2);
                    float _Errosion = Errosion(voxelPosition2D, scale / 5);

                    float perlinRaw = (_Continentalness * height) * _Errosion;

                    Parallel.For(0, ChunkSize, y =>
                    {
                        Vector3 voxelPosition = chunkCornerWorldPos + new Vector3(x, y, z);

                        byte voxelValue = 0;

                        float perlinValue = perlinRaw;

                        int perlinRounded = (Mathf.RoundToInt(perlinValue));
                        if (voxelPosition.y >= worldFloor)
                        {
                            if (voxelPosition.y > perlinRounded && voxelPosition.y <= waterLevel)
                            {
                                voxelValue = BlockList["Water"];
                            }
                            else
                            {
                                if (voxelPosition.y == perlinRounded)
                                {
                                    if (voxelPosition.y == waterLevel)
                                    {
                                        voxelValue = BlockList["Sand"];
                                    }
                                    else
                                    {
                                        if (voxelPosition.y < waterLevel)
                                        {
                                            voxelValue = BlockList["Sand"];
                                        }
                                        else
                                        {
                                            voxelValue = BlockList["Grass"]; //grass
                                        }
                                    }
                                }
                                else
                                {
                                    if (voxelPosition.y < perlinRounded)
                                    {
                                        voxelValue = BlockList["Dirt"]; //dirt
                                    }
                                    if (voxelPosition.y < perlinRounded - 4)
                                    {
                                        voxelValue = BlockList["Stone"]; //stone
                                    }
                                }
                            }
                        }
                        if (voxelPosition.y == worldFloor)
                        {
                            voxelValue = BlockList["Bedrock"];
                        }

                        VoxelData[x, y, z] = voxelValue;
                    });
                }
            }
        }

        if (!superflat)
        {
            Task.Run(() =>
            {
                for (int x = 0; x < ChunkSize; x++)
                {
                    for (int y = 0; y < ChunkSize; y++)
                    {
                        for (int z = 0; z < ChunkSize; z++)
                        {
                            if (VoxelData[x, y, z] == BlockList["Grass"])
                            {
                                if (y + 10 < ChunkSize && x + 2 < ChunkSize && z + 2 < ChunkSize
                                    && x - 2 > 0 && z - 2 > 0
                                    && random.NextDouble() > .975f && !HasWoodNeighbor(VoxelData, x, y, z, 3))
                                {
                                    for (int i = y + 1; i < y + 4; i++)
                                    {
                                        if (VoxelData[x, i, z] == 0)
                                        {
                                            VoxelData[x, i, z] = BlockList["Oak Log"];
                                        }
                                    }
                                    for (int offsetY = y + 4; offsetY < y + 8; offsetY++)
                                    {
                                        for (int offsetX = -2; offsetX <= 2; offsetX++)
                                        {
                                            for (int offsetZ = -2; offsetZ <= 2; offsetZ++)
                                            {
                                                if (VoxelData[x + offsetX, offsetY, z + offsetZ] == 0)
                                                {
                                                    VoxelData[x + offsetX, offsetY, z + offsetZ] = BlockList["Leaves"];
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }

        return VoxelData;
    }



    bool HasWoodNeighbor(byte[,,] voxelData, int x, int y, int z, int radius)
    {
        // Check neighboring blocks within a 3x3x3 cube
        for (int offsetX = -radius; offsetX <= radius; offsetX++)
        {
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetZ = -radius; offsetZ <= radius; offsetZ++)
                {
                    int neighborX = x + offsetX;
                    int neighborY = y + offsetY;
                    int neighborZ = z + offsetZ;

                    // Ensure neighbor is within bounds
                    if (neighborX >= 0 && neighborX < ChunkSize &&
                        neighborY >= 0 && neighborY < ChunkSize &&
                        neighborZ >= 0 && neighborZ < ChunkSize)
                    {
                        // Check if the neighbor is a wood block
                        if (voxelData[neighborX, neighborY, neighborZ] == 7) // Assuming 7 is the ID for wood blocks
                        {
                            return true; // Found a wood neighbor
                        }
                    }
                }
            }
        }
        return false; // No wood neighbors found
    }



    public float Perlin3D(float x, float y, float z, float scale)
    {
        x *= scale;
        y *= scale;
        z *= scale;
        float xy = Mathf.PerlinNoise(x, y);
        float xz = Mathf.PerlinNoise(x, z);
        float yz = Mathf.PerlinNoise(y, z);
        float yx = Mathf.PerlinNoise(y, x);
        float zx = Mathf.PerlinNoise(z, x);
        float zy = Mathf.PerlinNoise(z, y);

        return (xy + xz + yz + yx + zx + zy) / 6;

    }











    private Dictionary<Vector3Int, List<(Vector3Int, byte, byte)>> voxelDataForNonExistentChunks = new Dictionary<Vector3Int, List<(Vector3Int, byte, byte)>>();

    public void SetVoxelAtWorldPosition(Vector3 worldPosition, Vector3 worldNormal, byte voxelValue, bool regenerate, bool useCache)
    {

        const byte DEFAULT = 0;
        const byte NORTH = 1;
        const byte EAST = 2;
        const byte SOUTH = 3;
        const byte WEST = 4;

        byte FinalRotation = DEFAULT;

        float northAngle = Vector3.Angle(worldNormal, Vector3.forward);
        float eastAngle = Vector3.Angle(worldNormal, Vector3.right);
        float southAngle = Vector3.Angle(worldNormal, Vector3.back);
        float westAngle = Vector3.Angle(worldNormal, Vector3.left);

        float minAngle = Mathf.Min(northAngle, eastAngle, southAngle, westAngle);


        if (minAngle == northAngle)
        {
            FinalRotation = NORTH;
        }
        else if (minAngle == eastAngle)
        {
            FinalRotation = EAST;
        }
        else if (minAngle == southAngle)
        {
            FinalRotation = SOUTH;
        }
        else if (minAngle == westAngle)
        {
            FinalRotation = WEST;
        }



        Vector3Int chunkPosition = new Vector3Int(
            Mathf.FloorToInt(worldPosition.x / ChunkSize),
            Mathf.FloorToInt(worldPosition.y / ChunkSize),
            Mathf.FloorToInt(worldPosition.z / ChunkSize)
        );

        int localX = Mathf.FloorToInt(worldPosition.x) % ChunkSize;
        int localY = Mathf.FloorToInt(worldPosition.y) % ChunkSize;
        int localZ = Mathf.FloorToInt(worldPosition.z) % ChunkSize;

        // Ensure local position is non-negative
        localX = (localX + ChunkSize) % ChunkSize;
        localY = (localY + ChunkSize) % ChunkSize;
        localZ = (localZ + ChunkSize) % ChunkSize;

        Vector3Int localVoxelPosition = new Vector3Int(localX, localY, localZ);

        if (activeChunks.ContainsKey(chunkPosition))
        {
            Chunk chunk = activeChunks[chunkPosition];

            chunk.SetVoxel(localVoxelPosition.x, localVoxelPosition.y, localVoxelPosition.z, voxelValue);
            chunk.SetDirection(localVoxelPosition.x, localVoxelPosition.y, localVoxelPosition.z, FinalRotation);

            if (regenerate)
            {
                chunk.GenerateTerrain();
            }

            if (useCache)
            {
                chunkCacheDirection[chunkPosition] = chunk.GetDirectionData();
                chunkCache[chunkPosition] = chunk.GetData();
            }
        }
        else
        {
            if (!voxelDataForNonExistentChunks.ContainsKey(chunkPosition))
            {
                voxelDataForNonExistentChunks[chunkPosition] = new List<(Vector3Int, byte, byte)>();
            }
            voxelDataForNonExistentChunks[chunkPosition].Add((localVoxelPosition, FinalRotation, voxelValue));
        }
    }

    public byte GetVoxelPosition(Vector3 worldPosition)
    {
        Vector3Int chunkPosition = new Vector3Int(
            Mathf.FloorToInt(worldPosition.x / ChunkSize),
            Mathf.FloorToInt(worldPosition.y / ChunkSize),
            Mathf.FloorToInt(worldPosition.z / ChunkSize)
        );


        if (activeChunks.ContainsKey(chunkPosition))
        {
            Chunk chunk = activeChunks[chunkPosition];
            Vector3 localPosition = chunk.transform.InverseTransformPoint(worldPosition);

            int voxelX = Mathf.FloorToInt(localPosition.x);
            int voxelY = Mathf.FloorToInt(localPosition.y);
            int voxelZ = Mathf.FloorToInt(localPosition.z);

    

            return chunk.GetData()[voxelX, voxelY, voxelZ];
        }
        return 0;
    }


    public Chunk GetChunk(Vector3 worldPosition)
    {
        Vector3Int chunkPosition = new Vector3Int(
            Mathf.FloorToInt(worldPosition.x / ChunkSize),
            Mathf.FloorToInt(worldPosition.y / ChunkSize),
            Mathf.FloorToInt(worldPosition.z / ChunkSize)
        );


        if (activeChunks.ContainsKey(chunkPosition))
        {
            Chunk chunk = activeChunks[chunkPosition];
            return chunk;
        }
        return null;
    }





    public Chunk GetNeighboringChunk(Vector3Int currentChunkPosition, int dx, int dy, int dz)
    {
        Vector3Int neighboringChunkPosition = currentChunkPosition + new Vector3Int(dx, dy, dz);

        try
        {
            return activeChunks[neighboringChunkPosition];
        }
        catch
        {
            return null;
        }
    }


    void UpdateChunks()
    {

        var chunkKeys = new List<Vector3Int>(activeChunksObj.Keys);


        var chunksToRemove = new ConcurrentBag<Vector3Int>();

        Vector3 TargetPos = Camera.main.transform.position;

        // Use Parallel.For to iterate over the chunks
        Parallel.For(0, chunkKeys.Count, i =>
        {
            Vector3Int chunkPosition = chunkKeys[i];
            GameObject chunkObj = activeChunksObj[chunkPosition];
            float distance = Vector3.Distance(chunkPosition * ChunkSize, TargetPos);

            if (distance > renderDistance * ChunkSize)
            {
                chunksToRemove.Add(chunkPosition);
            }
        });
        List<Vector3Int> chunksToRemoveList = new List<Vector3Int>(chunksToRemove);

        foreach (var chunkPosition in chunksToRemoveList)
        {
            Destroy(activeChunksObj[chunkPosition].gameObject);
            activeChunks.Remove(chunkPosition);
            activeChunksObj.Remove(chunkPosition);
        }
    }

}
