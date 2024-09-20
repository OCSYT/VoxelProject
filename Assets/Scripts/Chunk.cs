using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static UnityEngine.Mesh;


public class Chunk : MonoBehaviour
{

    private int ChunkSize = 0;
    private byte[,,] VoxelData;
    private byte[,,] VoxelDataDirection;
    private byte[] TransparentIDs;
    private byte[] NoCollisionIDs;
    private byte[] NoCollisionTransparentIDs;
    private byte[] NoPlayerCollisionIDs;
    private MeshFilter _MeshFilter;
    private MeshRenderer _MeshRenderer;
    private Mesh _Mesh;
    private MeshFilter _NoPlayerCollisionMeshFilter;
    private MeshRenderer _NoPlayerCollisionMeshRenderer;
    private Mesh _NoPlayerCollisionMesh;
    private MeshFilter _MeshFilterTransparent;
    private MeshRenderer _MeshRendererTransparent;
    private Mesh _MeshTransparent;
    private MeshFilter _MeshFilterNoCollision;
    private MeshRenderer _MeshRendererNoCollision;
    private Mesh _MeshNoCollision;
    private MeshFilter _MeshFilterNoCollisionTransparent;
    private MeshRenderer _MeshRendererNoCollisionTransparent;
    private Mesh _MeshNoCollisionTransparent;
    private Material _Material;
    private float TextureSize = 256;
    private float BlockSize = 16;
    public Vector3Int currentPos;
    private Transform Main;
    private Transform NoPlayerCollisionObj;
    private Transform TransparentObj;
    private Transform NoCollisionObj;
    private Transform NoCollisionObjTransparent;
    private Dictionary<string, byte> BlockList = new Dictionary<string, byte>();
    private Dictionary<byte, Color> BlockListLight = new Dictionary<byte, Color>();
    private Dictionary<byte, int[]> BlockFaces = new Dictionary<byte, int[]>();
    bool isDestroyed = false;
    private Dictionary<string, (List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)> meshData;

    void Start()
    {
    }
    private void OnDestroy()
    {
        isDestroyed = true;
    }


