using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IEngine {
    void InitEngine();

    void UpdateEngine(Rigidbody rigidbody, Mavic2Inputs input);
}
