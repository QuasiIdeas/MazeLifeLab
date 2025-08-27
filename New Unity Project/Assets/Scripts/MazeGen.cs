using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct Wall
{
    public Vector3 A;
    public Vector3 B;
    public bool bVisible;
    public int cell_id1;
    public int cell_id2;
}

[Serializable]
public struct Cell
{
    public int id;
    public List<string> walls;
    public bool bVisited;
    public Vector3 A, B, C, D;
}

public class MazeGen : MonoBehaviour
{
    [Header("Геометрия лабиринта")]
    [Tooltip("Размер клетки (по X/Z) в мировых единицах")]
    public float cellSize = 5f;
    [Tooltip("Отступ от краёв плоскости")]
    public float margin = 0.5f;

    [Header("Стены")]
    public float wallThickness = 0.25f;
    public float wallHeight = 2.0f;
    public Material wallMaterial;     // опционально
    public Transform wallsParent;     // опционально, для порядка в иерархии
    public bool drawDebugLines = true;

    [Header("Повторяемость (Seed)")]
    [Tooltip("Сид генератора. Один и тот же seed -> одинаковый лабиринт.")]
    public int seed = 123456;
    [Tooltip("Применять сид автоматически при старте Play")]
    public bool applySeedOnStart = true;

    // Внутренние
    Dictionary<string, Wall> walls_map = new Dictionary<string, Wall>();
    Cell[,] cells;
    int nCellsX;
    int nCellsY;
    float groundY;
    Vector3 origin;        // левый-нижний угол области лабиринта
    Vector2 areaSize;      // фактический XZ размер занятой области

    void Start()
    {
        if (applySeedOnStart)
            UnityEngine.Random.InitState(seed);

        Build();
    }

    // ==== ПУБЛИЧНЫЕ МЕТОДЫ ====

    /// <summary>Переcоздать лабиринт, применив текущий seed.</summary>
    public void Regenerate()
    {
        UnityEngine.Random.InitState(seed);
        ClearSpawnedWalls();
        Build();
    }

    /// <summary>Установить сид и пересоздать.</summary>
    public void SetSeedAndRegenerate(int newSeed)
    {
        seed = newSeed;
        Regenerate();
    }

    // Удобная кнопка в инспекторе (контекст-меню на компоненте)
    [ContextMenu("Regenerate (apply seed)")]
    void RegenerateContextMenu() => Regenerate();

    // ==== ОСНОВНОЙ ПАЙПЛАЙН ====

    void Build()
    {
        ComputeAreaFromCarrier();
        CreateCells();
        GenerateMaze();     // детерминированно благодаря InitState(seed)
        InstantiateWalls();
    }

    // ==== ГЕОМЕТРИЯ ПЛОЩАДКИ ====

    void ComputeAreaFromCarrier()
    {
        var rend = GetComponent<Renderer>();
        var col  = GetComponent<Collider>();

        Bounds b;
        if (col != null) b = col.bounds;
        else if (rend != null) b = rend.bounds;
        else
        {
            Debug.LogWarning("MazeGen: объект не имеет Renderer/Collider. Использую 100x100 по умолчанию.");
            b = new Bounds(transform.position, new Vector3(100, 1, 100));
        }

        groundY = b.max.y;

        float useX = Mathf.Max(0, b.size.x - margin * 2f);
        float useZ = Mathf.Max(0, b.size.z - margin * 2f);

        nCellsX = Mathf.Max(1, Mathf.FloorToInt(useX / cellSize));
        nCellsY = Mathf.Max(1, Mathf.FloorToInt(useZ / cellSize));

        areaSize = new Vector2(nCellsX * cellSize, nCellsY * cellSize);

        Vector3 center = b.center;
        float startX = center.x - areaSize.x * 0.5f;
        float startZ = center.z - areaSize.y * 0.5f;
        origin = new Vector3(startX, groundY, startZ);
    }

    // ==== СЛУЖЕБНЫЕ КЛЮЧИ ДЛЯ КАРТЫ СТЕН ====

    string Mkey(Vector3 A, Vector3 B)
    {
        // Нормализуем направление, чтобы A->B и B->A давали один ключ
        Vector2 a2 = new Vector2(A.x, A.z);
        Vector2 b2 = new Vector2(B.x, B.z);
        if (a2.x > b2.x || (Mathf.Approximately(a2.x, b2.x) && a2.y > b2.y))
        {
            var t = A; A = B; B = t;
        }
        return $"{A.x:0.###}_{A.z:0.###}_{B.x:0.###}_{B.z:0.###}";
    }

    string ValidKey(Vector3 A, Vector3 B)
    {
        string k = Mkey(A, B);
        return walls_map.ContainsKey(k) ? k : "";
    }

    // ==== СОЗДАНИЕ ЯЧЕЕК И ПЕРИМЕТРА СТЕН ====

    void CreateCells()
    {
        cells = new Cell[nCellsX, nCellsY];
        walls_map.Clear();

        for (int j = 0; j < nCellsY; j++)
        for (int i = 0; i < nCellsX; i++)
        {
            int id = j * nCellsX + i;

            float x0 = origin.x + i * cellSize;
            float z0 = origin.z + j * cellSize;

            Vector3 A = new Vector3(x0,             groundY, z0);
            Vector3 B = new Vector3(x0 + cellSize,  groundY, z0);
            Vector3 C = new Vector3(x0,             groundY, z0 + cellSize);
            Vector3 D = new Vector3(x0 + cellSize,  groundY, z0 + cellSize);

            cells[i, j].A = A; cells[i, j].B = B; cells[i, j].C = C; cells[i, j].D = D;
            cells[i, j].id = id;
            cells[i, j].bVisited = false;
            cells[i, j].walls = new List<string>();

            CreateWallsForCell(id, i, j, A, B, C, D);
        }
    }

