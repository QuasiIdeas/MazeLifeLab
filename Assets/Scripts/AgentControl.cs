using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentControl : MonoBehaviour
{
    public float speed = 0.5f;
    public float rotationSpeed = 10.0f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    void MakeAScan()
    {
        //Physics.Raycast(Xnear, Xnew - Xnear, out hit, Vector3.Distance(Xnew, Xnear), layerMask);
        Vector3 P = transform.position;
        float phi = 0;
        int Nscans = 20;
        float delta = 2 * Mathf.PI / Nscans;
        float MaxDistance = 10;
        RaycastHit hit;
        int layerMask = 1 << 8;
        layerMask = ~layerMask;
        Dictionary<string, string> wall_list = new Dictionary<string, string>();
        for (int i = 0; i < Nscans; i++)
        {
            float x = Mathf.Cos(phi);
            float y = Mathf.Sin(phi);   
            phi += delta;
            Vector3 D = new Vector3(x, transform.position.y, y);
            Physics.Raycast(transform.position, D, out hit, MaxDistance, layerMask);
            if (hit.transform != null)
            {
                string key = $"{hit.transform.position.x},{hit.transform.position.z}";
                wall_list[key] = key;
                //Debug.Log($" hit.transform = {hit.point}");
                Debug.DrawLine(transform.position, hit.point);
            }
        }

        Debug.Log($"List of observations : {wall_list.Count}");
        string s="[";
        foreach (KeyValuePair<string, string> kv in wall_list)
        {
            s+=$"{ kv.Key },";
        }
        Debug.Log(s);

    }


    // Update is called once per frame
    void FixedUpdate()
    { 
    //{
    //    float translation = Input.GetAxis("Vertical") * speed;
    //    float rotation = Input.GetAxis("Horizontal") * rotationSpeed;
    //    float horizontalSpeed = 2.0f;
    //    float verticalSpeed = 2.0f;
    //    // Move translation along the object's z-axis
    //    transform.Translate(0, 0, translation);

    //    // Rotate around our y-axis
    //    //transform.Rotate(0, rotation, 0);
    //    // Get the mouse delta. This is not in the range -1...1
    //    float h = horizontalSpeed * Input.GetAxis("Mouse X");
    //    float v = verticalSpeed * Input.GetAxis("Mouse Y");

    //    transform.Rotate(v, h, 0);

    //    camera.transform.position = transform.position;
    //    camera.transform.rotation = transform.rotation;

        MakeAScan();
    }
}