    public void Init(Material mat, Material transparentMat, byte[] transparent, byte[] nocollision, byte[] noplayercollision, int _ChunkSize, float _TextureSize, float _BlockSize, Vector3Int Position)
    {
        BlockList = ChunkManager.Instance.BlockList;
        BlockListLight = ChunkManager.Instance.BlockListLight;
        BlockFaces = ChunkManager.Instance.BlockFaces;
        currentPos = Position;
        VoxelData = new byte[_ChunkSize, _ChunkSize, _ChunkSize];
        VoxelDataDirection = new byte[_ChunkSize, _ChunkSize, _ChunkSize];

        TextureSize = _TextureSize;
        BlockSize = _BlockSize;
        ChunkSize = _ChunkSize;
        TransparentIDs = transparent;
        NoCollisionTransparentIDs = transparent.Concat(nocollision).ToArray();
        NoPlayerCollisionIDs = noplayercollision;

        NoCollisionIDs = nocollision;

        Main = new GameObject("Main").transform;
        Main.transform.parent = transform;
        Main.transform.localPosition = Vector3.zero;
        _MeshFilter = Main.transform.AddComponent<MeshFilter>();
        _MeshRenderer = Main.transform.AddComponent<MeshRenderer>();
        _MeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        _MeshRenderer.sharedMaterial = mat;
        _Mesh = new Mesh();
        _Mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _MeshFilter.mesh = _Mesh;

        NoPlayerCollisionObj = new GameObject("NoPlayerCollision").transform;
        NoPlayerCollisionObj.transform.parent = transform;
        NoPlayerCollisionObj.transform.localPosition = Vector3.zero;
        NoPlayerCollisionObj.gameObject.layer = 10;
        _NoPlayerCollisionMeshFilter = NoPlayerCollisionObj.transform.AddComponent<MeshFilter>();
        _NoPlayerCollisionMeshRenderer = NoPlayerCollisionObj.transform.AddComponent<MeshRenderer>();
        _NoPlayerCollisionMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        _NoPlayerCollisionMeshRenderer.sharedMaterial = transparentMat;
        _NoPlayerCollisionMesh = new Mesh();
        _NoPlayerCollisionMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _NoPlayerCollisionMeshFilter.mesh = _NoPlayerCollisionMesh;

        TransparentObj = new GameObject("Transparent").transform;
        TransparentObj.transform.parent = transform;
        TransparentObj.transform.localPosition = Vector3.zero;
        _MeshFilterTransparent = TransparentObj.transform.AddComponent<MeshFilter>();
        _MeshRendererTransparent = TransparentObj.transform.AddComponent<MeshRenderer>();
        _MeshRendererTransparent.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        _MeshRendererTransparent.sharedMaterial = transparentMat;
        _MeshTransparent = new Mesh();
        _MeshTransparent.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _MeshFilterTransparent.mesh = _MeshTransparent;

        NoCollisionObj = new GameObject("NoCollision").transform;
        NoCollisionObj.gameObject.layer = ChunkManager.Instance.IgnoreLayer;
        NoCollisionObj.transform.parent = transform;
        NoCollisionObj.transform.localPosition = Vector3.zero;
        _MeshFilterNoCollision = NoCollisionObj.transform.AddComponent<MeshFilter>();
        _MeshRendererNoCollision = NoCollisionObj.transform.AddComponent<MeshRenderer>();
        _MeshRendererNoCollision.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        _MeshRendererNoCollision.sharedMaterial = mat;
        _MeshNoCollision = new Mesh();
        _MeshNoCollision.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _MeshFilterNoCollision.mesh = _MeshNoCollision;

        NoCollisionObjTransparent = new GameObject("NoCollisionTransparent").transform;
        NoCollisionObjTransparent.gameObject.layer = ChunkManager.Instance.IgnoreLayer;
        NoCollisionObjTransparent.transform.parent = transform;
        NoCollisionObjTransparent.transform.localPosition = Vector3.zero;
        _MeshFilterNoCollisionTransparent = NoCollisionObjTransparent.transform.AddComponent<MeshFilter>();
        _MeshRendererNoCollisionTransparent = NoCollisionObjTransparent.transform.AddComponent<MeshRenderer>();
        _MeshRendererNoCollisionTransparent.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        _MeshRendererNoCollisionTransparent.sharedMaterial = transparentMat;
        _MeshNoCollisionTransparent = new Mesh();
        _MeshNoCollisionTransparent.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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
    public byte[,,] SetDirection(int x, int y, int z, byte value)
    {
        if (IsValidPosition(x, y, z))
        {
            VoxelDataDirection[x, y, z] = value;
        }
        else
        {
            Debug.LogError("Invalid voxel position");
        }
        return VoxelDataDirection;
    }

    public byte[,,] SetDirectionData(byte[,,] data)
    {
        VoxelDataDirection = data;
        return data;
    }

    public byte[,,] GetDirectionData()
    {
        return VoxelDataDirection;
    }


    private bool IsValidPosition(int x, int y, int z)
    {
        return x >= 0 && x < ChunkSize && y >= 0 && y < ChunkSize && z >= 0 && z < ChunkSize;
    }

    public void BlockChunkUpdate()
    {
        Vector3 chunkCornerWorldPos = transform.position;
        var chunkSize = ChunkSize;
        var waterLevel = ChunkManager.Instance.waterLevel;
        var worldFloor = ChunkManager.Instance.worldFloor;
        var airBlock = BlockList["Air"];
        var waterBlock = BlockList["Water"];

        // Forward pass


        Parallel.For(0, chunkSize, x =>
        {
            for (int y = 0; y < chunkSize; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    Vector3 voxelPosition = chunkCornerWorldPos + new Vector3(x, y, z);

                    if (voxelPosition.y <= waterLevel && voxelPosition.y > worldFloor && VoxelData[x, y, z] == airBlock)
                    {
                        if (CheckForWaterNeighbor(x, y, z))
                        {
                            VoxelData[x, y, z] = waterBlock;
                        }
                    }
                }
            }
        });

        // Reverse pass
        Parallel.For(0, chunkSize, i =>
        {
            int x = chunkSize - 1 - i;
            for (int y = chunkSize - 1; y >= 0; y--)
            {
                for (int z = chunkSize - 1; z >= 0; z--)
                {
                    Vector3 voxelPosition = chunkCornerWorldPos + new Vector3(x, y, z);

                    if (voxelPosition.y <= waterLevel && voxelPosition.y > worldFloor && VoxelData[x, y, z] == airBlock)
                    {
                        if (CheckForWaterNeighbor(x, y, z))
                        {
                            VoxelData[x, y, z] = waterBlock;
                        }
                    }
                }
            }
        });
    }
        



