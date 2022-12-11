using System;
using UnityEngine;

[Serializable]
public class ActionModel
{

    [SerializeField]
    public float actionMultiplier = 1;

    [SerializeField]
    public float actionForce = 10;

    [SerializeField]
    public float actionCooldown = 2.0f;

    [SerializeField]
    public ForceMode actionForceMode = ForceMode.Impulse;

    [SerializeField]
    public bool readyToAction = true;
    
    [SerializeField]
    public bool input = false;

    public ActionModel() { }

    public ActionModel(float actionMultiplier, float actionForce, float actionCooldown, ForceMode actionForceMode, bool readyToAction)
    {
        this.actionMultiplier = actionMultiplier;
        this.actionForce = actionForce;
        this.actionCooldown = actionCooldown;
        this.actionForceMode = actionForceMode;
        this.readyToAction = readyToAction;
    }

    public void ResetAction()
    {
        readyToAction = true;
    }
}
