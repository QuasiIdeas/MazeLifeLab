using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
//using System.Numerics;
using Unity.VisualScripting;
using UnityEngine;

struct Wall
{
    public Vector3 A;
    public Vector3 B;
    public bool bVisible;
    public int cell_id1;
    public int cell_id2;
};

struct Cell
{

    public int id;
    public List<string> walls;
    public bool bVisited;
    public Vector3 A,B,C,D;
};


public class MazeGen : MonoBehaviour
{
    Dictionary<string, Wall> walls_map = new Dictionary<string, Wall>();
    const int nCellsX = 20;
    const int nCellsY = 20;
    const int wall_len = 5;
    Cell[,] cells = new Cell[nCellsX, nCellsY];

    // Start is called before the first frame update
    void Start()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = new Vector3(0, 0.5f, 0);
        CreateCells();
        GenerateMaze();
    }

    public string Mkey(Vector3 A, Vector3 B)
    {
        string key = $"{A.x}{A.z}{B.x}{B.z}";
        return key;
    }

    public void CreateWalls(int current_id, int i, int j, Vector3 A, Vector3 B, Vector3 C, Vector3 D)
    {
        cells[i, j].walls = new List<string>();
        
        if (walls_map.ContainsKey(Mkey(A, B)) || walls_map.ContainsKey(Mkey(B, A)))
            cells[i, j].walls.Add(Mkey(A, B));
        else
        {
            Wall wall = new Wall();
            wall.A = A; wall.B = B;
            wall.bVisible = true;
            wall.cell_id1 = current_id;
            if ((current_id - nCellsX) > 0)
                wall.cell_id2 = current_id - nCellsX;
            else
                wall.cell_id2 = current_id;
            walls_map[Mkey(A,B)] = wall;
            cells[i, j].walls.Add(Mkey(A, B));
        }
        if (walls_map.ContainsKey(Mkey(B, D)) || walls_map.ContainsKey(Mkey(D, B)))
            cells[i, j].walls.Add(Mkey(B, D));
        else
        {
            Wall wall = new Wall();
            wall.A = B; wall.B = D;
            wall.bVisible = true;
            wall.cell_id1 = current_id;
            if ((i + 1) < nCellsX)
                wall.cell_id2 = current_id + 1;
            else
                wall.cell_id2 = current_id;
            walls_map[Mkey(B, D)] = wall;
            cells[i, j].walls.Add(Mkey(B, D));
        }
        if (walls_map.ContainsKey(Mkey(D, C)) || walls_map.ContainsKey(Mkey(C, D)))
            cells[i, j].walls.Add(Mkey(D, C));
        else
        {
            Wall wall = new Wall();
            wall.A = D; wall.B = C;
            wall.bVisible = true;
            wall.cell_id1 = current_id;
            if ((j + 1) < nCellsY)
                wall.cell_id2 = current_id + nCellsX;
            else
                wall.cell_id2 = current_id;
            walls_map[Mkey(D, C)] = wall;
            cells[i, j].walls.Add(Mkey(D, C));
        }
        if (walls_map.ContainsKey(Mkey(C, A)) || walls_map.ContainsKey(Mkey(A, C)))
            cells[i, j].walls.Add(Mkey(C, A));
        else
        {
            Wall wall = new Wall();
            wall.A = C; wall.B = A;
            wall.bVisible = true;
            wall.cell_id1 = current_id;
            if ((i - 1) > 0)
                wall.cell_id2 = current_id - 1;
            else
                wall.cell_id2 = current_id;
            walls_map[Mkey(C, A)] = wall;

            cells[i, j].walls.Add(Mkey(C, A));
        }
       
    }

    Cell getCell(int id)
    {
        int i = id % nCellsX;
        int j = id / nCellsX;
        return cells[i, j];
    }

    bool pointOutOfMaze(Vector3 A)
    {
        if (A.x < 0) return true;
        if (A.x > wall_len * nCellsX) return true;
        if (A.z < 0) return true;
        if (A.z > wall_len * nCellsX) return true;
        return false;
    }

    string validKey(Vector3 A, Vector3 B)
    { 
        if ( walls_map.ContainsKey(Mkey(A, B)) )
            return Mkey(A, B);
        if (walls_map.ContainsKey(Mkey(B, A)))
            return Mkey(B, A);
        return "";
    }
    
    void AddWalls(Cell c, ref List<string> list)
    {
        Vector3 B = c.A;
        Vector3 A = new Vector3(B.x, 0, B.z - wall_len);
        if( validKey(A, B)!="" ) list.Add(validKey(A, B));
        A = new Vector3(B.x - wall_len, 0, B.z);
        if (validKey(A, B) != "") list.Add(validKey(A, B));
        
        B = c.B;
        A = new Vector3(B.x, 0, B.z - wall_len);
        if (validKey(A, B) != "") list.Add(validKey(A, B));
        A = new Vector3(B.x + wall_len, 0, B.z);
        if (validKey(A, B) != "") list.Add(validKey(A, B));

        B = c.D;
        A = new Vector3(B.x, 0, B.z + wall_len);
        if (validKey(A, B) != "") list.Add(validKey(A, B));
        A = new Vector3(B.x + wall_len, 0, B.z);
        if (validKey(A, B) != "") list.Add(validKey(A, B));

        B = c.C;
        A = new Vector3(B.x - wall_len, 0, B.z);
        if (validKey(A, B) != "") list.Add(validKey(A, B));
        A = new Vector3(B.x, 0, B.z + wall_len);
        if (validKey(A, B) != "") list.Add(validKey(A, B));
    }


    void GenerateMaze()
    {
        int i = UnityEngine.Random.Range(0, nCellsX);
        int j = UnityEngine.Random.Range(0, nCellsY);
        cells[i,j].bVisited = true;

        List<string> list = new List<string>();
        foreach (string w in cells[i, j].walls)
            list.Add(w);
        int maxIterations = 10000;
        while (list.Count > 0)
        {
            int k = UnityEngine.Random.Range(0, list.Count);
            if (!walls_map.ContainsKey(list[k]))
            {
                list.RemoveAt(k);
                continue;
            }
            Wall wall = walls_map[list[k]];
            Cell cell1 = getCell(wall.cell_id1);
            Cell cell2 = getCell(wall.cell_id2);
            int current_id = 0;
            string tmp = list[k];
            if ( cell1.bVisited ^ cell2.bVisited )
            {
                wall.bVisible = false;
                walls_map[list[k]] = wall;
                if ( cell1.bVisited )
                    current_id = cell2.id;
                else
                    current_id = cell1.id;

                int ii = current_id % nCellsX;
                int jj = current_id / nCellsX;
                cells[ii, jj].bVisited = true;
                //foreach (string w in cells[ii, jj].walls)
                //   list.Append(w);
                //add additional walls
                AddWalls(cells[ii, jj], ref list);
            }
            
        

            list.Remove(tmp);

            if (maxIterations <= 0)
                break;
            maxIterations--;
        }
        Debug.Log($"maxIterations {maxIterations}");

    }

    void CreateCells()
    {
        int x=0, y=0;
        for (int j = 0; j < nCellsY; j++)
        {
            x = 0;
            for (int i = 0; i < nCellsX; i++)
            {
                int current_id = j * nCellsX + i;
                y = j * wall_len;
                // check that wall already exist
                cells[i, j].A = new Vector3(x, 0, y);
                cells[i, j].B = new Vector3(x + wall_len, 0, y);
                cells[i, j].C = new Vector3(x, 0, y + wall_len);
                cells[i, j].D = new Vector3(x + wall_len, 0, y + wall_len);
                cells[i,j].id = current_id;
                CreateWalls(current_id, i, j, cells[i, j].A, cells[i, j].B, cells[i, j].C, cells[i, j].D);
                x+=wall_len;

            }
            
        }
    }

    void DrawWalls()
    { 
        foreach(KeyValuePair<string, Wall> kv in walls_map)
        {
            //kv.Value.A
            if (kv.Value.bVisible)
            {
                Color color = new Color(1.0f, 0, 0.0f);
                Debug.DrawLine(kv.Value.A, kv.Value.B, color);
            }
        }
    }

    private float q = 0.0f;

    void FixedUpdate()
    {
        // always draw a 5-unit colored line from the origin
        Color color = new Color(q, q, 1.0f);
        Debug.DrawLine(Vector3.zero, new Vector3(0, 5, 0), color);
        q = q + 0.01f;

        if (q > 1.0f)
        {
            q = 0.0f;
        }

        DrawWalls();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
