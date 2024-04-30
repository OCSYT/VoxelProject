using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using static UnityEditor.Searcher.SearcherWindow.Alignment;
using static UnityEditor.ShaderGraph.Internal.KeywordDependentCollection;


public class Chunk : MonoBehaviour
{
    private int ChunkSize = 0;
    private byte[,,] VoxelData;
    private byte[,,] LightDataR;
    private byte[,,] LightDataG;
    private byte[,,] LightDataB;
    private byte[,,] LightDataSun;
    private byte[] TransparentIDs;
    private byte[] NoCollisionIDs;
    private byte[] NoCollisionTransparentIDs;
    private MeshFilter _MeshFilter;
    private MeshRenderer _MeshRenderer;
    private MeshCollider _MeshCollider;
    private Mesh _Mesh;
    private MeshFilter _MeshFilterTransparent;
    private MeshRenderer _MeshRendererTransparent;
    private MeshCollider _MeshColliderTransparent;
    private Mesh _MeshTransparent;
    private MeshFilter _MeshFilterNoCollision;
    private MeshRenderer _MeshRendererNoCollision;
    private MeshCollider _MeshColliderNoCollision;
    private Mesh _MeshNoCollision;
    private MeshFilter _MeshFilterNoCollisionTransparent;
    private MeshRenderer _MeshRendererNoCollisionTransparent;
    private MeshCollider _MeshColliderNoCollisionTransparent;
    private Mesh _MeshNoCollisionTransparent;
    private Material _Material;
    private float TextureSize = 256;
    private float BlockSize = 16;
    private Vector3Int currentPos;
    private Transform Main;
    private Transform TransparentObj;
    private Transform NoCollisionObj;
    private Transform NoCollisionObjTransparent;
    private Coroutine LightingCoroutine;
    private bool LightingGenerating;
    private int currentSunLevel;
    void Start()
    {
        currentSunLevel = ChunkManager.Instance.SkyIntensity;
        InvokeRepeating("Tick", 0, 1);
    }

    void Tick()
    {
        if(ChunkManager.Instance.SkyIntensity != currentSunLevel)
        {
            currentSunLevel = ChunkManager.Instance.SkyIntensity;
            GenerateLighting(true, false);
        }
    }

    public void Init(Material mat,Material transparentMat, byte[] transparent, byte[] nocollision, int _ChunkSize, float _TextureSize, float _BlockSize, Vector3Int Position)
    {
        currentPos = Position;
        VoxelData = new byte[_ChunkSize, _ChunkSize, _ChunkSize];
        LightDataR = new byte[_ChunkSize, _ChunkSize, _ChunkSize];
        LightDataG = new byte[_ChunkSize, _ChunkSize, _ChunkSize];
        LightDataB = new byte[_ChunkSize, _ChunkSize, _ChunkSize];
        LightDataSun = new byte[_ChunkSize, _ChunkSize, _ChunkSize];

        TextureSize = _TextureSize;
        BlockSize = _BlockSize;
        ChunkSize = _ChunkSize;
        TransparentIDs = transparent;
        NoCollisionTransparentIDs = transparent.Concat(nocollision).ToArray();

        NoCollisionIDs = nocollision;

        Main = new GameObject("Main").transform;
        Main.transform.parent = transform;
        Main.transform.localPosition = Vector3.zero;
        _MeshFilter = Main.transform.AddComponent<MeshFilter>();
        _MeshRenderer = Main.transform.AddComponent<MeshRenderer>();
        _MeshRenderer.sharedMaterial = mat;
        _Mesh = new Mesh();
        _MeshFilter.mesh = _Mesh;

        TransparentObj = new GameObject("Transparent").transform;
        TransparentObj.transform.parent = transform;
        TransparentObj.transform.localPosition = Vector3.zero;
        _MeshFilterTransparent = TransparentObj.transform.AddComponent<MeshFilter>();
        _MeshRendererTransparent = TransparentObj.transform.AddComponent<MeshRenderer>();
        _MeshRendererTransparent.sharedMaterial = transparentMat;
        _MeshTransparent = new Mesh();
        _MeshFilterTransparent.mesh = _MeshTransparent;

        NoCollisionObj = new GameObject("NoCollision").transform;
        NoCollisionObj.transform.parent = transform;
        NoCollisionObj.transform.localPosition = Vector3.zero;
        _MeshFilterNoCollision = NoCollisionObj.transform.AddComponent<MeshFilter>();
        _MeshRendererNoCollision = NoCollisionObj.transform.AddComponent<MeshRenderer>();
        _MeshRendererNoCollision.sharedMaterial = mat;
        _MeshNoCollision = new Mesh();
        _MeshFilterNoCollision .mesh = _MeshNoCollision;

        NoCollisionObjTransparent = new GameObject("NoCollisionTransparent").transform;
        NoCollisionObjTransparent.transform.parent = transform;
        NoCollisionObjTransparent.transform.localPosition = Vector3.zero;
        _MeshFilterNoCollisionTransparent = NoCollisionObjTransparent.transform.AddComponent<MeshFilter>();
        _MeshRendererNoCollisionTransparent = NoCollisionObjTransparent.transform.AddComponent<MeshRenderer>();
        _MeshRendererNoCollisionTransparent.sharedMaterial = mat;
        _MeshNoCollisionTransparent = new Mesh();
        _MeshFilterNoCollisionTransparent.mesh = _MeshNoCollisionTransparent;

    }



