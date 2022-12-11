using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField]
    protected Transform target;

    [SerializeField]
    protected float distance;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        Vector3 back = -target.transform.forward;
        back.y = 0.5f; // this determines how high. Increase for higher view angle.
        transform.position = target.transform.position + back * distance;

        transform.forward = target.transform.position - transform.position;
    }
}