    void CreateWallsForCell(int current_id, int i, int j, Vector3 A, Vector3 B, Vector3 C, Vector3 D)
    {
        // Верхняя (A-B)
        AddOrLinkWall(current_id, i, j, A, B, (current_id - nCellsX) >= 0 ? current_id - nCellsX : current_id);
        // Правая (B-D)
        AddOrLinkWall(current_id, i, j, B, D, (i + 1) < nCellsX ? current_id + 1 : current_id);
        // Нижняя (D-C)
        AddOrLinkWall(current_id, i, j, D, C, (j + 1) < nCellsY ? current_id + nCellsX : current_id);
        // Левая (C-A)
        AddOrLinkWall(current_id, i, j, C, A, (i - 1) >= 0 ? current_id - 1 : current_id);
    }

    void AddOrLinkWall(int current_id, int i, int j, Vector3 A, Vector3 B, int neighborId)
    {
        string key = Mkey(A, B);
        if (!walls_map.ContainsKey(key))
        {
            Wall w = new Wall
            {
                A = A, B = B,
                bVisible = true,
                cell_id1 = current_id,
                cell_id2 = neighborId
            };
            walls_map[key] = w;
        }
        cells[i, j].walls.Add(key);
    }

    // Добавляем стены ячейки в «фронтир» генератора
    void AddWallsOfCell(Cell c, List<string> list)
    {
        string k;
        k = ValidKey(c.A, c.B); if (!string.IsNullOrEmpty(k)) list.Add(k);
        k = ValidKey(c.B, c.D); if (!string.IsNullOrEmpty(k)) list.Add(k);
        k = ValidKey(c.D, c.C); if (!string.IsNullOrEmpty(k)) list.Add(k);
        k = ValidKey(c.C, c.A); if (!string.IsNullOrEmpty(k)) list.Add(k);
    }

    // ==== ГЕНЕРАЦИЯ МЕТОДОМ РАНДОМИЗИРОВАННОГО ПРИМА ====

    void GenerateMaze()
    {
        // стартовая клетка — детерминированно из текущего Random state
        int i0 = UnityEngine.Random.Range(0, nCellsX);
        int j0 = UnityEngine.Random.Range(0, nCellsY);
        cells[i0, j0].bVisited = true;

        List<string> frontier = new List<string>(cells[i0, j0].walls);

        int guard = nCellsX * nCellsY * 20;
        while (frontier.Count > 0 && guard-- > 0)
        {
            int k = UnityEngine.Random.Range(0, frontier.Count);
            string key = frontier[k];
            frontier.RemoveAt(k);

            if (!walls_map.TryGetValue(key, out Wall wall))
                continue;

            Cell c1 = GetCell(wall.cell_id1);
            Cell c2 = GetCell(wall.cell_id2);

            bool oneVisited = c1.bVisited ^ c2.bVisited;
            if (!oneVisited) continue;

            // ломаем стену
            wall.bVisible = false;
            walls_map[key] = wall;

            int newId = c1.bVisited ? c2.id : c1.id;
            int ii = newId % nCellsX;
            int jj = newId / nCellsX;

            cells[ii, jj].bVisited = true;
            AddWallsOfCell(cells[ii, jj], frontier);
        }
    }

    Cell GetCell(int id)
    {
        int i = id % nCellsX;
        int j = id / nCellsX;
        return cells[i, j];
    }

    // ==== СПАВН СТЕН ====

    void InstantiateWalls()
    {
        if (wallsParent == null)
        {
            var parentGO = new GameObject("MazeWalls");
            parentGO.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            wallsParent = parentGO.transform;
        }

        foreach (var kv in walls_map)
        {
            Wall w = kv.Value;
            if (!w.bVisible) continue;

            Vector3 mid = (w.A + w.B) * 0.5f;
            Vector3 dir = (w.B - w.A);
            float length = dir.magnitude;

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Wall";
            cube.transform.SetParent(wallsParent, true);

            cube.transform.position = mid + Vector3.up * (wallHeight * 0.5f);
            cube.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            cube.transform.localScale = new Vector3(wallThickness, wallHeight, length);

            var rend = cube.GetComponent<Renderer>();
            if (wallMaterial != null && rend != null)
                rend.sharedMaterial = wallMaterial;
        }
    }

    void ClearSpawnedWalls()
    {
        if (wallsParent == null) return;
        for (int i = wallsParent.childCount - 1; i >= 0; i--)
            DestroyImmediate(wallsParent.GetChild(i).gameObject);
    }

    // ==== ДЕБАГ-ЛИНИИ ====

    void LateUpdate()
    {
        if (!drawDebugLines) return;
        Color c = Color.red;
        foreach (var kv in walls_map)
        {
            if (kv.Value.bVisible)
                Debug.DrawLine(kv.Value.A + Vector3.up * 0.01f, kv.Value.B + Vector3.up * 0.01f, c);
        }
    }
}