    public void GenerateTerrain()
    {
        GenerateMesh(true);
    }



    public byte[,,] GetData()
    {
        return VoxelData;
    }
    public byte[,,] SetData(byte[,,] Data)
    {
        VoxelData = Data;
        return VoxelData;
    }

    public byte[,,] GetLightDataR()
    {
        return LightDataR;
    }
    public byte[,,] GetLightDataG()
    {
        return LightDataG;
    }
    public byte[,,] GetLightDataB()
    {
        return LightDataB;
    }

    public byte[,,] GetLightDataSun()
    {
        return LightDataSun;
    }



    public byte[,,] SetVoxel(int x, int y, int z, byte value)
    {
        if (IsValidPosition(x, y, z))
        {
            VoxelData[x, y, z] = value;
        }
        else
        {
            Debug.LogError("Invalid voxel position");
        }
        return VoxelData;
    }

    public void SetLight(int x, int y, int z, int channel, byte value)
    {
        if (IsValidPosition(x, y, z))
        {
            if(channel == 0)
            {
                LightDataR[x, y, z] = value;
            }
            if(channel == 1)
            {
                LightDataG[x, y, z] = value;
            }
            if(channel == 2)
            {
                LightDataB[x, y, z] = value;
            }
        }
        else
        {
            Debug.LogError("Invalid voxel position");
        }
    }


    private bool IsValidPosition(int x, int y, int z)
    {
        return x >= 0 && x < ChunkSize && y >= 0 && y < ChunkSize && z >= 0 && z < ChunkSize;
    }


    private double GetAverage(byte[,,] array)
    {
        int totalSum = 0;
        int totalCount = 0;

        for (int i = 0; i < array.GetLength(0); i++)
        {
            for (int j = 0; j < array.GetLength(1); j++)
            {
                for (int k = 0; k < array.GetLength(2); k++)
                {
                    totalSum += array[i, j, k];
                    totalCount++;
                }
            }
        }

        if (totalCount == 0)
        {
            // Handle division by zero
            return 0;
        }

        return (double)totalSum / totalCount;
    }
    private byte GetMin(byte[,,] array)
    {
        byte min = byte.MaxValue;

        for (int i = 0; i < array.GetLength(0); i++)
        {
            for (int j = 0; j < array.GetLength(1); j++)
            {
                for (int k = 0; k < array.GetLength(2); k++)
                {
                    if (array[i, j, k] < min)
                    {
                        min = array[i, j, k];
                    }
                }
            }
        }

        return min;
    }
    private byte GetMax(byte[,,] array)
    {
        byte max = byte.MinValue;

        for (int i = 0; i < array.GetLength(0); i++)
        {
            for (int j = 0; j < array.GetLength(1); j++)
            {
                for (int k = 0; k < array.GetLength(2); k++)
                {
                    if (array[i, j, k] > max)
                    {
                        max = array[i, j, k];
                    }
                }
            }
        }

        return max;
    }



    private void AddLight(Vector3Int Position, List<Color> Lighting)
    {
        float sunLevel = LightDataSun[Position.x, Position.y, Position.z];
        float lightLevelR = Mathf.Clamp((float)LightDataR[Position.x, Position.y, Position.z] + sunLevel + .1f, 0, 15) / 15;
        float lightLevelG = Mathf.Clamp((float)LightDataG[Position.x, Position.y, Position.z] + sunLevel + .1f, 0, 15) / 15;
        float lightLevelB = Mathf.Clamp((float)LightDataB[Position.x, Position.y, Position.z] + sunLevel + .1f,0, 15) / 15;

        Lighting.Add(new Color(lightLevelR, lightLevelG, lightLevelB));
        Lighting.Add(new Color(lightLevelR, lightLevelG, lightLevelB));
        Lighting.Add(new Color(lightLevelR, lightLevelG, lightLevelB));
        Lighting.Add(new Color(lightLevelR, lightLevelG, lightLevelB));
    }

