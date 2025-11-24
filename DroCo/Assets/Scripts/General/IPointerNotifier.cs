using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPointerNotifier {

    public enum ClickObject {
        Drone,
        DroneScreen,
        Undefined
    }

    public abstract void OnClicked(ClickObject clickObject);
}