    private bool CheckForWaterNeighbor(int x, int y, int z)
    {
        Vector3Int[] directions = new Vector3Int[]
        {
        new Vector3Int(-1, 0, 0),   // Left
        new Vector3Int(1, 0, 0),    // Right
        new Vector3Int(0, -1, 0),   // Bottom
        new Vector3Int(0, 1, 0),    // Top
        new Vector3Int(0, 0, -1),   // Back
        new Vector3Int(0, 0, 1)     // Front
        };

        var waterBlock = BlockList["Water"];
        var chunkSize = ChunkSize;
        var chunkManager = ChunkManager.Instance;

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3Int direction = directions[i];

            int neighborX = x + direction.x;
            int neighborY = y + direction.y;
            int neighborZ = z + direction.z;

            if (neighborX >= 0 && neighborX < chunkSize &&
                neighborY >= 0 && neighborY < chunkSize &&
                neighborZ >= 0 && neighborZ < chunkSize)
            {
                // Check within the same chunk
                if (VoxelData[neighborX, neighborY, neighborZ] == waterBlock)
                {
                    return true;
                }
            }
            else
            {
                // Check neighboring chunk
                int neighborChunkX = (neighborX + chunkSize) % chunkSize;
                int neighborChunkY = (neighborY + chunkSize) % chunkSize;
                int neighborChunkZ = (neighborZ + chunkSize) % chunkSize;

                Chunk neighborChunk = chunkManager.GetNeighboringChunk(currentPos, direction.x, direction.y, direction.z);
                if (neighborChunk != null)
                {
                    if (neighborChunk.GetData()[neighborChunkX, neighborChunkY, neighborChunkZ] == waterBlock)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }



    private bool isGeneratingMesh = false;

    public async void GenerateMesh(bool updateNeighbors)
    {
        if (isGeneratingMesh || isDestroyed) return;

        isGeneratingMesh = true;
        CreateLights();
        BlockChunkUpdate();

        InitializeMeshData();
        Vector3 ChunkPosition = transform.position;

        await Task.Run(() =>
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        if (isDestroyed) return; // Exit early if destroyed

                        byte blockId = VoxelData[x, y, z];
                        if (blockId == BlockList["Air"]) continue;

                        Vector3Int[] directions =
                        {
                        new Vector3Int(-1, 0, 0), // Left
                        new Vector3Int(1, 0, 0),  // Right
                        new Vector3Int(0, -1, 0), // Bottom
                        new Vector3Int(0, 1, 0),  // Top
                        new Vector3Int(0, 0, -1), // Back
                        new Vector3Int(0, 0, 1)   // Front
                    };


                        byte BlockDirection = VoxelDataDirection[x, y, z];

                        foreach (Vector3Int direction in directions)
                        {
                            int dx = direction.x;
                            int dy = direction.y;
                            int dz = direction.z;

                            if (!IsFaceVisible(x, y, z, dx, dy, dz)) continue;

                            // Determine block properties once per iteration
                            bool isTransparent = TransparentIDs.Contains(blockId);
                            bool hasNoCollision = NoCollisionIDs.Contains(blockId);
                            bool noPlayerCollision = NoPlayerCollisionIDs.Contains(blockId);
                            bool isMain = !isTransparent && !hasNoCollision && !noPlayerCollision;
                            bool isTransparentNoCollision = isTransparent && hasNoCollision && !noPlayerCollision;
                            bool inverted = false;

                            // Retrieve quad vertices and direction boolean
                            (Vector3[] quadVertices, bool[] directionBool) = GetQuadVertices(dx, dy, dz, x, y, z, BlockDirection, ref inverted);

                            // Get lists for vertices, triangles, and UVs based on block properties
                            List<Vector3> vertices = GetVerticesList(isMain, noPlayerCollision, isTransparentNoCollision, isTransparent, hasNoCollision);
                            List<int> triangles = GetTrianglesList(isMain, noPlayerCollision, isTransparentNoCollision, isTransparent, hasNoCollision);
                            List<Vector2> uvs = GetUVsList(isMain, noPlayerCollision, isTransparentNoCollision, isTransparent, hasNoCollision);

                            // Add quad to mesh data
                            AddQuad(
                                vertices,
                                triangles,
                                uvs,
                                quadVertices[0], quadVertices[1], quadVertices[2], quadVertices[3],
                                blockId, inverted, directionBool
                            );
                        }

                    }
                }
            }
        });

        if (isDestroyed || !isGeneratingMesh) return;

        ApplyMeshData();
        if (updateNeighbors) UpdateNeighborChunks();

        isGeneratingMesh = false;
    }

    private void AddQuad(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, Vector3 bottomLeft, Vector3 bottomRight, Vector3 topLeft, Vector3 topRight, int blockId, bool inverted, bool[] direction)
    {
        Vector3 center = Vector3.zero;
        int vertIndex = vertices.Count;

        int textureID = 0;

        for (int i = 0; i < BlockFaces[(byte)(blockId)].Length; i++)
        {
            if (direction[i] == true)
            {
                int newTexID = BlockFaces[(byte)(blockId)][i];
                textureID = newTexID == 0 ? blockId - 1 : newTexID;
                break;
            }
        }


        // Add vertices
        vertices.Add(bottomLeft);
        vertices.Add(bottomRight);
        vertices.Add(topLeft);
        vertices.Add(topRight);

        float atlasSize = TextureSize;
        float blockSize = BlockSize;
        float blocksPerRow = atlasSize / blockSize;
        float row = Mathf.Floor(textureID / blocksPerRow);
        float col = textureID % blocksPerRow;
        float blockX = col * (blockSize / atlasSize);
        float blockY = row * (blockSize / atlasSize);
        float uvSize = 1.0f / blocksPerRow;

        Vector2[] quadUVs;
        if (!inverted)
        {
            quadUVs = new Vector2[]
            {
            new Vector2(blockX, blockY),
            new Vector2(blockX + uvSize, blockY),
            new Vector2(blockX, blockY + uvSize),
            new Vector2(blockX + uvSize, blockY + uvSize)
            };
        }
        else
        {
            quadUVs = new Vector2[]
            {
            new Vector2(blockX + uvSize, blockY),
            new Vector2(blockX, blockY),
            new Vector2(blockX + uvSize, blockY + uvSize),
            new Vector2(blockX, blockY + uvSize)
            };
        }

        uvs.AddRange(quadUVs);

        // Add triangles
        triangles.Add(vertIndex);
        triangles.Add(vertIndex + 2);
        triangles.Add(vertIndex + 1);
        triangles.Add(vertIndex + 2);
        triangles.Add(vertIndex + 3);
        triangles.Add(vertIndex + 1);
    }


    private void InitializeMeshData()
    {
        meshData = new Dictionary<string, (List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)>
        {
            { "main", (new List<Vector3>(), new List<int>(), new List<Vector2>()) },
            { "noPlayerCollision", (new List<Vector3>(), new List<int>(), new List<Vector2>()) },
            { "transparent", (new List<Vector3>(), new List<int>(), new List<Vector2>()) },
            { "noCollision", (new List<Vector3>(), new List<int>(), new List<Vector2>()) },
            { "noCollisionTransparent", (new List<Vector3>(), new List<int>(), new List<Vector2>()) }
        };
    }

        
    private void ApplyMeshData()
    {
        if (Main) UpdateMesh(_Mesh, meshData["main"], Main.gameObject);
        if (NoPlayerCollisionObj) UpdateMesh(_NoPlayerCollisionMesh, meshData["noPlayerCollision"], NoPlayerCollisionObj.gameObject);
        if (TransparentObj) UpdateMesh(_MeshTransparent, meshData["transparent"], TransparentObj.gameObject);
        if (NoCollisionObj) UpdateMesh(_MeshNoCollision, meshData["noCollision"], NoCollisionObj.gameObject);
        if (NoCollisionObjTransparent) UpdateMesh(_MeshNoCollisionTransparent, meshData["noCollisionTransparent"], NoCollisionObjTransparent.gameObject);
    }

    private List<Vector3> GetVerticesList(bool isMain, bool noPlayerCollision, bool isTransparentNoCollision, bool isTransparent, bool hasNoCollision)
    {
        if (isMain) return meshData["main"].vertices;
        if (noPlayerCollision) return meshData["noPlayerCollision"].vertices;
        if (isTransparentNoCollision) return meshData["noCollisionTransparent"].vertices;
        if (isTransparent) return meshData["transparent"].vertices;
        if (hasNoCollision) return meshData["noCollision"].vertices;
        return meshData["main"].vertices; // Default or fallback
    }

    private List<int> GetTrianglesList(bool isMain, bool noPlayerCollision, bool isTransparentNoCollision, bool isTransparent, bool hasNoCollision)
    {
        if (isMain) return meshData["main"].triangles;
        if (noPlayerCollision) return meshData["noPlayerCollision"].triangles;
        if (isTransparentNoCollision) return meshData["noCollisionTransparent"].triangles;
        if (isTransparent) return meshData["transparent"].triangles;
        if (hasNoCollision) return meshData["noCollision"].triangles;
        return meshData["main"].triangles; // Default or fallback
    }

    private List<Vector2> GetUVsList(bool isMain, bool noPlayerCollision, bool isTransparentNoCollision, bool isTransparent, bool hasNoCollision)
    {
        if (isMain) return meshData["main"].uvs;
        if (noPlayerCollision) return meshData["noPlayerCollision"].uvs;
        if (isTransparentNoCollision) return meshData["noCollisionTransparent"].uvs;
        if (isTransparent) return meshData["transparent"].uvs;
        if (hasNoCollision) return meshData["noCollision"].uvs;
        return meshData["main"].uvs; // Default or fallback
    }

    private (Vector3[], bool[] DirectionVal) GetQuadVertices(int dx, int dy, int dz, int x, int y, int z, byte blockDirection, ref bool inverted)
    {
        Vector3[] quadVertices = new Vector3[4];
        bool[] DirectionBool = new bool[6];

        if (dx == -1) // Left face
        {
            DirectionBool = SetBlockDirection(0, blockDirection);
            quadVertices[0] = new Vector3(x, y, z);
            quadVertices[1] = new Vector3(x, y, z + 1);
            quadVertices[2] = new Vector3(x, y + 1, z);
            quadVertices[3] = new Vector3(x, y + 1, z + 1);
            inverted = true;
        }
        else if (dx == 1) // Right face
        {
            DirectionBool = SetBlockDirection(1, blockDirection);
            quadVertices[0] = new Vector3(x + 1, y, z);
            quadVertices[1] = new Vector3(x + 1, y, z + 1);
            quadVertices[2] = new Vector3(x + 1, y + 1, z);
            quadVertices[3] = new Vector3(x + 1, y + 1, z + 1);
        }
        else if (dy == -1) // Bottom face
        {
            DirectionBool = SetBlockDirection(2, blockDirection);
            quadVertices[0] = new Vector3(x, y, z);
            quadVertices[1] = new Vector3(x, y, z + 1);
            quadVertices[2] = new Vector3(x + 1, y, z);
            quadVertices[3] = new Vector3(x + 1, y, z + 1);
        }
        else if (dy == 1) // Top face
        {
            DirectionBool = SetBlockDirection(3, blockDirection);
            quadVertices[0] = new Vector3(x, y + 1, z);
            quadVertices[1] = new Vector3(x, y + 1, z + 1);
            quadVertices[2] = new Vector3(x + 1, y + 1, z);
            quadVertices[3] = new Vector3(x + 1, y + 1, z + 1);
            inverted = true;
        }
        else if (dz == -1) // Back face
        {
            DirectionBool = SetBlockDirection(4, blockDirection);
            quadVertices[0] = new Vector3(x, y, z);
            quadVertices[1] = new Vector3(x + 1, y, z);
            quadVertices[2] = new Vector3(x, y + 1, z);
            quadVertices[3] = new Vector3(x + 1, y + 1, z);
        }
        else if (dz == 1) // Front face
        {
            DirectionBool = SetBlockDirection(5, blockDirection);
            quadVertices[0] = new Vector3(x, y, z + 1);
            quadVertices[1] = new Vector3(x + 1, y, z + 1);
            quadVertices[2] = new Vector3(x, y + 1, z + 1);
            quadVertices[3] = new Vector3(x + 1, y + 1, z + 1);
            inverted = true;
        }

        if (inverted)
        {
            // Swap vertices to invert the quad
            Vector3 temp = quadVertices[0];
            quadVertices[0] = quadVertices[1];
            quadVertices[1] = temp;

            temp = quadVertices[2];
            quadVertices[2] = quadVertices[3];
            quadVertices[3] = temp;
        }

        return (quadVertices, DirectionBool);
    }


    private bool[] SetBlockDirection(int face, byte blockDirectionVal)
    {
        bool[] directionBool = new bool[6];

        const byte DEFAULT = 0;
        const byte NORTH = 1;
        const byte EAST = 2;
        const byte SOUTH = 3;
        const byte WEST = 4;

        // Set directions based on the block's orientation
        if (blockDirectionVal == DEFAULT || blockDirectionVal == NORTH)
        {
            if (face == 0) directionBool[2] = true; // Left
            if (face == 1) directionBool[3] = true; // Right
            if (face == 2) directionBool[5] = true; // Bottom
            if (face == 3) directionBool[4] = true; // Top
            if (face == 4) directionBool[1] = true; // Back
            if (face == 5) directionBool[0] = true; // Front
        }
        else if (blockDirectionVal == EAST)
        {
            if (face == 0) directionBool[1] = true; // Left
            if (face == 1) directionBool[0] = true; // Right
            if (face == 2) directionBool[5] = true; // Bottom
            if (face == 3) directionBool[4] = true; // Top
            if (face == 4) directionBool[3] = true; // Back
            if (face == 5) directionBool[2] = true; // Front
        }
        else if (blockDirectionVal == SOUTH)
        {
            if (face == 0) directionBool[3] = true; // Left
            if (face == 1) directionBool[2] = true; // Right
            if (face == 2) directionBool[5] = true; // Bottom
            if (face == 3) directionBool[4] = true; // Top
            if (face == 4) directionBool[0] = true; // Back
            if (face == 5) directionBool[1] = true; // Front
        }
        else if (blockDirectionVal == WEST)
        {
            if (face == 0) directionBool[0] = true; // Left
            if (face == 1) directionBool[1] = true; // Right
            if (face == 2) directionBool[5] = true; // Bottom
            if (face == 3) directionBool[4] = true; // Top
            if (face == 4) directionBool[2] = true; // Back
            if (face == 5) directionBool[3] = true; // Front
        }
        return directionBool;
    }



    void UpdateMesh(Mesh mesh, (List<Vector3> vertices, List<int> triangles, List<Vector2> uvs) meshData, GameObject obj)
    {
        MeshCollider meshCollider = obj.GetComponent<MeshCollider>();

        var verticesArray = meshData.vertices.ToArray();
        var trianglesArray = meshData.triangles.ToArray();
        var uvsArray = meshData.uvs.ToArray();

        mesh.Clear();
        mesh.vertices = verticesArray;
        mesh.triangles = trianglesArray;
        mesh.uv = uvsArray;
        mesh.RecalculateNormals();

        if (verticesArray.Length == 0 || trianglesArray.Length == 0)
        {
            if (meshCollider)
            {
                Destroy(meshCollider);
                meshCollider = null;
            }
            return;
        }
        if (!meshCollider)
        {
            meshCollider = obj.AddComponent<MeshCollider>();
        }
        meshCollider.sharedMesh = mesh;
        meshCollider.convex = false; // Convex should be set to false unless necessary for your use case
    }



    private GameObject LightParent;
    async void CreateLights()
    {
        if (LightParent == null)
        {
            LightParent = new GameObject("Lights");
            LightParent.transform.parent = transform;
        }

        List<(Vector3, Color)> LightPositions = new List<(Vector3, Color)>();
        Vector3 chunkCornerWorldPos = transform.position;
        await Task.Run(() =>
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        if (isDestroyed)
                        {
                            break;
                        }
                        if (VoxelData[x, y, z] == BlockList["Air"])
                            continue;

                        Vector3 voxelPosition = chunkCornerWorldPos + new Vector3(x, y, z);

                        Color LightData = BlockListLight[VoxelData[x, y, z]];
                        if (LightData.r != 0 || LightData.g != 0 || LightData.b != 0)
                        {
                            LightPositions.Add((voxelPosition + new Vector3(0.5f, 0.5f, 0.5f), LightData));
                        }
                    }
                }
            }
        });

        if (isDestroyed)
        {
            return;
        }

        // Collect existing light positions
        HashSet<Vector3> existingLightPositions = new HashSet<Vector3>();
        foreach (Transform child in LightParent.transform)
        {
            existingLightPositions.Add(child.position);
        }

        // Create new lights if they do not already exist
        HashSet<Vector3> newLightPositions = new HashSet<Vector3>();
        foreach ((Vector3, Color) LightPos in LightPositions)
        {
            if (!existingLightPositions.Contains(LightPos.Item1))
            {
                GameObject LightObj = new GameObject(LightPos.Item1.ToString());
                LightObj.transform.position = LightPos.Item1;
                LightObj.transform.parent = LightParent.transform;
                Light LightComponent = LightObj.AddComponent<Light>();
                LightComponent.type = LightType.Point;
                LightComponent.color = LightPos.Item2;
                LightComponent.intensity = 1 / Mathf.Clamp(((LightPos.Item2.r + LightPos.Item2.g + LightPos.Item2.b) / 3), 1, Mathf.Infinity);
                LightComponent.range = 50;
            }
            newLightPositions.Add(LightPos.Item1);
        }

        // Destroy lights that are not in the current LightPositions
        foreach (Transform child in LightParent.transform)
        {
            if (!newLightPositions.Contains(child.position))
            {
                Destroy(child.gameObject);
            }
        }
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

    public string GetBlockKey (Dictionary<string, byte> dictionary, byte value)
    {
        foreach(KeyValuePair<string, byte> key in  dictionary)
        {
            if(key.Value == value)
            {
                return key.Key;
            }
        }
        return "";
    }

    private bool IsFaceVisible(int x, int y, int z, int dx, int dy, int dz)
    {
        int neighborX = x + dx;
        int neighborY = y + dy;
        int neighborZ = z + dz;


        if (neighborX >= 0 && neighborX < ChunkSize &&
            neighborY >= 0 && neighborY < ChunkSize &&
            neighborZ >= 0 && neighborZ < ChunkSize)
        {
            byte neighborBlockID = VoxelData[neighborX, neighborY, neighborZ];
            byte currentBlockID = VoxelData[x, y, z];

            bool isCurrentTransparent = ReturnType(currentBlockID, TransparentIDs);
            bool isCurrentNoCollision = ReturnType(currentBlockID, NoCollisionIDs);
            bool isCurrentNoCollisionTransparent = ReturnType(currentBlockID, NoCollisionTransparentIDs);

            bool isNeighborTransparent = ReturnType(neighborBlockID, TransparentIDs);
            bool isNeighborNoCollision = ReturnType(neighborBlockID, NoCollisionIDs);
            bool isNeighborNoCollisionTransparent = ReturnType(neighborBlockID, NoCollisionTransparentIDs);


            if (!isCurrentTransparent)
            {
                if (neighborBlockID == BlockList["Air"] || isNeighborTransparent)
                {
                    return true;
                }
            }
            else
            {
                string KeyName = GetBlockKey(BlockList, neighborBlockID);

                bool isNeighborSameType = (neighborBlockID == currentBlockID && !KeyName.Contains("Leaves") && !KeyName.Contains("Tall Grass") && !KeyName.Contains("Roses"));
                if ((!isNeighborSameType && isNeighborTransparent) || neighborBlockID == BlockList["Air"])
                {
                    return true;
                }
            }
        }
        else
        {
            // Check neighboring chunk
            Chunk neighboringChunk = ChunkManager.Instance.GetNeighboringChunk(currentPos, dx, dy, dz);
            if (neighboringChunk != null)
            {
                int wrappedX = (neighborX + ChunkSize) % ChunkSize;
                int wrappedY = (neighborY + ChunkSize) % ChunkSize;
                int wrappedZ = (neighborZ + ChunkSize) % ChunkSize;

                byte[,,] neighborVoxelData = neighboringChunk.GetData();
                byte neighborBlockID = neighborVoxelData[wrappedX, wrappedY, wrappedZ];
                byte currentBlockID = VoxelData[x, y, z];

                // Cache results of ReturnType
                bool isCurrentTransparent = ReturnType(currentBlockID, TransparentIDs);
                bool isCurrentNoCollision = ReturnType(currentBlockID, NoCollisionIDs);
                bool isCurrentNoCollisionTransparent = ReturnType(currentBlockID, NoCollisionTransparentIDs);

                bool isNeighborTransparent = ReturnType(neighborBlockID, TransparentIDs);
                bool isNeighborNoCollision = ReturnType(neighborBlockID, NoCollisionIDs);
                bool isNeighborNoCollisionTransparent = ReturnType(neighborBlockID, NoCollisionTransparentIDs);

                // Check visibility
                if (!isCurrentTransparent)
                {
                    if (neighborBlockID == BlockList["Air"] || isNeighborTransparent)
                    {
                        return true;
                    }
                }
                else
                {
                    string KeyName = GetBlockKey(BlockList, neighborBlockID);
                    bool isNeighborSameType = (neighborBlockID == currentBlockID && !KeyName.Contains("Leaves") && !KeyName.Contains("Tall Grass") && !KeyName.Contains("Roses"));
                    if ((!isNeighborSameType && isNeighborTransparent) || neighborBlockID == BlockList["Air"])
                    {
                        return true;
                    }
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
        new Vector3Int(1, 1, 1),
        new Vector3Int(1, 1, -1),
        new Vector3Int(1, -1, 1),
        new Vector3Int(1, -1, -1),
        new Vector3Int(-1, 1, 1),
        new Vector3Int(-1, 1, -1),
        new Vector3Int(-1, -1, 1),
        new Vector3Int(-1, -1, -1),
    };

        if (useDiagonal)
        {
            // Combine the regular and diagonal directions
            directions = directions.Concat(diagonalDirections).ToArray();
        }

        ChunkManager chunkManager = ChunkManager.Instance;

        // Use Parallel.ForEach to process directions in parallel
        Parallel.ForEach(directions, direction =>
        {
            bool isDiagonal = diagonalDirections.Contains(direction);

            Chunk neighboringChunk = chunkManager.GetNeighboringChunk(currentPos, direction.x, direction.y, direction.z);
            if (neighboringChunk != null)
            {
                lock (neighboringChunks) // Ensure thread-safe access to neighboringChunks list
                {
                    neighboringChunks.Add(neighboringChunk);
                    IsDiagonal.Add(isDiagonal);
                }
            }
        });

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
                continue;
            }
        }
    }

}