    public async void GenerateLighting(bool generateSun, bool generatePoint)
    {

        if (LightingGenerating || GetMax(VoxelData) == 0) return;
        LightingGenerating = true;

        List<Color> Lighting = new List<Color>();
        List<Color> LightingTransparent = new List<Color>();
        List<Color> LightingNoCollision = new List<Color>();
        List<Color> LightingNoCollisionTransparent = new List<Color>();

        // Define the six directions as offsets from the current voxel position
        Vector3Int[] directions = new Vector3Int[]
        {
                    new Vector3Int(-1, 0, 0),   // Left
                    new Vector3Int(1, 0, 0),    // Right
                    new Vector3Int(0, -1, 0),   // Bottom
                    new Vector3Int(0, 1, 0),    // Top
                    new Vector3Int(0, 0, -1),   // Back
                    new Vector3Int(0, 0, 1)     // Front
        };


        await CalculateChunkLighting(this, generateSun, generatePoint);
        await Task.Run(async () =>
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        if (VoxelData[x, y, z] == 0)
                            continue;

                        Vector3Int voxelPos = new Vector3Int(x, y, z);


                        for(int i = 0; i < directions.Length; i++)
                        {
                            Vector3Int direction = directions[i];
                            int dx = direction.x;
                            int dy = direction.y;
                            int dz = direction.z;

                            if (IsFaceVisible(x, y, z, dx, dy, dz))
                            {
                                bool isTransparent = ReturnType(VoxelData[x, y, z], TransparentIDs);
                                bool hasNoCollision = ReturnType(VoxelData[x, y, z], NoCollisionIDs);
                                bool isMain = !isTransparent && !hasNoCollision;
                                bool isTransparentNoCollision = isTransparent && hasNoCollision;




                                if (isMain)
                                {
                                    AddLight(voxelPos, Lighting);
                                }
                                else if (isTransparent)
                                {
                                    AddLight(voxelPos, LightingTransparent);
                                }
                                else if (hasNoCollision)
                                {
                                    AddLight(voxelPos, LightingNoCollision);
                                }
                                else if (isTransparentNoCollision)
                                {
                                    AddLight(voxelPos, LightingNoCollisionTransparent);
                                }
                            }
                        }
                    }
                }
            }
            await Task.CompletedTask;
        });



        // Store lighting arrays and meshes in variables
        Color[] mainColors = Lighting.ToArray();
        Color[] transparentColors = LightingTransparent.ToArray();
        Color[] noCollisionColors = LightingNoCollision.ToArray();
        Color[] noCollisionTransparentColors = LightingNoCollisionTransparent.ToArray();

        // Apply colors if the lengths match and neither vertices nor colors is empty
        if (Main && mainColors.Length == _Mesh.vertices.Length && mainColors.Length != 0 && _Mesh.vertices.Length != 0)
        {
            _Mesh.colors = (mainColors);
        }

        if (TransparentObj && transparentColors.Length == _MeshTransparent.vertices.Length && transparentColors.Length != 0 && _MeshTransparent.vertices.Length != 0)
        {
            _MeshTransparent.colors = (transparentColors);
        }

        if (NoCollisionObj && noCollisionColors.Length == _MeshNoCollision.vertices.Length && noCollisionColors.Length != 0 && _MeshNoCollision.vertices.Length != 0)
        {
            _MeshNoCollision.colors = (noCollisionColors);
        }

        if (NoCollisionObjTransparent && noCollisionTransparentColors.Length == _MeshNoCollisionTransparent.vertices.Length && noCollisionTransparentColors.Length != 0 && _MeshNoCollisionTransparent.vertices.Length != 0)
        {
            _MeshNoCollisionTransparent.colors = (noCollisionColors);
        }

        LightingGenerating = false;
    }










    private async Task<(byte[,,] R, byte[,,] G, byte[,,] B)> CalculateChunkLighting(Chunk chunk, bool generateSun, bool generatePoint)
    {
        if (generateSun)
        {
            chunk.LightDataSun = new byte[ChunkSize, ChunkSize, ChunkSize];
            Chunk AboveChunk = ChunkManager.Instance.GetNeighboringChunk(chunk.currentPos, 0, 1, 0);

            SunLighting(AboveChunk, chunk);
        }

        if (generatePoint)
        {
            chunk.LightDataR = new byte[ChunkSize, ChunkSize, ChunkSize];
            chunk.LightDataG = new byte[ChunkSize, ChunkSize, ChunkSize];
            chunk.LightDataB = new byte[ChunkSize, ChunkSize, ChunkSize];
            Chunk[] NeighborChunks = GetNeighboringChunks(true).Item1;

            //CurrentChunkPass
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        if (VoxelData[x, y, z] == 10)
                        {
                            AddLightWithRadius(chunk, (15, 15, 15), x, y, z, 15, .1f, true);
                        }
                        for (int i = 0; i < NeighborChunks.Length; i++)
                        {
                            Chunk neighborChunk = NeighborChunks[i];
                            if (neighborChunk)
                            {
                                Vector3Int offset = new Vector3Int(neighborChunk.currentPos.x - chunk.currentPos.x,
                                                     neighborChunk.currentPos.y - chunk.currentPos.y,
                                                     neighborChunk.currentPos.z - chunk.currentPos.z);
                                if (neighborChunk.GetData()[x, y, z] == 10)
                                {
                                    int currentX = x + offset.x * ChunkSize;
                                    int currentY = y + offset.y * ChunkSize;
                                    int currentZ = z + offset.z * ChunkSize;

                                    AddLightWithRadius(chunk, (15, 15, 15), currentX, currentY, currentZ, 15, .1f, true);
                                }
                            }
                        }
                    }
                }
            }
        }




        await Task.CompletedTask;
        return (chunk.LightDataR, chunk.LightDataG, chunk.LightDataB);
    }

    private void SunLighting(Chunk AboveChunk, Chunk chunk)
    {
        byte[,,] AboveChunkData = new byte[ChunkSize, ChunkSize, ChunkSize];
        byte[,,] AboveLightDataSun = new byte[ChunkSize, ChunkSize, ChunkSize];
        if (AboveChunk)
        {
            AboveChunkData = AboveChunk.GetData();
            AboveLightDataSun = AboveChunk.GetLightDataSun();
        }


        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                bool IsEmpty = true;
                byte LightValueSun = 0;
                if (AboveChunk != null)
                {
                    for (int yAbove = 0; yAbove < ChunkSize; yAbove++)
                    {
                        byte AboveVoxel = AboveChunkData[x, yAbove, z];
                        LightValueSun = AboveLightDataSun[x, yAbove, z];

                        if (!(AboveVoxel == 0 || ReturnType(AboveVoxel, TransparentIDs)))
                        {
                            IsEmpty = false;
                            break;
                        }
                    }
                }
                if (IsEmpty)
                {
                    LightValueSun = (byte)ChunkManager.Instance.SkyIntensity;
                }


                for (int y = ChunkSize - 1; y >= 0; y--)
                {
                    //this is for other chunks to affect this chunk
                    if (IsEmpty)
                    {
                        chunk.LightDataSun[x, y, z] = LightValueSun;
                    }
                    else
                    {
                        byte Voxel = chunk.VoxelData[x, y, z];
                        if (!(Voxel == 0 || ReturnType(Voxel, TransparentIDs)))
                        {
                            LightValueSun = (byte)Mathf.Clamp(LightValueSun - 1, 0, 15);
                            chunk.LightDataSun[x, y, z] = LightValueSun;
                        }
                    }
                }
                if (IsEmpty)
                {
                    for (int y = 0; y < ChunkSize; y++)
                    {
                        byte Voxel = chunk.VoxelData[x, y, z];
                        if (!(Voxel == 0 || ReturnType(Voxel, TransparentIDs)))
                        {
                            for (int voxely = y; voxely >= 0; voxely--)
                            {
                                chunk.LightDataSun[x, voxely, z] = (byte)Mathf.Clamp(chunk.LightDataSun[x, voxely, z] - 1, 0, 15);
                            }
                        }
                    }
                }
            }
        }
    }

    private void AddLightWithRadius(Chunk currentChunk, (byte, byte, byte) rgb, int centerX, int centerY, int centerZ, int radius, float falloff, bool additive)
    {
        int radiusSquared = radius * radius;

        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int z = -radius; z <= radius; z++)
                {
                    int distanceSquared = x * x + y * y + z * z;

                    if (distanceSquared <= radiusSquared)
                    {
                        int voxelX = centerX + x;
                        int voxelY = centerY + y;
                        int voxelZ = centerZ + z;

                        // Check if the voxel position is within the chunk boundaries
                        if (voxelX >= 0 && voxelX < currentChunk.ChunkSize &&
                            voxelY >= 0 && voxelY < currentChunk.ChunkSize &&
                            voxelZ >= 0 && voxelZ < currentChunk.ChunkSize)
                        {

                            if (currentChunk != null)
                            {
                                // Calculate falloff based on distance
                                float falloffFactor = 1f / (1f + falloff * distanceSquared);

                                // Apply falloff to intensity
                                byte adjustedIntensityR = (byte)(rgb.Item1 * falloffFactor);
                                byte adjustedIntensityG = (byte)(rgb.Item2 * falloffFactor);
                                byte adjustedIntensityB = (byte)(rgb.Item3 * falloffFactor);

                                if (additive)
                                {
                                    adjustedIntensityR += currentChunk.LightDataR[voxelX, voxelY, voxelZ];
                                    adjustedIntensityG += currentChunk.LightDataG[voxelX, voxelY, voxelZ];
                                    adjustedIntensityB += currentChunk.LightDataB[voxelX, voxelY, voxelZ];

                                    adjustedIntensityR = (byte)Mathf.Clamp(adjustedIntensityR, 0, 15);
                                    adjustedIntensityG = (byte)Mathf.Clamp(adjustedIntensityG, 0, 15);
                                    adjustedIntensityB = (byte)Mathf.Clamp(adjustedIntensityB, 0, 15);
                                }

                                currentChunk.LightDataR[voxelX, voxelY, voxelZ] = adjustedIntensityR;
                                currentChunk.LightDataG[voxelX, voxelY, voxelZ] = adjustedIntensityG;
                                currentChunk.LightDataB[voxelX, voxelY, voxelZ] = adjustedIntensityB;
                            }
                        }
                    }
                }
            }
        }
    }














    private bool isGeneratingMesh = false;
    public async void GenerateMesh(bool updateNeighbors)
    {
        if(isGeneratingMesh) return;
        isGeneratingMesh = true;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();


        List<Vector3> verticesTransparent = new List<Vector3>();
        List<int> trianglesTransparent = new List<int>();
        List<Vector2> uvsTransparent = new List<Vector2>();

        List<Vector3> verticesNoCollision = new List<Vector3>();
        List<int> trianglesNoCollision = new List<int>();
        List<Vector2> uvsNoCollision = new List<Vector2>();

        List<Vector3> verticesNoCollisionTransparent = new List<Vector3>();
        List<int> trianglesNoCollisionTransparent = new List<int>();
        List<Vector2> uvsNoCollisionTransparent = new List<Vector2>();





        await Task.Run(async () =>
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        if (VoxelData[x, y, z] == 0)
                            continue;

                        Vector3Int voxelPos = new Vector3Int(x, y, z);

                        // Define the six directions as offsets from the current voxel position
                        Vector3Int[] directions = new Vector3Int[]
                        {
                    new Vector3Int(-1, 0, 0),   // Left
                    new Vector3Int(1, 0, 0),    // Right
                    new Vector3Int(0, -1, 0),   // Bottom
                    new Vector3Int(0, 1, 0),    // Top
                    new Vector3Int(0, 0, -1),   // Back
                    new Vector3Int(0, 0, 1)     // Front
                        };

                        for (int i = 0; i < directions.Length; i++)
                        {
                            Vector3Int direction = directions[i];
                            int dx = direction.x;
                            int dy = direction.y;
                            int dz = direction.z;

                            if (IsFaceVisible(x, y, z, dx, dy, dz))
                            {
                                bool isTransparent = TransparentIDs.Contains(VoxelData[x, y, z]);
                                bool hasNoCollision = NoCollisionIDs.Contains(VoxelData[x, y, z]);
                                bool isMain = !isTransparent && !hasNoCollision;
                                bool isTransparentNoCollision = isTransparent && hasNoCollision;
                                bool Inverted = false;

                                Vector3[] quadVertices = new Vector3[4];
                                bool inverted = false;
                                if (dx == -1) // Left face
                                {
                                    quadVertices[0] = new Vector3(x, y, z);
                                    quadVertices[1] = new Vector3(x, y, z + 1);
                                    quadVertices[2] = new Vector3(x, y + 1, z);
                                    quadVertices[3] = new Vector3(x, y + 1, z + 1);
                                    inverted = true;
                                }
                                else if (dx == 1) // Right face
                                {
                                    quadVertices[0] = new Vector3(x + 1, y, z);
                                    quadVertices[1] = new Vector3(x + 1, y, z + 1);
                                    quadVertices[2] = new Vector3(x + 1, y + 1, z);
                                    quadVertices[3] = new Vector3(x + 1, y + 1, z + 1);
                                }
                                else if (dy == -1) // Bottom face
                                {
                                    quadVertices[0] = new Vector3(x, y, z);
                                    quadVertices[1] = new Vector3(x, y, z + 1);
                                    quadVertices[2] = new Vector3(x + 1, y, z);
                                    quadVertices[3] = new Vector3(x + 1, y, z + 1);
                                }
                                else if (dy == 1) // Top face
                                {
                                    quadVertices[0] = new Vector3(x, y + 1, z);
                                    quadVertices[1] = new Vector3(x, y + 1, z + 1);
                                    quadVertices[2] = new Vector3(x + 1, y + 1, z);
                                    quadVertices[3] = new Vector3(x + 1, y + 1, z + 1);
                                    inverted = true;
                                }
                                else if (dz == -1) // Back face
                                {
                                    quadVertices[0] = new Vector3(x, y, z);
                                    quadVertices[1] = new Vector3(x + 1, y, z);
                                    quadVertices[2] = new Vector3(x, y + 1, z);
                                    quadVertices[3] = new Vector3(x + 1, y + 1, z);
                                }
                                else if (dz == 1) // Front face
                                {
                                    quadVertices[0] = new Vector3(x, y, z + 1);
                                    quadVertices[1] = new Vector3(x + 1, y, z + 1);
                                    quadVertices[2] = new Vector3(x, y + 1, z + 1);
                                    quadVertices[3] = new Vector3(x + 1, y + 1, z + 1);
                                    inverted = true;
                                }
                                if (inverted)
                                {
                                    // Swap all vertices
                                    Vector3 temp = quadVertices[0];
                                    quadVertices[0] = quadVertices[1];
                                    quadVertices[1] = temp;

                                    temp = quadVertices[2];
                                    quadVertices[2] = quadVertices[3];
                                    quadVertices[3] = temp;
                                }




                                if (isMain)
                                {
                                    AddQuad(vertices, triangles, uvs, quadVertices[0], quadVertices[1], quadVertices[2], quadVertices[3], VoxelData[x, y, z], Inverted);
                                }
                                else if (isTransparent)
                                {
                                    AddQuad(verticesTransparent, trianglesTransparent, uvsTransparent,
                                           quadVertices[0], quadVertices[1], quadVertices[2], quadVertices[3], VoxelData[x, y, z], Inverted);
                                }
                                else if (hasNoCollision)
                                {
                                    AddQuad(verticesNoCollision, trianglesNoCollision, uvsNoCollision, quadVertices[0], quadVertices[1], quadVertices[2], quadVertices[3], VoxelData[x, y, z], Inverted);
                                }
                                else if (isTransparentNoCollision)
                                {
                                    AddQuad(verticesNoCollisionTransparent, trianglesNoCollisionTransparent, uvsNoCollisionTransparent, quadVertices[0], quadVertices[1], quadVertices[2], quadVertices[3],
                                           VoxelData[x, y, z], Inverted);
                                }
                            }
                        }
                    }
                }
            }
        });






        if (Main)
        {
            _Mesh.Clear();
            _Mesh.vertices = vertices.ToArray();
            _Mesh.triangles = triangles.ToArray();
            _Mesh.uv = uvs.ToArray();
            _Mesh.colors = null;

            if (_Mesh.triangles.Length > 0)
            {
                if (!_MeshCollider)
                {
                    _MeshCollider = Main.AddComponent<MeshCollider>();
                    _MeshCollider.sharedMesh = _Mesh;
                    _MeshCollider.convex = true;
                    _MeshCollider.convex = false;
                }
                else
                {
                    _MeshCollider.sharedMesh = _Mesh;
                    _MeshCollider.convex = true;
                    _MeshCollider.convex = false;
                }
            }
            else
            {
                if (_MeshCollider)
                {
                    Destroy(_MeshCollider);
                    _MeshCollider = null;
                }
            }
        }

        if (TransparentObj)
        {
            _MeshTransparent.Clear();
            _MeshTransparent.vertices = verticesTransparent.ToArray();
            _MeshTransparent.triangles = trianglesTransparent.ToArray();
            _MeshTransparent.uv = uvsTransparent.ToArray();
            _MeshTransparent.colors = null;

            if (_MeshTransparent.triangles.Length > 0)
            {
                if (!_MeshColliderTransparent)
                {
                    _MeshColliderTransparent = TransparentObj.AddComponent<MeshCollider>();
                    _MeshColliderTransparent.sharedMesh = _MeshTransparent;
                }
                else
                {
                    _MeshColliderTransparent.sharedMesh = _MeshTransparent;
                }
            }
            else
            {
                if (_MeshColliderTransparent)
                {
                    Destroy(_MeshColliderTransparent);
                    _MeshColliderTransparent = null;
                }
            }
        }

        if (NoCollisionObj)
        {
            _MeshNoCollision.Clear();
            _MeshNoCollision.vertices = verticesNoCollision.ToArray();
            _MeshNoCollision.triangles = trianglesNoCollision.ToArray();
            _MeshNoCollision.uv = uvsNoCollision.ToArray();
            _MeshNoCollision.colors = null;

            if (_MeshNoCollision.triangles.Length > 0)
            {
                if (!_MeshColliderNoCollision)
                {
                    _MeshColliderNoCollision = NoCollisionObj.AddComponent<MeshCollider>();
                    _MeshColliderNoCollision.sharedMesh = _MeshNoCollision;
                }
                else
                {
                    _MeshColliderNoCollision.sharedMesh = _MeshNoCollision;
                }
            }
            else
            {
                if (_MeshColliderNoCollision)
                {
                    Destroy(_MeshColliderNoCollision);
                    _MeshColliderNoCollision = null;
                }
            }
        }

        if (NoCollisionObjTransparent)
        {
            _MeshNoCollisionTransparent.Clear();
            _MeshNoCollisionTransparent.vertices = verticesNoCollisionTransparent.ToArray();
            _MeshNoCollisionTransparent.triangles = trianglesNoCollisionTransparent.ToArray();
            _MeshNoCollisionTransparent.uv = uvsNoCollisionTransparent.ToArray();
            _MeshNoCollisionTransparent.colors = null;

            if (_MeshNoCollisionTransparent.triangles.Length > 0)
            {
                if (!_MeshColliderNoCollisionTransparent)
                {
                    _MeshColliderNoCollisionTransparent = NoCollisionObjTransparent.AddComponent<MeshCollider>();
                    _MeshColliderNoCollisionTransparent.sharedMesh = _MeshNoCollisionTransparent;;
                }
                else
                {
                    _MeshColliderNoCollisionTransparent.sharedMesh = _MeshNoCollisionTransparent;
                }
            }
            else
            {
                if (_MeshColliderNoCollisionTransparent)
                {
                    Destroy(_MeshColliderNoCollisionTransparent);
                    _MeshColliderNoCollisionTransparent = null;
                }
            }
        }


        if (updateNeighbors)
        {
            UpdateNeighborChunks();
        }
        GenerateLighting(true, true);

        isGeneratingMesh = false;
    }




    private bool ReturnType(byte id, byte[] idlist)
    {
        for (int i = 0; i < idlist.Length; i++)
        {
            if (id == idlist[i])
            {
                return true;
            }
        }

        return false;
    }


    private bool IsFaceVisible(int x, int y, int z, int dx, int dy, int dz)
    {

        if (x + dx >= 0 && x + dx < ChunkSize &&
            y + dy >= 0 && y + dy < ChunkSize &&
            z + dz >= 0 && z + dz < ChunkSize)
        {
            byte NeighbourBlockID = VoxelData[x + dx, y + dy, z + dz];
           

            bool isTransparent = ReturnType(VoxelData[x, y, z], TransparentIDs);
            bool isNoCollision = ReturnType(VoxelData[x,y, z], NoCollisionIDs);
            bool isNoCollisionTransparent = ReturnType(VoxelData[x,y,z], NoCollisionTransparentIDs);


            bool NeighborisTransparent = ReturnType(NeighbourBlockID, TransparentIDs);
            bool NeighborisNoCollision = ReturnType(NeighbourBlockID, NoCollisionIDs);
            bool NeighborisNoCollisionTransparent = ReturnType(NeighbourBlockID, NoCollisionTransparentIDs);


            if (!isTransparent)
            {
                if (NeighbourBlockID == 0 || NeighborisTransparent)
                {
                    return true;
                }
            }
            else
            {
                if (NeighbourBlockID == 0 && !NeighborisTransparent)
                {
                    return true;
                }
            }
        }
        else
        {
            Chunk neighboringChunk = ChunkManager.Instance.GetNeighboringChunk(currentPos, dx, dy, dz);
            if (neighboringChunk != null)
            {
                int neighborX = (x + dx + ChunkSize) % ChunkSize;
                int neighborY = (y + dy + ChunkSize) % ChunkSize;
                int neighborZ = (z + dz + ChunkSize) % ChunkSize;

                byte[,,] VoxelDataNeighbor = neighboringChunk.GetData();
                if (VoxelDataNeighbor.Length > 0)
                {
                    byte NeighbourBlockID = VoxelDataNeighbor[neighborX, neighborY, neighborZ];

                    bool isTransparent = ReturnType(VoxelData[x, y, z], TransparentIDs);
                    bool isNoCollision = ReturnType(VoxelData[x, y, z], NoCollisionIDs);
                    bool isNoCollisionTransparent = ReturnType(VoxelData[x, y, z], NoCollisionTransparentIDs);


                    bool NeighborisTransparent = ReturnType(NeighbourBlockID, TransparentIDs);
                    bool NeighborisNoCollision = ReturnType(NeighbourBlockID, NoCollisionIDs);
                    bool NeighborisNoCollisionTransparent = ReturnType(NeighbourBlockID, NoCollisionTransparentIDs);


                    if (!isTransparent)
                    {
                        if (NeighbourBlockID == 0 || NeighborisTransparent)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (NeighbourBlockID == 0 && !NeighborisTransparent)
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        return false; 
    }

    private (Chunk[], bool[]) GetNeighboringChunks(bool useDiagonal)
    {
        List<Chunk> neighboringChunks = new List<Chunk>();
        List<bool> IsDiagonal = new List<bool>();

        Vector3Int[] directions =
        {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
        new Vector3Int(0, 0, -1),
        new Vector3Int(0, 0, 1),
    };

        Vector3Int[] diagonalDirections =
        {
            new Vector3Int(1, 1, 0),
            new Vector3Int(1, -1, 0),
            new Vector3Int(-1, 1, 0),
            new Vector3Int(-1, -1, 0),
            new Vector3Int(1, 0, 1),
            new Vector3Int(1, 0, -1),
            new Vector3Int(-1, 0, 1),
            new Vector3Int(-1, 0, -1),
            new Vector3Int(0, 1, 1),
            new Vector3Int(0, 1, -1),
            new Vector3Int(0, -1, 1),
            new Vector3Int(0, -1, -1),
        };
        if (useDiagonal)
        {

            // Combine the regular and diagonal directions
            directions = directions.Concat(diagonalDirections).ToArray();
        }

        ChunkManager chunkManager = ChunkManager.Instance;

        foreach (Vector3Int direction in directions)
        {

            IsDiagonal.Add(diagonalDirections.Contains(direction));

            Chunk neighboringChunk = chunkManager.GetNeighboringChunk(currentPos, direction.x, direction.y, direction.z);
            if (neighboringChunk != null)
            {
                neighboringChunks.Add(neighboringChunk);
            }
        }

        return (neighboringChunks.ToArray(), IsDiagonal.ToArray());
    }




    public void UpdateNeighborChunks()
    {
        (Chunk[], bool[]) chunksData = GetNeighboringChunks(true);
        Chunk[] chunks = chunksData.Item1;
        bool[] isDiagonal = chunksData.Item2;
        for (int i = 0; i < chunks.Length; i++)
        {
            if (!isDiagonal[i])
            {
                chunks[i].GenerateMesh(false);
            }
            else
            {
                chunks[i].GenerateLighting(false, true);
            }
        }
    }

    private void AddQuad(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Vector3 bottomLeft, Vector3 bottomRight, Vector3 topLeft, Vector3 topRight, int blockId, bool inverted = false)
    {
        int vertIndex = vertices.Count;
        int TextureID = blockId - 1;

        if (!inverted)
        {
            vertices.Add(bottomLeft);
            vertices.Add(bottomRight);
            vertices.Add(topLeft);
            vertices.Add(topRight);
        }
        else
        {
            vertices.Add(bottomRight);
            vertices.Add(bottomLeft);
            vertices.Add(topRight);
            vertices.Add(topLeft);
        }

        float atlasSize = TextureSize;
        float blockSize = BlockSize;
        float blocksPerRow = atlasSize / blockSize;
        float row = Mathf.Floor(TextureID / blocksPerRow);
        float col = TextureID % blocksPerRow;
        float blockX = col * (blockSize / atlasSize);
        float blockY = row * (blockSize / atlasSize);
        float uvSize = 1.0f / blocksPerRow;

        if (!inverted)
        {
            uvs.Add(new Vector2(blockX, blockY));
            uvs.Add(new Vector2(blockX + uvSize, blockY));
            uvs.Add(new Vector2(blockX, blockY + uvSize));
            uvs.Add(new Vector2(blockX + uvSize, blockY + uvSize));
        }
        else
        {
            uvs.Add(new Vector2(blockX + uvSize, blockY));
            uvs.Add(new Vector2(blockX, blockY));
            uvs.Add(new Vector2(blockX + uvSize, blockY + uvSize));
            uvs.Add(new Vector2(blockX, blockY + uvSize));
        }


        triangles.Add(vertIndex);
        triangles.Add(vertIndex + 2);
        triangles.Add(vertIndex + 1);
        triangles.Add(vertIndex + 2);
        triangles.Add(vertIndex + 3);
        triangles.Add(vertIndex + 1);
    }




}
