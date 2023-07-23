using System.Collections;
using System.Collections.Generic;
using UnityEngine;

struct Edge
{
    public Vector3 X1;
    public Vector3 X2;
};

public class Rrt : MonoBehaviour
{
    const int K = 5000;
    const int maxEdges = K * 2;
    const float delta_t = 2.0f;
    // Start is called before the first frame update
    [SerializeField] GameObject ground;
    Vector3 StartP;
    Vector3 EndP;
    Vector3[] G = new Vector3[K];
    Edge[] E = new Edge[maxEdges];
    int Nnodes;
    int Nedges;

    Vector3 line_eq(Vector3 A, Vector3 d, float t)
    {
        return A + d * t;
    }

    Vector3 new_state(Vector3 Xnear, float delta_t)
    {
        float x = UnityEngine.Random.Range(-1f, 1f);
        float z = UnityEngine.Random.Range(-1f, 1f);
        Vector3 d = new Vector3(x, 0, z);
        return line_eq(Xnear, d, delta_t);
    }
    
    Vector3 nearest_neighbor(Vector3 Xrand)
    {
        float minD = float.MaxValue;
        Vector3 nearest = new Vector3();
        for (int i = 0; i < Nnodes; i++)
        {
            if (minD > Vector3.Distance(G[i], Xrand))
            {
                minD = Vector3.Distance(G[i], Xrand);
                nearest = G[i];
            }
        }
        return nearest;
    }

    void Extend(Vector3 Xrand)
    {
        Vector3 Xnear = nearest_neighbor(Xrand);
        Vector3 Xnew = new_state(Xnear, delta_t);
        RaycastHit hit;
        int layerMask = 1 << 8;
        layerMask = ~layerMask;
        if (!Physics.Raycast(Xnear, Xnew - Xnear, out hit, Vector3.Distance(Xnew, Xnear), layerMask) )
        {
            G[Nnodes] = Xnew;
            E[Nedges].X1 = Xnew;
            E[Nedges].X2 = Xnear;
            Nedges++; Nnodes++;
        }
    }

    void Start()
    {
        StartP = new Vector3(-94.0999985f, -0.100000001f, -94.4000015f);
        G[0] = StartP;
        Nnodes = 1;
        float xmax = 4 * ground.transform.localScale.x;
        for (int i = 0; i < K; i++)
        { 
            float xrand = UnityEngine.Random.Range(-xmax, xmax);
            float zrand = UnityEngine.Random.Range(-xmax, xmax);
            Extend(new Vector3(xrand, 0, zrand));
        }
        Debug.Log("AHA");




    }

    // Update is called once per frame
    void FixedUpdate()
    {
        for (int i = 0; i < Nedges; i++)
        {
            Debug.DrawLine(E[i].X1, E[i].X2, new Color(0, 1.0f, 0));
        }
    }
}
